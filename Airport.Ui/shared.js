const Airport = (() => {
  const apiBase = localStorage.getItem("airport_ui_api_base") || "http://localhost:8080";
  const tokenKey = "airport_ui_token";
  const userKey = "airport_ui_user";
  const pageSize = 30;
  const paginationState = new Map();

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

  function updateUser(user) {
    localStorage.setItem(userKey, JSON.stringify(user));
  }

  function clearSession() {
    localStorage.removeItem(tokenKey);
    localStorage.removeItem(userKey);
  }

  function endpoint(path) {
    return `${apiBase}${path}`;
  }

  async function api(path, options = {}) {
    const loadingText = options.loadingText || "Loading";
    const skipLoader = options.skipLoader === true;
    if (!skipLoader) beginLoading(loadingText);
    const token = getToken();
    const { loadingText: _loadingText, skipLoader: _skipLoader, ...fetchOptions } = options;
    try {
      const response = await fetch(endpoint(path), {
        ...fetchOptions,
        headers: {
          ...(fetchOptions.body ? { "Content-Type": "application/json" } : {}),
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          ...(fetchOptions.headers || {}),
        },
      });
      if (!response.ok) {
        throw new Error(problem(await response.text(), response.statusText));
      }
      return response.status === 204 ? null : response.json();
    } finally {
      if (!skipLoader) endLoading();
    }
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

  function ensurePlaneLoader() {
    let loader = document.querySelector("#searchLoader");
    if (loader) return loader;

    loader = document.createElement("div");
    loader.id = "searchLoader";
    loader.className = "search-loader";
    loader.hidden = true;
    loader.innerHTML = `<div class="search-loader-box"><div class="plane-track"><span class="plane-icon">&#9992;</span></div><strong id="searchLoaderText">Searching</strong></div>`;
    document.body.append(loader);
    return loader;
  }

  function setPlaneLoading(isLoading, text = "Searching") {
    if (isLoading) {
      beginLoading(text);
      return;
    }

    endLoading();
  }

  function beginLoading(text = "Loading") {
    const loader = ensurePlaneLoader();
    const label = loader.querySelector("#searchLoaderText") || loader.querySelector("strong");
    if (label) label.textContent = text;
    loader.dataset.loadingCount = String((Number(loader.dataset.loadingCount) || 0) + 1);
    loader.hidden = false;
  }

  function endLoading() {
    const loader = ensurePlaneLoader();
    const count = Math.max(0, (Number(loader.dataset.loadingCount) || 0) - 1);
    loader.dataset.loadingCount = String(count);
    loader.hidden = count > 0 ? false : true;
  }

  function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
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

  function table(headers, rows, options = {}) {
    if (!rows.length) return empty("No records");
    const visibleRows = options.paginateKey ? pagedItems(options.paginateKey, rows, options.onPageChange) : rows;
    const controls = options.paginateKey ? paginationControls(options.paginateKey, rows.length) : "";
    return `<div class="table-wrap"><table><thead><tr>${headers.map(h => `<th>${h}</th>`).join("")}</tr></thead><tbody>${visibleRows.map(row => `<tr>${row.map(cell => `<td>${cell}</td>`).join("")}</tr>`).join("")}</tbody></table></div>${controls}`;
  }

  function paginatedList(key, items, renderItem, emptyText = "No records", options = {}) {
    if (!items.length) return empty(emptyText);
    const visibleItems = pagedItems(key, items, options.onPageChange);
    return `${visibleItems.map(renderItem).join("")}${paginationControls(key, items.length)}`;
  }

  function pagedItems(key, items, onPageChange) {
    const state = paginationState.get(key) || { page: 1 };
    state.onPageChange = onPageChange || state.onPageChange;
    state.total = items.length;
    const maxPage = Math.max(1, Math.ceil(items.length / pageSize));
    state.page = Math.min(Math.max(1, state.page || 1), maxPage);
    paginationState.set(key, state);
    const start = (state.page - 1) * pageSize;
    return items.slice(start, start + pageSize);
  }

  function paginationControls(key, total) {
    if (total <= pageSize) return "";
    const state = paginationState.get(key) || { page: 1 };
    const maxPage = Math.max(1, Math.ceil(total / pageSize));
    const start = (state.page - 1) * pageSize + 1;
    const end = Math.min(total, state.page * pageSize);
    return `<div class="pagination">
      <span>${start}-${end} of ${total}</span>
      <div class="pagination-actions">
        <button class="tiny-button" type="button" data-pagination-key="${key}" data-pagination-page="${state.page - 1}" ${state.page <= 1 ? "disabled" : ""}>Previous</button>
        <strong>${state.page} / ${maxPage}</strong>
        <button class="tiny-button" type="button" data-pagination-key="${key}" data-pagination-page="${state.page + 1}" ${state.page >= maxPage ? "disabled" : ""}>Next</button>
      </div>
    </div>`;
  }

  function resetPagination(key) {
    const state = paginationState.get(key);
    if (state) state.page = 1;
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
    beginLoading("Preparing ticket file");
    try {
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
    } finally {
      endLoading();
    }
  }

  document.body.addEventListener("click", event => {
    const button = event.target.closest("[data-pagination-key]");
    if (!button || button.disabled) return;
    const state = paginationState.get(button.dataset.paginationKey);
    if (!state) return;
    state.page = Number(button.dataset.paginationPage) || 1;
    state.onPageChange?.();
  });

  return { api, setSession, updateUser, clearSession, getUser, toast, setDefaultDates, flightQuery, badge, empty, table, paginatedList, resetPagination, formatDate, money, escapeHtml, priceForClass, downloadTicket, setPlaneLoading, delay };
})();
