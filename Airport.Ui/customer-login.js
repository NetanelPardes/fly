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

document.querySelector("#openRegister").addEventListener("click", () => document.querySelector("#registerDialog").showModal());
document.querySelector("#closeRegister").addEventListener("click", () => document.querySelector("#registerDialog").close());
document.querySelector("#registerForm").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const firstName = document.querySelector("#registerFirstName").value.trim();
    const lastName = document.querySelector("#registerLastName").value.trim();
    const result = await Airport.api("/api/auth/register-passenger", {
      method: "POST",
      body: JSON.stringify({
        nationalId: document.querySelector("#registerNationalId").value.trim(),
        firstName,
        lastName,
        fullName: `${firstName} ${lastName}`,
        email: document.querySelector("#registerEmail").value.trim(),
        password: document.querySelector("#registerPassword").value,
        gender: document.querySelector("#registerGender").value,
        address: document.querySelector("#registerAddress").value.trim(),
        phoneNumber: document.querySelector("#registerPhone").value.trim()
      }),
    });
    Airport.setSession(result);
    location.href = "customer-flights.html";
  } catch (error) {
    Airport.toast(error.message);
  }
});
