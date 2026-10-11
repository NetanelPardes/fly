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
  airportOptions = (catalog.locations || []).map(location => ({
    value: locationLabel(location),
    search: location.city || location.airportName || location.iataCode || "",
  })).sort((a, b) => a.value.localeCompare(b.value));
  document.querySelector("#originOptions").innerHTML = airportOptions.map(option => `<option value="${Airport.escapeHtml(option.value)}"></option>`).join("");
}

async function refresh() {
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
  return `${location.country || ""}, ${location.city || ""} (${location.airportName || location.iataCode || ""})`;
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
    await Airport.api(`/api/flights/${flight.id}`, {
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
    });
    await refresh();
  } catch (error) {
    Airport.toast(error.message);
  }
}
