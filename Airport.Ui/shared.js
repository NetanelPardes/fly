const Airport = (() => {
  const apiBase = localStorage.getItem("airport_ui_api_base") || "http://localhost:8080";
  const tokenKey = "airport_ui_token";
  const userKey = "airport_ui_user";

  function getToken() {
    return localStorage.getItem(tokenKey);
  }

  function getUser() {
    return JSON.parse(localStorage.getItem(userKey) || "null");
  }

  function setSession(result) {
    localStorage.setItem(tokenKey, result.token);
    localStorage.setItem(userKey, JSON.stringify(result.user));
  }

  function clearSession() {
    localStorage.removeItem(tokenKey);
    localStorage.removeItem(userKey);
  }

  function endpoint(path) {
    return `${apiBase}${path}`;
  }

  async function api(path, options = {}) {
    const token = getToken();
    const response = await fetch(endpoint(path), {
      ...options,
      headers: {
        ...(options.body ? { "Content-Type": "application/json" } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(options.headers || {}),
      },
    });
    if (!response.ok) {
      throw new Error(problem(await response.text(), response.statusText));
    }
    return response.status === 204 ? null : response.json();
  }

  function problem(text, fallback) {
    if (!text) return fallback;
    try {
      const parsed = JSON.parse(text);
      if (parsed.errors) {
        const messages = Object.entries(parsed.errors)
          .flatMap(([field, errors]) => (Array.isArray(errors) ? errors : [errors]).map(error => `${field}: ${error}`));
        if (messages.length) return messages.join(" ");
      }

      return parsed.detail || parsed.title || JSON.stringify(parsed);
    } catch {
      return text;
    }
  }

  function toast(text) {
    const el = document.querySelector("#toast");
    el.textContent = text;
    el.hidden = false;
    clearTimeout(toast.timer);
    toast.timer = setTimeout(() => el.hidden = true, 4200);
  }

  function setDefaultDates() {
    const from = new Date();
    from.setMonth(from.getMonth() - 6);
    const to = new Date();
    to.setMonth(to.getMonth() + 6);
    document.querySelector("#fromDate").value = toDateInput(from);
    document.querySelector("#toDate").value = toDateInput(to);
  }

  function toDateInput(date) {
    return date.toISOString().slice(0, 10);
  }

  function flightQuery() {
    const params = new URLSearchParams();
    const from = document.querySelector("#fromDate")?.value;
    const to = document.querySelector("#toDate")?.value;
    const origin = document.querySelector("#origin")?.value;
    const destination = document.querySelector("#destination")?.value;
    const status = document.querySelector("#status")?.value;
    if (from) params.set("from", `${from}T00:00:00Z`);
    if (to) params.set("to", `${to}T23:59:59Z`);
    if (origin) params.set("origin", origin.trim());
    if (destination) params.set("destination", destination.trim());
    if (status) params.set("status", status);
    return params;
  }

  function badge(value) {
    return `<span class="status ${value}">${value}</span>`;
  }

  function empty(text) {
    return `<div class="empty-state">${escapeHtml(text)}</div>`;
  }

  function table(headers, rows) {
    if (!rows.length) return empty("No records");
    return `<div class="table-wrap"><table><thead><tr>${headers.map(h => `<th>${h}</th>`).join("")}</tr></thead><tbody>${rows.map(row => `<tr>${row.map(cell => `<td>${cell}</td>`).join("")}</tr>`).join("")}</tbody></table></div>`;
  }

  function formatDate(value) {
    return new Date(value).toLocaleString("en-GB", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
  }

  function money(value, currency = "USD") {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(value);
  }

  function escapeHtml(value) {
    return String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;");
  }

  function priceForClass(klass) {
    return klass === "First" ? 738 : klass === "Business" ? 432 : 180;
  }

  async function downloadTicket(ticketId) {
    const response = await fetch(endpoint(`/api/tickets/${ticketId}/file`), {
      headers: { Authorization: `Bearer ${getToken()}` },
    });
    if (!response.ok) throw new Error(problem(await response.text(), response.statusText));
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `ticket-${ticketId}.txt`;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
  }

  return { api, setSession, clearSession, getUser, toast, setDefaultDates, flightQuery, badge, empty, table, formatDate, money, escapeHtml, priceForClass, downloadTicket };
})();
