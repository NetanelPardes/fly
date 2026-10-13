const SEAT_SELECTION_PRICE = 18;
const BAGGAGE_PRICES = { 10: 35, 20: 60, 25: 75, 35: 95 };
const BOOKING_CUTOFF_HOURS = 1;
const SEARCH_MIN_DEPARTURE_HOURS = 1;
const MAX_CONNECTIONS = 3;
const MIN_CONNECTION_MINUTES = 60;
const MAX_CONNECTION_MINUTES = 24 * 60;
const MAX_DIRECT_RESULTS = 200;
const MAX_CONNECTION_RESULTS = 120;
const CONNECTION_RESULT_LIMITS = { 1: 50, 2: 50, 3: 20 };
const MAX_CONNECTION_CANDIDATES_PER_LEG = 80;
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
let searchFlightsPool = [];
let visibleFlights = [];
let flightsAreRoutes = false;
let airportLocations = [];
let airportCountries = [];
let locationOptions = [];
let selectedFlight = null;
let hasSearched = false;
let selectedCurrency = localStorage.getItem("airport_currency") || "USD";
let exchangeRates = { USD: 1 };
let selectedBookingRoute = null;
let bookingLegIndex = 0;
let bookingLegOptions = [];

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
  document.querySelector("#sortBy").addEventListener("change", () => {
    Airport.resetPagination("customer-flights");
    if (hasSearched && flightsAreRoutes) {
      searchFlights().catch(error => Airport.toast(error.message));
      return;
    }

    renderFlights();
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
    setBookingOptionRowVisible("seatPickerRow", event.target.checked);
    updateBookingPrice();
  });
  document.querySelector("#addBaggage").addEventListener("change", event => {
    setBookingOptionRowVisible("baggagePickerRow", event.target.checked);
    updateBookingPrice();
  });
  document.querySelector("#closeDetails").addEventListener("click", () => document.querySelector("#detailsDialog").close());
  document.querySelector("#detailsBookButton").addEventListener("click", () => {
    document.querySelector("#detailsDialog").close();
    if (selectedFlight) openBooking(selectedFlight.routeId || selectedFlight.id);
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

    if (query.useConnectionSearch) {
      const routeQuery = new URLSearchParams(query);
      routeQuery.set("originSearch", query.routeOriginSearch);
      routeQuery.set("destinationSearch", query.routeDestinationSearch);
      routeQuery.set("sortBy", document.querySelector("#sortBy").value);
      flights = await Airport.api(`/api/flights/routes?${routeQuery}`);
      flightsAreRoutes = true;
      searchFlightsPool = [];
    } else {
      const queryText = query.toString();
      flights = await Airport.api(`/api/flights?${queryText}`);
      flightsAreRoutes = false;
      searchFlightsPool = [];
    }
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

  const destination = document.querySelector("#destination").value.trim();
  if (destination) {
    if (!isExactAirport(destination)) {
      Airport.toast("Choose a destination country or city from the list.");
      return null;
    }
    params.useConnectionSearch = true;
    params.routeOriginSearch = searchTermForLocation(document.querySelector("#origin"));
    params.routeDestinationSearch = searchTermForLocation(document.querySelector("#destination"));
  } else {
    params.set("originSearch", searchTermForLocation(document.querySelector("#origin")));
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
  searchFlightsPool = [];
  flightsAreRoutes = false;
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
  if (flightsAreRoutes) {
    visibleFlights = flights
      .filter(route => routeLegs(route).every(leg => isAtLeastOneHourAhead(leg) && leg.status !== "Cancelled" && leg.status !== "Departed"))
      .filter(route => routeMatchesSearch(route, origin, destination))
      .sort(compareFlights);
    updateFlightResultSummary(origin, destination);
    return;
  }

  const directFlights = flights
    .filter(f => isAtLeastOneHourAhead(f))
    .filter(f => f.status !== "Cancelled" && f.status !== "Departed")
    .filter(f => locationMatches(f, "origin", origin))
    .filter(f => locationMatches(f, "destination", destination));
  const connectionRoutes = destination ? findConnectionRoutes(origin, destination, directFlights) : [];
  const sortedDirectFlights = directFlights
    .map(singleFlightRoute)
    .sort(compareFlights)
    .slice(0, MAX_DIRECT_RESULTS);
  visibleFlights = [...sortedDirectFlights, ...connectionRoutes]
    .sort(compareFlights);
  updateFlightResultSummary(origin, destination);
}

function updateFlightResultSummary(origin, destination) {
  document.querySelector("#metricFlights").textContent = visibleFlights.length;
  document.querySelector("#metricOutgoing").textContent = new Set(visibleFlights.map(routeDestination)).size;
  document.querySelector("#metricIncoming").textContent = new Set(visibleFlights.map(route => dateOnly(routeDepartureTime(route)))).size;
  document.querySelector("#metricBestFare").textContent = visibleFlights.length ? formatFare(Math.min(...visibleFlights.map(routeBasePrice))) : "-";
  document.querySelector("#searchResultCount").textContent = `${visibleFlights.length} ${visibleFlights.length === 1 ? "result" : "results"}`;
  document.querySelector("#resultsPanel").hidden = false;
  document.querySelector("#resultsTitle").textContent = destination ? `${origin} to ${destination}` : `Flights from ${origin}`;
  document.querySelector("#flightsGrid").innerHTML = Airport.paginatedList("customer-flights", visibleFlights, flightRow, "No flights match this search", { onPageChange: renderFlights });
}

function singleFlightRoute(flight) {
  return { ...flight, isConnectionRoute: false, legs: [flight], connectionCount: 0, routeId: `flight-${flight.id}` };
}

function findConnectionRoutes(origin, destination, directFlights) {
  const directIds = new Set(directFlights.map(flight => flight.id));
  const pool = (searchFlightsPool.length ? searchFlightsPool : flights)
    .filter(f => isAtLeastOneHourAhead(f))
    .filter(f => f.status !== "Cancelled" && f.status !== "Departed")
    .sort((a, b) => new Date(a.departureTime) - new Date(b.departureTime));
  const results = [];
  const resultCounts = { 1: 0, 2: 0, 3: 0 };
  const seenRoutes = new Set();
  const startFlights = pool.filter(flight => locationMatches(flight, "origin", origin));
  const connectionIndex = buildConnectionIndex(pool);

  for (const start of startFlights) {
    exploreConnectionRoute([start], new Set([connectionNodeKey(start, "origin")]));
    if (allConnectionLimitsReached()) break;
  }

  return results
    .filter(route => !directIds.has(route.legs[0].id) || route.connectionCount > 0)
    .sort((a, b) => routeConnectionCount(a) - routeConnectionCount(b) || routeBasePrice(a) - routeBasePrice(b) || new Date(routeDepartureTime(a)) - new Date(routeDepartureTime(b)))
    .slice(0, MAX_CONNECTION_RESULTS);

  function exploreConnectionRoute(legs, visitedAirports) {
    const last = legs[legs.length - 1];
    const destinationKey = connectionNodeKey(last, "destination");

    if (locationMatches(last, "destination", destination) && legs.length > 1) {
      const connectionCount = legs.length - 1;
      if (connectionLimitReached(connectionCount)) return;
      const routeKeyText = legs.map(leg => leg.id).join("-");
      if (!seenRoutes.has(routeKeyText)) {
        seenRoutes.add(routeKeyText);
        results.push(connectionRoute(legs));
        resultCounts[connectionCount] += 1;
      }
      return;
    }

    if (legs.length >= MAX_CONNECTIONS + 1 || allConnectionLimitsReached()) return;
    if (visitedAirports.has(destinationKey)) return;
    const nextVisited = new Set(visitedAirports);
    nextVisited.add(destinationKey);

    for (const next of nextConnectionCandidates(last, connectionIndex)) {
      if (!isValidConnection(last, next)) continue;
      if (nextVisited.has(connectionNodeKey(next, "destination"))) continue;
      exploreConnectionRoute([...legs, next], nextVisited);
      if (allConnectionLimitsReached()) break;
    }
  }

  function connectionLimitReached(connectionCount) {
    return resultCounts[connectionCount] >= (CONNECTION_RESULT_LIMITS[connectionCount] || 0);
  }

  function allConnectionLimitsReached() {
    return connectionLimitReached(1) && connectionLimitReached(2) && connectionLimitReached(3);
  }
}

function buildConnectionIndex(pool) {
  const index = new Map();
  for (const flight of pool) {
    for (const key of connectionOriginKeys(flight)) {
      if (!index.has(key)) index.set(key, []);
      index.get(key).push(flight);
    }
  }

  for (const options of index.values()) {
    options.sort((a, b) => new Date(a.departureTime) - new Date(b.departureTime));
  }

  return index;
}

function nextConnectionCandidates(previous, index) {
  const earliest = new Date(previous.arrivalTime).getTime() + MIN_CONNECTION_MINUTES * 60000;
  const latest = new Date(previous.arrivalTime).getTime() + MAX_CONNECTION_MINUTES * 60000;
  const candidates = new Map();

  for (const key of connectionDestinationKeys(previous)) {
    const options = index.get(key) || [];
    for (const next of options) {
      if (next.id === previous.id) continue;
      const departure = new Date(next.departureTime).getTime();
      if (departure < earliest) continue;
      if (departure > latest) break;
      candidates.set(next.id, next);
      if (candidates.size >= MAX_CONNECTION_CANDIDATES_PER_LEG) break;
    }
    if (candidates.size >= MAX_CONNECTION_CANDIDATES_PER_LEG) break;
  }

  return [...candidates.values()];
}

function connectionRoute(legs) {
  const first = legs[0];
  const last = legs[legs.length - 1];
  return {
    ...first,
    id: `connection-${legs.map(leg => leg.id).join("-")}`,
    routeId: `connection-${legs.map(leg => leg.id).join("-")}`,
    isConnectionRoute: true,
    legs,
    connectionCount: legs.length - 1,
    flightNumber: legs.map(leg => leg.flightNumber).join(" + "),
    destination: last.destination,
    destinationAirportCode: last.destinationAirportCode,
    destinationAirportName: last.destinationAirportName,
    destinationIataCode: last.destinationIataCode,
    destinationCountry: last.destinationCountry,
    arrivalTime: last.arrivalTime,
    basePrice: legs.reduce((sum, leg) => sum + basePriceForFlight(leg), 0)
  };
}

function isValidConnection(previous, next) {
  const waitMinutes = Math.round((new Date(next.departureTime) - new Date(previous.arrivalTime)) / 60000);
  return waitMinutes >= MIN_CONNECTION_MINUTES && waitMinutes <= MAX_CONNECTION_MINUTES;
}

function airportKey(flight, side) {
  const code = side === "origin"
    ? flight.originIataCode || flight.originAirportCode
    : flight.destinationIataCode || flight.destinationAirportCode;
  if (code) return String(code).trim().toLowerCase();

  const city = side === "origin" ? flight.origin : flight.destination;
  const country = side === "origin" ? flight.originCountry : flight.destinationCountry;
  const airportName = side === "origin" ? flight.originAirportName : flight.destinationAirportName;
  return [city, country, airportName].filter(Boolean).map(value => String(value).trim().toLowerCase()).join("|");
}

function connectionOriginKeys(flight) {
  return connectionKeys(flight, "origin");
}

function connectionDestinationKeys(flight) {
  return connectionKeys(flight, "destination");
}

function connectionKeys(flight, side) {
  const keys = new Set();
  const airport = airportKey(flight, side);
  if (airport) keys.add(`airport:${airport}`);

  const country = side === "origin" ? flight.originCountry : flight.destinationCountry;
  if (country) keys.add(`country:${String(country).trim().toLowerCase()}`);

  const city = side === "origin" ? flight.origin : flight.destination;
  if (city) keys.add(`city:${String(city).trim().toLowerCase()}`);

  return [...keys];
}

function connectionNodeKey(flight, side) {
  const country = side === "origin" ? flight.originCountry : flight.destinationCountry;
  if (country) return `country:${String(country).trim().toLowerCase()}`;
  return `airport:${airportKey(flight, side)}`;
}

function routeConnectionCount(route) {
  return route.connectionCount || 0;
}

function routeLegs(route) {
  return route.legs?.length ? route.legs : [route];
}

function routeDepartureTime(route) {
  return routeLegs(route)[0].departureTime;
}

function routeArrivalTime(route) {
  return routeLegs(route).at(-1).arrivalTime;
}

function routeBasePrice(route) {
  return routeLegs(route).reduce((sum, leg) => sum + basePriceForFlight(leg), 0);
}

function routeDurationMinutes(route) {
  return Math.max(1, Math.round((new Date(routeArrivalTime(route)) - new Date(routeDepartureTime(route))) / 60000));
}

function routeDestination(route) {
  return routeLegs(route).at(-1).destination;
}

function routeMatchesSearch(route, origin, destination) {
  const legs = routeLegs(route);
  const first = legs[0];
  const last = legs.at(-1);
  return locationMatches(first, "origin", origin) && locationMatches(last, "destination", destination);
}

function routeStopLabel(route) {
  const count = routeConnectionCount(route);
  if (count === 0) return "Direct";
  if (count === 1) return "1 connection";
  return `${count} connections`;
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
  if (mode === "connections") return routeConnectionCount(a) - routeConnectionCount(b) || new Date(routeDepartureTime(a)) - new Date(routeDepartureTime(b)) || routeBasePrice(a) - routeBasePrice(b);
  if (mode === "price") return routeBasePrice(a) - routeBasePrice(b) || routeConnectionCount(a) - routeConnectionCount(b);
  return new Date(routeDepartureTime(a)) - new Date(routeDepartureTime(b)) || routeConnectionCount(a) - routeConnectionCount(b);
}

function recommendationScore(flight) {
  const price = routeBasePrice(flight);
  const duration = routeDurationMinutes(flight);
  const hoursFromNoon = Math.abs(new Date(routeDepartureTime(flight)).getHours() - 12);
  const blockedPenalty = bookingBlockReason(flight) ? 10000 : 0;
  return price * 3 + duration + hoursFromNoon * 8 + routeConnectionCount(flight) * 90 + blockedPenalty;
}

function flightRow(flight) {
  const reason = bookingBlockReason(flight);
  const legs = routeLegs(flight);
  const first = legs[0];
  const last = legs.at(-1);
  const origin = placeLabel(first.origin, first.originCountry);
  const destination = placeLabel(last.destination, last.destinationCountry);
  const routeId = Airport.escapeHtml(flight.routeId || String(flight.id));
  const primaryFlightId = first.id;
  const action = flight.isConnectionRoute
    ? `<button class="primary-button" data-flight-details="${routeId}">View route</button>`
    : `<button class="primary-button" data-book-flight="${primaryFlightId}" ${reason ? "disabled" : ""}>${reason || "Book"}</button>`;
  return `<article class="flight-row">
    <div class="flight-row-main">
      <div class="flight-row-title">
        <strong>${Airport.escapeHtml(flight.flightNumber || routeFlightNumbers(flight))}</strong>
        <span class="mini-tag">${Airport.escapeHtml(routeStopLabel(flight))}</span>
        ${Airport.badge(first.status)}
        ${recommendationTags(flight).map(tag => `<span class="mini-tag">${tag}</span>`).join("")}
      </div>
      <div class="flight-row-route">${Airport.escapeHtml(origin)} <span>-></span> ${Airport.escapeHtml(destination)}</div>
      <div class="flight-row-subroute">${Airport.escapeHtml(routeAirportSummary(flight))}</div>
    </div>
    <div class="flight-cell"><small>Depart</small><strong>${timeOnly(routeDepartureTime(flight))}</strong><span>${dateOnly(routeDepartureTime(flight))}</span></div>
    <div class="flight-cell"><small>Arrive</small><strong>${timeOnly(routeArrivalTime(flight))}</strong><span>${dateOnly(routeArrivalTime(flight))}</span></div>
    <div class="flight-cell"><small>Duration</small><strong>${routeDurationLabel(flight)}</strong><span>${Airport.escapeHtml(connectionWaitLabel(flight))}</span></div>
    <div class="flight-cell"><small>Legs</small><strong>${legs.length}</strong><span>${Airport.escapeHtml(routeFlightNumbers(flight))}</span></div>
    <div class="flight-row-fare"><small>From</small><strong>${formatFare(routeBasePrice(flight))}</strong></div>
    <div class="flight-row-actions">
      <button class="ghost-button" data-flight-details="${routeId}">Details</button>
      ${action}
    </div>
  </article>`;
}

function routeAirportSummary(route) {
  const legs = routeLegs(route);
  return legs
    .map((leg, index) => index === 0
      ? airportLabel(leg.originAirportName, leg.originIataCode || leg.originAirportCode)
      : airportLabel(leg.destinationAirportName, leg.destinationIataCode || leg.destinationAirportCode))
    .join(" to ");
}

function routeFlightNumbers(route) {
  return routeLegs(route).map(leg => leg.flightNumber).join(", ");
}

function connectionWaitLabel(route) {
  const legs = routeLegs(route);
  if (legs.length === 1) return legs[0].boardingGate || legs[0].gateNumber || "TBD";
  const waits = [];
  for (let index = 0; index < legs.length - 1; index++) {
    waits.push(Math.round((new Date(legs[index + 1].departureTime) - new Date(legs[index].arrivalTime)) / 60000));
  }
  return `Layover ${durationFromMinutes(Math.min(...waits))}`;
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
  if (active.length && routeBasePrice(flight) === Math.min(...active.map(routeBasePrice))) tags.push("Cheapest");
  if (!bookingBlockReason(flight) && recommendationScore(flight) === Math.min(...active.map(recommendationScore))) tags.push("Recommended");
  return tags.slice(0, 2);
}

function openFlightDetails(flightId) {
  selectedFlight = visibleFlights.find(f => String(f.routeId || f.id) === String(flightId))
    || flights.find(f => String(f.id) === String(flightId));
  if (!selectedFlight) return;
  if (selectedFlight.legs?.length === 1 && !selectedFlight.isConnectionRoute) selectedFlight = selectedFlight.legs[0];
  const reason = bookingBlockReason(selectedFlight);
  document.querySelector("#detailsTitle").textContent = selectedFlight.isConnectionRoute ? `${routeStopLabel(selectedFlight)} route details` : `${selectedFlight.flightNumber} flight details`;
  document.querySelector("#detailsBody").innerHTML = selectedFlight.isConnectionRoute
    ? connectionDetailsHtml(selectedFlight)
    : `<div class="detail-grid">
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
  button.textContent = reason || (selectedFlight.isConnectionRoute ? "Book this route" : "Continue to booking");
  document.querySelector("#detailsDialog").showModal();
}

function connectionDetailsHtml(route) {
  const legs = routeLegs(route);
  const legRows = legs.map((leg, index) => {
    const wait = index === legs.length - 1 ? "" : `<span class="connection-wait">Connection wait: ${durationFromMinutes(Math.round((new Date(legs[index + 1].departureTime) - new Date(leg.arrivalTime)) / 60000))}</span>`;
    return `<article class="connection-leg">
      <small>Leg ${index + 1}</small>
      <strong>${Airport.escapeHtml(leg.flightNumber)}: ${Airport.escapeHtml(placeLabel(leg.origin, leg.originCountry))} -> ${Airport.escapeHtml(placeLabel(leg.destination, leg.destinationCountry))}</strong>
      <span>${Airport.formatDate(leg.departureTime)} to ${Airport.formatDate(leg.arrivalTime)}</span>
      ${wait}
    </article>`;
  }).join("");

  return `<div class="detail-grid">
    <article><small>Route</small><strong>${Airport.escapeHtml(routeAirportSummary(route))}</strong></article>
    <article><small>Connections</small><strong>${routeConnectionCount(route)}</strong></article>
    <article><small>Total duration</small><strong>${routeDurationLabel(route)}</strong></article>
    <article><small>Total from</small><strong>${formatFare(routeBasePrice(route))}</strong></article>
  </div>
  <div class="connection-legs">${legRows}</div>
  <p class="notice">Connection routes are calculated from available flights. You can choose options for each leg and book all legs together.</p>`;
}

function bookingBlockReason(flight) {
  if (flight.isConnectionRoute) {
    return routeLegs(flight).some(leg => bookingBlockReason(leg)) ? "Unavailable" : "";
  }
  if (flight.status === "Cancelled" || flight.status === "Departed") return "Unavailable";
  if (new Date(routeDepartureTime(flight)) <= new Date(Date.now() + BOOKING_CUTOFF_HOURS * 60 * 60 * 1000)) return "Closed";
  return "";
}

function openBooking(flightId) {
  selectedBookingRoute = visibleFlights.find(f => String(f.routeId || f.id) === String(flightId))
    || visibleFlights.find(f => !f.isConnectionRoute && String(routeLegs(f)[0].id) === String(flightId))
    || flights.find(f => String(f.id) === String(flightId));
  if (!selectedBookingRoute) return;

  const legs = routeLegs(selectedBookingRoute);
  if (legs.some(leg => bookingBlockReason(leg))) {
    Airport.toast("One of the route legs cannot be booked.");
    return;
  }

  bookingLegIndex = 0;
  bookingLegOptions = legs.map(() => defaultBookingOptions());
  renderBookingLeg();
  document.querySelector("#bookingDialog").showModal();
}

function renderBookingLeg() {
  const legs = routeLegs(selectedBookingRoute);
  selectedFlight = legs[bookingLegIndex];
  const options = bookingLegOptions[bookingLegIndex] || defaultBookingOptions();
  document.querySelector("#bookingTitle").textContent = legs.length > 1 ? `Book flight ${bookingLegIndex + 1} of ${legs.length}` : "Book flight";
  document.querySelector("#bookingFlightId").value = selectedFlight.id;
  document.querySelector("#bookingFlightText").textContent = `${selectedFlight.flightNumber}: ${placeLabel(selectedFlight.origin, selectedFlight.originCountry)} -> ${placeLabel(selectedFlight.destination, selectedFlight.destinationCountry)} - ${Airport.formatDate(selectedFlight.departureTime)}`;
  document.querySelector("#chooseSeat").checked = options.chooseSeat;
  setBookingOptionRowVisible("seatPickerRow", options.chooseSeat);
  document.querySelector("#bookingSeat").value = options.seatNumber;
  document.querySelector("#bookingClass").value = options.klass;
  document.querySelector("#addBaggage").checked = options.addBaggage;
  setBookingOptionRowVisible("baggagePickerRow", options.addBaggage);
  document.querySelector("#bookingBaggage").value = String(options.baggageAllowanceKg || 10);
  document.querySelector("#bookingSubmitButton").textContent = bookingLegIndex < legs.length - 1 ? "Continue to next flight" : legs.length > 1 ? "Book all tickets" : "Book ticket";
  updateBookingPrice();
}

function setBookingOptionRowVisible(rowId, visible) {
  const row = document.querySelector(`#${rowId}`);
  row.classList.toggle("is-visible", visible);
  row.setAttribute("aria-hidden", String(!visible));
  row.querySelectorAll("input, select").forEach(control => {
    control.disabled = !visible;
    control.tabIndex = visible ? 0 : -1;
  });
}

function defaultBookingOptions() {
  return {
    chooseSeat: false,
    seatNumber: "",
    klass: "Economy",
    addBaggage: false,
    baggageAllowanceKg: 10
  };
}

function readBookingOptions() {
  return {
    chooseSeat: document.querySelector("#chooseSeat").checked,
    seatNumber: document.querySelector("#bookingSeat").value.trim(),
    klass: document.querySelector("#bookingClass").value,
    addBaggage: document.querySelector("#addBaggage").checked,
    baggageAllowanceKg: Number(document.querySelector("#bookingBaggage").value)
  };
}

function saveCurrentBookingOptions() {
  bookingLegOptions[bookingLegIndex] = readBookingOptions();
}

function updateBookingPrice() {
  if (!selectedFlight) return;
  const base = basePriceForFlight(selectedFlight);
  const klass = document.querySelector("#bookingClass").value;
  const classExtra = classTotal(selectedFlight, klass) - base;
  const seat = document.querySelector("#chooseSeat").checked ? SEAT_SELECTION_PRICE : 0;
  const bag = document.querySelector("#addBaggage").checked ? BAGGAGE_PRICES[document.querySelector("#bookingBaggage").value] || 0 : 0;
  document.querySelector("#flightPriceText").textContent = formatFare(base);
  document.querySelector("#seatPriceText").textContent = formatFare(seat);
  document.querySelector("#bagPriceText").textContent = formatFare(bag);
  document.querySelector("#classPriceText").textContent = formatFare(classExtra);
  const currentLegTotal = base + classExtra + seat + bag;
  document.querySelector("#bookingPrice").value = routeLegs(selectedBookingRoute).length > 1
    ? bookingRouteTotal(currentLegTotal).toFixed(2)
    : currentLegTotal.toFixed(2);
}

function bookingRouteTotal(currentLegTotal) {
  return routeLegs(selectedBookingRoute).reduce((sum, leg, index) => {
    if (index === bookingLegIndex) return sum + currentLegTotal;
    return sum + bookingLegPrice(leg, bookingLegOptions[index] || defaultBookingOptions());
  }, 0);
}

function bookingLegPrice(flight, options) {
  const base = basePriceForFlight(flight);
  const classExtra = classTotal(flight, options.klass) - base;
  const seat = options.chooseSeat ? SEAT_SELECTION_PRICE : 0;
  const bag = options.addBaggage ? BAGGAGE_PRICES[options.baggageAllowanceKg] || 0 : 0;
  return base + classExtra + seat + bag;
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
  return durationFromMinutes(minutes);
}

function routeDurationLabel(route) {
  return durationFromMinutes(routeDurationMinutes(route));
}

function durationFromMinutes(minutes) {
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
  saveCurrentBookingOptions();
  const legs = routeLegs(selectedBookingRoute);
  if (bookingLegIndex < legs.length - 1) {
    bookingLegIndex += 1;
    renderBookingLeg();
    return;
  }

  try {
    document.querySelector("#bookingDialog").close();
    openPurchaseOverlay();
    if (legs.length > 1) {
      await Airport.api("/api/tickets/route", {
        method: "POST",
        skipLoader: true,
        body: JSON.stringify({
          legs: legs.map((leg, index) => ticketPayloadForLeg(leg, bookingLegOptions[index] || defaultBookingOptions()))
        })
      });
    } else {
      await Airport.api("/api/tickets", {
        method: "POST",
        skipLoader: true,
        body: JSON.stringify(ticketPayloadForLeg(legs[0], bookingLegOptions[0] || defaultBookingOptions()))
      });
    }
    await showPurchaseConfirmation();
  } catch (error) {
    closePurchaseOverlay();
    Airport.toast(error.message);
  }
}

function ticketPayloadForLeg(flight, options) {
  const payload = {
    flightId: Number(flight.id),
    class: options.klass,
    baggageAllowanceKg: options.addBaggage ? Number(options.baggageAllowanceKg) : 0
  };

  if (user.nationalId) payload.passengerNationalId = user.nationalId;
  if (user.email) payload.passengerEmail = user.email;
  if (options.chooseSeat && options.seatNumber) payload.seatNumber = options.seatNumber;
  return payload;
}

function openPurchaseOverlay() {
  const overlay = document.querySelector("#purchaseOverlay");
  const progressText = document.querySelector("#purchaseAnimation strong");
  if (progressText) progressText.textContent = routeLegs(selectedBookingRoute).length > 1 ? "We are booking your route" : "We are booking your flight";
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
