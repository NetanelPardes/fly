AdminCommon.init("admin-gates.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
document.querySelector("#gateForm").addEventListener("submit", createGate);
refresh();

async function refresh() {
  try {
    const gates = await Airport.api("/api/gates");
    document.querySelector("#gatesGrid").innerHTML = gates.map(g => `<article class="gate-card"><strong>${Airport.escapeHtml(g.gateNumber)}</strong><span>Terminal ${Airport.escapeHtml(g.terminal)}</span></article>`).join("") || Airport.empty("No gates");
  } catch (error) {
    Airport.toast(error.message);
  }
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
