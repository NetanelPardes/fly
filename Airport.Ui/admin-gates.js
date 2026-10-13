AdminCommon.init("admin-gates.html");
document.querySelector("#refreshButton").addEventListener("click", refresh);
document.querySelector("#gateForm").addEventListener("submit", createGate);
let gates = [];
refresh();

async function refresh() {
  Airport.setPlaneLoading(true, "Loading gates");
  try {
    gates = await Airport.api("/api/gates");
    renderGates();
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}

function renderGates() {
  document.querySelector("#gatesGrid").innerHTML = Airport.paginatedList("admin-gates", gates, g => `<article class="gate-card"><strong>${Airport.escapeHtml(g.gateNumber)}</strong><span>Terminal ${Airport.escapeHtml(g.terminal)}</span></article>`, "No gates", { onPageChange: renderGates });
}

async function createGate(event) {
  event.preventDefault();
  try {
    Airport.setPlaneLoading(true, "Adding gate");
    await Airport.api("/api/gates", { method: "POST", body: JSON.stringify({ gateNumber: document.querySelector("#gateNumber").value, terminal: document.querySelector("#gateTerminal").value }) });
    event.target.reset();
    Airport.resetPagination("admin-gates");
    await refresh();
  } catch (error) {
    Airport.toast(error.message);
  } finally {
    Airport.setPlaneLoading(false);
  }
}
