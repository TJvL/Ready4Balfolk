/* The browser's own strings.
   Domain sends a kind, not a rendered label, so each surface localizes for itself. These mirror the
   Presentation_* entries in UiStrings.resx; keep them in step when those change. The language comes
   from the app's own setting through /api/config, so the projector and the desktop window never
   disagree.
   scripts/check-translations.py holds the two tables to the same keys and placeholders and fails
   on a key no page reads. It reads each entry as one line of key: "text", so keep them that way. */

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

      oneSecond: "1 second",
      seconds: "{0} seconds",
      oneMinute: "1 minute",
      minutes: "{0} minutes",
      delayWithDuration: "Delay ({0})",
      messageWithDuration: "Message ({0})",

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
      nowMovedOn: "That dance has ended, have another look",
      autoAdded: "Added automatically",

      randomTrack: "Random track",
      fromPool: "from the pool set at the computer",
      openEnded: "open ended",
      delayLength: "Delay length",
      delayShorter: "{0} seconds shorter",
      delayLonger: "{0} seconds longer",
      queueDelay: "Queue delay",
      queueMessage: "Queue message",
      queueEndOfNight: "End the night",
      messagePlaceholder: "Bar closes at midnight",
      queued: "Queued",
      queuedDance: "Queued {0}",

      searchLabel: "Search",
      searchPlaceholder: "Dance, artist or title",
      noMatches: "Nothing matches that",

      silentPause: "Silent pause",
      waitsForYou: "Waits for you",
      onScreen: "On screen",
      nothingFollows: "Nothing follows this",

      remoteTitle: "Ready4Balfolk remote",
      pinTitle: "Remote",
      pinLabel: "PIN",
      pinHint: "Enter the PIN shown in the app's settings",
      pinButton: "Connect",
      pinWrong: "That PIN is not right",
      pinLocked: "Too many tries. Wait {0} seconds",
      pinDisabled: "The remote is switched off in the app",
      turnedOut: "This remote is no longer let in. Ask for the PIN and enter it again",
      connectionLost: "Connection lost, reconnecting",

      /* The hub's own refusals, which it sends as these keys rather than as words. */
      noTrackToPick: "No track could be picked from the dance list",
      noEndOfNightAudio: "No end-of-the-night audio has been chosen at the computer",
      messageEmpty: "A message needs some text",
      messageTooLong: "A message can be at most {0} characters",
      trackGone: "That track is no longer in the library",
      cannotMoveUp: "That item cannot move up",
      cannotMoveDown: "That item cannot move down",
      cannotRemove: "That item cannot be removed"
    },

    nl: {
      noTrack: "Geen nummer aan het afspelen",
      next: "Volgende",
      then: "daarna",
      noNext: "Geen volgend nummer",
      delay: "Onderbreking",
      gap: "Even tijd tussen twee dansen",
      stop: "Stop",
      message: "Bericht",
      endOfNight: "Einde van de avond",
      reconnecting: "Opnieuw verbinden",

      oneSecond: "1 seconde",
      seconds: "{0} seconden",
      oneMinute: "1 minuut",
      minutes: "{0} minuten",
      delayWithDuration: "Onderbreking ({0})",
      messageWithDuration: "Bericht ({0})",

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
      nowMovedOn: "Die dans is intussen afgelopen, kijk even opnieuw",
      autoAdded: "Automatisch toegevoegd",

      randomTrack: "Willekeurig nummer",
      fromPool: "uit de pool die op de computer is ingesteld",
      openEnded: "zonder eindtijd",
      delayLength: "Onderbrekingsduur",
      delayShorter: "{0} seconden korter",
      delayLonger: "{0} seconden langer",
      queueDelay: "Onderbreking aanvragen",
      queueMessage: "Bericht aanvragen",
      queueEndOfNight: "Avond afsluiten",
      messagePlaceholder: "De bar sluit om middernacht",
      queued: "Toegevoegd",
      queuedDance: "{0} toegevoegd",

      searchLabel: "Zoeken",
      searchPlaceholder: "Dans, artiest of titel",
      noMatches: "Niets gevonden",

      silentPause: "Stille onderbreking",
      waitsForYou: "Wacht op jou",
      onScreen: "Op het scherm",
      nothingFollows: "Hierna komt niets meer",

      remoteTitle: "Ready4Balfolk-afstandsbediening",
      pinTitle: "Afstandsbediening",
      pinLabel: "Pincode",
      pinHint: "Voer de pincode in die in de instellingen staat",
      pinButton: "Verbinden",
      pinWrong: "Die pincode klopt niet",
      pinLocked: "Te veel pogingen. Wacht {0} seconden",
      pinDisabled: "De afstandsbediening staat uit in de app",
      turnedOut: "Deze afstandsbediening wordt niet meer toegelaten. Vraag de pincode en voer hem opnieuw in",
      connectionLost: "Verbinding verbroken, opnieuw verbinden",

      noTrackToPick: "Er kon geen nummer uit de dansenlijst gekozen worden",
      noEndOfNightAudio: "Er is op de computer nog geen slotmuziek gekozen",
      messageEmpty: "Een bericht heeft wat tekst nodig",
      messageTooLong: "Een bericht mag hoogstens {0} tekens lang zijn",
      trackGone: "Dat nummer staat niet meer in de bibliotheek",
      cannotMoveUp: "Dat item kan niet omhoog",
      cannotMoveDown: "Dat item kan niet omlaag",
      cannotRemove: "Dat item kan niet verwijderd worden"
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

  /* The large line for an item: a track's dance, a message's text, or the surface's own label for
     the kinds that carry no text of their own. Every page draws this line, so the fallback lives
     here rather than once per page. */
  R4B.primaryLabel = function (item) {
    return item.primary || R4B.kindLabel(item.kind);
  };

  /* A track's artist and title as one line, written the way the desktop window writes them. A
     library always has a track missing one of them, and a field with nothing in it takes its
     separator with it: a track with no artist is its title, not " - " and the title, and not
     nothing at all. Both pages join here, so the projector and the phone cannot drift apart. */
  R4B.trackLine = function (artist, title) {
    return [artist, title]
      .map(function (field) { return (field || "").trim(); })
      .filter(function (field) { return field.length > 0; })
      .join(" - ");
  };

  /* How long, in words: seconds for a short wait, minutes once it stops being one. */
  R4B.durationPhrase = function (totalSeconds) {
    var rounded = Math.round(totalSeconds);
    if (rounded < 60) {
      return rounded === 1 ? R4B.t("oneSecond") : R4B.t("seconds", rounded);
    }
    var minutes = Math.max(1, Math.round(rounded / 60));
    return minutes === 1 ? R4B.t("oneMinute") : R4B.t("minutes", minutes);
  };

  /* A delay or a timed message says how long it lasts, since there is no countdown bar under it
     yet the way there is once it starts playing. */
  R4B.kindLabelWithDuration = function (kind, totalSeconds) {
    var phrase = R4B.durationPhrase(totalSeconds);
    if (kind === "Delay") return R4B.t("delayWithDuration", phrase);
    if (kind === "Message") return R4B.t("messageWithDuration", phrase);
    return R4B.kindLabel(kind);
  };

  /* The label for what is queued up next. A message never falls back to its own words here: those
     are drawn on their own line instead, the way the desktop window keeps them apart too. */
  R4B.nextLabel = function (item) {
    if (item.kind === "Message") {
      return item.durationSeconds != null
        ? R4B.kindLabelWithDuration(item.kind, item.durationSeconds)
        : R4B.t("message");
    }
    if (item.kind === "Delay" && item.durationSeconds != null) {
      return R4B.kindLabelWithDuration(item.kind, item.durationSeconds);
    }
    return R4B.primaryLabel(item);
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
