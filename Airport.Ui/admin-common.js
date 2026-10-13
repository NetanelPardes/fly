const AdminCommon = (() => {
  let user = Airport.getUser();
  const links = [
    ["admin-dashboard.html", "Overview"],
    ["admin-flights.html", "Flights"],
    ["admin-tickets.html", "Tickets"],
    ["admin-users.html", "Users"],
  ];

  if (!user || user.role === "Passenger") location.href = "admin-login.html";

  function init(activeHref) {
    document.querySelector("#adminNav").innerHTML = links.map(([href, label]) => `<a class="nav-item ${href === activeHref ? "active" : ""}" href="${href}">${label}</a>`).join("");
    setupManagerActions();
    renderIdentity(user);
    document.querySelector("#logoutButton").addEventListener("click", () => {
      Airport.clearSession();
      location.href = "admin-login.html";
    });
  }

  function setupManagerActions() {
    const identityCard = document.querySelector(".identity-card");
    if (identityCard && identityCard.tagName !== "BUTTON") {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "identity-card manager-profile-button";
      button.innerHTML = identityCard.innerHTML;
      identityCard.replaceWith(button);
    }

    const profileButton = document.querySelector(".manager-profile-button");
    if (profileButton && !document.querySelector("#addManagerButton")) {
      const addButton = document.createElement("button");
      addButton.id = "addManagerButton";
      addButton.type = "button";
      addButton.className = "manager-add-button";
      addButton.textContent = "Add manager";
      profileButton.before(addButton);
    }

    ensureManagerDialogs();
    document.querySelector("#addManagerButton")?.addEventListener("click", openCreateManager);
    document.querySelector(".manager-profile-button")?.addEventListener("click", openManagerProfile);
    document.querySelector("#closeManagerProfileDialog")?.addEventListener("click", () => document.querySelector("#managerProfileDialog").close());
    document.querySelector("#closeCreateManagerDialog")?.addEventListener("click", () => document.querySelector("#createManagerDialog").close());
    document.querySelector("#managerProfileForm")?.addEventListener("submit", saveManagerProfile);
    document.querySelector("#createManagerForm")?.addEventListener("submit", createManager);
    setupBackdropClose("#managerProfileDialog");
    setupBackdropClose("#createManagerDialog");
  }

  function ensureManagerDialogs() {
    if (document.querySelector("#managerProfileDialog")) return;
    document.body.insertAdjacentHTML("beforeend", `
      <dialog id="managerProfileDialog">
        <form id="managerProfileForm" method="dialog" class="dialog-form">
          <h2>Manager details</h2>
          <div class="compact-grid">
            ${managerFields("profile")}
          </div>
          <div class="dialog-actions">
            <button type="button" id="closeManagerProfileDialog" class="ghost-button">Close</button>
            <button class="primary-button">Save details</button>
          </div>
        </form>
      </dialog>
      <dialog id="createManagerDialog">
        <form id="createManagerForm" method="dialog" class="dialog-form">
          <h2>Add manager</h2>
          <div class="compact-grid">
            ${managerFields("create")}
            <label>Password<input id="createPassword" type="password" minlength="4" maxlength="100" required></label>
          </div>
          <div class="dialog-actions">
            <button type="button" id="closeCreateManagerDialog" class="ghost-button">Close</button>
            <button class="primary-button">Create manager</button>
          </div>
        </form>
      </dialog>
    `);
  }

  function managerFields(prefix) {
    return `
      <label>National ID<input id="${prefix}NationalId" maxlength="9" required></label>
      <label>First name<input id="${prefix}FirstName" maxlength="80" required></label>
      <label>Last name<input id="${prefix}LastName" maxlength="80" required></label>
      <label>Full name<input id="${prefix}FullName" maxlength="120" required></label>
      <label>Email<input id="${prefix}Email" type="email" maxlength="254" required></label>
      <label>Phone<input id="${prefix}Phone" maxlength="30" required></label>
      <label>Gender<input id="${prefix}Gender" maxlength="20" required></label>
      <label>Address<input id="${prefix}Address" maxlength="240" required></label>
    `;
  }

  function renderIdentity(manager) {
    document.querySelector("#userName").textContent = manager.fullName;
    document.querySelector("#userEmail").textContent = manager.email;
    document.querySelector("#roleBadge").textContent = manager.role;
  }

  async function openManagerProfile() {
    try {
      Airport.setPlaneLoading(true, "Loading manager details");
      const latest = await Airport.api("/api/users/me", { skipLoader: true });
      user = latest;
      Airport.updateUser(latest);
      fillManagerForm("profile", latest);
      renderIdentity(latest);
      document.querySelector("#managerProfileDialog").showModal();
    } catch (error) {
      Airport.toast(error.message);
    } finally {
      Airport.setPlaneLoading(false);
    }
  }

  function openCreateManager() {
    document.querySelector("#createManagerForm").reset();
    document.querySelector("#createGender").value = "m";
    document.querySelector("#createManagerDialog").showModal();
  }

  async function saveManagerProfile(event) {
    event.preventDefault();
    const payload = readManagerForm("profile");
    document.querySelector("#managerProfileDialog").close();
    try {
      Airport.setPlaneLoading(true, "Saving manager details");
      const updated = await Promise.all([
        Airport.api("/api/users/me", { method: "PUT", skipLoader: true, body: JSON.stringify(payload) }),
        Airport.delay(900)
      ]).then(([result]) => result);
      user = updated;
      Airport.updateUser(updated);
      renderIdentity(updated);
      Airport.toast("Manager details updated.");
    } catch (error) {
      Airport.toast(error.message);
    } finally {
      Airport.setPlaneLoading(false);
    }
  }

  async function createManager(event) {
    event.preventDefault();
    const payload = {
      ...readManagerForm("create"),
      password: document.querySelector("#createPassword").value,
      role: "Admin"
    };
    document.querySelector("#createManagerDialog").close();
    try {
      Airport.setPlaneLoading(true, "Creating manager");
      await Promise.all([
        Airport.api("/api/users", { method: "POST", skipLoader: true, body: JSON.stringify(payload) }),
        Airport.delay(900)
      ]);
      if (typeof refresh === "function") await refresh();
      Airport.toast("Manager created.");
    } catch (error) {
      Airport.toast(error.message);
    } finally {
      Airport.setPlaneLoading(false);
    }
  }

  function fillManagerForm(prefix, manager) {
    document.querySelector(`#${prefix}NationalId`).value = manager.nationalId || "";
    document.querySelector(`#${prefix}FirstName`).value = manager.firstName || "";
    document.querySelector(`#${prefix}LastName`).value = manager.lastName || "";
    document.querySelector(`#${prefix}FullName`).value = manager.fullName || "";
    document.querySelector(`#${prefix}Email`).value = manager.email || "";
    document.querySelector(`#${prefix}Phone`).value = manager.phoneNumber || "";
    document.querySelector(`#${prefix}Gender`).value = manager.gender || "";
    document.querySelector(`#${prefix}Address`).value = manager.address || "";
  }

  function readManagerForm(prefix) {
    return {
      nationalId: document.querySelector(`#${prefix}NationalId`).value.trim(),
      firstName: document.querySelector(`#${prefix}FirstName`).value.trim(),
      lastName: document.querySelector(`#${prefix}LastName`).value.trim(),
      fullName: document.querySelector(`#${prefix}FullName`).value.trim(),
      email: document.querySelector(`#${prefix}Email`).value.trim(),
      phoneNumber: document.querySelector(`#${prefix}Phone`).value.trim(),
      gender: document.querySelector(`#${prefix}Gender`).value.trim(),
      address: document.querySelector(`#${prefix}Address`).value.trim()
    };
  }

  function setupBackdropClose(selector) {
    const dialog = document.querySelector(selector);
    dialog?.addEventListener("click", event => {
      if (event.target === dialog) dialog.close();
    });
  }

  return { get user() { return user; }, init };
})();
