/* Holo — landing page motion: the tilting box, alerts landing beside it, the next-check countdown, the "set your price"
   demo, the price chart and the scroll reveal. All prices and store names here are examples. */
(function () {
  "use strict";
  var root = document.querySelector(".lp");
  if (!root) return;
  var logo = root.getAttribute("data-logo") || "/img/holo.svg";
  var still = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  var ALERTS = [
    { t: "now", b: "🟢 In stock at your price", l: "Pitch Black · Elite Trainer Box · Porto TCG", p: "€52.90", s: "<span>−12% vs MSRP</span>" },
    { t: "4 min ago", b: "🔻 Price drop", l: "OP-13 · Booster Box · Madrid Games", p: "€119.90", s: "<s>€134.90</s>" },
    { t: "1 h ago", b: "🕒 Pre-orders open", l: "Mega Evolution 6 · Booster Display · out 21 Nov", p: "", s: "" },
    { t: "2 h ago", b: "🔁 Back in stock", l: "Riftbound · Origins Display · Lisbon Cards", p: "€104.90", s: "<span>at MSRP</span>" },
    { t: "yesterday", b: "🟢 In stock at your price", l: "Chaos Rising · Booster Bundle · Valencia Hobby", p: "€28.50", s: "<span>−5% vs MSRP</span>" }
  ];
  var MSRP = 59.99;
  var OFFERS = [
    { s: "Porto TCG", a: "PT · shipping €3.90", p: 52.90, st: "in" },
    { s: "Lisbon Cards", a: "PT · free shipping", p: 54.99, st: "in" },
    { s: "Madrid Games", a: "ES · shipping €4.95", p: 56.50, st: "in" },
    { s: "Online PT", a: "PT · free shipping", p: 57.90, st: "out" },
    { s: "Valencia Hobby", a: "ES · shipping €5.50", p: 61.95, st: "in" },
    { s: "Braga Collect", a: "PT · shipping €3.50", p: 64.90, st: "in" },
    { s: "Sevilla Cartas", a: "ES · pre-order", p: 69.00, st: "pre" }
  ];

  function alertHTML(a, cls) {
    return '<div class="alert' + (cls ? " " + cls : "") + '"><div class="hd"><img src="' + logo + '" alt="" />HOLO<em>' + a.t + "</em></div><b>" + a.b +
      '</b><span class="ln">' + a.l + "</span>" + (a.p ? '<span class="pr">' + a.p + a.s + "</span>" : "") + "</div>";
  }

  /* Tilt the box with the pointer, and let it drift on its own when idle. */
  var art = root.querySelector(".lp-art");
  var wrap = root.querySelector("[data-tilt]");
  if (art && wrap) {
    var shine = wrap.querySelector(".shine");
    var idle = true, t0 = performance.now();
    var tilt = function (rx, ry) {
      wrap.style.transform = "rotateX(" + rx + "deg) rotateY(" + ry + "deg)";
      if (shine) shine.style.setProperty("--a", (200 + ry * 6 + rx * 3) + "deg");
    };
    art.addEventListener("pointermove", function (e) {
      if (e.pointerType === "touch") return;
      idle = false;
      var r = art.getBoundingClientRect();
      tilt(-((e.clientY - r.top) / r.height - 0.5) * 18, ((e.clientX - r.left) / r.width - 0.5) * 22);
    });
    art.addEventListener("pointerleave", function () { idle = true; t0 = performance.now(); });
    if (still) tilt(-2, -8);
    else (function drift(now) {
      if (idle) { var k = (now - t0) / 1000; tilt(Math.sin(k * 0.7) * 6 - 2, Math.cos(k * 0.5) * 10 - 8); }
      requestAnimationFrame(drift);
    })(performance.now());
  }

  /* Alerts land one after another (the newest on top). */
  var stack = root.querySelector("[data-alerts]");
  if (stack) {
    var n = 0;
    var show = function () {
      var list = [0, 1, 2].map(function (k) { return ALERTS[(n - k + ALERTS.length * 10) % ALERTS.length]; });
      list[0] = Object.assign({}, list[0], { t: "now" });
      stack.innerHTML = list.map(function (a, k) { return alertHTML(a, k === 0 && n > 0 ? "land" : ""); }).join("");
      n++;
    };
    show();
    if (!still) setInterval(function () { if (!document.hidden) show(); }, 3400);
  }

  /* The next check counts down (an illustration: the real interval is in Settings). */
  var count = root.querySelector("[data-count]");
  if (count && !still) {
    var sec = 101;
    setInterval(function () {
      sec = sec <= 0 ? 180 : sec - 1;
      count.textContent = String(Math.floor(sec / 60)).padStart(2, "0") + ":" + String(sec % 60).padStart(2, "0");
    }, 1000);
  }

  /* Set your price: the slider decides which example stores would ping you. */
  var range = root.querySelector("[data-margin]");
  if (range) {
    var presets = root.querySelectorAll("[data-presets] button");
    var preview = root.querySelector("[data-preview]");
    var paint = function () {
      var m = Number(range.value);
      var limit = Math.round(MSRP * (1 + m / 100) * 100) / 100;
      range.style.setProperty("--p", ((m + 15) / 35 * 100) + "%");
      root.querySelector("[data-limit]").innerHTML = "<small>€</small>" + limit.toFixed(2);
      root.querySelector("[data-rule]").innerHTML = "MSRP <b>" + (m < 0 ? "−" : "+") + Math.abs(m) + "%</b><br />shipping included";
      presets.forEach(function (b) { b.setAttribute("aria-pressed", String(Number(b.getAttribute("data-m")) === m)); });

      var hits = OFFERS.filter(function (o) { return o.st === "in" && o.p <= limit; });
      root.querySelector("[data-hits]").textContent = hits.length;
      root.querySelector("[data-sumtext]").innerHTML = hits.length
        ? "<b>" + (hits.length === 1 ? "store" : "stores") + " at your price right now.</b> Holo would ping you for the cheapest first."
        : "<b>stores at your price right now.</b> Holo keeps watching and pings you when one gets there.";
      root.querySelector("[data-offers]").innerHTML = OFFERS.map(function (o) {
        var hit = o.st === "in" && o.p <= limit;
        var pct = Math.round((o.p / MSRP - 1) * 100);
        var pill = hit ? "Alert" : o.st === "out" ? "Sold out" : o.st === "pre" ? "Pre-order" : "Above";
        var diff = pct === 0 ? "MSRP" : (pct > 0 ? "+" : "−") + Math.abs(pct) + "%";
        return '<div class="lp-offer' + (hit ? " hit" : "") + (o.st === "out" ? " out" : "") + '"><div class="st"><b>' + o.s + "</b><span>" + o.a +
          '</span></div><div class="p num">€' + o.p.toFixed(2) + '<small class="' + (pct <= 0 ? "good-t" : "") + '">' + diff +
          '</small></div><span class="pill">' + pill + "</span></div>";
      }).join("");

      var best = hits[0];
      var html = best
        ? alertHTML({ t: "now", b: "🟢 In stock at your price", l: "Pitch Black · Elite Trainer Box · " + best.s, p: "€" + best.p.toFixed(2), s: "<span>−" + Math.round((1 - best.p / MSRP) * 100) + "% vs MSRP</span>" })
        : alertHTML({ t: "waiting", b: "⏳ Watching 7 stores", l: "Nothing at your price yet · next check in a few minutes", p: "", s: "" });
      if (preview.getAttribute("data-last") !== html) {
        preview.innerHTML = html;
        preview.firstElementChild.classList.add("land");
        preview.setAttribute("data-last", html);
      }
    };
    range.addEventListener("input", paint);
    presets.forEach(function (b) { b.addEventListener("click", function () { range.value = b.getAttribute("data-m"); paint(); }); });
    paint();
  }

  /* The price chart, with its lowest point today. */
  var chart = root.querySelector("[data-chart]");
  if (chart) {
    var pts = [64.9, 63.5, 64.9, 62.0, 61.5, 62.9, 60.9, 59.99, 61.2, 58.9, 57.5, 58.4, 56.9, 55.5, 54.99, 52.9];
    var lo = 50, hi = 66, W = 300, H = 120;
    var xy = pts.map(function (v, k) { return [6 + k * (W - 12) / (pts.length - 1), 8 + (hi - v) / (hi - lo) * (H - 16)]; });
    var d = xy.map(function (p, k) { return (k ? "L" : "M") + p[0].toFixed(1) + " " + p[1].toFixed(1); }).join(" ");
    var last = xy[xy.length - 1];
    var msrpY = 8 + (hi - MSRP) / (hi - lo) * (H - 16);
    chart.innerHTML = '<defs><linearGradient id="lp-line" x1="0" x2="1"><stop offset="0" stop-color="#7CF2D6"/><stop offset=".5" stop-color="#8FA8FF"/><stop offset="1" stop-color="#F49BEA"/></linearGradient></defs>' +
      '<line x1="0" x2="' + W + '" y1="' + msrpY + '" y2="' + msrpY + '" stroke="currentColor" stroke-opacity=".35" stroke-dasharray="4 5" vector-effect="non-scaling-stroke"/>' +
      '<path d="' + d + " L" + last[0] + " " + H + ' L6 ' + H + 'Z" fill="url(#lp-line)" opacity=".14"/>' +
      '<path d="' + d + '" fill="none" stroke="url(#lp-line)" stroke-width="3" stroke-linejoin="round" stroke-linecap="round" vector-effect="non-scaling-stroke"/>' +
      '<circle cx="' + last[0] + '" cy="' + last[1] + '" r="5" fill="#4FD7A2"/>';
  }

  /* Blocks below the fold slide in as they scroll into view (everything shows at once without IntersectionObserver). */
  if (!still && "IntersectionObserver" in window) {
    var blocks = root.querySelectorAll(".reveal");
    var fold = window.innerHeight;
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); } });
    }, { threshold: 0.12, rootMargin: "0px 0px -6% 0px" });
    root.classList.add("js-reveal");
    blocks.forEach(function (el) {
      if (el.getBoundingClientRect().top < fold) el.classList.add("in");
      else io.observe(el);
    });
  }
})();
