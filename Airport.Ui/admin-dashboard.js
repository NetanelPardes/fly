AdminCommon.init("admin-dashboard.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
setInterval(() => document.querySelector("#clock").textContent = new Date().toLocaleTimeString(), 1000);
refresh();

async function refresh() {
  try {
    const [flights, tickets, users] = await Promise.all([Airport.api("/api/flights"), Airport.api("/api/tickets"), Airport.api("/api/users")]);
    document.querySelector("#metricFlights").textContent = flights.length;
    document.querySelector("#metricTickets").textContent = tickets.length;
    document.querySelector("#metricActiveTickets").textContent = `${tickets.filter(t => t.status === "Active").length} active`;
    document.querySelector("#metricUsers").textContent = users.length;
    document.querySelector("#departureBoard").innerHTML = flights.slice(0, 10).map(f => `<div class="flight-row"><strong>${Airport.escapeHtml(f.flightNumber)}</strong><div><strong>${Airport.escapeHtml(f.origin)} -> ${Airport.escapeHtml(f.destination)}</strong><small>${Airport.formatDate(f.departureTime)}</small></div>${Airport.badge(f.status)}<strong>${Airport.escapeHtml(f.boardingGate || f.gateNumber || "TBD")}</strong></div>`).join("");
  } catch (error) {
    Airport.toast(error.message);
  }
}
