const BAGGAGE_PRICE = 45;
const BOOKING_CUTOFF_HOURS = 5;
let user = Airport.getUser();
let flights = [];
let visibleFlights = [];
let airportLocations = [];
let airportCountries = [];
let locationOptions = [];
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
  setupLocationInput("#origin", "#originOptions");
  setupLocationInput("#destination", "#destinationOptions");
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

  const catalog = await Airport.api("/api/locations/airports").catch(error => {
    Airport.toast(error.message);
    return { countries: [], locations: [] };
  });
  flights = [];
  airportLocations = catalog.locations || [];
  airportCountries = catalog.countries || [];
  renderAutocomplete();
  resetSearchResults();
}

async function searchFlights() {
  hasSearched = true;
  const query = customerFlightQuery();
  if (!query) return;
  const status = await Airport.api("/api/flights/schedule-status").catch(() => null);
  if (status && !status.ready) {
    Airport.toast("Flight schedule is still loading. Try again in a moment.");
    return;
  }

  try {
    flights = await Airport.api(`/api/flights?${query}`);
  } catch (error) {
    Airport.toast(error.message === "Failed to fetch" ? "API is still starting. Try again in a moment." : error.message);
    return;
  }
  renderAutocomplete();
  renderFlights();
}

function customerFlightQuery() {
  const params = new URLSearchParams();
  const from = document.querySelector("#fromDate").value.trim();
  const to = document.querySelector("#toDate").value.trim();
  const origin = document.querySelector("#origin").value.trim();
  if (!origin) {
    Airport.toast("Choose an origin.");
    return null;
  }

  if (!from || !to) {
    Airport.toast("Choose from and to dates.");
    return null;
  }

  const startDate = parseDateInput(from);
  const endDate = parseDateInput(to);

  if (!startDate || !endDate) {
    Airport.toast("Choose a valid date.");
    return null;
  }

  const start = withTime(startDate, 0, 0);
  const end = withTime(endDate, 23, 59);

  if (end < start) {
    Airport.toast("End time must be after start time.");
    return null;
  }

  params.set("from", start.toISOString());
  params.set("to", end.toISOString());
  params.set("originSearch", searchTermForLocation(document.querySelector("#origin")));

  const destination = document.querySelector("#destination").value.trim();
  if (destination) params.set("destinationSearch", searchTermForLocation(document.querySelector("#destination")));

  const status = document.querySelector("#status").value;
  if (status) params.set("status", status);
  return params;
}

function parseDateInput(value) {
  const match = value.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!match) return null;
  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) return null;
  return date;
}

function withTime(date, hours, minutes) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate(), hours, minutes, 0);
}

function resetSearchResults() {
  hasSearched = false;
  visibleFlights = [];
  document.querySelector("#metricFlights").textContent = "0";
  document.querySelector("#metricOutgoing").textContent = "0";
  document.querySelector("#metricIncoming").textContent = "0";
  document.querySelector("#metricBestFare").textContent = "-";
  document.querySelector("#searchResultCount").textContent = "0 results";
  document.querySelector("#resultsPanel").hidden = true;
  document.querySelector("#flightsGrid").innerHTML = "";
}

function renderAutocomplete() {
  const allLocations = mergeLocations(airportLocations);
  const countryEntries = airportCountries.map(country => ({
    type: "country",
    value: country,
    searchValue: country,
    tokens: [country]
  }));
  const locationEntries = allLocations.map(item => ({
    type: "airport",
    value: locationValue(item),
    label: "",
    searchValue: locationValue(item),
    tokens: [item.city, item.country, item.iataCode, item.icaoCode, item.airportName].filter(Boolean)
  }));

  locationOptions = [...countryEntries, ...locationEntries].sort((a, b) => a.value.localeCompare(b.value));
  updateLocationOptions(document.querySelector("#origin").value, "#originOptions");
  updateLocationOptions(document.querySelector("#destination").value, "#destinationOptions");
}

function mergeLocations(locations) {
  const byCity = new Map();
  locations.forEach(item => {
    if (!item.city) return;
    const key = `${item.city}|${item.country || ""}`.toLowerCase();
    if (!byCity.has(key)) byCity.set(key, item);
  });
  return [...byCity.values()].sort((a, b) => locationLabel(a).localeCompare(locationLabel(b)));
}

function locationLabel(item) {
  return `${item.city || ""}, ${item.country || ""}`;
}

function locationValue(item) {
  const country = item.country ? `, ${item.country}` : "";
  return `${item.city}${country}`;
}

function setupLocationInput(inputSelector, listSelector) {
  const input = document.querySelector(inputSelector);

  input.addEventListener("input", () => {
    delete input.dataset.searchValue;
    updateLocationOptions(input.value, listSelector);
  });
  input.addEventListener("focus", () => updateLocationOptions(input.value, listSelector));
}

function updateLocationOptions(query, listSelector) {
  const list = document.querySelector(listSelector);
  if (!list) return;
  const normalized = query.trim().toLowerCase();
  list.innerHTML = locationOptions
    .filter(option => !normalized || option.tokens.some(token => token.toLowerCase().startsWith(normalized)))
    .slice(0, 160)
    .map(option => `<option value="${Airport.escapeHtml(option.value)}"${option.label ? ` label="${Airport.escapeHtml(option.label)}"` : ""}></option>`)
    .join("");
}

function searchTermForLocation(input) {
  const value = input.value.trim();
  if (input.dataset.searchValue) return input.dataset.searchValue;
  return searchTermFromValue(value);
}

function searchTermFromValue(value) {
  const match = locationOptions.find(option => option.value.toLowerCase() === value.toLowerCase());
  if (match) return match.searchValue;
  return value.replace(/\s*\([A-Z0-9]{2,4}\)\s*$/, "").split(",")[0].trim();
}

function renderFlights() {
  const origin = document.querySelector("#origin").value.trim();
  const destination = document.querySelector("#destination").value.trim();
  const status = document.querySelector("#status").value;
  const bookableOnly = document.querySelector("#bookableOnly").checked;
  visibleFlights = flights
    .filter(f => locationMatches(f, "origin", origin))
    .filter(f => locationMatches(f, "destination", destination))
    .filter(f => !status || f.status === status)
    .filter(f => !bookableOnly || !bookingBlockReason(f))
    .sort(compareFlights);
  document.querySelector("#metricFlights").textContent = visibleFlights.length;
  document.querySelector("#metricOutgoing").textContent = new Set(visibleFlights.map(f => f.destination)).size;
  document.querySelector("#metricIncoming").textContent = new Set(visibleFlights.map(f => dateOnly(f.departureTime))).size;
  document.querySelector("#metricBestFare").textContent = visibleFlights.length ? Airport.money(Math.min(...visibleFlights.map(basePriceForFlight))) : "-";
  document.querySelector("#searchResultCount").textContent = `${visibleFlights.length} ${visibleFlights.length === 1 ? "result" : "results"}`;
  document.querySelector("#resultsPanel").hidden = false;
  document.querySelector("#resultsTitle").textContent = `Flights from ${origin}`;
  document.querySelector("#flightsGrid").innerHTML = visibleFlights.map(flightCard).join("") || Airport.empty("No flights match this search");
}

function locationMatches(flight, side, value) {
  if (!value) return true;
  const queries = clientSearchTerms(searchTermFromValue(value));
  const fields = side === "origin"
    ? [flight.origin, flight.originCountry, flight.originIataCode, flight.originAirportName]
    : [flight.destination, flight.destinationCountry, flight.destinationIataCode, flight.destinationAirportName];
  return queries.some(query => fields.some(field => String(field || "").toLowerCase().includes(query)));
}

function clientSearchTerms(value) {
  const withoutCode = value.replace(/\s*\([A-Z0-9]{2,4}\)\s*$/, "");
  return [...withoutCode.split(","), withoutCode]
    .map(term => term.trim().toLowerCase())
    .filter(Boolean);
}

function compareFlights(a, b) {
  const mode = document.querySelector("#sortBy").value;
  if (mode === "price") return basePriceForFlight(a) - basePriceForFlight(b);
  return new Date(a.departureTime) - new Date(b.departureTime);
}

function recommendationScore(flight) {
  const price = basePriceForFlight(flight);
  const duration = durationMinutes(flight);
  const hoursFromNoon = Math.abs(new Date(flight.departureTime).getHours() - 12);
  const blockedPenalty = bookingBlockReason(flight) ? 10000 : 0;
  return price * 3 + duration + hoursFromNoon * 8 + blockedPenalty;
}

function flightCard(flight) {
  const reason = bookingBlockReason(flight);
  const origin = placeLabel(flight.origin, flight.originCountry);
  const destination = placeLabel(flight.destination, flight.destinationCountry);
  return `<article class="flight-card">
    <div class="flight-card-top"><strong>${Airport.escapeHtml(flight.flightNumber)}</strong>${Airport.badge(flight.status)}</div>
    <div class="flight-tags">${recommendationTags(flight).map(tag => `<span>${tag}</span>`).join("")}</div>
    <h3>${Airport.escapeHtml(origin)} -> ${Airport.escapeHtml(destination)}</h3>
    <div class="flight-times"><div><strong>${timeOnly(flight.departureTime)}</strong><small>${dateOnly(flight.departureTime)}</small></div><div class="flight-line"><span>${durationLabel(flight)}</span></div><div><strong>${timeOnly(flight.arrivalTime)}</strong><small>${dateOnly(flight.arrivalTime)}</small></div></div>
    <p>Gate ${Airport.escapeHtml(flight.gateNumber || "TBD")} - ${Airport.escapeHtml(flight.airlineName || flight.source || "Unknown")}</p>
    <div class="fare-row"><span>From</span><strong>${Airport.money(basePriceForFlight(flight))}</strong></div>
    <div class="row-actions"><button class="ghost-button" data-flight-details="${flight.id}">Details</button><button class="primary-button" data-book-flight="${flight.id}" ${reason ? "disabled" : ""}>${reason || "Book"}</button></div>
  </article>`;
}

function placeLabel(city, country) {
  return country ? `${city}, ${country}` : city;
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
    <article><small>Route</small><strong>${Airport.escapeHtml(placeLabel(selectedFlight.origin, selectedFlight.originCountry))} -> ${Airport.escapeHtml(placeLabel(selectedFlight.destination, selectedFlight.destinationCountry))}</strong></article>
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
  if (flight.status === "Cancelled" || flight.status === "Departed") return "Unavailable";
  if (new Date(flight.departureTime) <= new Date(Date.now() + BOOKING_CUTOFF_HOURS * 60 * 60 * 1000)) return "Closed";
  return "";
}

function openBooking(flightId) {
  selectedFlight = flights.find(f => f.id === Number(flightId));
  if (!selectedFlight || bookingBlockReason(selectedFlight)) return;
  document.querySelector("#bookingFlightId").value = selectedFlight.id;
  document.querySelector("#bookingFlightText").textContent = `${selectedFlight.flightNumber}: ${placeLabel(selectedFlight.origin, selectedFlight.originCountry)} -> ${placeLabel(selectedFlight.destination, selectedFlight.destinationCountry)} - ${Airport.formatDate(selectedFlight.departureTime)}`;
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
  return new Date(value).toLocaleDateString("en-GB", { day: "2-digit", month: "2-digit", year: "numeric" });
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
