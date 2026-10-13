AdminCommon.init("admin-baggage.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
refresh();

async function refresh() {
  Airport.setPlaneLoading(true, "Loading baggage");
  try {
    const baggage = await Airport.api("/api/baggage");
    document.querySelector("#baggageTable").innerHTML = Airport.table(["ID", "Ticket", "Tag", "Weight", "Status", "Updated"], baggage.map(b => [b.id, b.ticketId, Airport.escapeHtml(b.tagNumber), `${b.weightKg} kg`, Airport.badge(b.status), Airport.formatDate(b.lastUpdatedAt)]), { paginateKey: "admin-baggage", onPageChange: () => renderBaggage(baggage) });
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function renderBaggage(baggage) {
  document.querySelector("#baggageTable").innerHTML = Airport.table(["ID", "Ticket", "Tag", "Weight", "Status", "Updated"], baggage.map(b => [b.id, b.ticketId, Airport.escapeHtml(b.tagNumber), `${b.weightKg} kg`, Airport.badge(b.status), Airport.formatDate(b.lastUpdatedAt)]), { paginateKey: "admin-baggage", onPageChange: () => renderBaggage(baggage) });
}
