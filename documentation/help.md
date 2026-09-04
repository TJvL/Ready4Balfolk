# Ready4Balfolk Help

Ready4Balfolk is a music queue management application designed for balfolk dance events. It helps dancers/organizers manage tracks by dance type, build playlists, and display current and upcoming dances to the dance floor.

---

## Getting Started

### Music Directory

Ready4Balfolk asks for one folder the first time it runs, in a short setup that also fetches the
dance list and shows you what is waiting. Everything below that folder counts, however it is
arranged. You can run the setup again later from **Settings**.

### How your files are read

Tracks are discovered automatically from your music directory. There is **no required naming convention**, and nothing is assumed about how your library is arranged: loose files in one folder and a five-deep tree are both ordinary.

- **The dance** is recognised when a name from your dance list appears in the file name or in the tags, wherever it sits: `10. Hep Harz (Cercle).mp3`, `11-La Violette - valse 5tps.mp3`, or a dance written into the tags. Two sources agreeing is what makes an answer trustworthy, and when a file names two dances with nothing to separate them, nothing is assumed.
- **The artist** comes from the artist tags. A folder name is not read as an artist: the same level is an artist in one library and a country in the next.
- **The title** comes from the title tag, falling back to the file name with any leading track number taken off.

Anything not answered this way waits for you in **Review** rather than being filled in with a guess. A track is in your library or in review, never both: crossing over needs an artist, a title, a dance from the published list, and you having agreed to all three. An unreviewed library correctly shows no music.

Review is a fixture rather than a step of setup. Retag a file, rename one or drop new ones in and they come back on their own, with what you answered before kept.

Supported audio formats: MP3, MP2, MP1, WAV, OGG, AIFF, and FLAC.

### Rules: telling it how your files are named

If your library *does* follow a shape, you can say so. Open **Review** and press **Rules**; the panel opens over the queue it exists to empty. What you state there outranks everything the application worked out for itself, because you know your library and it does not.

Each of the four is ticked on separately, and they all start off. Tick the ones your library actually uses; the rest stay folded away and do nothing, so a rule you cannot see never reaches your files. Unticking one keeps what you wrote in it, in case you want it back.

- **File name patterns.** `%d` dance, `%a` artist, `%t` title, `%n` track number, `%i` ignore, `%ex` extension; anything else has to be there exactly. `%d - %a - %t` reads `Mazurka - Naragonia - Idiosyncrasie.mp3`. A pattern has to match a whole name, and the first pattern in the list that does is the one that answers, so put the most specific first.
- **Folder levels.** Counted from the outside in. Saying level 1 is the artist reads that folder as the artist for every file deep enough to have one, and says nothing about the files that are not.
- **Tags.** Which tag fields hold which value. Left alone, artist and album artist are read as the artist, the title tag as the title, and no tag is read as the dance. A dance name from your list is still recognised inside any tag whatever you set here.
- **A custom dance tag.** Some libraries carry the dance in a free-form tag of their own: an ID3v2 `TXXX` frame or a Xiph field named `DANCE`, `STYLE`, or whatever your tagger called it. Name that tag here and its value is read as the dance, whole: a value the list does not know parks the track exactly as any other declared answer would. The panel shows how many of your files carry a tag of that name before you commit to it.

**A rule is a bulk approval**, which is the point of it: rather than answering two thousand files one at a time, you agree once to the rule that answers them. So you are shown what it does before you add it, in the numbers that matter, how many files it takes, what it makes of them, and how many would be left. Adding, removing or reordering a rule re-reads your library, because a rule is meant to answer the files already sitting in it.

**It also tells you what your library looks like.** At the top of the panel are the shapes measured from your own file names and folders: "296 of 2685 files are shaped like `%d - %i - %t`", "level 1 looks like the artist, 96 of 121 agree", each with the counts behind it and the files it was read from. They are proposals: nothing is applied until you press **Declare it**. Where the measurements do not agree, the shape is shown and nothing is named, because a confident guess about your whole library is worse than no guess.

**A dance missing from the published list is not a rule problem.** The panel links to [BigBalfolkList](https://tjvl.github.io/BigBalfolkList/) at the top, because that is where a missing dance is proposed. Beneath the rules is a switch for letting a dance the list does not carry into your library anyway. It is off to begin with: the shared list is what makes a dance name mean the same thing to everybody, and a track let in this way can never come up in a random pick, because those draw by tag and a dance nobody has published has none.

### Main Screen Layout

The main screen is divided into two columns:

- **Left column**: Toolbar, playback controls, the equalizer, and the queue or history view
- **Right column**: Track catalog or dance list

You can toggle between views in each column using the toggle buttons at the top of each panel.

---

## Toolbar

The toolbar sits at the top-left of the main screen and provides navigation to the main sections of the application.

### Exit

Closes the application.

### Help

Opens this help screen.

### Settings

Opens the settings screen: queue behaviour, presentation displays, theme, and the way back into
setup.

### Review

Everything waiting for you, with a count of how many tracks that is. Nothing reaches your library without an artist, a title, a dance from the published list and your agreement, so this is where a library is made rather than a chore at the end of one. See [Review](#review-1) below.

---

## Playback

The playback panel shows what is currently playing and provides transport controls.

### Now Playing Display

- **Dance name**: Shown prominently at the top
- **Artist and title**: Displayed below the dance name as "Artist: Title"
- **Progress bar**: Shows the current position in the track, with elapsed time on the left and total duration on the right

When a message item is playing, the display switches to message mode with auto-scrolling text.

### Transport Controls

- **Play / Pause**: Toggles playback. When a track is loaded, click to start or pause playback.
- **Restart**: Restarts the current track from the beginning. If a track is currently playing, a confirmation dialog appears first (unless disabled in settings).
- **Next / Clear**: This button changes behavior depending on the queue state:
  - **Next** (when the queue has items): Skips to the next item in the queue. A confirmation dialog appears if a track is currently playing (unless disabled in settings).
  - **Clear** (when the queue is empty): Stops playback and clears the current item.

---

## Equalizer

The equalizer shapes the sound going out to the PA. It exists for the nights where the app is the
only place the sound can be adjusted: no mixing desk, no sound engineer. Set it against the room
early in the evening and leave it alone.

It is collapsed by default. The header shows **on** or **off**, so an equalizer left engaged from a
previous night is visible without opening the panel. Changes take effect immediately, including
while a track is playing, which is the only practical way to judge a room.

### Bands

Seven sliders, each cutting or boosting by up to 15 dB at a fixed frequency:

| Slider | Typically used for |
|---|---|
| 63 Hz | Weight and rumble |
| 160 Hz | Boom and boxiness, the usual problem in a hard-walled hall |
| 400 Hz | Muddiness |
| 1 kHz | Body of accordion, fiddle and voice |
| 2.5 kHz | Presence and attack |
| 6.3 kHz | Harshness, sibilance |
| 16 kHz | Air and sparkle |

The outer two are shelving filters, so they lift or cut everything below 63 Hz and above 16 kHz
rather than only a band around the centre. Cutting is almost always safer than boosting.

### Low cut

A high pass filter that removes everything below the chosen frequency, adjustable from 20 to
200 Hz. Useful against stage rumble, handling noise and traffic, and on small speakers that cannot
usefully reproduce the bottom octave anyway. Start around 40 to 60 Hz.

### Preamp

Boosting bands makes the signal louder and can clip the output, which sounds like distortion that
gets worse on the loudest parts of a track. If you have boosted anything, pull the preamp down by
roughly the size of your largest boost.

### Reset to flat

Returns every slider to 0 dB and switches the low cut off. The equalizer stays enabled.

If the panel says the equalizer is unavailable, the BASS_FX audio library could not be loaded.
Playback is unaffected.

---

## Queue

The queue shows the upcoming items to be played, in order from top to bottom.

### Queue Items

The queue can contain several types of items, each with a distinct appearance:

- **Track**: A music file to play. Shows the dance name, artist, title, and duration.
- **Auto-track**: A randomly selected track, shown with a faded appearance and a recycling icon. It waits at the bottom of the queue, below any requests, while the auto-queue feature is enabled and something is playing. It has two extra actions:
  - **Refresh**: Pick a different random track
  - **Pin**: Convert the auto-track into a regular track, keeping it in the queue permanently
- **Stop**: A marker where playback will pause until you manually continue. Shown with an orange highlight.
- **Delay**: A timed pause. Playback resumes automatically after the configured duration. Shown with a blue highlight.
- **Message**: A text announcement displayed on screen, optionally with a duration. Shown with a teal highlight.
- **End of the night**: The music that ends the ball, shown with a violet highlight. It is not a track: it is the file named in the settings, it never enters your library, and nothing goes into the queue after it. Remove it and the evening is open again.

### Managing the Queue

- **Reorder**: Drag and drop items to rearrange them
- **Remove**: Select an item and press the Delete key, or use the Remove button in the toolbar
- **Double-click a track** in the Track Catalog to add it to the queue

### Queue Toolbar

The toolbar above the queue provides these actions:

- **Toggle to History**: Switch the left panel to show the history view
- **Queue Random Track**: Add a randomly selected track, drawn from the tags currently in the pool. With nothing chosen it draws from every dance you own a track for.
- **Enqueue Stop**: Insert a stop marker into the queue
- **Enqueue Delay**: Insert a delay marker with the duration configured in settings
- **Request Message**: Opens a dialog where you can type a message and optionally set a duration
- **End the Night**: Queue the end-of-the-night audio. Switched off until a file is named in the settings, and while one is already queued or playing.
- **Remove Selected**: Delete the currently selected queue item
- **Clear Queue**: Remove all items from the queue (with confirmation)

### Status Bar

At the bottom of the queue panel:

- **Item count**: Shows the number of items in the queue, or "Queue empty"
- **Finish time**: What the queue is going to do. With the auto-queue off, it is the estimated time the playlist runs out, shown as "Playlist finishes at HH:mm". With the auto-queue on there is no such moment, because the queue keeps refilling itself: if you have set an end time it shows that, as "Playlist winds down at HH:mm", and if you have not it says the playlist keeps going until you stop it. A stop, or a message without a duration, overrides all of that with "halts at", since playback will pause at that point. Once the end of the night is queued the evening has a real end again, so it goes back to "finishes at".

---

## History

The history view shows a log of items that have been played or skipped during the current night.

### Viewing History

Switch to the history view using the toggle button in the queue toolbar. Each entry shows:

- **Description**: The dance name (for tracks), message text, delay, stop, or the end of the night
- **Duration**: How long the item played
- **Status**: Whether it was finished or skipped

### History Toolbar

- **Toggle to Queue**: Switch back to the queue view
- **Export History**: Save the night to a JSON file. Useful for keeping records of what was played at an event.
- **New night**: Keep tonight and start a new one (with confirmation). Nothing is deleted: the evening is filed and the history starts empty. Use it after a soundcheck, or on any evening that did not end with the end-of-the-night audio.
- **Delete**: Throw tonight's history away (with confirmation). This cannot be undone.

### Nights

An evening ends by itself once the end-of-the-night audio has played: the night is kept and the history starts fresh, so nothing has to be remembered while packing up. The exported file and the history on screen are always the current night, never a mix of tonight and last month.

If an evening was never ended, because the application was closed or the laptop went flat, it is still there when the application starts again. After more than eight hours of quiet it asks once, before anything is playing, whether to keep it and start fresh or carry on with it. Neither answer deletes anything.

### Status Bar

At the bottom of the history panel:

- **Item count**: Number of history entries, or "No history"
- **Total duration**: Combined playback time of all history entries

---

## Track Catalog

The track catalog shows your library, the tracks you have answered in Review, in a searchable,
sortable table. Anything still waiting is not here; it is in [Review](#review-1).

### Browsing Tracks

The catalog displays tracks in a data grid with these columns:

- **Dance**: The dance type
- **Artist**: The artist or band name
- **Title**: The track title
- **Length**: The track duration in MM:SS format

Click any column header to sort by that column. Click again to reverse the sort order.

### Searching

Use the search box in the toolbar to filter tracks. The search matches against the dance name, artist, and title simultaneously. Results update in real-time as you type. Click the clear button to reset the search.

### Enqueueing Tracks

Double-click a track to add it to the end of the queue. If duplicate prevention is enabled in settings, tracks that are currently playing, already in the queue, or already in the history cannot be added again.

### Fixing a typo where you see it

Right-click a track, here, or on a track sitting in the queue, and choose **Edit track** to
correct its dance, artist or title on the spot. The track never leaves your library while you do:
what you change is saved as your own answer, and only the fields you actually changed are touched.
The dance still has to be one the published list knows; a missing dance is a proposal at
[BigBalfolkList](https://tjvl.github.io/BigBalfolkList/), not a local override.

### Toggle to Dance List

Use the toggle button in the toolbar to switch the right panel to the dance list.

---

## Dance List

The dance list is every balfolk dance and every name each one goes by. It comes from
[BigBalfolkList](https://tjvl.github.io/BigBalfolkList/) and Ready4Balfolk uses it exactly as
published: there is nothing to build, nothing to fill in, and nothing in the application edits it.

No copy is shipped with Ready4Balfolk. The first time you open it, setup asks you to fetch the list
from BigBalfolkList, or to import a `dances.json` somebody carried in on a stick if this machine
never goes online. Nothing can be answered about your music until one of those has happened, and
the application never fetches anything on its own: the Update button in the dance list panel is
how you ask for a newer one.

### What is in it

- **The names of a dance are equals.** Spelling is contested and this list does not take sides; the
  first name is simply the one shown in the application. A name belongs to exactly one dance, which
  is what lets Ready4Balfolk answer with one dance when it recognises a name in a filename.
- **Everything else is a tag**: where a dance comes from, which family it belongs to, whether it is
  danced as part of a suite. A dance can be Breton *and* a gavotte *and* part of a suite without
  being filed under one of them.
- **Grammar is not a spelling.** The list carries two small word lists, so a number word counts as
  its number and glue like *de*, *la*, *the* and *temps* is ignored when names are compared. That is
  what makes `Bourrée à 3 temps`, `Bourrée in 3`, `Bourrée à trois temps` and `Bourrée 3t` one
  dance, and what lets a library written in French, Dutch or German match at all.

### Choosing what random picks from

The tags in the left-hand rail are the **pool**. A click walks a tag through three states: out,
**drawn from** (filled), and **never drawn** (red border, struck through); a third click puts it
back out. A random pick, and the auto-queue, draw from the dances carrying any included tag and
none of the excluded ones, an exclusion always wins, so *bretagne* but never *chain* means exactly
that. With nothing chosen the pool is every dance.

The toolbar always says what is being drawn from, because a tag is easy to click on the way past and
hard to notice afterwards. **Everything** empties the pool again.

Tags are sized by how many dances carry them, and clicking a tag on a card does the same thing as
clicking it in the rail.

### One particular dance

Click the **dice** on a dance to queue a random track of that dance, whatever the pool is set to.
A dance you own no tracks for says so instead, and can never come up in a random pick either.

### Searching

The search box matches every spelling of every dance, ignoring case, accents and punctuation, so
`hanterdro` finds *Hanter dro* and `bourree 3` finds *Bourrée à trois temps*.

### Keeping it up to date

- **Update**: fetches the list as BigBalfolkList publishes it right now. Useful when you know
  something was just added.
- **From a file**: takes the list from a `dances.json` on this computer, for a machine that never
  reaches the internet.

Either way the list is replaced whole and checked first; if it cannot be read, the one you already
have stays in use.

### Something missing or misspelled?

Propose it at [BigBalfolkList](https://tjvl.github.io/BigBalfolkList/). The site lets you add a
spelling, fix one, tag a dance or add a dance that is missing, and it turns what you did into a
proposal for someone to look at. Everyone using the list gets your correction, which is the point of
there being one list.

---

## Review

Nothing reaches your library until it has an artist, a title, a dance from the published list, and
you having agreed to all three. Review is where that agreement happens, and it is a permanent part
of the application rather than a step of setup: retag a file, rename one or drop new ones in, and
they come back here on their own with whatever you answered before still on them.

A first run therefore shows a library with no music in it and a queue holding everything. That is
correct, and it is the point: a track that was filled in by a guess nobody looked at is worse than
one that is honestly still waiting.

### What the queue shows

- **One row per track**, not one per mistake. A file that says nothing at all about itself has to be
  answerable too, and it could never appear in a list of things that were spelled wrongly.
- **The least certain first**, so stopping halfway leaves your library better rather than merely
  different. Whoever answers forty rows has answered the forty nothing could speak for.
- **Grouped by folder**, with the folder's name in a band above its tracks. Tracks lying loose in
  your music folder are shown apart: they were filed nowhere, so there is nothing to answer together.
- **Each field says where it came from**: a tag, the file name, a folder, one of your rules, or you.
  A wrong answer is only obvious when you can see what produced it.

### The keyboard

| | |
|---|---|
| Up, Down | between tracks |
| Tab | between the three fields of a track, wrapping round |
| Enter | answer this track and move to the next one waiting |
| Shift+Enter | answer every track in this folder that is complete |
| Shift+Space | listen to the selected track |
| Escape | stop listening |
| Left, Right | skip five seconds, while something is playing |

Selecting a track puts the cursor in its first empty field, so you can simply type. Typing a dance
offers the names the list holds; the arrows walk them and Enter takes the highlighted one.

Answering a folder **confirms** rather than fills in: each track keeps the artist and title it
already has, and a track still missing something is left where it is. A folder of more than a
handful asks before it goes ahead.

If a track cannot be answered, the row flashes red rather than doing nothing quietly; asking a
folder points at every track holding it up.

### Colours

| | |
|---|---|
| plain | waiting for you |
| green | answered, and in your library |
| amber | answered, and waiting for the dance list to carry the name you gave it |

### When a dance is not in the list

The row offers what the name might have meant, so a misspelling is one click rather than retyping.
Beside that:

- **Use for all N that say X** sets that dance on every waiting track claiming the same thing. It
  sets the dance and nothing else, so each of those tracks still wants its own confirmation:
  artists and titles are not shared.
- **X is not a dance** says the value is junk. `trad` is not a dance and never will be, so it is
  cleared from every track claiming it and remembered, and a rescan does not put it back. Those
  tracks still need a real dance.

A dance that is genuinely missing belongs in a proposal at
[BigBalfolkList](https://tjvl.github.io/BigBalfolkList/), linked from the Rules panel. Your answer is
kept in the meantime, and the moment an updated list carries the name, those tracks go into your
library without you being asked again.

---

## Settings

The settings screen lets you configure application behavior. All changes are saved automatically.

### Music Directory

Where your music is, shown rather than edited: changing it re-reads the whole library and re-decides
every track, which is setup rather than a setting to nudge. **Run setup again** is how it is changed.
Everything below that folder counts, however it is arranged.

### Maximum Queue Items

The maximum number of items allowed in the queue, between 1 and 100. When the queue is full, new items cannot be added until existing ones are played or removed.

### Delay Duration

The default duration (in seconds) for delay markers added to the queue, between 1 and 300 seconds. This value is used when you click "Enqueue Delay" in the queue toolbar.

### Stop accepting requests after a set time

An end time for the night. Once the queue would run past it (plus a grace period in minutes), new
entries are refused, so the last dance ends when the hall closes rather than twenty minutes after.
The auto-queue is held to the same line and stops adding tracks, rather than carrying the evening on
by itself. While a stop request is queued the end time is unknown and the limit is not applied; use a
delay instead if you know how long the pause will be.

### End-of-the-night audio

The music that means the ball is over: stop dancing, find your coat, help stack the chairs. One
file, wherever you keep it. It is not imported and never enters the library, because it has no
dance, no artist and no title and would sit in the review queue forever asking for them. Type the
path or use the browse button; leave it empty and the button in the queue toolbar stays switched
off, as it does if the file later moves.

Queueing it declares the evening over. Nothing goes in after it, not a track, not a request, not a
delay or a message, and the auto-queue stops so the machine does not extend an evening you have just
ended. Removing it reopens the evening.

With **play it after the last track the end time allowed** ticked, it is queued for you the moment
the end time refuses the next track, so the last thing the room hears is the thing that means go
home and nobody has to remember to press anything while packing up. The end time never refuses the
end-of-the-night audio itself: it is what happens after the line, not something sneaking past it.

### Show text on buttons

Replaces the icons on buttons with a short description of what they do. Useful while learning the
application, or for anyone who prefers words to pictograms.

### Language

The application language, English or Dutch. Takes effect after a restart.

### Presentation Displays

The number of presentation windows to show, between 0 and 10. Set to 0 to disable presentation windows entirely. Presentation windows are designed to be shown on projectors or external screens visible to dancers.

### Auto-queue Random Track

When enabled, a randomly picked track waits at the bottom of the queue whenever something is playing, so the music never simply stops. The auto-track appears with a faded style and can be refreshed (to pick a different track) or pinned (to keep it permanently). It stays below anything you add yourself, and a fresh one is picked each time the previous one starts playing. If you have set an end time for the night, the auto-queue stops adding once a track would run past it, so the evening winds down on schedule.

### Allow Duplicate Tracks

When disabled, tracks that are currently playing, already in the queue, or already in the session history cannot be added to the queue again. This prevents the same track from being played twice in a session.

### Confirm Playback Actions

When enabled (the default), a confirmation dialog is shown before skipping to the next item, clearing the current track, restarting playback, or moving to a spot you clicked on the progress bar. Disable this if you find the confirmations disruptive during a performance.

### Theme

Choose between three options:

- **Auto**: Follows the system theme
- **Light**: Light color scheme
- **Dark**: Dark color scheme

### Export Log File

Click to save the application log file. Useful for troubleshooting issues or submitting bug reports.

---

## Presentation Display

Presentation windows are full-screen displays intended for projectors or external monitors, showing the audience what is currently playing and what comes next.

### Layout

- **Top half**: The current dance name in large text, with artist and title below. When a message item is active, the message text is displayed instead.
- **Progress bar**: A green bar in the middle showing playback progress
- **Bottom half**: The next upcoming dance name and track details, or "No next track" if the queue is empty

### Configuration

Set the number of presentation windows in **Settings > Presentation Displays**. Each window can be moved to a different screen and will remember its position between sessions.

---

## Phone Remote and Web Display

Ready4Balfolk can serve two pages over your local network from a small built-in web server:

- **The display page** shows what is playing and what comes next, for any device with a browser.
  A spare tablet by the stage works as a presentation screen without a video cable.
- **The remote** can play, pause, skip, queue a random track, a stop, a delay or a message, end the
  night, and search the library, the controls a DJ needs while away from the desk, and nothing
  more. Deliberately, it cannot change the pool: what random picks draw from is decided at the
  computer, and the remote draws from whatever the screen there says. Ending the night works the
  same way: which file that is was decided at the computer, and with none chosen the remote says so
  rather than offering to pick one.

Both are enabled in **Settings**. The remote is off unless you turn it on, and it is guarded by a
PIN: anyone who can reach the page and knows the PIN can change what the room is dancing to, so
treat it accordingly. **New PIN** mints a fresh one and disconnects every phone using the old one.
That phone is told so and shown the PIN form again, so a helper at the bar can tell being turned out
from an application that has stopped working. The same happens to a phone whose access has simply
aged out overnight: it asks for the PIN rather than showing a remote that does nothing.

The random pick on the remote draws from the same pool as the desktop, exclusions included.
