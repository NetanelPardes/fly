document.querySelector("#customerLoginForm").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const result = await Airport.api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email: document.querySelector("#loginEmail").value, password: document.querySelector("#loginPassword").value }),
    });
    if (result.user.role !== "Passenger") {
      Airport.toast("This login is for customers only.");
      return;
    }
    Airport.setSession(result);
    location.href = "customer-flights.html";
  } catch (error) {
    Airport.toast(error.message);
  }
});
