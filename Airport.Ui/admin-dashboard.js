AdminCommon.init("admin-dashboard.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
setInterval(() => document.querySelector("#clock").textContent = new Date().toLocaleTimeString(), 1000);
refresh();

async function refresh() {
  Airport.setPlaneLoading(true, "Loading board");
  try {
    const now = new Date();
    const oneHourFromNow = new Date(now.getTime() + 60 * 60 * 1000);
    const params = new URLSearchParams({
      from: now.toISOString(),
      to: oneHourFromNow.toISOString()
    });
    const [allFlights, boardFlights, tickets, users] = await Promise.all([
      Airport.api("/api/flights"),
      Airport.api(`/api/flights?${params}`),
      Airport.api("/api/tickets"),
      Airport.api("/api/users")
    ]);
    document.querySelector("#metricFlights").textContent = allFlights.length;
    document.querySelector("#metricTickets").textContent = tickets.length;
    document.querySelector("#metricActiveTickets").textContent = `${tickets.filter(t => t.status === "Active").length} active`;
    document.querySelector("#metricUsers").textContent = users.length;
    document.querySelector("#departureBoard").innerHTML = boardFlights.length
      ? boardFlights.map(f => `<div class="board-flight-row">
          <div><small>Flight</small><strong>${Airport.escapeHtml(f.flightNumber)}</strong></div>
          <div class="board-route"><strong>${Airport.escapeHtml(f.origin)} -> ${Airport.escapeHtml(f.destination)}</strong><small>${Airport.escapeHtml(f.originAirportCode || "")} to ${Airport.escapeHtml(f.destinationAirportCode || "")}</small></div>
          <div><small>Departure</small><strong>${timeOnly(f.departureTime)}</strong><small>${dateOnly(f.departureTime)}</small></div>
          <div><small>Gate</small><strong>${Airport.escapeHtml(f.boardingGate || f.gateNumber || "TBD")}</strong></div>
          ${Airport.badge(f.status)}
        </div>`).join("")
      : Airport.empty("No flights departing in the next hour");
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function timeOnly(value) {
  return new Date(value).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}

function dateOnly(value) {
  return new Date(value).toLocaleDateString("en-GB", { day: "2-digit", month: "2-digit", year: "numeric" });
}
