/* The display page. Receives snapshots and draws them; sends nothing, ever. */

(function () {
  "use strict";

  var el = {
    current: document.querySelector('[data-when="current"]'),
    idle: document.getElementById("idle"),
    primary: document.getElementById("primary"),
    track: document.getElementById("track"),
    mid: document.getElementById("mid"),
    bar: document.getElementById("bar"),
    remaining: document.getElementById("remaining"),
    next: document.querySelector('[data-when="next"]'),
    nextIdle: document.getElementById("nextIdle"),
    nextLabel: document.getElementById("nextLabel"),
    nextPrimary: document.getElementById("nextPrimary"),
    nextTrack: document.getElementById("nextTrack"),
    behind: document.querySelector('[data-when="behind"]'),
    behindLabel: document.getElementById("behindLabel"),
    behindPrimary: document.getElementById("behindPrimary"),
    behindTrack: document.getElementById("behindTrack"),
    lost: document.getElementById("lost")
  };

  function show(node, visible) {
    node.classList.toggle("is-hidden", !visible);
  }

  /* The line under a dance, hidden when there is nothing to put on it so it takes no room. */
  function line(node, text) {
    node.textContent = text;
    show(node, text.length > 0);
  }

  function trackLineOf(item) {
    return item.kind === "Track" ? window.R4B.trackLine(item.artist, item.title) : "";
  }

  function applyStaticText() {
    el.idle.textContent = window.R4B.t("noTrack");
    el.nextIdle.textContent = window.R4B.t("noNext");
    el.nextLabel.textContent = window.R4B.t("next");
    el.behindLabel.textContent = window.R4B.t("then");
    el.lost.textContent = window.R4B.t("reconnecting");
  }

  function render(snapshot) {
    var current = snapshot.current;
    var next = snapshot.next;
    var hasCurrent = current.kind !== "None";
    var hasNext = next.kind !== "None";

    show(el.current, hasCurrent);
    show(el.idle, !hasCurrent);
    el.mid.style.visibility = hasCurrent ? "visible" : "hidden";

    if (hasCurrent) {
      el.primary.textContent = window.R4B.primaryLabel(current);
      line(el.track, trackLineOf(current));

      var duration = snapshot.durationSeconds;
      var elapsed = snapshot.elapsedSeconds;
      el.bar.style.width = window.R4B.progressWidth(elapsed, duration);
      el.remaining.textContent = duration > 0 ? window.R4B.mmss(duration - elapsed) : "";
    }

    show(el.next, hasNext);
    show(el.nextIdle, !hasNext);

    if (hasNext) {
      // A queued announcement is billed as "Message" with its text beneath, rather than shouting
      // the whole announcement in the next-up slot before its turn. A timed delay or message says
      // for how long, since there is no countdown bar under it yet to say so instead.
      var isMessage = next.kind === "Message";
      el.nextPrimary.textContent = window.R4B.nextLabel(next);
      line(el.nextTrack, isMessage ? next.primary || "" : trackLineOf(next));
    }

    // The dance the pause is for. A delay or a stop is often queued so the room can make lines or
    // find a partner, and it is exactly then that the floor wants to know what for.
    var behind = snapshot.behind;
    var hasBehind = hasNext && behind.kind !== "None";

    show(el.behind, hasBehind);

    if (hasBehind) {
      el.behindPrimary.textContent = window.R4B.primaryLabel(behind);
      line(el.behindTrack, trackLineOf(behind));
    }
  }

  window.R4B.loadConfig().then(function () {
    document.documentElement.lang = window.R4B.lang;
    applyStaticText();

    var connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/display")
      // The reason SignalR is here rather than a bare WebSocket: a projector left running all
      // evening will lose its socket at some point, and nobody is standing at it to reload.
      .withAutomaticReconnect(window.R4B.retryDelays)
      .build();

    connection.on("snapshot", render);

    connection.onreconnecting(function () { el.lost.hidden = false; });
    connection.onreconnected(function () { el.lost.hidden = true; });
    // SignalR's own retries end about eighteen seconds in. A laptop that sleeps through the break,
    // or the application closed and started again, takes longer than that, so the page starts
    // over rather than saying it is reconnecting for the rest of the evening.
    connection.onclose(function () {
      el.lost.hidden = false;
      start();
    });

    function start() {
      connection.start()
        .then(function () { el.lost.hidden = true; })
        .catch(function () {
          el.lost.hidden = false;
          window.setTimeout(start, 3000);
        });
    }

    start();
  });
})();
