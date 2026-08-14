/* The phone remote. Everything here is a reaction to the room: the library, the dance pool and the
   settings stay on the computer, where preparation happens. */

(function () {
  "use strict";

  var t = null;          /* bound after the config load */
  var connection = null;
  var delaySeconds = 30;
  var openRow = -1;
  var queue = [];
  var searchTimer = null;

  var MARK = {
    Track: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 18V5l12-2v13"/><circle cx="6" cy="18" r="3"/><circle cx="18" cy="16" r="3"/></svg>',
    Delay: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg>',
    Stop: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><rect x="5" y="5" width="14" height="14" rx="2"/></svg>',
    Message: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>',
    EndOfNight: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 14.5A8.5 8.5 0 0 1 9.5 4a8.5 8.5 0 1 0 10.5 10.5z"/></svg>'
  };

  function id(name) { return document.getElementById(name); }

  function text(name, value) { id(name).textContent = value; }

  function escapeHtml(value) {
    return String(value).replace(/[&<>"]/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c];
    });
  }

  var toastTimer = null;
  function toast(message, bad) {
    var node = id("toast");
    node.textContent = message;
    node.classList.toggle("is-bad", !!bad);
    node.classList.add("is-on");
    window.clearTimeout(toastTimer);
    toastTimer = window.setTimeout(function () { node.classList.remove("is-on"); }, 1800);
  }

  /* Hands a refused command straight back rather than pretending it worked: the queue guard's own
     wording is better than anything invented here. */
  function report(result, okMessage) {
    if (result && result.accepted === false) {
      toast(result.reason || "", true);
      return;
    }
    if (okMessage) toast(okMessage);
  }

  function primaryOf(item) {
    return item.primary || window.R4B.kindLabel(item.kind);
  }

  function subtitleOf(item) {
    if (item.kind === "Track") {
      return item.artist + (item.title ? " — " + item.title : "");
    }
    if (item.kind === "Delay") return t("silentPause");
    if (item.kind === "Stop") return t("waitsForYou");
    if (item.kind === "Message") return t("onScreen");
    if (item.kind === "EndOfNight") return t("nothingFollows");
    return "";
  }

  /* ---------------------------------------------------------------- rendering */

  function renderSnapshot(snapshot) {
    var current = snapshot.current;
    var next = snapshot.next;
    var hasCurrent = current.kind !== "None";

    text("nowPrimary", hasCurrent ? primaryOf(current) : t("nothingPlaying"));
    text("nowSub", hasCurrent ? subtitleOf(current) : t("queueEmpty"));

    var duration = snapshot.durationSeconds;
    var elapsed = snapshot.elapsedSeconds;
    text("nowElapsed", window.R4B.mmss(elapsed));
    text("nowLeft", duration > 0 ? "-" + window.R4B.mmss(duration - elapsed) : "");
    id("nowBar").style.width = duration > 0 ? Math.min(100, (elapsed / duration) * 100) + "%" : "0%";

    id("ppIcon").innerHTML = snapshot.isPlaying
      ? '<rect x="6" y="5" width="4" height="14" rx="1"/><rect x="14" y="5" width="4" height="14" rx="1"/>'
      : '<path d="M7 4l13 8-13 8z"/>';
    text("ppLabel", snapshot.isPlaying ? t("pause") : t("play"));

    id("upnextText").innerHTML = next.kind !== "None"
      ? "<b>" + escapeHtml(primaryOf(next)) + "</b><br>" + escapeHtml(subtitleOf(next))
      : "<b>" + escapeHtml(t("noNext")) + "</b>";
  }

  function renderQueue(entries) {
    queue = entries || [];
    var list = id("queueList");
    list.innerHTML = "";

    text("queueCount", queue.length + " " + (queue.length === 1 ? t("item") : t("items")));

    if (queue.length === 0) {
      list.innerHTML = '<div class="empty">' + escapeHtml(t("queueEmpty")) + "</div>";
      return;
    }

    queue.forEach(function (entry, index) {
      var row = document.createElement("div");
      row.className = "qrow" + (openRow === index ? " is-open" : "");

      var main = document.createElement("button");
      main.type = "button";
      main.className = "qmain";
      main.innerHTML =
        '<span class="qmark k-' + entry.kind + '">' + (MARK[entry.kind] || "") + "</span>" +
        "<span><span class=\"qtitle\">" + escapeHtml(primaryOf(entry)) + "</span>" +
        '<span class="qsub">' + escapeHtml(entry.isAuto ? t("autoAdded") : subtitleOf(entry)) + "</span></span>" +
        '<span class="qdur">' +
        (entry.durationSeconds ? window.R4B.mmss(entry.durationSeconds) : "—") + "</span>";

      main.addEventListener("click", function () {
        openRow = openRow === index ? -1 : index;
        renderQueue(queue);
      });

      row.appendChild(main);

      var actions = document.createElement("div");
      actions.className = "qactions";
      // An automatically added track is not the user's to move or remove, exactly as on the desktop.
      actions.innerHTML =
        '<button class="qact" type="button" data-move="up"' +
        (index === 0 || entry.isAuto ? " disabled" : "") + ">↑ " + escapeHtml(t("moveUp")) + "</button>" +
        '<button class="qact" type="button" data-move="down"' +
        (index === queue.length - 1 || entry.isAuto ? " disabled" : "") + ">↓ " + escapeHtml(t("moveDown")) + "</button>" +
        '<button class="qact danger" type="button" data-move="remove"' +
        (entry.isAuto ? " disabled" : "") + ">✕ " + escapeHtml(t("remove")) + "</button>";

      actions.addEventListener("click", function (event) {
        var button = event.target.closest("button");
        if (!button || button.disabled) return;

        var move = button.getAttribute("data-move");
        if (move === "up") send("MoveUp", index);
        if (move === "down") send("MoveDown", index);
        if (move === "remove") {
          openRow = -1;
          send("RemoveAt", index, t("removed"));
        }
      });

      row.appendChild(actions);
      list.appendChild(row);
    });
  }

  function renderHits(hits) {
    var host = id("hits");
    host.innerHTML = "";

    if (!hits.length) {
      host.innerHTML = '<div class="empty">' + escapeHtml(t("noMatches")) + "</div>";
      return;
    }

    hits.forEach(function (hit) {
      var button = document.createElement("button");
      button.type = "button";
      button.className = "hit";
      button.innerHTML =
        '<span><span class="badge">' + escapeHtml(hit.dance) + "</span><br>" +
        '<span class="h-sub">' + escapeHtml(hit.artist + (hit.title ? " — " + hit.title : "")) + "</span></span>" +
        '<span class="plus">+</span>';

      button.addEventListener("click", function () {
        send("QueueTrack", hit.id, t("queued") + " " + hit.dance);
      });

      host.appendChild(button);
    });
  }

  /* ------------------------------------------------------------------ sending */

  function send(method, arg, okMessage) {
    if (!connection) return Promise.resolve(null);

    var call = arg === undefined ? connection.invoke(method) : connection.invoke(method, arg);
    return call
      .then(function (result) { report(result, okMessage); return result; })
      .catch(function () { toast(t("connectionLost"), true); return null; });
  }

  /* --------------------------------------------------------------------- wire */

  function applyStaticText() {
    text("gateTitle", t("pinTitle"));
    text("gateHint", t("pinHint"));
    text("gateButton", t("pinButton"));

    text("nowKicker", t("playing"));
    text("restartLabel", t("restart"));
    text("ppLabel", t("pause"));
    text("skipLabel", t("holdToSkip"));
    text("upnextKicker", t("next"));

    text("randomLabel", t("randomTrack"));
    text("randomHint", t("fromPool"));
    text("stopLabel", t("stop"));
    text("stopHint", t("openEnded"));
    text("delayLengthLabel", t("delayLength"));
    text("delayLabel", t("queueDelay"));
    text("messageKicker", t("message"));
    text("messageLabel", t("queueMessage"));
    text("endOfNightLabel", t("queueEndOfNight"));
    text("endOfNightHint", t("nothingFollows"));
    id("messageText").placeholder = t("messagePlaceholder");
    id("search").placeholder = t("searchPlaceholder");

    text("tabNow", t("tabNow"));
    text("tabQueue", t("tabQueue"));
    text("tabAdd", t("tabAdd"));
    text("tabFind", t("tabFind"));
  }

  function wireTabs() {
    id("tabs").addEventListener("click", function (event) {
      var button = event.target.closest("button");
      if (!button) return;

      var tab = button.getAttribute("data-tab");
      Array.prototype.forEach.call(id("tabs").querySelectorAll("button"), function (other) {
        other.setAttribute("aria-selected", String(other === button));
      });
      Array.prototype.forEach.call(document.querySelectorAll(".pane"), function (pane) {
        pane.classList.toggle("is-active", pane.getAttribute("data-pane") === tab);
      });

      if (tab === "find") runSearch();
    });
  }

  function wireActions() {
    document.addEventListener("click", function (event) {
      var button = event.target.closest("[data-act]");
      if (!button) return;

      switch (button.getAttribute("data-act")) {
        case "playpause": send("PlayPause"); break;
        case "restart": send("Restart", undefined, t("restarted")); break;
        case "random": send("QueueRandom", undefined, t("queued")); break;
        case "stop": send("QueueStop", undefined, t("queued")); break;
        case "endofnight": send("QueueEndOfNight", undefined, t("queued")); break;
        case "delay": send("QueueDelay", delaySeconds, t("queued")); break;
        case "delay-up":
          delaySeconds = Math.min(600, delaySeconds + 15);
          text("delayValue", delaySeconds + "s");
          break;
        case "delay-down":
          delaySeconds = Math.max(15, delaySeconds - 15);
          text("delayValue", delaySeconds + "s");
          break;
        case "message": {
          var value = id("messageText").value.trim();
          if (!value) return;
          send("QueueMessage", value, t("queued")).then(function (result) {
            if (!result || result.accepted !== false) id("messageText").value = "";
          });
          break;
        }
        default: break;
      }
    });
  }

  /* Held rather than tapped, with the fill showing how far along the hold is. */
  function wireSkip() {
    var button = id("skip");
    var fill = id("skipFill");
    var label = id("skipLabel");
    var frame = null;
    var startedAt = 0;
    var HOLD_MS = 650;

    function step(now) {
      var pct = Math.min(100, ((now - startedAt) / HOLD_MS) * 100);
      fill.style.width = pct + "%";
      if (pct >= 100) { fire(); return; }
      frame = window.requestAnimationFrame(step);
    }

    function begin(event) {
      event.preventDefault();
      startedAt = window.performance.now();
      label.textContent = t("keepHolding");
      frame = window.requestAnimationFrame(step);
    }

    function cancel() {
      if (frame) window.cancelAnimationFrame(frame);
      frame = null;
      fill.style.width = "0%";
      label.textContent = t("holdToSkip");
    }

    function fire() {
      cancel();
      if (window.navigator.vibrate) window.navigator.vibrate(20);
      send("Skip", undefined, t("skipped"));
    }

    button.addEventListener("pointerdown", begin);
    button.addEventListener("pointerup", cancel);
    button.addEventListener("pointerleave", cancel);
    button.addEventListener("pointercancel", cancel);
  }

  function runSearch() {
    if (!connection) return;
    connection.invoke("Search", id("search").value).then(renderHits).catch(function () { });
  }

  function wireSearch() {
    id("search").addEventListener("input", function () {
      window.clearTimeout(searchTimer);
      searchTimer = window.setTimeout(runSearch, 200);
    });
  }

  /* --------------------------------------------------------------------- gate */

  function connect(token) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/remote?access_token=" + encodeURIComponent(token))
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .build();

    connection.on("snapshot", renderSnapshot);
    connection.on("queue", renderQueue);

    connection.onreconnecting(function () { text("link", t("reconnecting")); });
    connection.onreconnected(function () { text("link", ""); });
    connection.onclose(function () { text("link", t("connectionLost")); });

    return connection.start().then(function () {
      id("gate").classList.add("is-hidden");
      id("app").classList.remove("is-hidden");
      text("delayValue", delaySeconds + "s");
      runSearch();
    });
  }

  function wireGate() {
    id("gateForm").addEventListener("submit", function (event) {
      event.preventDefault();
      var error = id("gateError");
      error.textContent = "";

      fetch("/api/remote/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ pin: id("pin").value })
      })
        .then(function (response) { return response.json(); })
        .then(function (result) {
          if (result.isGranted) {
            // Survives a screen lock and a browser restart; the app drops it when the PIN changes.
            try { window.sessionStorage.setItem("r4b-token", result.token); } catch (e) { /* private mode */ }
            return connect(result.token);
          }

          if (result.status === "locked") {
            error.textContent = t("pinLocked", Math.ceil(result.retryAfterSeconds));
          } else if (result.status === "disabled") {
            error.textContent = t("pinDisabled");
          } else {
            error.textContent = t("pinWrong");
          }

          id("pin").value = "";
          return null;
        })
        .catch(function () { error.textContent = t("connectionLost"); });
    });
  }

  window.R4B.loadConfig().then(function () {
    document.documentElement.lang = window.R4B.lang;
    t = window.R4B.t;

    applyStaticText();
    wireGate();
    wireTabs();
    wireActions();
    wireSkip();
    wireSearch();

    var saved = null;
    try { saved = window.sessionStorage.getItem("r4b-token"); } catch (e) { saved = null; }
    if (saved) {
      connect(saved).catch(function () {
        try { window.sessionStorage.removeItem("r4b-token"); } catch (e) { /* ignore */ }
      });
    }
  });
})();
