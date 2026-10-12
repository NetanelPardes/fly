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
  document.querySelector("#profileButton").addEventListener("click", openProfile);
  document.querySelector("#closeProfile").addEventListener("click", () => document.querySelector("#profileDialog").close());
  document.querySelector("#profileForm").addEventListener("submit", saveProfile);
  document.querySelector("#closeTicketDialog").addEventListener("click", () => document.querySelector("#ticketDialog").close());
  document.querySelector("#ticketOptionsForm").addEventListener("submit", updateTicket);
  document.body.addEventListener("click", handleActions);
  setupBackdropClose("#ticketDialog");
  setupBackdropClose("#profileDialog");
  refresh().catch(error => Airport.toast(error.message));
}

async function refresh() {
  Airport.setPlaneLoading(true, "Loading tickets");
  try {
    [tickets, baggage] = await Promise.all([Airport.api("/api/tickets"), Airport.api("/api/baggage")]);
    render();
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function setupBackdropClose(selector) {
  const dialog = document.querySelector(selector);
  if (!dialog) return;
  dialog.addEventListener("click", event => {
    const box = dialog.querySelector(".dialog-form");
    if (box && !box.contains(event.target)) dialog.close();
  });
}

async function openProfile() {
  try {
    Airport.setPlaneLoading(true, "Loading details");
    const profile = await Airport.api("/api/users/me");
    fillProfile(profile);
    document.querySelector("#profileDialog").showModal();
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function fillProfile(profile) {
  document.querySelector("#profileNationalId").value = profile.nationalId || "";
  document.querySelector("#profileFirstName").value = profile.firstName || "";
  document.querySelector("#profileLastName").value = profile.lastName || "";
  document.querySelector("#profileFullName").value = profile.fullName || "";
  document.querySelector("#profileEmail").value = profile.email || "";
  document.querySelector("#profilePhone").value = profile.phoneNumber || "";
  document.querySelector("#profileGender").value = profile.gender || "";
  document.querySelector("#profileAddress").value = profile.address || "";
}

async function saveProfile(event) {
  event.preventDefault();
  try {
    Airport.setPlaneLoading(true, "Saving details");
    const updated = await Airport.api("/api/users/me", {
      method: "PUT",
      body: JSON.stringify({
        nationalId: document.querySelector("#profileNationalId").value.trim(),
        firstName: document.querySelector("#profileFirstName").value.trim(),
        lastName: document.querySelector("#profileLastName").value.trim(),
        fullName: document.querySelector("#profileFullName").value.trim(),
        email: document.querySelector("#profileEmail").value.trim(),
        phoneNumber: document.querySelector("#profilePhone").value.trim(),
        gender: document.querySelector("#profileGender").value.trim(),
        address: document.querySelector("#profileAddress").value.trim()
      })
    });
    user = { ...user, ...updated };
    localStorage.setItem("airport_ui_user", JSON.stringify(user));
    document.querySelector("#userName").textContent = user.fullName;
    document.querySelector("#userEmail").textContent = user.email;
    document.querySelector("#profileDialog").close();
    Airport.toast("Your details were updated.");
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
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
    Airport.setPlaneLoading(true, "Saving ticket");
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
  } finally {
    Airport.setPlaneLoading(false);
  }
}

async function handleActions(event) {
  try {
    if (event.target.dataset.manageTicket && !event.target.disabled) openTicket(event.target.dataset.manageTicket);
    if (event.target.dataset.cancelTicket) {
      Airport.setPlaneLoading(true, "Cancelling ticket");
      await Airport.api(`/api/tickets/${event.target.dataset.cancelTicket}/cancel`, { method: "POST" });
      await refresh();
    }
    if (event.target.dataset.downloadTicket) await Airport.downloadTicket(event.target.dataset.downloadTicket);
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}
