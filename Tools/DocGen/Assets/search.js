// Client-side search over DOQUA_SEARCH (assets/search-index.js); no server needed.
(function () {
  const input = document.getElementById("search");
  const results = document.getElementById("search-results");
  if (!input || typeof DOQUA_SEARCH === "undefined") return;
  let active = -1;

  function score(entry, words) {
    const name = entry.n.toLowerCase(), summary = (entry.s || "").toLowerCase();
    let total = 0;
    for (const word of words) {
      if (name === word) total += 100;
      else if (name.endsWith("." + word) || name.startsWith(word)) total += 40;
      else if (name.includes(word)) total += 20;
      else if (summary.includes(word)) total += 5;
      else return 0;
    }
    return total - name.length * 0.01 + (entry.k === "guide" || entry.k === "example" ? 3 : 0);
  }

  function escape(text) {
    return text.replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
  }

  function update() {
    const words = input.value.trim().toLowerCase().split(/\s+/).filter(w => w);
    active = -1;
    if (words.length === 0) { results.style.display = "none"; return; }
    const hits = DOQUA_SEARCH.map(e => [score(e, words), e]).filter(h => h[0] > 0)
      .sort((a, b) => b[0] - a[0]).slice(0, 30);
    results.innerHTML = hits.length === 0 ? '<a>No results</a>' : hits.map(([, e]) =>
      `<a href="${DOQUA_ROOT}${e.u}"><span class="kind">${escape(e.k)}</span>${escape(e.n)}` +
      (e.s ? `<span class="summary">${escape(e.s.length > 140 ? e.s.slice(0, 140) + "…" : e.s)}</span>` : "") + "</a>").join("");
    results.style.display = "block";
  }

  input.addEventListener("input", update);
  input.addEventListener("focus", update);
  input.addEventListener("keydown", event => {
    const links = results.querySelectorAll("a[href]");
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (links.length === 0) return;
      active = (active + (event.key === "ArrowDown" ? 1 : -1) + links.length) % links.length;
      links.forEach((a, i) => a.classList.toggle("active", i === active));
      links[active].scrollIntoView({ block: "nearest" });
    } else if (event.key === "Enter" && links.length > 0) {
      window.location.href = links[Math.max(active, 0)].href;
    } else if (event.key === "Escape") {
      results.style.display = "none";
      input.blur();
    }
  });
  document.addEventListener("click", event => {
    if (!event.target.closest(".search")) results.style.display = "none";
  });
})();
