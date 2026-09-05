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
      then: "then",
      noNext: "No next track",
      delay: "Delay",
      gap: "A moment between dances",
      stop: "Stop",
      message: "Message",
      endOfNight: "End of the night",
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
      queueMovedOn: "The queue moved on, have another look",
      autoAdded: "Added automatically",

      randomTrack: "Random track",
      fromPool: "from the pool set at the computer",
      openEnded: "open ended",
      delayLength: "Delay length",
      queueDelay: "Queue delay",
      queueMessage: "Queue message",
      queueEndOfNight: "End the night",
      messagePlaceholder: "Bar closes at midnight",
      queued: "Queued",

      searchPlaceholder: "Dance, artist or title",
      noMatches: "Nothing matches that",
      searchHint: "Search the library",

      silentPause: "Silent pause",
      waitsForYou: "Waits for you",
      onScreen: "On screen",
      nothingFollows: "Nothing follows this",

      pinTitle: "Remote",
      pinHint: "Enter the PIN shown in the app's settings",
      pinButton: "Connect",
      pinWrong: "That PIN is not right",
      pinLocked: "Too many tries. Wait {0} seconds",
      pinDisabled: "The remote is switched off in the app",
      turnedOut: "This remote is no longer let in. Ask for the PIN and enter it again",
      connectionLost: "Connection lost, reconnecting"
    },

    nl: {
      noTrack: "Geen nummer aan het afspelen",
      next: "Volgende",
      then: "daarna",
      noNext: "Geen volgend nummer",
      delay: "Pauze",
      gap: "Even tijd tussen twee dansen",
      stop: "Stop",
      message: "Bericht",
      endOfNight: "Einde van de avond",
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
      queueMovedOn: "De wachtrij is intussen opgeschoven, kijk even opnieuw",
      autoAdded: "Automatisch toegevoegd",

      randomTrack: "Willekeurig nummer",
      fromPool: "uit de pool die op de computer is ingesteld",
      openEnded: "zonder eindtijd",
      delayLength: "Pauzeduur",
      queueDelay: "Pauze toevoegen",
      queueMessage: "Bericht toevoegen",
      queueEndOfNight: "Avond afsluiten",
      messagePlaceholder: "De bar sluit om middernacht",
      queued: "Toegevoegd",

      searchPlaceholder: "Dans, artiest of titel",
      noMatches: "Niets gevonden",
      searchHint: "Doorzoek de bibliotheek",

      silentPause: "Stille pauze",
      waitsForYou: "Wacht op jou",
      onScreen: "Op het scherm",
      nothingFollows: "Hierna komt niets meer",

      pinTitle: "Afstandsbediening",
      pinHint: "Voer de pincode in die in de instellingen staat",
      pinButton: "Verbinden",
      pinWrong: "Die pincode klopt niet",
      pinLocked: "Te veel pogingen. Wacht {0} seconden",
      pinDisabled: "De afstandsbediening staat uit in de app",
      turnedOut: "Deze afstandsbediening wordt niet meer toegelaten. Vraag de pincode en voer hem opnieuw in",
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
    if (kind === "Gap") return R4B.t("gap");
    if (kind === "Stop") return R4B.t("stop");
    if (kind === "Message") return R4B.t("message");
    if (kind === "EndOfNight") return R4B.t("endOfNight");
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
