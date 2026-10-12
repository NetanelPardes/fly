AdminCommon.init("admin-dashboard.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
setInterval(() => document.querySelector("#clock").textContent = new Date().toLocaleTimeString(), 1000);
refresh();

async function refresh() {
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
      ? boardFlights.map(f => `<div class="board-flight-row"><strong>${Airport.escapeHtml(f.flightNumber)}</strong><div><strong>${Airport.escapeHtml(f.origin)} -> ${Airport.escapeHtml(f.destination)}</strong><small>${Airport.formatDate(f.departureTime)}</small></div>${Airport.badge(f.status)}<strong>${Airport.escapeHtml(f.boardingGate || f.gateNumber || "TBD")}</strong></div>`).join("")
      : Airport.empty("No flights departing in the next hour");
  } catch (error) {
    Airport.toast(error.message);
  }
}
