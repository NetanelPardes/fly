const HOME_AIRPORT = "Tel Aviv";
const BAGGAGE_PRICE = 45;
const BOOKING_CUTOFF_HOURS = 5;
let user = Airport.getUser();
let flights = [];
let visibleFlights = [];
let direction = "outgoing";
let selectedFlight = null;
let hasSearched = false;

if (!user || user.role !== "Passenger") location.href = "customer-login.html";
init();

function init() {
  document.querySelector("#userName").textContent = user.fullName;
  document.querySelector("#userEmail").textContent = user.email;
  document.querySelector("#logoutButton").addEventListener("click", logout);
  document.querySelector("#refreshButton").addEventListener("click", () => refresh().catch(e => Airport.toast(e.message)));
  document.querySelector("#flightFilters").addEventListener("submit", e => { e.preventDefault(); searchFlights().catch(error => Airport.toast(error.message)); });
  document.querySelectorAll("[data-direction]").forEach(button => button.addEventListener("click", () => {
    direction = button.dataset.direction;
    document.querySelectorAll("[data-direction]").forEach(item => item.classList.toggle("active", item === button));
    resetSearchResults();
  }));
  document.querySelector("#bookingClass").addEventListener("change", updateBookingPrice);
  document.querySelector("#bookingBaggage").addEventListener("change", updateBookingPrice);
  document.querySelector("#closeDetails").addEventListener("click", () => document.querySelector("#detailsDialog").close());
  document.querySelector("#detailsBookButton").addEventListener("click", () => {
    document.querySelector("#detailsDialog").close();
    if (selectedFlight) openBooking(selectedFlight.id);
  });
  document.querySelector("#closeBooking").addEventListener("click", () => document.querySelector("#bookingDialog").close());
  document.querySelector("#bookingForm").addEventListener("submit", bookTicket);
  document.body.addEventListener("click", event => {
    if (event.target.dataset.flightDetails) openFlightDetails(event.target.dataset.flightDetails);
    if (event.target.dataset.bookFlight) openBooking(event.target.dataset.bookFlight);
  });
  refresh().catch(error => Airport.toast(error.message));
}

function logout() {
  Airport.clearSession();
  location.href = "customer-login.html";
}

async function refresh() {
  if (hasSearched) {
    await searchFlights();
    return;
  }

  flights = await Airport.api("/api/flights");
  renderAutocomplete();
  updateDirectionMetrics(flights);
  resetSearchResults();
}

async function searchFlights() {
  hasSearched = true;
  const query = customerFlightQuery();
  if (!query) return;
  flights = await Airport.api(`/api/flights?${query}`);
  renderAutocomplete();
  renderFlights();
}

function customerFlightQuery() {
  const params = new URLSearchParams();
  const from = document.querySelector("#fromDate").value;
  const to = document.querySelector("#toDate").value;
  const startDate = from || localDateInput(new Date());
  const endDate = from ? (to || from) : startDate;
  const start = localDateTime(startDate, "00:00");
  const end = localDateTime(endDate, "23:59");

  if (end < start) {
    Airport.toast("End time must be after start time.");
    return null;
  }

  params.set("from", start.toISOString());
  params.set("to", end.toISOString());

  const status = document.querySelector("#status").value;
  if (status) params.set("status", status);
  return params;
}

function localDateInput(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function localDateTime(dateValue, timeValue) {
  return new Date(`${dateValue}T${timeValue}:00`);
}

function resetSearchResults() {
  hasSearched = false;
  visibleFlights = [];
  document.querySelector("#metricFlights").textContent = "0";
  document.querySelector("#metricBestFare").textContent = "-";
  document.querySelector("#flightsGrid").innerHTML = Airport.empty("Choose filters and press Search to see flights");
}

function renderAutocomplete() {
  const flightLocations = flights.flatMap(f => [
    { city: f.origin, country: "Available route" },
    { city: f.destination, country: "Available route" }
  ]);
  const allLocations = mergeLocations([...(window.AirportLocations || []), ...flightLocations]);
  const options = allLocations.map(item => `<option value="${Airport.escapeHtml(item.city)}" label="${Airport.escapeHtml(item.country)}"></option>`).join("");
  document.querySelector("#originOptions").innerHTML = options;
  document.querySelector("#destinationOptions").innerHTML = options;
}

function mergeLocations(locations) {
  const byCity = new Map();
  locations.forEach(item => {
    if (!item.city) return;
    const key = item.city.toLowerCase();
    const existing = byCity.get(key);
    if (!existing || existing.country !== "Available route") byCity.set(key, item);
  });
  return [...byCity.values()].sort((a, b) => a.city.localeCompare(b.city));
}

function renderFlights() {
  const origin = document.querySelector("#origin").value.trim().toLowerCase();
  const destination = document.querySelector("#destination").value.trim().toLowerCase();
  const status = document.querySelector("#status").value;
  const bookableOnly = document.querySelector("#bookableOnly").checked;
  const outgoing = flights.filter(f => f.origin === HOME_AIRPORT);
  const incoming = flights.filter(f => f.destination === HOME_AIRPORT);
  visibleFlights = (direction === "outgoing" ? outgoing : incoming)
    .filter(f => !origin || f.origin.toLowerCase().includes(origin))
    .filter(f => !destination || f.destination.toLowerCase().includes(destination))
    .filter(f => !status || f.status === status)
    .filter(f => !bookableOnly || !bookingBlockReason(f))
    .sort(compareFlights);
  document.querySelector("#metricFlights").textContent = visibleFlights.length;
  document.querySelector("#metricBestFare").textContent = visibleFlights.length ? Airport.money(Math.min(...visibleFlights.map(basePriceForFlight))) : "-";
  updateDirectionMetrics(flights);
  document.querySelector("#flightsGrid").innerHTML = visibleFlights.map(flightCard).join("") || Airport.empty("No flights match this search");
}

function compareFlights(a, b) {
  const mode = document.querySelector("#sortBy").value;
  if (mode === "price") return basePriceForFlight(a) - basePriceForFlight(b);
  return recommendationScore(a) - recommendationScore(b);
}

function recommendationScore(flight) {
  const price = basePriceForFlight(flight);
  const duration = durationMinutes(flight);
  const hoursFromNoon = Math.abs(new Date(flight.departureTime).getHours() - 12);
  const blockedPenalty = bookingBlockReason(flight) ? 10000 : 0;
  return price * 3 + duration + hoursFromNoon * 8 + blockedPenalty;
}

function updateDirectionMetrics(sourceFlights) {
  document.querySelector("#metricOutgoing").textContent = sourceFlights.filter(f => f.origin === HOME_AIRPORT).length;
  document.querySelector("#metricIncoming").textContent = sourceFlights.filter(f => f.destination === HOME_AIRPORT).length;
}

function flightCard(flight) {
  const reason = bookingBlockReason(flight);
  return `<article class="flight-card ${direction}">
    <div class="flight-card-top"><strong>${Airport.escapeHtml(flight.flightNumber)}</strong>${Airport.badge(direction === "outgoing" ? "Outgoing" : "Incoming")}</div>
    <div class="flight-tags">${recommendationTags(flight).map(tag => `<span>${tag}</span>`).join("")}</div>
    <h3>${Airport.escapeHtml(flight.origin)} -> ${Airport.escapeHtml(flight.destination)}</h3>
    <div class="flight-times"><div><strong>${timeOnly(flight.departureTime)}</strong><small>${dateOnly(flight.departureTime)}</small></div><div class="flight-line"><span>${durationLabel(flight)}</span></div><div><strong>${timeOnly(flight.arrivalTime)}</strong><small>${dateOnly(flight.arrivalTime)}</small></div></div>
    <p>Gate ${Airport.escapeHtml(flight.gateNumber || "TBD")} - ${Airport.badge(flight.status)} - ${Airport.escapeHtml(flight.source || "Unknown")}</p>
    <div class="fare-row"><span>From</span><strong>${Airport.money(basePriceForFlight(flight))}</strong></div>
    <div class="row-actions"><button class="ghost-button" data-flight-details="${flight.id}">Details</button><button class="primary-button" data-book-flight="${flight.id}" ${reason ? "disabled" : ""}>${reason || "Book"}</button></div>
  </article>`;
}

function recommendationTags(flight) {
  const active = visibleFlights.length ? visibleFlights : flights;
  const tags = [];
  if (active.length && basePriceForFlight(flight) === Math.min(...active.map(basePriceForFlight))) tags.push("Cheapest");
  if (!bookingBlockReason(flight) && recommendationScore(flight) === Math.min(...active.map(recommendationScore))) tags.push("Recommended");
  return tags.slice(0, 2);
}

function openFlightDetails(flightId) {
  selectedFlight = flights.find(f => f.id === Number(flightId));
  if (!selectedFlight) return;
  const reason = bookingBlockReason(selectedFlight);
  document.querySelector("#detailsTitle").textContent = `${selectedFlight.flightNumber} flight details`;
  document.querySelector("#detailsBody").innerHTML = `<div class="detail-grid">
    <article><small>Route</small><strong>${Airport.escapeHtml(selectedFlight.origin)} -> ${Airport.escapeHtml(selectedFlight.destination)}</strong></article>
    <article><small>Departure</small><strong>${Airport.formatDate(selectedFlight.departureTime)}</strong></article>
    <article><small>Arrival</small><strong>${Airport.formatDate(selectedFlight.arrivalTime)}</strong></article>
    <article><small>Duration</small><strong>${durationLabel(selectedFlight)}</strong></article>
    <article><small>Gate</small><strong>${Airport.escapeHtml(selectedFlight.gateNumber || "TBD")}</strong></article>
    <article><small>Status</small><strong>${selectedFlight.status}</strong></article>
    <article><small>Source</small><strong>${Airport.escapeHtml(selectedFlight.source || "Unknown")}</strong></article>
    <article><small>Economy from</small><strong>${Airport.money(basePriceForFlight(selectedFlight))}</strong></article>
    <article><small>Business from</small><strong>${Airport.money(basePriceForFlight(selectedFlight) + 252)}</strong></article>
  </div>${reason ? `<p class="notice">${Airport.escapeHtml(reason)} - this flight cannot be booked here.</p>` : ""}`;
  const button = document.querySelector("#detailsBookButton");
  button.disabled = Boolean(reason);
  button.textContent = reason || "Continue to booking";
  document.querySelector("#detailsDialog").showModal();
}

function bookingBlockReason(flight) {
  if (direction !== "outgoing") return "Incoming only";
  if (flight.status === "Cancelled" || flight.status === "Departed") return "Unavailable";
  if (new Date(flight.departureTime) <= new Date(Date.now() + BOOKING_CUTOFF_HOURS * 60 * 60 * 1000)) return "Closed";
  return "";
}

function openBooking(flightId) {
  selectedFlight = flights.find(f => f.id === Number(flightId));
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
  const base = basePriceForFlight(selectedFlight);
  const seat = document.querySelector("#bookingClass").value === "Business" ? 252 : 0;
  const bag = document.querySelector("#bookingBaggage").value === "bag" ? BAGGAGE_PRICE : 0;
  document.querySelector("#flightPriceText").textContent = Airport.money(base);
  document.querySelector("#seatPriceText").textContent = Airport.money(seat);
  document.querySelector("#bagPriceText").textContent = Airport.money(bag);
  document.querySelector("#bookingPrice").value = (base + seat + bag).toFixed(2);
}

function basePriceForFlight(flight) {
  const hoursAway = flight ? (new Date(flight.departureTime) - Date.now()) / 36e5 : 999;
  const routeWeight = flight ? Math.abs((flight.destination || "").length - (flight.origin || "").length) * 6 : 0;
  const demand = flight?.status === "Boarding" ? 50 : flight?.status === "Delayed" ? -20 : 0;
  return Math.max(95, (hoursAway < 48 ? 260 : hoursAway < 168 ? 220 : 180) + routeWeight + demand);
}

function durationMinutes(flight) {
  return Math.max(1, Math.round((new Date(flight.arrivalTime) - new Date(flight.departureTime)) / 60000));
}

function durationLabel(flight) {
  const minutes = durationMinutes(flight);
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return `${hours}h ${String(rest).padStart(2, "0")}m`;
}

function timeOnly(value) {
  return new Date(value).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}

function dateOnly(value) {
  return new Date(value).toLocaleDateString(undefined, { month: "short", day: "2-digit" });
}

async function bookTicket(event) {
  event.preventDefault();
  try {
    const created = await Airport.api("/api/tickets", { method: "POST", body: JSON.stringify({ passengerId: null, flightId: Number(document.querySelector("#bookingFlightId").value), seatNumber: document.querySelector("#bookingSeat").value, class: document.querySelector("#bookingClass").value, price: Number(document.querySelector("#bookingPrice").value) }) });
    if (document.querySelector("#bookingBaggage").value === "bag") {
      await Airport.api("/api/baggage", { method: "POST", body: JSON.stringify({ ticketId: created.id, tagNumber: `BAG-${created.id}`, weightKg: 18.5 }) }).catch(() => {});
    }
    document.querySelector("#bookingDialog").close();
    Airport.toast("Ticket booked.");
    setTimeout(() => location.href = "customer-tickets.html", 700);
  } catch (error) {
    Airport.toast(error.message);
  }
}
