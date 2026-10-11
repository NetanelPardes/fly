AdminCommon.init("admin-tickets.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
document.querySelector("#ticketFilters").addEventListener("submit", event => { event.preventDefault(); renderTickets(); });
document.querySelector("#closeTicketDialog").addEventListener("click", () => document.querySelector("#ticketDialog").close());
document.querySelector("#ticketOptionsForm").addEventListener("submit", updateTicket);
document.body.addEventListener("click", handleActions);
let tickets = [];
refresh();

async function refresh() {
  try {
    tickets = await Airport.api("/api/tickets");
    renderTickets();
  } catch (error) {
    Airport.toast(error.message);
  }
}

function renderTickets() {
  const flightFilter = document.querySelector("#filterFlight").value.trim().toLowerCase();
  const nationalIdFilter = document.querySelector("#filterNationalId").value.trim();
  if (!flightFilter && !nationalIdFilter) {
    document.querySelector("#ticketsTable").innerHTML = Airport.empty("Enter a flight or passenger ID to view tickets");
    return;
  }

  const visible = tickets
    .filter(ticket => !flightFilter || String(ticket.flightId).includes(flightFilter) || String(ticket.flightNumber || "").toLowerCase().includes(flightFilter))
    .filter(ticket => !nationalIdFilter || String(ticket.passengerNationalId || "").includes(nationalIdFilter))
    .sort((a, b) => new Date(a.bookingDate || 0) - new Date(b.bookingDate || 0));

  document.querySelector("#ticketsTable").innerHTML = Airport.table(["ID", "Passenger", "Passenger ID", "Email", "Flight", "Seat", "Baggage", "Class", "Booked", "Status", "Actions"], visible.map(t => [
    t.id,
    Airport.escapeHtml(t.passengerName),
    Airport.escapeHtml(t.passengerNationalId || ""),
    Airport.escapeHtml(t.passengerEmail || ""),
    Airport.escapeHtml(t.flightNumber),
    Airport.escapeHtml(t.seatNumber),
    `${t.baggageAllowanceKg || 0} kg`,
    t.class,
    t.bookingDate ? Airport.formatDate(t.bookingDate) : "-",
    Airport.badge(t.status),
    `<button class="tiny-button" data-manage-ticket="${t.id}">Change</button> ${t.status === "Active" ? `<button class="tiny-button" data-cancel-ticket="${t.id}">Cancel</button>` : ""}`
  ]));
}

function openTicket(id) {
  const ticket = tickets.find(t => t.id === Number(id));
  if (!ticket) return;
  document.querySelector("#manageTicketId").value = ticket.id;
  document.querySelector("#manageSeat").value = ticket.seatNumber;
  document.querySelector("#manageClass").value = ticket.class;
  document.querySelector("#manageBaggage").value = String(ticket.baggageAllowanceKg || 0);
  document.querySelector("#ticketDialog").showModal();
}

async function updateTicket(event) {
  event.preventDefault();
  try {
    await Airport.api(`/api/tickets/${document.querySelector("#manageTicketId").value}/options`, { method: "PUT", body: JSON.stringify({ seatNumber: document.querySelector("#manageSeat").value, class: document.querySelector("#manageClass").value, baggageAllowanceKg: Number(document.querySelector("#manageBaggage").value) }) });
    document.querySelector("#ticketDialog").close();
    await refresh();
  } catch (error) {
    Airport.toast(error.message);
  }
}

async function handleActions(event) {
  try {
    if (event.target.dataset.manageTicket) openTicket(event.target.dataset.manageTicket);
    if (event.target.dataset.cancelTicket) {
      await Airport.api(`/api/tickets/${event.target.dataset.cancelTicket}/cancel`, { method: "POST" });
      await refresh();
    }
  } catch (error) {
    Airport.toast(error.message);
  }
}
