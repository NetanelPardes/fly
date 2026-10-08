import json
import os
import time
from datetime import datetime, timedelta, timezone

import requests
from confluent_kafka import Producer


AMADEUS_CITY_NAMES = {
    "TLV": "Tel Aviv",
    "LHR": "London",
    "LGW": "London",
    "LCY": "London",
    "LTN": "London",
    "STN": "London",
    "LON": "London",
    "CDG": "Paris",
    "ORY": "Paris",
    "PAR": "Paris",
    "ATH": "Athens",
    "DXB": "Dubai",
    "FCO": "Rome",
    "CIA": "Rome",
    "ROM": "Rome",
    "AMS": "Amsterdam",
    "BER": "Berlin",
    "MAD": "Madrid",
    "BCN": "Barcelona",
    "IST": "Istanbul",
    "JFK": "New York",
    "EWR": "New York",
    "NYC": "New York",
}


class ConfigurationError(RuntimeError):
    pass


def env(name, default):
    return os.getenv(name, default)


def utc_iso(value):
    return value.astimezone(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def demo_flights():
    now = datetime.now(timezone.utc).replace(microsecond=0)
    return [
        {
            "flightNumber": "PY100",
            "origin": "Tel Aviv",
            "destination": "Athens",
            "departureTime": utc_iso(now + timedelta(hours=2)),
            "arrivalTime": utc_iso(now + timedelta(hours=4, minutes=10)),
            "status": "Scheduled",
            "externalId": f"demo-py100-{now.date().isoformat()}",
            "source": "PythonDemo",
        },
        {
            "flightNumber": "PY220",
            "origin": "Rome",
            "destination": "Tel Aviv",
            "departureTime": utc_iso(now + timedelta(hours=5)),
            "arrivalTime": utc_iso(now + timedelta(hours=8, minutes=20)),
            "status": "Delayed",
            "externalId": f"demo-py220-{now.date().isoformat()}",
            "source": "PythonDemo",
        },
    ]


def opensky_flights():
    token = os.getenv("OPENSKY_TOKEN")
    if not token:
        raise RuntimeError("OPENSKY_TOKEN is required for API mode. Use FLIGHT_PRODUCER_MODE=demo without credentials.")

    response = requests.get(
        "https://opensky-network.org/api/states/all",
        headers={"Authorization": f"Bearer {token}"},
        timeout=20,
    )
    response.raise_for_status()
    data = response.json()
    states = data.get("states") or []
    now = datetime.now(timezone.utc).replace(microsecond=0)

    flights = []
    for state in states[:10]:
        callsign = (state[1] or "UNKNOWN").strip()
        icao24 = state[0]
        last_contact = state[4] or int(now.timestamp())
        departure = datetime.fromtimestamp(last_contact, timezone.utc)
        flights.append(
            {
                "flightNumber": callsign,
                "origin": "Unknown",
                "destination": "Unknown",
                "departureTime": utc_iso(departure),
                "arrivalTime": utc_iso(departure + timedelta(hours=2)),
                "status": "Scheduled",
                "externalId": f"opensky-{icao24}-{last_contact}",
                "source": "OpenSky",
            }
        )
    return flights


def amadeus_token():
    client_id = os.getenv("AMADEUS_CLIENT_ID")
    client_secret = os.getenv("AMADEUS_CLIENT_SECRET")
    if not client_id or not client_secret:
        raise ConfigurationError(
            "AMADEUS_CLIENT_ID and AMADEUS_CLIENT_SECRET are required for Amadeus mode. "
            "Create an Amadeus for Developers app, then set these environment variables before running Docker Compose."
        )

    base_url = env("AMADEUS_BASE_URL", "https://test.api.amadeus.com")
    response = requests.post(
        f"{base_url}/v1/security/oauth2/token",
        data={
            "grant_type": "client_credentials",
            "client_id": client_id,
            "client_secret": client_secret,
        },
        timeout=20,
    )
    response.raise_for_status()
    return response.json()["access_token"]


def amadeus_routes():
    raw_routes = env("AMADEUS_ROUTES", "TLV:LON,TLV:PAR,TLV:ATH,TLV:DXB,LON:TLV,PAR:TLV,ATH:TLV,ROM:TLV")
    routes = []
    for route in raw_routes.split(","):
        parts = [part.strip().upper() for part in route.split(":")]
        if len(parts) == 2 and parts[0] and parts[1]:
            routes.append((parts[0], parts[1]))
    return routes


def amadeus_departure_dates():
    today = datetime.now(timezone.utc).date()
    days_ahead = int(env("AMADEUS_DAYS_AHEAD", "14"))
    return [(today + timedelta(days=offset)).isoformat() for offset in range(days_ahead + 1)]


def amadeus_flights():
    token = amadeus_token()
    base_url = env("AMADEUS_BASE_URL", "https://test.api.amadeus.com")
    max_offers = env("AMADEUS_MAX_OFFERS_PER_SEARCH", "8")
    flights = []

    for origin, destination in amadeus_routes():
        for departure_date in amadeus_departure_dates():
            response = requests.get(
                f"{base_url}/v2/shopping/flight-offers",
                headers={"Authorization": f"Bearer {token}"},
                params={
                    "originLocationCode": origin,
                    "destinationLocationCode": destination,
                    "departureDate": departure_date,
                    "adults": "1",
                    "currencyCode": env("AMADEUS_CURRENCY", "USD"),
                    "max": max_offers,
                },
                timeout=30,
            )
            if response.status_code == 400:
                print(f"Amadeus returned no usable data for {origin}->{destination} on {departure_date}: {response.text[:180]}")
                continue
            response.raise_for_status()
            for offer in response.json().get("data", []):
                itinerary = (offer.get("itineraries") or [{}])[0]
                segments = itinerary.get("segments") or []
                if not segments:
                    continue
                first = segments[0]
                last = segments[-1]
                carrier = first.get("carrierCode") or "AM"
                number = first.get("number") or offer.get("id") or "000"
                departure = parse_amadeus_datetime(first["departure"]["at"])
                arrival = parse_amadeus_datetime(last["arrival"]["at"])
                external_id = f'{offer.get("id", "offer")}-{origin}-{destination}-{departure_date}-{carrier}{number}'
                flights.append(
                    {
                        "flightNumber": f"{carrier}{number}",
                        "origin": airport_name(first["departure"].get("iataCode", origin)),
                        "destination": airport_name(last["arrival"].get("iataCode", destination)),
                        "departureTime": utc_iso(departure),
                        "arrivalTime": utc_iso(arrival),
                        "status": "Scheduled",
                        "externalId": external_id,
                        "source": "Amadeus",
                    }
                )

    if not flights:
        raise RuntimeError("Amadeus returned no flight offers for the configured routes and dates.")
    return flights


def parse_amadeus_datetime(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(timezone.utc)


def airport_name(code):
    return AMADEUS_CITY_NAMES.get((code or "").upper(), code or "Unknown")


def publish_once():
    producer = Producer({"bootstrap.servers": env("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")})
    topic = env("KAFKA_FLIGHT_TOPIC", "flight-updates")
    mode = env("FLIGHT_PRODUCER_MODE", "demo").lower()
    try:
        if mode == "amadeus":
            flights = amadeus_flights()
        elif mode == "opensky":
            flights = opensky_flights()
        else:
            flights = demo_flights()
    except ConfigurationError:
        raise
    except Exception as error:
        if env("FLIGHT_PRODUCER_FALLBACK_TO_DEMO", "true").lower() == "true":
            print(f"{mode} mode failed, publishing demo flights instead: {error}")
            flights = demo_flights()
        else:
            raise

    for flight in flights:
        payload = json.dumps(flight)
        producer.produce(topic, key=f'{flight["source"]}:{flight["externalId"]}', value=payload)
        print(f"Published {flight['source']}:{flight['externalId']}")

    producer.flush()


if __name__ == "__main__":
    interval = int(env("FLIGHT_PRODUCER_INTERVAL_SECONDS", "60"))
    run_once = env("FLIGHT_PRODUCER_RUN_ONCE", "false").lower() == "true"

    while True:
        publish_once()
        if run_once:
            break
        time.sleep(interval)
