const SEAT_SELECTION_PRICE = 18;
const BAGGAGE_PRICES = { 10: 35, 20: 60, 25: 75, 35: 95 };
const BOOKING_CUTOFF_HOURS = 1;
const SEARCH_MIN_DEPARTURE_HOURS = 1;
const CURRENCIES = [
  ["USD", "US Dollar"],
  ["ILS", "Israeli shekel"],
  ["EUR", "Euro"],
  ["GBP", "Pound sterling"],
  ["JPY", "Japanese yen"],
  ["RUB", "Russian ruble"]
];
let user = Airport.getUser();
let flights = [];
let visibleFlights = [];
let airportLocations = [];
let airportCountries = [];
let locationOptions = [];
let selectedFlight = null;
let hasSearched = false;
let selectedCurrency = localStorage.getItem("airport_currency") || "USD";
let exchangeRates = { USD: 1 };

if (!user || user.role !== "Passenger") location.href = "customer-login.html";
init();

function init() {
  document.querySelector("#userName").textContent = user.fullName;
  document.querySelector("#userEmail").textContent = user.email;
  document.querySelector("#logoutButton").addEventListener("click", logout);
  document.querySelector("#profileButton").addEventListener("click", openProfile);
  document.querySelector("#closeProfile").addEventListener("click", () => document.querySelector("#profileDialog").close());
  document.querySelector("#profileForm").addEventListener("submit", saveProfile);
  document.querySelector("#flightFilters").addEventListener("submit", e => {
    e.preventDefault();
    Airport.resetPagination("customer-flights");
    searchFlights().catch(error => Airport.toast(error.message));
  });
  setupLocationInput("#origin", "#originOptions");
  setupLocationInput("#destination", "#destinationOptions");
  renderCurrencyPicker();
  document.querySelector("#currencyPicker").addEventListener("change", event => {
    changeCurrency(event.target.value).catch(error => Airport.toast(error.message));
  });
  document.querySelector("#bookingClass").addEventListener("change", updateBookingPrice);
  document.querySelector("#bookingBaggage").addEventListener("change", updateBookingPrice);
  document.querySelector("#chooseSeat").addEventListener("change", event => {
    document.querySelector("#seatPickerRow").hidden = !event.target.checked;
    updateBookingPrice();
  });
  document.querySelector("#addBaggage").addEventListener("change", event => {
    document.querySelector("#baggagePickerRow").hidden = !event.target.checked;
    updateBookingPrice();
  });
  document.querySelector("#closeDetails").addEventListener("click", () => document.querySelector("#detailsDialog").close());
  document.querySelector("#detailsBookButton").addEventListener("click", () => {
    document.querySelector("#detailsDialog").close();
    if (selectedFlight) openBooking(selectedFlight.id);
  });
  document.querySelector("#closeBooking").addEventListener("click", () => document.querySelector("#bookingDialog").close());
  setupBackdropClose("#detailsDialog");
  setupBackdropClose("#bookingDialog");
  setupBackdropClose("#profileDialog");
  document.querySelector("#bookingForm").addEventListener("submit", bookTicket);
  document.querySelector("#purchaseOverlay").addEventListener("click", closePurchaseOverlay);
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

function setupBackdropClose(selector) {
  const dialog = document.querySelector(selector);
  if (!dialog) return;
  dialog.addEventListener("click", event => {
    const box = dialog.querySelector(".dialog-form");
    if (box && !box.contains(event.target)) dialog.close();
  });
}

async function changeCurrency(currency) {
  selectedCurrency = currency;
  localStorage.setItem("airport_currency", selectedCurrency);
  await ensureCurrencyRate();
  renderCurrencyPicker();
  renderFlights();
  if (selectedFlight && document.querySelector("#detailsDialog").open) openFlightDetails(selectedFlight.id);
  if (selectedFlight && document.querySelector("#bookingDialog").open) updateBookingPrice();
}

async function ensureCurrencyRate() {
  if (selectedCurrency === "USD" || exchangeRates[selectedCurrency]) return;

  try {
    const result = await Airport.api("/api/currency/rates");
    exchangeRates = { USD: 1, ...(result.rates || {}) };
    if (exchangeRates[selectedCurrency]) return;
  } catch {
    // Keep flights usable in USD when the external currency API is unavailable.
  }

  selectedCurrency = "USD";
  localStorage.setItem("airport_currency", selectedCurrency);
  renderCurrencyPicker();
  Airport.toast("Currency conversion is unavailable right now. Showing USD.");
}

function renderCurrencyPicker() {
  const picker = document.querySelector("#currencyPicker");
  if (!picker) return;
  picker.innerHTML = CURRENCIES.map(([code]) => `<option value="${code}" ${code === selectedCurrency ? "selected" : ""}>${code}</option>`).join("");
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

  setSearchLoading(true);
  try {
    const status = await Airport.api("/api/flights/schedule-status").catch(() => null);
    if (status && !status.ready) {
      Airport.toast("Flight schedule is still loading. Try again in a moment.");
      return;
    }

    flights = await Airport.api(`/api/flights?${query}`);
    await ensureCurrencyRate();
    renderAutocomplete();
    renderFlights();
  } catch (error) {
    Airport.toast(error.message === "Failed to fetch" ? "API is still starting. Try again in a moment." : error.message);
  } finally {
    setSearchLoading(false);
  }
}

function setSearchLoading(isLoading) {
  const loader = document.querySelector("#searchLoader");
  const button = document.querySelector("#flightFilters button");
  if (loader) loader.hidden = !isLoading;
  Airport.setPlaneLoading(isLoading, isLoading ? "Searching flights" : "Searching");
  if (button) button.disabled = isLoading;
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
  const payload = profilePayload();
  try {
    document.querySelector("#profileDialog").close();
    Airport.setPlaneLoading(true, "Your details are being updated");
    const [updated] = await Promise.all([Airport.api("/api/users/me", {
      method: "PUT",
      body: JSON.stringify(payload)
    }), Airport.delay(1200)]);
    user = { ...user, ...updated };
    localStorage.setItem("airport_ui_user", JSON.stringify(user));
    document.querySelector("#userName").textContent = user.fullName;
    document.querySelector("#userEmail").textContent = user.email;
    Airport.toast("Your details were updated.");
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function profilePayload() {
  return {
    nationalId: document.querySelector("#profileNationalId").value.trim(),
    firstName: document.querySelector("#profileFirstName").value.trim(),
    lastName: document.querySelector("#profileLastName").value.trim(),
    fullName: document.querySelector("#profileFullName").value.trim(),
    email: document.querySelector("#profileEmail").value.trim(),
    phoneNumber: document.querySelector("#profilePhone").value.trim(),
    gender: document.querySelector("#profileGender").value.trim(),
    address: document.querySelector("#profileAddress").value.trim()
  };
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

  if (!isExactAirport(origin)) {
    Airport.toast("Choose an origin country or city from the list.");
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

  const earliestDeparture = new Date(Date.now() + SEARCH_MIN_DEPARTURE_HOURS * 60 * 60 * 1000);
  const selectedStart = withTime(startDate, 0, 0);
  const start = selectedStart < earliestDeparture ? earliestDeparture : selectedStart;
  const end = withTime(endDate, 23, 59);

  if (end < start) {
    Airport.toast("Choose a date range with flights at least 1 hour from now.");
    return null;
  }

  params.set("from", start.toISOString());
  params.set("to", end.toISOString());
  params.set("bookableOnly", "true");
  params.set("originSearch", searchTermForLocation(document.querySelector("#origin")));

  const destination = document.querySelector("#destination").value.trim();
  if (destination) {
    if (!isExactAirport(destination)) {
      Airport.toast("Choose a destination country or city from the list.");
      return null;
    }
    params.set("destinationSearch", searchTermForLocation(document.querySelector("#destination")));
  }

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
  const locationEntries = allLocations.map(item => {
    const value = locationValue(item);
    const legacyValue = legacyLocationValue(item);
    const codeValue = item.iataCode ? `${item.city || ""}, ${item.country || ""} (${item.iataCode})` : "";
    return {
      type: "city",
      value,
      label: "",
      searchValue: item.city || item.country || item.iataCode || item.icaoCode || value,
      aliases: [value, legacyValue, codeValue].filter(Boolean).map(text => text.toLowerCase()),
      tokens: [item.city, item.country, item.iataCode, item.icaoCode, item.airportName, value, legacyValue].filter(Boolean)
    };
  });
  const countryEntries = airportCountries.map(country => ({
    type: "country",
    value: country,
    label: "Country",
    searchValue: country,
    aliases: [country.toLowerCase()],
    tokens: [country]
  }));

  locationOptions = [...countryEntries, ...locationEntries].sort((a, b) => a.value.localeCompare(b.value));
  updateLocationOptions(document.querySelector("#origin").value, "#originOptions");
  updateLocationOptions(document.querySelector("#destination").value, "#destinationOptions");
}

function isExactAirport(value) {
  const normalized = value.toLowerCase();
  return locationOptions.some(option => option.aliases.includes(normalized));
}

function mergeLocations(locations) {
  const byCity = new Map();
  locations.forEach(item => {
    if (!item.country && !item.city) return;
    const key = `${item.country || ""}|${item.city || ""}`.toLowerCase();
    if (!byCity.has(key)) byCity.set(key, item);
  });
  return [...byCity.values()].sort((a, b) => locationLabel(a).localeCompare(locationLabel(b)));
}

function locationLabel(item) {
  return [item.country, item.city].filter(Boolean).join(", ");
}

function locationValue(item) {
  return locationLabel(item);
}

function legacyLocationValue(item) {
  const code = item.iataCode || item.icaoCode || "";
  const cityCountry = [item.city, item.country].filter(Boolean).join(", ");
  const codeText = code ? ` (${code})` : "";
  const airportText = item.airportName ? ` - ${item.airportName}` : "";
  return `${cityCountry}${codeText}${airportText}`;
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
  const normalized = value.toLowerCase();
  const match = locationOptions.find(option => option.aliases.includes(normalized));
  if (match) return match.searchValue;
  return value.replace(/\s*\([^)]*\)\s*$/, "").split(",").map(part => part.trim()).filter(Boolean).slice(-1)[0] || value;
}

function renderFlights() {
  const origin = document.querySelector("#origin").value.trim();
  const destination = document.querySelector("#destination").value.trim();
  visibleFlights = flights
    .filter(f => isAtLeastOneHourAhead(f))
    .filter(f => f.status !== "Cancelled" && f.status !== "Departed")
    .filter(f => locationMatches(f, "origin", origin))
    .filter(f => locationMatches(f, "destination", destination))
    .sort(compareFlights);
  document.querySelector("#metricFlights").textContent = visibleFlights.length;
  document.querySelector("#metricOutgoing").textContent = new Set(visibleFlights.map(f => f.destination)).size;
  document.querySelector("#metricIncoming").textContent = new Set(visibleFlights.map(f => dateOnly(f.departureTime))).size;
  document.querySelector("#metricBestFare").textContent = visibleFlights.length ? formatFare(Math.min(...visibleFlights.map(basePriceForFlight))) : "-";
  document.querySelector("#searchResultCount").textContent = `${visibleFlights.length} ${visibleFlights.length === 1 ? "result" : "results"}`;
  document.querySelector("#resultsPanel").hidden = false;
  document.querySelector("#resultsTitle").textContent = destination ? `${origin} to ${destination}` : `Flights from ${origin}`;
  document.querySelector("#flightsGrid").innerHTML = Airport.paginatedList("customer-flights", visibleFlights, flightRow, "No flights match this search", { onPageChange: renderFlights });
}

function locationMatches(flight, side, value) {
  if (!value) return true;
  const queries = clientSearchTerms(searchTermFromValue(value));
  const codeFields = side === "origin"
    ? [flight.originAirportCode, flight.originIataCode]
    : [flight.destinationAirportCode, flight.destinationIataCode];
  const fields = side === "origin"
    ? [flight.origin, flight.originCountry, flight.originAirportName]
    : [flight.destination, flight.destinationCountry, flight.destinationAirportName];
  const codeQuery = queries.find(isAirportCodeQuery);
  if (codeQuery) {
    return codeFields.some(field => String(field || "").toLowerCase() === codeQuery);
  }

  return queries.some(query => fields.some(field => String(field || "").toLowerCase().includes(query)));
}

function isAtLeastOneHourAhead(flight) {
  return new Date(flight.departureTime) >= new Date(Date.now() + SEARCH_MIN_DEPARTURE_HOURS * 60 * 60 * 1000);
}

function clientSearchTerms(value) {
  const withoutCode = value.replace(/\s*\([A-Z0-9]{2,4}\)\s*$/, "");
  return [...withoutCode.split(","), withoutCode]
    .map(term => term.trim().toLowerCase())
    .filter(Boolean);
}

function isAirportCodeQuery(value) {
  return /^[a-z0-9]{2,4}$/.test(value);
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

function flightRow(flight) {
  const reason = bookingBlockReason(flight);
  const origin = placeLabel(flight.origin, flight.originCountry);
  const destination = placeLabel(flight.destination, flight.destinationCountry);
  return `<article class="flight-row">
    <div class="flight-row-main">
      <div class="flight-row-title">
        <strong>${Airport.escapeHtml(flight.flightNumber)}</strong>
        ${Airport.badge(flight.status)}
        ${recommendationTags(flight).map(tag => `<span class="mini-tag">${tag}</span>`).join("")}
      </div>
      <div class="flight-row-route">${Airport.escapeHtml(origin)} <span>-></span> ${Airport.escapeHtml(destination)}</div>
      <div class="flight-row-subroute">${Airport.escapeHtml(airportLabel(flight.originAirportName, flight.originIataCode || flight.originAirportCode))} to ${Airport.escapeHtml(airportLabel(flight.destinationAirportName, flight.destinationIataCode || flight.destinationAirportCode))}</div>
    </div>
    <div class="flight-cell"><small>Depart</small><strong>${timeOnly(flight.departureTime)}</strong><span>${dateOnly(flight.departureTime)}</span></div>
    <div class="flight-cell"><small>Arrive</small><strong>${timeOnly(flight.arrivalTime)}</strong><span>${dateOnly(flight.arrivalTime)}</span></div>
    <div class="flight-cell"><small>Duration</small><strong>${durationLabel(flight)}</strong><span>${Airport.escapeHtml(flight.boardingGate || flight.gateNumber || "TBD")}</span></div>
    <div class="flight-cell"><small>Aircraft</small><strong>${Airport.escapeHtml([flight.aircraftManufacturer, flight.aircraftModel].filter(Boolean).join(" ") || flight.aircraftCode || "TBD")}</strong><span>${Airport.escapeHtml(flight.seatCapacity || "TBD")} seats</span></div>
    <div class="flight-row-fare"><small>From</small><strong>${formatFare(basePriceForFlight(flight))}</strong></div>
    <div class="flight-row-actions">
      <button class="ghost-button" data-flight-details="${flight.id}">Details</button>
      <button class="primary-button" data-book-flight="${flight.id}" ${reason ? "disabled" : ""}>${reason || "Book"}</button>
    </div>
  </article>`;
}

function airportLabel(name, code) {
  return [name, code ? `(${code})` : ""].filter(Boolean).join(" ") || "Airport";
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
    <article><small>Gate</small><strong>${Airport.escapeHtml(selectedFlight.boardingGate || selectedFlight.gateNumber || "TBD")}</strong></article>
    <article><small>Status</small><strong>${selectedFlight.status}</strong></article>
    <article><small>Aircraft</small><strong>${Airport.escapeHtml([selectedFlight.aircraftManufacturer, selectedFlight.aircraftModel].filter(Boolean).join(" ") || selectedFlight.aircraftCode || "Unknown")}</strong></article>
    <article><small>Seats</small><strong>${Airport.escapeHtml(selectedFlight.seatCapacity || "TBD")}</strong></article>
    <article><small>Economy from</small><strong>${formatFare(basePriceForFlight(selectedFlight))}</strong></article>
    <article><small>Business from</small><strong>${formatFare(classTotal(selectedFlight, "Business"))}</strong></article>
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
  document.querySelector("#chooseSeat").checked = false;
  document.querySelector("#seatPickerRow").hidden = true;
  document.querySelector("#bookingSeat").value = "";
  document.querySelector("#bookingClass").value = "Economy";
  document.querySelector("#addBaggage").checked = false;
  document.querySelector("#baggagePickerRow").hidden = true;
  document.querySelector("#bookingBaggage").value = "10";
  updateBookingPrice();
  document.querySelector("#bookingDialog").showModal();
}

function updateBookingPrice() {
  const base = basePriceForFlight(selectedFlight);
  const klass = document.querySelector("#bookingClass").value;
  const classExtra = classTotal(selectedFlight, klass) - base;
  const seat = document.querySelector("#chooseSeat").checked ? SEAT_SELECTION_PRICE : 0;
  const bag = document.querySelector("#addBaggage").checked ? BAGGAGE_PRICES[document.querySelector("#bookingBaggage").value] || 0 : 0;
  document.querySelector("#flightPriceText").textContent = formatFare(base);
  document.querySelector("#seatPriceText").textContent = formatFare(seat);
  document.querySelector("#bagPriceText").textContent = formatFare(bag);
  document.querySelector("#classPriceText").textContent = formatFare(classExtra);
  document.querySelector("#bookingPrice").value = (base + classExtra + seat + bag).toFixed(2);
}

function formatFare(usdValue) {
  const rate = exchangeRates[selectedCurrency] || 1;
  return Airport.money(Number(usdValue || 0) * rate, selectedCurrency);
}

function basePriceForFlight(flight) {
  if (flight?.basePrice) return Number(flight.basePrice);
  const hoursAway = flight ? (new Date(flight.departureTime) - Date.now()) / 36e5 : 999;
  const routeWeight = flight ? Math.abs((flight.destination || "").length - (flight.origin || "").length) * 6 : 0;
  const demand = flight?.status === "Boarding" ? 50 : flight?.status === "Delayed" ? -20 : 0;
  return Math.max(95, (hoursAway < 48 ? 260 : hoursAway < 168 ? 220 : 180) + routeWeight + demand);
}

function classTotal(flight, klass) {
  const base = basePriceForFlight(flight);
  if (klass === "First") return base * 4.1;
  if (klass === "Business") return base * 2.4;
  return base;
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
  const payload = {
    flightId: Number(document.querySelector("#bookingFlightId").value),
    class: document.querySelector("#bookingClass").value,
    baggageAllowanceKg: document.querySelector("#addBaggage").checked ? Number(document.querySelector("#bookingBaggage").value) : 0
  };

  const seatNumber = document.querySelector("#chooseSeat").checked ? document.querySelector("#bookingSeat").value.trim() : "";
  if (user.nationalId) payload.passengerNationalId = user.nationalId;
  if (user.email) payload.passengerEmail = user.email;
  if (seatNumber) payload.seatNumber = seatNumber;

  try {
    document.querySelector("#bookingDialog").close();
    openPurchaseOverlay();
    const ticket = await Airport.api("/api/tickets", {
      method: "POST",
      skipLoader: true,
      body: JSON.stringify(payload)
    });
    await showPurchaseConfirmation(ticket);
  } catch (error) {
    closePurchaseOverlay();
    Airport.toast(error.message);
  }
}

function openPurchaseOverlay() {
  const overlay = document.querySelector("#purchaseOverlay");
  const progressText = document.querySelector("#purchaseAnimation strong");
  if (progressText) progressText.textContent = "We are booking your flight";
  document.querySelector("#purchaseAnimation").hidden = false;
  document.querySelector("#purchaseSuccess").hidden = true;
  overlay.hidden = false;
}

async function showPurchaseConfirmation() {
  await Airport.delay(5000);
  document.querySelector("#purchaseAnimation").hidden = true;
  document.querySelector("#purchaseSuccess").hidden = false;
}

function closePurchaseOverlay() {
  const overlay = document.querySelector("#purchaseOverlay");
  overlay.hidden = true;
  document.querySelector("#purchaseAnimation").hidden = false;
  document.querySelector("#purchaseSuccess").hidden = true;
}
