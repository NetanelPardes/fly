const BAGGAGE_PRICE = 45;
const BOOKING_CUTOFF_HOURS = 5;

let flights = [];
let visibleFlights = [];
let tickets = [];
let baggage = [];
let user = Airport.getUser();
let direction = "outgoing";
let selectedFlight = null;

Airport.setDefaultDates();
boot();

function boot() {
  if (user?.role === "Passenger") showApp();
  document.querySelector("#loginForm").addEventListener("submit", login);
  document.querySelector("#logoutButton").addEventListener("click", logout);
  document.querySelector("#refreshButton").addEventListener("click", () => refresh().catch(e => Airport.toast(e.message)));
  document.querySelector("#flightFilters").addEventListener("submit", event => {
    event.preventDefault();
    renderFlights();
  });
  document.querySelectorAll("[data-direction]").forEach(button => {
    button.addEventListener("click", () => {
      direction = button.dataset.direction;
      document.querySelectorAll("[data-direction]").forEach(item => item.classList.toggle("active", item === button));
      renderFlights();
    });
  });
  document.querySelector("#origin").addEventListener("input", renderFlights);
  document.querySelector("#destination").addEventListener("input", renderFlights);
  document.querySelector("#status").addEventListener("change", renderFlights);
  document.querySelector("#bookingClass").addEventListener("change", updateBookingPrice);
  document.querySelector("#bookingBaggage").addEventListener("change", updateBookingPrice);
  document.querySelector("#closeBooking").addEventListener("click", () => document.querySelector("#bookingDialog").close());
  document.querySelector("#closeTicketDialog").addEventListener("click", () => document.querySelector("#ticketDialog").close());
  document.querySelector("#bookingForm").addEventListener("submit", bookTicket);
  document.querySelector("#ticketOptionsForm").addEventListener("submit", updateTicket);
  document.body.addEventListener("click", handleActions);
}

async function login(event) {
  event.preventDefault();
  try {
    const result = await Airport.api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email: document.querySelector("#loginEmail").value, password: document.querySelector("#loginPassword").value }),
    });
    if (result.user.role !== "Passenger") {
      Airport.toast("This page is for customers. Use the manager portal for staff accounts.");
      return;
    }
    Airport.setSession(result);
    user = result.user;
    showApp();
  } catch (error) {
    Airport.toast(error.message);
  }
}

function logout() {
  Airport.clearSession();
  location.reload();
}

async function showApp() {
  document.querySelector("#loginPanel").hidden = true;
  document.querySelector("#customerApp").hidden = false;
  document.querySelector("#refreshButton").hidden = false;
  document.querySelector("#logoutButton").hidden = false;
  document.querySelector("#userName").textContent = user.fullName;
  document.querySelector("#userEmail").textContent = user.email;
  await refresh();
}

async function refresh() {
  const params = Airport.flightQuery();
  [flights, tickets, baggage] = await Promise.all([
    Airport.api(`/api/flights?${params}`),
    Airport.api("/api/tickets"),
    Airport.api("/api/baggage"),
  ]);
  renderAutocomplete();
  render();
}

function render() {
  document.querySelector("#metricTickets").textContent = tickets.length;
  document.querySelector("#metricActiveTickets").textContent = `${tickets.filter(t => t.status === "Active").length} active`;
  document.querySelector("#metricBaggage").textContent = baggage.length;
  renderFlights();
  renderTickets();
}

function renderAutocomplete() {
  const origins = [...new Set(flights.map(f => f.origin))].sort();
  const destinations = [...new Set(flights.map(f => f.destination))].sort();
  document.querySelector("#originOptions").innerHTML = origins.map(value => `<option value="${Airport.escapeHtml(value)}"></option>`).join("");
  document.querySelector("#destinationOptions").innerHTML = destinations.map(value => `<option value="${Airport.escapeHtml(value)}"></option>`).join("");
}

function renderFlights() {
  const origin = document.querySelector("#origin").value.trim().toLowerCase();
  const destination = document.querySelector("#destination").value.trim().toLowerCase();
  const status = document.querySelector("#status").value;
  visibleFlights = flights
    .filter(f => !origin || f.origin.toLowerCase().includes(origin))
    .filter(f => !destination || f.destination.toLowerCase().includes(destination))
    .filter(f => !status || f.status === status)
    .sort((a, b) => new Date(a.departureTime) - new Date(b.departureTime));
  document.querySelector("#metricFlights").textContent = visibleFlights.length;
  document.querySelector("#flightsGrid").innerHTML = visibleFlights.map(flightCard).join("") || Airport.empty("No flights match this search");
}

function flightCard(flight) {
  const reason = bookingBlockReason(flight);
  return `
    <article class="flight-card ${direction}">
      <div class="flight-card-top"><strong>${Airport.escapeHtml(flight.flightNumber)}</strong>${Airport.badge(direction === "outgoing" ? "Outgoing" : "Incoming")}</div>
      <h3>${Airport.escapeHtml(flight.origin)} -> ${Airport.escapeHtml(flight.destination)}</h3>
      <p>${Airport.formatDate(flight.departureTime)} - Gate ${Airport.escapeHtml(flight.gateNumber || "TBD")}</p>
      <p>${Airport.badge(flight.status)}</p>
      <button class="primary-button" data-book-flight="${flight.id}" ${reason ? "disabled" : ""}>${reason || "Book flight"}</button>
    </article>
  `;
}

function bookingBlockReason(flight) {
  if (direction !== "outgoing") return "Incoming only";
  if (flight.status === "Cancelled" || flight.status === "Departed") return "Unavailable";
  const cutoff = new Date(Date.now() + BOOKING_CUTOFF_HOURS * 60 * 60 * 1000);
  if (new Date(flight.departureTime) <= cutoff) return "Closed";
  return "";
}

function renderTickets() {
  document.querySelector("#ticketsGrid").innerHTML = tickets.map(ticket => `
    <article class="ticket-card">
      <div>
        <small>Ticket #${ticket.id}</small>
        <h3>${Airport.escapeHtml(ticket.flightNumber)} - Seat ${Airport.escapeHtml(ticket.seatNumber)}</h3>
        <p>${ticket.class} - ${Airport.money(ticket.price)} - ${Airport.badge(ticket.status)}</p>
      </div>
      <div class="row-actions">
        <button class="tiny-button" data-manage-ticket="${ticket.id}">Seat / upgrade / baggage</button>
        ${ticket.status === "Active" ? `<button class="tiny-button" data-cancel-ticket="${ticket.id}">Cancel</button>` : ""}
        ${ticket.ticketFilePath ? `<button class="tiny-button" data-download-ticket="${ticket.id}">Download</button>` : ""}
      </div>
    </article>
  `).join("") || Airport.empty("No tickets yet");
}

function openBooking(flightId) {
  selectedFlight = flights.find(item => item.id === Number(flightId));
  if (!selectedFlight || bookingBlockReason(selectedFlight)) return;
  document.querySelector("#bookingFlightId").value = selectedFlight.id;
  document.querySelector("#bookingFlightText").textContent = `${selectedFlight.flightNumber}: ${selectedFlight.origin} -> ${selectedFlight.destination} - ${Airport.formatDate(selectedFlight.departureTime)}`;
  document.querySelector("#bookingPassengerName").value = user.fullName;
  document.querySelector("#bookingPassengerEmail").value = user.email;
  document.querySelector("#bookingSeat").value = "12A";
  document.querySelector("#bookingClass").value = "Economy";
  document.querySelector("#bookingBaggage").value = "none";
  updateBookingPrice();
  document.querySelector("#bookingDialog").showModal();
}

function updateBookingPrice() {
  const klass = document.querySelector("#bookingClass").value;
  const bagChoice = document.querySelector("#bookingBaggage").value;
  const base = basePriceForFlight(selectedFlight);
  const seat = klass === "Business" ? 252 : 0;
  const bag = bagChoice === "bag" ? BAGGAGE_PRICE : 0;
  document.querySelector("#flightPriceText").textContent = Airport.money(base);
  document.querySelector("#seatPriceText").textContent = Airport.money(seat);
  document.querySelector("#bagPriceText").textContent = Airport.money(bag);
  document.querySelector("#bookingPrice").value = (base + seat + bag).toFixed(2);
}

function basePriceForFlight(flight) {
  if (!flight) return 180;
  const hoursAway = (new Date(flight.departureTime) - Date.now()) / 36e5;
  return hoursAway < 48 ? 260 : hoursAway < 168 ? 220 : 180;
}

async function bookTicket(event) {
  event.preventDefault();
  try {
    const created = await Airport.api("/api/tickets", {
      method: "POST",
      body: JSON.stringify({
        passengerId: null,
        flightId: Number(document.querySelector("#bookingFlightId").value),
        seatNumber: document.querySelector("#bookingSeat").value,
        class: document.querySelector("#bookingClass").value,
        price: Number(document.querySelector("#bookingPrice").value),
      }),
    });
    if (document.querySelector("#bookingBaggage").value === "bag") {
      await Airport.api("/api/baggage", {
        method: "POST",
        body: JSON.stringify({ ticketId: created.id, tagNumber: `BAG-${created.id}`, weightKg: 18.5 }),
      }).catch(() => {});
    }
    document.querySelector("#bookingDialog").close();
    await refresh();
    Airport.toast("Ticket booked.");
  } catch (error) {
    Airport.toast(error.message);
  }
}

function openTicket(ticketId) {
  const ticket = tickets.find(item => item.id === Number(ticketId));
  if (!ticket) return;
  document.querySelector("#manageTicketId").value = ticket.id;
  document.querySelector("#manageSeat").value = ticket.seatNumber;
  document.querySelector("#manageClass").value = ticket.class;
  document.querySelector("#manageBagTag").value = `BAG-${ticket.id}`;
  document.querySelector("#manageBagWeight").value = "18.5";
  document.querySelector("#ticketDialog").showModal();
}

async function updateTicket(event) {
  event.preventDefault();
  const ticketId = document.querySelector("#manageTicketId").value;
  try {
    await Airport.api(`/api/tickets/${ticketId}/options`, {
      method: "PUT",
      body: JSON.stringify({ seatNumber: document.querySelector("#manageSeat").value, class: document.querySelector("#manageClass").value }),
    });
    const tag = document.querySelector("#manageBagTag").value.trim();
    const weight = Number(document.querySelector("#manageBagWeight").value);
    if (tag && weight) {
      await Airport.api("/api/baggage", { method: "POST", body: JSON.stringify({ ticketId: Number(ticketId), tagNumber: tag, weightKg: weight }) }).catch(() => {});
    }
    document.querySelector("#ticketDialog").close();
    await refresh();
    Airport.toast("Ticket updated.");
  } catch (error) {
    Airport.toast(error.message);
  }
}

async function handleActions(event) {
  try {
    if (event.target.dataset.bookFlight) openBooking(event.target.dataset.bookFlight);
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
