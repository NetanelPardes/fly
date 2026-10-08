AdminCommon.init("admin-flights.html");
Airport.setDefaultDates();
document.querySelector("#refreshButton").addEventListener("click", refresh);
document.querySelector("#flightFilters").addEventListener("submit", event => { event.preventDefault(); refresh(); });
refresh();

async function refresh() {
  try {
    const flights = await Airport.api(`/api/flights?${Airport.flightQuery()}`);
    document.querySelector("#flightsTable").innerHTML = Airport.table(["ID", "Flight", "Route", "Time", "Status", "Gate"], flights.map(f => [f.id, Airport.escapeHtml(f.flightNumber), `${Airport.escapeHtml(f.origin)} -> ${Airport.escapeHtml(f.destination)}`, Airport.formatDate(f.departureTime), Airport.badge(f.status), Airport.escapeHtml(f.gateNumber || "TBD")]));
  } catch (error) {
    Airport.toast(error.message);
  }
}
