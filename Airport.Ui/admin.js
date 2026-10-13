let flights = [];
let gates = [];
let tickets = [];
let baggage = [];
let users = [];
let user = Airport.getUser();

Airport.setDefaultDates();
boot();

function boot() {
  if (user && user.role !== "Passenger") showApp();
  document.querySelector("#loginForm").addEventListener("submit", login);
  document.querySelector("#logoutButton").addEventListener("click", logout);
  document.querySelector("#refreshButton").addEventListener("click", () => refresh().catch(e => Airport.toast(e.message)));
  document.querySelector("#flightFilters").addEventListener("submit", event => { event.preventDefault(); refresh().catch(e => Airport.toast(e.message)); });
  document.querySelector("#gateForm").addEventListener("submit", createGate);
  document.querySelector("#userForm").addEventListener("submit", createUser);
  document.querySelector("#closeTicketDialog").addEventListener("click", () => document.querySelector("#ticketDialog").close());
  document.querySelector("#ticketOptionsForm").addEventListener("submit", updateTicket);
  document.body.addEventListener("click", handleActions);
  document.querySelectorAll(".nav-item").forEach(button => button.addEventListener("click", () => switchView(button.dataset.view)));
  setInterval(() => document.querySelector("#clock").textContent = new Date().toLocaleTimeString(), 1000);
}

async function login(event) {
  event.preventDefault();
  try {
    const result = await Airport.api("/api/auth/login", { method: "POST", body: JSON.stringify({ email: document.querySelector("#loginEmail").value, password: document.querySelector("#loginPassword").value }) });
    if (result.user.role === "Passenger") {
      Airport.toast("Passenger accounts belong in the customer portal.");
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
  document.querySelector("#adminApp").hidden = false;
  document.querySelector("#refreshButton").hidden = false;
  document.querySelector("#logoutButton").hidden = false;
  document.querySelector("#userName").textContent = user.fullName;
  document.querySelector("#userEmail").textContent = user.email;
  document.querySelector("#roleBadge").textContent = user.role;
  await refresh();
}

async function refresh() {
  Airport.setPlaneLoading(true, "Searching");
  try {
    const route = document.querySelector("#origin").value.trim();
    const params = Airport.flightQuery();
    if (route) params.set("origin", route);
    [flights, gates, tickets, baggage] = await Promise.all([
      Airport.api(`/api/flights?${params}`),
      Airport.api("/api/gates"),
      Airport.api("/api/tickets"),
      Airport.api("/api/baggage"),
    ]);
    users = user.role === "Admin" ? await Airport.api("/api/users").catch(() => []) : [];
    render();
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function render() {
  document.querySelector("#metricFlights").textContent = flights.length;
  document.querySelector("#metricTickets").textContent = tickets.length;
  document.querySelector("#metricActiveTickets").textContent = `${tickets.filter(t => t.status === "Active").length} active`;
  document.querySelector("#metricBaggage").textContent = baggage.length;
  document.querySelector("#metricGates").textContent = gates.length;
  document.querySelector("#departureBoard").innerHTML = flights.slice(0, 10).map(f => `<div class="flight-row"><strong>${Airport.escapeHtml(f.flightNumber)}</strong><div><strong>${Airport.escapeHtml(f.origin)} -> ${Airport.escapeHtml(f.destination)}</strong><small>${Airport.formatDate(f.departureTime)}</small></div>${Airport.badge(f.status)}<strong>${Airport.escapeHtml(f.gateNumber || "TBD")}</strong></div>`).join("") || Airport.empty("No flights");
  document.querySelector("#flightsTable").innerHTML = Airport.table(["ID", "Flight", "Route", "Time", "Status", "Gate"], flights.map(f => [f.id, Airport.escapeHtml(f.flightNumber), `${Airport.escapeHtml(f.origin)} -> ${Airport.escapeHtml(f.destination)}`, Airport.formatDate(f.departureTime), Airport.badge(f.status), Airport.escapeHtml(f.gateNumber || "TBD")]), { paginateKey: "legacy-admin-flights", onPageChange: render });
  document.querySelector("#ticketsTable").innerHTML = Airport.table(["ID", "Passenger", "Flight", "Seat", "Class", "Price", "Status", "Actions"], tickets.map(t => [t.id, Airport.escapeHtml(t.passengerName), Airport.escapeHtml(t.flightNumber), Airport.escapeHtml(t.seatNumber), t.class, Airport.money(t.price), Airport.badge(t.status), `<button class="tiny-button" data-manage-ticket="${t.id}">Change</button> ${t.status === "Active" ? `<button class="tiny-button" data-cancel-ticket="${t.id}">Cancel</button>` : ""}`]), { paginateKey: "legacy-admin-tickets", onPageChange: render });
  document.querySelector("#baggageTable").innerHTML = Airport.table(["ID", "Ticket", "Tag", "Weight", "Status", "Updated"], baggage.map(b => [b.id, b.ticketId, Airport.escapeHtml(b.tagNumber), `${b.weightKg} kg`, Airport.badge(b.status), Airport.formatDate(b.lastUpdatedAt)]), { paginateKey: "legacy-admin-baggage", onPageChange: render });
  document.querySelector("#gatesGrid").innerHTML = Airport.paginatedList("legacy-admin-gates", gates, g => `<article class="gate-card"><strong>${Airport.escapeHtml(g.gateNumber)}</strong><span>Terminal ${Airport.escapeHtml(g.terminal)}</span></article>`, "No gates", { onPageChange: render });
  document.querySelector("#usersTable").innerHTML = Airport.table(["ID", "Name", "Email", "Role"], users.map(u => [u.id, Airport.escapeHtml(u.fullName), Airport.escapeHtml(u.email), Airport.badge(u.role)]), { paginateKey: "legacy-admin-users", onPageChange: render });
}

function switchView(viewId) {
  document.querySelectorAll(".view").forEach(view => view.classList.toggle("active", view.id === viewId));
  document.querySelectorAll(".nav-item").forEach(item => item.classList.toggle("active", item.dataset.view === viewId));
}

async function createGate(event) {
  event.preventDefault();
  try {
    await Airport.api("/api/gates", { method: "POST", body: JSON.stringify({ gateNumber: document.querySelector("#gateNumber").value, terminal: document.querySelector("#gateTerminal").value }) });
    event.target.reset();
    await refresh();
  } catch (error) {
    Airport.toast(error.message);
  }
}

async function createUser(event) {
  event.preventDefault();
  try {
    await Airport.api("/api/users", { method: "POST", body: JSON.stringify({ fullName: document.querySelector("#userFullName").value, email: document.querySelector("#userEmailInput").value, password: document.querySelector("#userPassword").value, role: document.querySelector("#userRole").value }) });
    event.target.reset();
    await refresh();
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
  document.querySelector("#ticketDialog").showModal();
}

async function updateTicket(event) {
  event.preventDefault();
  const ticketId = document.querySelector("#manageTicketId").value;
  const payload = {
    seatNumber: document.querySelector("#manageSeat").value,
    class: document.querySelector("#manageClass").value
  };
  document.querySelector("#ticketDialog").close();
  try {
    Airport.setPlaneLoading(true, "Updating ticket details");
    await Promise.all([
      Airport.api(`/api/tickets/${ticketId}/options`, { method: "PUT", skipLoader: true, body: JSON.stringify(payload) }),
      Airport.delay(1200)
    ]);
    await refresh();
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}

async function handleActions(event) {
  let loading = false;
  try {
    if (event.target.dataset.manageTicket) openTicket(event.target.dataset.manageTicket);
    if (event.target.dataset.cancelTicket) {
      loading = true;
      Airport.setPlaneLoading(true, "Cancelling ticket");
      await Airport.api(`/api/tickets/${event.target.dataset.cancelTicket}/cancel`, { method: "POST" });
      await refresh();
    }
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    if (loading) Airport.setPlaneLoading(false);
  }
}
