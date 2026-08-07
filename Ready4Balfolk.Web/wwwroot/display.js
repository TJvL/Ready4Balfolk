/* The display page. Receives snapshots and draws them; sends nothing, ever. */

(function () {
  "use strict";

  var el = {
    current: document.querySelector('[data-when="current"]'),
    idle: document.getElementById("idle"),
    primary: document.getElementById("primary"),
    sub: document.getElementById("sub"),
    artist: document.getElementById("artist"),
    dash: document.getElementById("dash"),
    title: document.getElementById("title"),
    mid: document.getElementById("mid"),
    bar: document.getElementById("bar"),
    remaining: document.getElementById("remaining"),
    next: document.querySelector('[data-when="next"]'),
    nextIdle: document.getElementById("nextIdle"),
    nextLabel: document.getElementById("nextLabel"),
    nextPrimary: document.getElementById("nextPrimary"),
    nextSub: document.getElementById("nextSub"),
    nextArtist: document.getElementById("nextArtist"),
    nextDash: document.getElementById("nextDash"),
    nextTitle: document.getElementById("nextTitle"),
    lost: document.getElementById("lost")
  };

  function show(node, visible) {
    node.classList.toggle("is-hidden", !visible);
  }

  /* The large line: a track's dance, a message's text, or the surface's own label for the kinds
     that carry no text. */
  function primaryOf(item) {
    return item.primary || window.R4B.kindLabel(item.kind);
  }

  function applyStaticText() {
    el.idle.textContent = window.R4B.t("noTrack");
    el.nextIdle.textContent = window.R4B.t("noNext");
    el.nextLabel.textContent = window.R4B.t("next");
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
      el.primary.textContent = primaryOf(current);
      show(el.sub, current.kind === "Track" && current.artist.length > 0);
      el.artist.textContent = current.artist;
      el.title.textContent = current.title;
      show(el.dash, current.title.length > 0);

      var duration = snapshot.durationSeconds;
      var elapsed = snapshot.elapsedSeconds;
      // A stop has no end, so the bar stays empty rather than sitting at a meaningless zero.
      el.bar.style.width = duration > 0 ? Math.min(100, (elapsed / duration) * 100) + "%" : "0%";
      el.remaining.textContent = duration > 0 ? window.R4B.mmss(duration - elapsed) : "";
    }

    show(el.next, hasNext);
    show(el.nextIdle, !hasNext);

    if (hasNext) {
      // A queued announcement is billed as "Message" with its text beneath, rather than shouting
      // the whole announcement in the next-up slot before its turn.
      var isMessage = next.kind === "Message";
      el.nextPrimary.textContent = isMessage ? window.R4B.t("message") : primaryOf(next);
      el.nextArtist.textContent = isMessage ? next.primary : next.artist;
      el.nextTitle.textContent = isMessage ? "" : next.title;
      show(el.nextSub, isMessage || (next.kind === "Track" && next.artist.length > 0));
      show(el.nextDash, !isMessage && next.title.length > 0);
    }
  }

  window.R4B.loadConfig().then(function () {
    document.documentElement.lang = window.R4B.lang;
    applyStaticText();

    var connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/display")
      // The reason SignalR is here rather than a bare WebSocket: a projector left running all
      // evening will lose its socket at some point, and nobody is standing at it to reload.
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .build();

    connection.on("snapshot", render);

    connection.onreconnecting(function () { el.lost.hidden = false; });
    connection.onreconnected(function () { el.lost.hidden = true; });
    connection.onclose(function () { el.lost.hidden = false; });

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
