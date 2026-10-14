/* Holo — the light/dark switch on the landing page and the sign-in pages. It saves the same preference as
   Settings → Mode (tcgstockbot.mode), so the app opens in the mode picked here. */
(function () {
  "use strict";
  var root = document.documentElement;
  var meta = document.querySelector('meta[name="theme-color"]');

  function label() {
    var dark = root.getAttribute("data-mode") !== "light";
    document.querySelectorAll("[data-theme-toggle]").forEach(function (button) {
      button.setAttribute("aria-label", dark ? "Switch to light mode" : "Switch to dark mode");
    });
    if (meta) meta.setAttribute("content", dark ? "#0C0C0F" : "#F3F2EE");
  }

  function flip() {
    var next = root.getAttribute("data-mode") === "light" ? "dark" : "light";
    root.setAttribute("data-mode", next);
    try { localStorage.setItem("tcgstockbot.mode", JSON.stringify(next)); } catch (e) { /* private mode: this page only */ }
    label();
  }

  document.addEventListener("click", function (event) {
    if (!event.target.closest("[data-theme-toggle]")) return;
    var still = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (document.startViewTransition && !still) document.startViewTransition(flip);   // a soft cross-fade where supported
    else flip();
  });
  label();
})();
