AdminCommon.init("admin-flights.html");
document.querySelector("#flightDate").value = new Date().toISOString().slice(0, 10);
document.querySelector("#refreshButton").addEventListener("click", refresh);
document.querySelector("#flightFilters").addEventListener("submit", event => { event.preventDefault(); refresh(); });
document.body.addEventListener("click", handleActions);

let flights = [];
let airportOptions = [];
init();

async function init() {
  await loadAirportOptions();
  await refresh();
}

async function loadAirportOptions() {
  const catalog = await Airport.api("/api/locations/airports").catch(() => ({ locations: [] }));
  airportOptions = uniqueCityLocations(catalog.locations || []).map(location => ({
    value: locationLabel(location),
    search: location.city || location.country || location.iataCode || "",
  })).sort((a, b) => a.value.localeCompare(b.value));
  document.querySelector("#originOptions").innerHTML = airportOptions.map(option => `<option value="${Airport.escapeHtml(option.value)}"></option>`).join("");
}

async function refresh() {
  Airport.setPlaneLoading(true, "Searching flights");
  try {
    const query = adminFlightQuery();
    flights = await Airport.api(`/api/flights?${query}`);
    const sorted = [...flights].sort((a, b) => new Date(a.departureTime) - new Date(b.departureTime));
    const selectedDate = document.querySelector("#flightDate").value || "selected day";
    const selectedAirport = document.querySelector("#origin").value || "all airports";
    document.querySelector("#flightsTable").innerHTML =
      `<p class="table-caption">Flights on ${Airport.escapeHtml(selectedDate)} at ${Airport.escapeHtml(selectedAirport)}</p>` +
      Airport.table(["Time", "Flight", "Route", "Status", "Gate", "Aircraft", "Actions"], sorted.map(f => [
        timeOnly(f.departureTime),
        Airport.escapeHtml(f.flightNumber),
        `${Airport.escapeHtml(f.origin)} (${Airport.escapeHtml(f.originAirportCode || "")}) -> ${Airport.escapeHtml(f.destination)} (${Airport.escapeHtml(f.destinationAirportCode || "")})`,
        Airport.badge(f.status),
        Airport.escapeHtml(f.boardingGate || f.gateNumber || "TBD"),
        Airport.escapeHtml(f.aircraftModel || f.aircraftCode || "TBD"),
        `<button class="tiny-button" data-delay-flight="${f.id}">Delay</button> <button class="tiny-button" data-cancel-flight="${f.id}">Cancel</button>`
      ]));
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function adminFlightQuery() {
  const params = new URLSearchParams();
  const selectedDate = document.querySelector("#flightDate").value;
  if (selectedDate) {
    params.set("from", `${selectedDate}T00:00:00Z`);
    params.set("to", `${selectedDate}T23:59:59Z`);
  }

  const origin = document.querySelector("#origin").value.trim();
  if (origin) {
    const option = airportOptions.find(item => item.value.toLowerCase() === origin.toLowerCase());
    const fallback = origin.replace(/\s*\([^)]*\)\s*$/, "").split(",").map(part => part.trim()).filter(Boolean).slice(-1)[0] || origin;
    params.set("originSearch", option?.search || fallback);
  }
  return params;
}

function locationLabel(location) {
  return [location.country, location.city].filter(Boolean).join(", ");
}

function uniqueCityLocations(locations) {
  const byCity = new Map();
  locations.forEach(location => {
    if (!location.country && !location.city) return;
    const key = `${location.country || ""}|${location.city || ""}`.toLowerCase();
    if (!byCity.has(key)) byCity.set(key, location);
  });
  return [...byCity.values()];
}

function timeOnly(value) {
  return new Date(value).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}

async function handleActions(event) {
  const id = event.target.dataset.delayFlight || event.target.dataset.cancelFlight;
  if (!id) return;
  const flight = flights.find(item => item.id === Number(id));
  if (!flight) return;
  const status = event.target.dataset.delayFlight ? "Delayed" : "Cancelled";
  try {
    Airport.setPlaneLoading(true, status === "Delayed" ? "Updating flight delay" : "Cancelling flight");
    await Promise.all([Airport.api(`/api/flights/${flight.id}`, {
      method: "PUT",
      body: JSON.stringify({
        flightNumber: flight.flightNumber,
        origin: flight.origin,
        originAirportCode: flight.originAirportCode,
        destination: flight.destination,
        destinationAirportCode: flight.destinationAirportCode,
        boardingGate: flight.boardingGate || flight.gateNumber || "",
        aircraftCode: flight.aircraftCode || "",
        departureTime: flight.departureTime,
        arrivalTime: flight.arrivalTime,
        status,
        basePrice: flight.basePrice || 180,
        gateId: flight.gateId,
        externalId: flight.externalId,
        source: flight.source || "Admin"
      })
    }), Airport.delay(1200)]);
    await refresh();
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}
