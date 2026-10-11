import json
import os
import socket
import ssl
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


def required_env(name, mode):
    value = os.getenv(name)
    if not value:
        raise ConfigurationError(f"{name} is required for {mode} mode.")
    return value


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


def firehose_flights():
    username = required_env("FLIGHTAWARE_FIREHOSE_USERNAME", "Firehose")
    password = required_env("FLIGHTAWARE_FIREHOSE_PASSWORD", "Firehose")
    host = env("FLIGHTAWARE_FIREHOSE_HOST", "firehose.flightaware.com")
    port = int(env("FLIGHTAWARE_FIREHOSE_PORT", "1501"))
    read_seconds = int(env("FLIGHTAWARE_FIREHOSE_READ_SECONDS", "55"))
    max_messages = int(env("FLIGHTAWARE_FIREHOSE_MAX_MESSAGES", "100"))
    init_args = env(
        "FLIGHTAWARE_FIREHOSE_INIT_ARGS",
        'events "flifo departure arrival cancellation" keepalive 60',
    ).strip()
    command = f"live {init_args} username {username} password {password}\n"

    deadline = time.monotonic() + read_seconds
    flights = []
    context = ssl.create_default_context()

    with socket.create_connection((host, port), timeout=20) as raw_socket:
        with context.wrap_socket(raw_socket, server_hostname=host) as tls_socket:
            tls_socket.settimeout(10)
            tls_socket.sendall(command.encode("ascii"))
            reader = tls_socket.makefile("r", encoding="ascii", newline="\n")

            while time.monotonic() < deadline and len(flights) < max_messages:
                try:
                    line = reader.readline()
                except socket.timeout:
                    continue
                if not line:
                    break

                message = json.loads(line)
                flight = firehose_to_flight_update(message)
                if flight:
                    flights.append(flight)

    if not flights:
        raise RuntimeError("Firehose returned no mappable flight status messages during the read window.")
    return flights


def firehose_to_flight_update(message):
    message_type = (message.get("type") or "").lower()
    if message_type in {"keepalive", "position", "ground_position", "vehicle_position", "near_surface_position"}:
        return None
    if message_type == "error":
        raise RuntimeError(f"Firehose error: {message}")

    ident = message.get("ident") or message.get("atcident") or message.get("reg")
    origin = message.get("orig") or message.get("origin")
    destination = message.get("dest") or message.get("destination")
    external_id = message.get("id") or message.get("faFlightID") or firehose_fallback_external_id(message)
    if not ident or not origin or not destination or not external_id:
        return None

    departure = first_firehose_time(
        message,
        "actual_off",
        "estimated_off",
        "predicted_off",
        "scheduled_off",
        "actual_out",
        "estimated_out",
        "predicted_out",
        "scheduled_out",
        "departureTime",
        "edt",
        "fdt",
    )
    arrival = first_firehose_time(
        message,
        "actual_on",
        "estimated_on",
        "predicted_on",
        "scheduled_on",
        "actual_in",
        "estimated_in",
        "predicted_in",
        "scheduled_in",
        "arrivalTime",
        "eta",
    )
    if departure is None:
        departure = datetime.now(timezone.utc)
    if arrival is None or arrival <= departure:
        arrival = departure + timedelta(hours=2)

    return {
        "flightNumber": str(ident).strip(),
        "origin": str(origin).strip(),
        "destination": str(destination).strip(),
        "departureTime": utc_iso(departure),
        "arrivalTime": utc_iso(arrival),
        "status": firehose_status(message),
        "externalId": str(external_id).strip(),
        "source": "FlightAwareFirehose",
    }


def firehose_fallback_external_id(message):
    ident = message.get("ident") or "unknown"
    pitr = message.get("pitr") or message.get("clock") or int(time.time())
    return f"firehose-{ident}-{pitr}"


def first_firehose_time(message, *names):
    for name in names:
        value = message.get(name)
        if value in (None, "", 0):
            continue
        parsed = parse_firehose_time(value)
        if parsed:
            return parsed
    return None


def parse_firehose_time(value):
    if isinstance(value, (int, float)):
        return datetime.fromtimestamp(value, timezone.utc)
    if isinstance(value, str):
        text = value.strip()
        if text.isdigit():
            return datetime.fromtimestamp(int(text), timezone.utc)
        try:
            return datetime.fromisoformat(text.replace("Z", "+00:00")).astimezone(timezone.utc)
        except ValueError:
            return None
    return None


def firehose_status(message):
    message_type = (message.get("type") or "").lower()
    if message_type == "cancellation" or message.get("cancelled"):
        return "Cancelled"
    if message_type in {"departure", "arrival"} or any(message.get(name) for name in ("actual_off", "actual_on", "actual_out", "actual_in")):
        return "Departed"
    if any(message.get(name) for name in ("estimated_off", "estimated_on", "estimated_out", "estimated_in")):
        return "Delayed"
    return "Scheduled"


def parse_amadeus_datetime(value):
    return datetime.fromisoformat(value.replace("Z", "+00:00")).astimezone(timezone.utc)


def airport_name(code):
    return AMADEUS_CITY_NAMES.get((code or "").upper(), code or "Unknown")


def publish_once():
    producer = Producer({"bootstrap.servers": env("KAFKA_BOOTSTRAP_SERVERS", "localhost:9092")})
    topic = env("KAFKA_FLIGHT_TOPIC", "flight-updates")
    mode = env("FLIGHT_PRODUCER_MODE", "demo").lower()
    print(f"Flight producer mode: {mode}")
    try:
        if mode == "firehose":
            flights = firehose_flights()
        elif mode == "amadeus":
            flights = amadeus_flights()
        elif mode == "opensky":
            flights = opensky_flights()
        else:
            flights = demo_flights()
    except ConfigurationError:
        raise
    except Exception as error:
        if env("FLIGHT_PRODUCER_FALLBACK_TO_DEMO", "false").lower() == "true":
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
