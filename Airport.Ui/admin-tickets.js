AdminCommon.init("admin-tickets.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
document.querySelector("#closeTicketDialog").addEventListener("click", () => document.querySelector("#ticketDialog").close());
document.querySelector("#ticketOptionsForm").addEventListener("submit", updateTicket);
document.body.addEventListener("click", handleActions);
let tickets = [];
refresh();

async function refresh() {
  try {
    tickets = await Airport.api("/api/tickets");
    document.querySelector("#ticketsTable").innerHTML = Airport.table(["ID", "Passenger", "Flight", "Seat", "Class", "Price", "Status", "Actions"], tickets.map(t => [t.id, Airport.escapeHtml(t.passengerName), Airport.escapeHtml(t.flightNumber), Airport.escapeHtml(t.seatNumber), t.class, Airport.money(t.price), Airport.badge(t.status), `<button class="tiny-button" data-manage-ticket="${t.id}">Change</button> ${t.status === "Active" ? `<button class="tiny-button" data-cancel-ticket="${t.id}">Cancel</button>` : ""}`]));
  } catch (error) {
    Airport.toast(error.message);
  }
}

function openTicket(id) {
  const ticket = tickets.find(t => t.id === Number(id));
  if (!ticket) return;
  document.querySelector("#manageTicketId").value = ticket.id;
  document.querySelector("#manageSeat").value = ticket.seatNumber;
  document.querySelector("#manageClass").value = ticket.class;
  document.querySelector("#ticketDialog").showModal();
}

async function updateTicket(event) {
  event.preventDefault();
  try {
    await Airport.api(`/api/tickets/${document.querySelector("#manageTicketId").value}/options`, { method: "PUT", body: JSON.stringify({ seatNumber: document.querySelector("#manageSeat").value, class: document.querySelector("#manageClass").value }) });
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
