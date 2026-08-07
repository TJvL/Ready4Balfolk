/* The browser's own strings.
   Domain sends a kind, not a rendered label, so each surface localizes for itself. These mirror the
   Presentation_* entries in UiStrings.resx; keep them in step when those change. The language comes
   from the app's own setting through /api/config, so the projector and the desktop window never
   disagree. */

(function (global) {
  "use strict";

  var TABLE = {
    en: {
      noTrack: "No track playing",
      next: "Next",
      noNext: "No next track",
      delay: "Delay",
      stop: "Stop",
      message: "Message",
      reconnecting: "Reconnecting",

      playing: "Playing",
      nothingPlaying: "Nothing playing",
      queueEmpty: "The queue is empty",
      play: "Play",
      pause: "Pause",
      restart: "Restart",
      holdToSkip: "Hold to skip",
      keepHolding: "Keep holding",
      skipped: "Skipped",
      restarted: "Restarted",

      tabNow: "Now",
      tabQueue: "Queue",
      tabAdd: "Add",
      tabFind: "Find",

      items: "items",
      item: "item",
      moveUp: "Up",
      moveDown: "Down",
      remove: "Remove",
      removed: "Removed",
      autoAdded: "Added automatically",

      randomTrack: "Random track",
      fromDanceTree: "from the dance tree",
      openEnded: "open ended",
      delayLength: "Delay length",
      queueDelay: "Queue delay",
      queueMessage: "Queue message",
      messagePlaceholder: "Bar closes at midnight",
      queued: "Queued",

      searchPlaceholder: "Dance, artist or title",
      noMatches: "Nothing matches that",
      searchHint: "Search the library",

      silentPause: "Silent pause",
      waitsForYou: "Waits for you",
      onScreen: "On screen",

      pinTitle: "Remote",
      pinHint: "Enter the PIN shown in the app's settings",
      pinButton: "Connect",
      pinWrong: "That PIN is not right",
      pinLocked: "Too many tries. Wait {0} seconds",
      pinDisabled: "The remote is switched off in the app",
      connectionLost: "Connection lost, reconnecting"
    },

    nl: {
      noTrack: "Geen nummer aan het afspelen",
      next: "Volgende",
      noNext: "Geen volgend nummer",
      delay: "Pauze",
      stop: "Stop",
      message: "Bericht",
      reconnecting: "Opnieuw verbinden",

      playing: "Speelt nu",
      nothingPlaying: "Niets aan het afspelen",
      queueEmpty: "De wachtrij is leeg",
      play: "Afspelen",
      pause: "Pauzeren",
      restart: "Opnieuw",
      holdToSkip: "Houd vast om over te slaan",
      keepHolding: "Blijf vasthouden",
      skipped: "Overgeslagen",
      restarted: "Opnieuw gestart",

      tabNow: "Nu",
      tabQueue: "Wachtrij",
      tabAdd: "Toevoegen",
      tabFind: "Zoeken",

      items: "items",
      item: "item",
      moveUp: "Omhoog",
      moveDown: "Omlaag",
      remove: "Verwijderen",
      removed: "Verwijderd",
      autoAdded: "Automatisch toegevoegd",

      randomTrack: "Willekeurig nummer",
      fromDanceTree: "uit de dansboom",
      openEnded: "zonder eindtijd",
      delayLength: "Pauzeduur",
      queueDelay: "Pauze toevoegen",
      queueMessage: "Bericht toevoegen",
      messagePlaceholder: "De bar sluit om middernacht",
      queued: "Toegevoegd",

      searchPlaceholder: "Dans, artiest of titel",
      noMatches: "Niets gevonden",
      searchHint: "Doorzoek de bibliotheek",

      silentPause: "Stille pauze",
      waitsForYou: "Wacht op jou",
      onScreen: "Op het scherm",

      pinTitle: "Afstandsbediening",
      pinHint: "Voer de pincode in die in de instellingen staat",
      pinButton: "Verbinden",
      pinWrong: "Die pincode klopt niet",
      pinLocked: "Te veel pogingen. Wacht {0} seconden",
      pinDisabled: "De afstandsbediening staat uit in de app",
      connectionLost: "Verbinding verbroken, opnieuw verbinden"
    }
  };

  var R4B = global.R4B || (global.R4B = {});
  R4B.lang = "en";

  R4B.t = function (key, arg) {
    var table = TABLE[R4B.lang] || TABLE.en;
    var value = table[key] !== undefined ? table[key] : TABLE.en[key];
    if (value === undefined) return key;
    return arg === undefined ? value : value.replace("{0}", arg);
  };

  /* The label for a kind that carries no text of its own. */
  R4B.kindLabel = function (kind) {
    if (kind === "Delay") return R4B.t("delay");
    if (kind === "Stop") return R4B.t("stop");
    if (kind === "Message") return R4B.t("message");
    return "";
  };

  R4B.loadConfig = function () {
    return fetch("/api/config")
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (config) {
        if (config && TABLE[config.language]) R4B.lang = config.language;
        return config;
      })
      .catch(function () { return null; });
  };

  R4B.mmss = function (seconds) {
    var total = Math.max(0, Math.round(seconds || 0));
    var s = total % 60;
    return Math.floor(total / 60) + ":" + (s < 10 ? "0" + s : String(s));
  };
})(window);
