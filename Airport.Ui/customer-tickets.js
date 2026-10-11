let user = Airport.getUser();
let tickets = [];
let baggage = [];
if (!user || user.role !== "Passenger") location.href = "customer-login.html";
init();

function init() {
  document.querySelector("#userName").textContent = user.fullName;
  document.querySelector("#userEmail").textContent = user.email;
  document.querySelector("#logoutButton").addEventListener("click", () => { Airport.clearSession(); location.href = "customer-login.html"; });
  document.querySelector("#refreshButton").addEventListener("click", () => refresh().catch(e => Airport.toast(e.message)));
  document.querySelector("#closeTicketDialog").addEventListener("click", () => document.querySelector("#ticketDialog").close());
  document.querySelector("#ticketOptionsForm").addEventListener("submit", updateTicket);
  document.body.addEventListener("click", handleActions);
  refresh().catch(error => Airport.toast(error.message));
}

async function refresh() {
  [tickets, baggage] = await Promise.all([Airport.api("/api/tickets"), Airport.api("/api/baggage")]);
  render();
}

function render() {
  document.querySelector("#metricTickets").textContent = tickets.length;
  document.querySelector("#metricActiveTickets").textContent = `${tickets.filter(t => t.status === "Active").length} active`;
  document.querySelector("#metricBaggage").textContent = baggage.length;
  document.querySelector("#metricFiles").textContent = tickets.filter(t => t.ticketFilePath).length;
  document.querySelector("#ticketsGrid").innerHTML = tickets.map(ticket => `<article class="ticket-card"><div><small>Ticket #${ticket.id}</small><h3>${Airport.escapeHtml(ticket.flightNumber)} - Seat ${Airport.escapeHtml(ticket.seatNumber)}</h3><p>${ticket.class} - ${ticket.baggageAllowanceKg || 0} kg baggage - ${Airport.money(ticket.price)} - ${Airport.badge(ticket.status)}</p></div><div class="row-actions"><button class="tiny-button" data-manage-ticket="${ticket.id}">Seat / upgrade / baggage</button>${ticket.status === "Active" ? `<button class="tiny-button" data-cancel-ticket="${ticket.id}">Cancel</button>` : ""}${ticket.ticketFilePath ? `<button class="tiny-button" data-download-ticket="${ticket.id}">Download</button>` : ""}</div></article>`).join("") || Airport.empty("No tickets yet");
}

function openTicket(ticketId) {
  const ticket = tickets.find(t => t.id === Number(ticketId));
  if (!ticket) return;
  document.querySelector("#manageTicketId").value = ticket.id;
  document.querySelector("#manageSeat").value = ticket.seatNumber;
  document.querySelector("#manageClass").value = ticket.class;
  document.querySelector("#manageBaggage").value = String(ticket.baggageAllowanceKg || 0);
  document.querySelector("#ticketDialog").showModal();
}

async function updateTicket(event) {
  event.preventDefault();
  const ticketId = document.querySelector("#manageTicketId").value;
  try {
    await Airport.api(`/api/tickets/${ticketId}/options`, { method: "PUT", body: JSON.stringify({ seatNumber: document.querySelector("#manageSeat").value, class: document.querySelector("#manageClass").value, baggageAllowanceKg: Number(document.querySelector("#manageBaggage").value) }) });
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
    if (event.target.dataset.downloadTicket) await Airport.downloadTicket(event.target.dataset.downloadTicket);
  } catch (error) {
    Airport.toast(error.message);
  }
}
