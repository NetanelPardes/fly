AdminCommon.init("admin-users.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
document.querySelector("#userSearchForm").addEventListener("submit", event => { event.preventDefault(); searchUsers(); });

let users = [];
refresh();

async function refresh() {
  try {
    users = await Airport.api("/api/users");
    renderUsers();
  } catch (error) {
    Airport.toast(error.message);
  }
}

async function searchUsers() {
  Airport.setPlaneLoading(true, "Searching users");
  try {
    await Airport.delay(450);
    renderUsers();
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function renderUsers() {
  const nationalId = document.querySelector("#searchNationalId").value.trim();
  const email = document.querySelector("#searchEmail").value.trim().toLowerCase();
  const visible = users
    .filter(user => !nationalId || String(user.nationalId || "").includes(nationalId))
    .filter(user => !email || String(user.email || "").toLowerCase().includes(email))
    .sort((a, b) => a.fullName.localeCompare(b.fullName));

  document.querySelector("#usersTable").innerHTML = Airport.table(["ID", "Name", "National ID", "Email", "Phone", "Role"], visible.map(user => [
    user.id,
    Airport.escapeHtml(user.fullName),
    Airport.escapeHtml(user.nationalId || ""),
    Airport.escapeHtml(user.email),
    Airport.escapeHtml(user.phoneNumber || ""),
    Airport.badge(user.role)
  ]));
}
