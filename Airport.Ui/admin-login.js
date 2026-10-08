document.querySelector("#adminLoginForm").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const result = await Airport.api("/api/auth/login", { method: "POST", body: JSON.stringify({ email: document.querySelector("#loginEmail").value, password: document.querySelector("#loginPassword").value }) });
    if (result.user.role === "Passenger") {
      Airport.toast("Passenger accounts belong in the customer portal.");
      return;
    }
    Airport.setSession(result);
    location.href = "admin-dashboard.html";
  } catch (error) {
    Airport.toast(error.message);
  }
});
