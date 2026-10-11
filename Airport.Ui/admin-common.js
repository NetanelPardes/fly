const AdminCommon = (() => {
  const user = Airport.getUser();
  const links = [
    ["admin-dashboard.html", "Overview"],
    ["admin-flights.html", "Flights"],
    ["admin-tickets.html", "Tickets"],
    ["admin-users.html", "Users"],
  ];

  if (!user || user.role === "Passenger") location.href = "admin-login.html";

  function init(activeHref) {
    document.querySelector("#adminNav").innerHTML = links.map(([href, label]) => `<a class="nav-item ${href === activeHref ? "active" : ""}" href="${href}">${label}</a>`).join("");
    document.querySelector("#userName").textContent = user.fullName;
    document.querySelector("#userEmail").textContent = user.email;
    document.querySelector("#roleBadge").textContent = user.role;
    document.querySelector("#logoutButton").addEventListener("click", () => {
      Airport.clearSession();
      location.href = "admin-login.html";
    });
  }

  return { user, init };
})();
