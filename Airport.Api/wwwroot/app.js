let token = localStorage.getItem("airport_token");
let currentUser = JSON.parse(localStorage.getItem("airport_user") || "null");

const appPanel = document.querySelector("#appPanel");
const loginPanel = document.querySelector("#loginPanel");
const sessionText = document.querySelector("#sessionText");
const message = document.querySelector("#message");
const logoutButton = document.querySelector("#logoutButton");

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: {
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { "Authorization": `Bearer ${token}` } : {}),
      ...(options.headers || {}),
    },
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || response.statusText);
  }

  return response.status === 204 ? null : response.json();
}

function showMessage(text) {
  message.textContent = text;
}

function renderSession() {
  const signedIn = Boolean(token);
  appPanel.hidden = !signedIn;
  loginPanel.hidden = signedIn;
  logoutButton.hidden = !signedIn;
  sessionText.textContent = signedIn
    ? `Signed in as ${currentUser.fullName} (${currentUser.role})`
    : "Sign in with a demo account to manage the airport.";
}

async function loadFlights() {
  const params = new URLSearchParams();
  const origin = document.querySelector("#origin").value.trim();
  const destination = document.querySelector("#destination").value.trim();
  const status = document.querySelector("#status").value;
  if (origin) params.set("origin", origin);
  if (destination) params.set("destination", destination);
  if (status) params.set("status", status);

  const flights = await api(`/api/flights?${params}`);
  document.querySelector("#flights").innerHTML = flights.map(flight => `
    <tr>
      <td>${flight.id}: ${flight.flightNumber}</td>
      <td>${flight.origin} to ${flight.destination}</td>
      <td>${flight.status}</td>
      <td>${flight.gateNumber ?? "-"}</td>
      <td>${new Date(flight.departureTime).toISOString()}</td>
    </tr>
  `).join("");
}

async function loadGates() {
  const gates = await api("/api/gates");
  document.querySelector("#gates").innerHTML = gates.map(gate => `
    <li><strong>${gate.gateNumber}</strong><br>Terminal ${gate.terminal}</li>
  `).join("");
}

async function loadTickets() {
  const tickets = await api("/api/tickets");
  document.querySelector("#tickets").innerHTML = tickets.map(ticket => `
    <tr>
      <td>${ticket.id}</td>
      <td>${ticket.passengerName}</td>
      <td>${ticket.flightNumber}</td>
      <td>${ticket.seatNumber}</td>
      <td>${ticket.status}</td>
      <td>${ticket.ticketFilePath ?? "-"}</td>
    </tr>
  `).join("");
}

async function loadBaggage() {
  const baggage = await api("/api/baggage");
  document.querySelector("#baggage").innerHTML = baggage.map(item => `
    <tr>
      <td>${item.id}</td>
      <td>${item.ticketId}</td>
      <td>${item.tagNumber}</td>
      <td>${item.weightKg}</td>
      <td>${item.status}</td>
    </tr>
  `).join("");
}

async function loadAll() {
  try {
    await Promise.all([loadFlights(), loadGates(), loadTickets(), loadBaggage()]);
    showMessage("");
  } catch (error) {
    showMessage(error.message);
  }
}

document.querySelector("#loginForm").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const result = await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({
        email: document.querySelector("#email").value,
        password: document.querySelector("#password").value,
      }),
    });
    token = result.token;
    currentUser = result.user;
    localStorage.setItem("airport_token", token);
    localStorage.setItem("airport_user", JSON.stringify(currentUser));
    renderSession();
    await loadAll();
  } catch (error) {
    showMessage(error.message);
  }
});

logoutButton.addEventListener("click", () => {
  token = null;
  currentUser = null;
  localStorage.removeItem("airport_token");
  localStorage.removeItem("airport_user");
  renderSession();
});

document.querySelectorAll("nav button").forEach(button => {
  button.addEventListener("click", () => {
    document.querySelectorAll("nav button").forEach(item => item.classList.remove("active"));
    document.querySelectorAll(".tab").forEach(item => item.classList.remove("active"));
    button.classList.add("active");
    document.querySelector(`#${button.dataset.tab}Tab`).classList.add("active");
  });
});

document.querySelector("#loadFlights").addEventListener("click", loadFlights);

document.querySelector("#ticketForm").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    await api("/api/tickets", {
      method: "POST",
      body: JSON.stringify({
        passengerId: Number(document.querySelector("#ticketPassengerId").value),
        flightId: Number(document.querySelector("#ticketFlightId").value),
        seatNumber: document.querySelector("#ticketSeat").value,
        class: document.querySelector("#ticketClass").value,
        price: Number(document.querySelector("#ticketPrice").value),
      }),
    });
    await loadTickets();
    showMessage("Ticket created. The worker will generate the file shortly.");
  } catch (error) {
    showMessage(error.message);
  }
});

document.querySelector("#baggageForm").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    await api("/api/baggage", {
      method: "POST",
      body: JSON.stringify({
        ticketId: Number(document.querySelector("#baggageTicketId").value),
        tagNumber: document.querySelector("#baggageTag").value,
        weightKg: Number(document.querySelector("#baggageWeight").value),
      }),
    });
    await loadBaggage();
    showMessage("Baggage registered.");
  } catch (error) {
    showMessage(error.message);
  }
});

renderSession();
if (token) {
  loadAll();
}
