AdminCommon.init("admin-users.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
document.querySelector("#userForm").addEventListener("submit", createUser);
refresh();

async function refresh() {
  try {
    const users = await Airport.api("/api/users");
    document.querySelector("#usersTable").innerHTML = Airport.table(["ID", "Name", "Email", "Role"], users.map(u => [u.id, Airport.escapeHtml(u.fullName), Airport.escapeHtml(u.email), Airport.badge(u.role)]));
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
