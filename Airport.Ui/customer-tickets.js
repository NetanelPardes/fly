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
  document.querySelector("#ticketsList").innerHTML = tickets.map(ticketRow).join("") || Airport.empty("No tickets yet");
}

function ticketRow(ticket) {
  const active = ticket.status === "Active";
  return `<article class="ticket-row">
    <div class="ticket-main">
      <div class="ticket-title-line">
        <small>Ticket #${ticket.id}</small>
        <strong>${Airport.escapeHtml(ticket.flightNumber)}</strong>
        ${Airport.badge(ticket.status)}
      </div>
      <div class="ticket-route">${Airport.escapeHtml(place(ticket.origin, ticket.originCountry))} <span>-></span> ${Airport.escapeHtml(place(ticket.destination, ticket.destinationCountry))}</div>
      <div class="ticket-subroute">${Airport.escapeHtml(airport(ticket.originAirportName, ticket.originAirportCode))} to ${Airport.escapeHtml(airport(ticket.destinationAirportName, ticket.destinationAirportCode))}</div>
    </div>
    <dl class="ticket-details-list">
      <div><dt>Departure</dt><dd>${Airport.formatDate(ticket.departureTime)}</dd></div>
      <div><dt>Arrival</dt><dd>${Airport.formatDate(ticket.arrivalTime)}</dd></div>
      <div><dt>Seat</dt><dd>${Airport.escapeHtml(ticket.seatNumber)}</dd></div>
      <div><dt>Class</dt><dd>${Airport.escapeHtml(ticket.class)}</dd></div>
      <div><dt>Baggage</dt><dd>${ticket.baggageAllowanceKg || 0} kg</dd></div>
      <div><dt>Aircraft</dt><dd>${Airport.escapeHtml([ticket.aircraftManufacturer, ticket.aircraftModel].filter(Boolean).join(" ") || ticket.aircraftCode || "TBD")}</dd></div>
      <div><dt>Gate</dt><dd>${Airport.escapeHtml(ticket.boardingGate || "TBD")}</dd></div>
      <div><dt>Total</dt><dd>${Airport.money(ticket.price)}</dd></div>
    </dl>
    <div class="ticket-actions">
      <button class="tiny-button" data-manage-ticket="${ticket.id}" ${active ? "" : "disabled"}>Change details</button>
      ${active ? `<button class="tiny-button" data-cancel-ticket="${ticket.id}">Cancel</button>` : ""}
      ${ticket.ticketFilePath ? `<button class="tiny-button" data-download-ticket="${ticket.id}">Download</button>` : ""}
    </div>
  </article>`;
}

function place(city, country) {
  return country ? `${city}, ${country}` : city;
}

function airport(name, code) {
  return [name, code ? `(${code})` : ""].filter(Boolean).join(" ");
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
    await Airport.api(`/api/tickets/${ticketId}/options`, {
      method: "PUT",
      body: JSON.stringify({
        seatNumber: document.querySelector("#manageSeat").value.trim(),
        class: document.querySelector("#manageClass").value,
        baggageAllowanceKg: Number(document.querySelector("#manageBaggage").value)
      })
    });
    document.querySelector("#ticketDialog").close();
    await refresh();
    Airport.toast("Ticket details updated.");
  } catch (error) {
    Airport.toast(error.message);
  }
}

async function handleActions(event) {
  try {
    if (event.target.dataset.manageTicket && !event.target.disabled) openTicket(event.target.dataset.manageTicket);
    if (event.target.dataset.cancelTicket) {
      await Airport.api(`/api/tickets/${event.target.dataset.cancelTicket}/cancel`, { method: "POST" });
      await refresh();
    }
    if (event.target.dataset.downloadTicket) await Airport.downloadTicket(event.target.dataset.downloadTicket);
  } catch (error) {
    Airport.toast(error.message);
  }
}
