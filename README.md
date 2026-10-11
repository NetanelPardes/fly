# AirportManagementSystem

Mini airport management system learning project.

This project is meant for local development and study. It is not production-ready: secrets are demo values, token settings are simplified, and services are configured for a local Docker network.

## Projects

- `Airport.Api` - ASP.NET Core Web API, EF Core MySQL data model, JWT auth, role checks, Swagger, static frontend in `wwwroot`, and RabbitMQ outbox publisher.
- `Airport.Ui` - standalone polished HTML/CSS/Vanilla JS operations console that connects to the API.
- `Airport.FlightUpdates.Consumer` - .NET 8 Console App Kafka consumer. Automatic offset commits are disabled; offsets are committed only after MySQL persistence succeeds.
- `Data` - local CSV data sources for aircraft, airports, and airlines.
- `python/flight-producer` - optional Python Kafka producer for external flight APIs, kept behind a Docker Compose profile.
- `python/ticket-generator` - Python RabbitMQ consumer that generates ticket text files, updates MySQL, acknowledges only after processing, and prevents duplicate handling with `EventId`.
- `docker-compose.yml` - MySQL, RabbitMQ, API, standalone UI, and Python ticket worker by default. Kafka and external flight ingestion are optional.

## Demo Users

All demo users use password `Password123!`.

- `admin@airport.local` - Admin
- `ticketing@airport.local` - Ticketing
- `baggage@airport.local` - Baggage
- `passenger@airport.local` - Passenger

Users are seeded on API startup after EF migrations run.

## Docker Run

Start Docker Desktop first, then run:

```powershell
docker compose up --build
```

Open:

- Frontend: `http://localhost:8080`
- New UI: `http://localhost:5173`
- Customer login: `http://localhost:5173/customer-login.html`
- Customer flights: `http://localhost:5173/customer-flights.html`
- Customer tickets: `http://localhost:5173/customer-tickets.html`
- Manager login: `http://localhost:5173/admin-login.html`
- Manager dashboard: `http://localhost:5173/admin-dashboard.html`
- Swagger: `http://localhost:8080/swagger`
- RabbitMQ UI: `http://localhost:15672` with `airport` / `airport`
- MySQL: `localhost:3306`

The API applies migrations automatically at startup. MySQL data persists in the `mysql-data` Docker volume. Generated ticket files persist in the `ticket-files` Docker volume.

On startup, the API reads `Data/aircraft.csv`, `Data/airports.csv`, and `Data/Airlines.csv` and creates a local flight schedule. It stores the enriched CSV-derived details in `GeneratedFlightSchedules` and creates matching `Flights` rows so booking still works.

To rebuild only the standalone UI:

```powershell
docker compose up --build airport-ui
```

## Local API Run

Start infrastructure:

```powershell
docker compose up -d mysql rabbitmq kafka
```

Run the API:

```powershell
dotnet run --project Airport.Api
```

Run the C# Kafka consumer only when using the optional external flight API profile:

```powershell
dotnet run --project Airport.FlightUpdates.Consumer
```

Run the Python producer locally only when using the optional external flight API profile and Python dependencies are installed:

```powershell
pip install -r python/flight-producer/requirements.txt
python python/flight-producer/producer.py
```

Run the Python ticket worker locally if Python dependencies are installed:

```powershell
pip install -r python/ticket-generator/requirements.txt
python python/ticket-generator/worker.py
```

## API Flow

1. Login with `POST /api/auth/login`.
2. Use the returned JWT as `Authorization: Bearer <token>`.
3. Admin manages flights, gates, and users.
4. Ticketing creates and cancels tickets.
5. The API saves ticket bookings and writes an outbox row in the same database transaction.
6. The API outbox publisher sends ticket generation tasks to RabbitMQ.
7. The Python ticket worker generates `/tickets/ticket-{id}.txt`, updates `TicketFilePath`, and records `EventId`.
8. Download generated files with `GET /api/tickets/{id}/file`.
9. Baggage users register baggage and update status.
10. Passengers can read only their own tickets and baggage.

## Kafka Flow

The Python producer publishes `flight-updates` messages:

```json
{
  "flightNumber": "PY100",
  "origin": "Tel Aviv",
  "destination": "Athens",
  "departureTime": "2026-10-08T12:00:00Z",
  "arrivalTime": "2026-10-08T14:10:00Z",
  "status": "Scheduled",
  "externalId": "demo-py100-2026-10-08",
  "source": "PythonDemo"
}
```

The C# consumer upserts by `Source + ExternalId`, not by `FlightNumber`.

## Local CSV Flight Schedule

The default Docker setup no longer calls a flight API. It generates flights from the three local CSV files:

- `Data/aircraft.csv`
- `Data/airports.csv`
- `Data/Airlines.csv`

Default generation settings:

- `AIRPORT_CSV_FLIGHT_DAYS=14`
- `AIRPORT_CSV_DESTINATIONS_PER_ORIGIN=1`
- `AIRPORT_CSV_MIN_FLIGHTS_PER_ROUTE_DAY=1`
- `AIRPORT_CSV_MAX_FLIGHTS_PER_ROUTE_DAY=3`

This creates a moderate schedule with at least one random destination from every scheduled large/medium airport source, and 1 to 3 flights per route per day.

To regenerate CSV flights in an existing database, set:

```powershell
$env:AIRPORT_CSV_REGENERATE_FLIGHTS = "true"
docker compose up -d --build airport-api
```

## Optional Real Flight API Mode

The flight producer supports Amadeus mode when `FLIGHT_PRODUCER_MODE` is set to `amadeus`:

```powershell
FLIGHT_PRODUCER_MODE=amadeus
```

To use real flight offers, create an Amadeus for Developers app and set its API key and API secret in PowerShell before running Docker Compose:

```powershell
$env:AMADEUS_CLIENT_ID = "your_api_key"
$env:AMADEUS_CLIENT_SECRET = "your_api_secret"
```

Then rebuild and run with the external API profile:

```powershell
docker compose --profile external-flight-api up -d --build flight-producer flight-consumer airport-api
```

Amadeus uses OAuth client credentials. The producer requests a token from `/v1/security/oauth2/token`, then searches `/v2/shopping/flight-offers` for the configured routes and publishes the returned flights to Kafka. The C# consumer stores them in MySQL with `Source = Amadeus`.

The Amadeus test environment can return limited results for some routes or dates. `FLIGHT_PRODUCER_FALLBACK_TO_DEMO` is `false` by default so you will see a clear error instead of silently getting demo flights when real API mode fails.

If the producer fails, inspect it with:

```powershell
docker compose logs -f flight-producer
```

## FlightAware Firehose Mode

The same Python producer can also read from FlightAware Firehose and publish normalized `flight-updates` messages to Kafka. The existing C# consumer will persist those messages to MySQL without API changes.

Firehose connects over a long-lived TLS socket to `firehose.flightaware.com:1501`. The producer sends an initiation command and maps `flifo`, `departure`, `arrival`, and `cancellation` messages into the project's flight update format.

Firehose is available only when the `external-flight-api` Docker Compose profile is enabled. If credentials are missing or the Firehose connection fails, the producer fails loudly instead of publishing demo flights.

Set your credentials as environment variables before running Docker Compose:

```powershell
$env:FLIGHT_PRODUCER_MODE = "firehose"
$env:FLIGHTAWARE_FIREHOSE_USERNAME = "your_username"
$env:FLIGHTAWARE_FIREHOSE_PASSWORD = "your_firehose_api_key_or_trial_password"
```

Optional filters can reduce the stream volume:

```powershell
$env:FLIGHTAWARE_FIREHOSE_INIT_ARGS = 'events "flifo departure arrival cancellation" airport_filter "LLBG" keepalive 60'
```

Then rebuild and run with the external API profile:

```powershell
docker compose --profile external-flight-api up -d --build flight-producer flight-consumer airport-api
```

To inspect the Firehose connector:

```powershell
docker compose logs -f flight-producer
```

Do not commit Firehose credentials into source files. Use environment variables or a local `.env` file that stays out of git.

If old demo flights are still visible, remember that MySQL data persists in the `mysql-data` Docker volume. Those old rows can remain even after the producer has switched to Firehose.

## Checks

```powershell
dotnet build
dotnet ef migrations list --project Airport.Api --startup-project Airport.Api --no-connect
docker compose config
docker compose build
```

If Docker Desktop is not running, `docker compose build` will fail with a Docker daemon connection error.

## Main Endpoints

- `POST /api/auth/login`
- `POST /api/auth/register-passenger`
- `GET /api/flights?origin=Tel Aviv&destination=London&status=Scheduled`
- `POST /api/flights`
- `GET /api/gates`
- `POST /api/gates`
- `GET /api/users`
- `POST /api/tickets`
- `POST /api/tickets/{id}/cancel`
- `GET /api/tickets/{id}/file`
- `GET /api/baggage`
- `POST /api/baggage`
- `PUT /api/baggage/{id}/status`
