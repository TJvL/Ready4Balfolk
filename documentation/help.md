# Ready4Balfolk Help

Ready4Balfolk is a music queue management application designed for balfolk dance events. It helps dancers/organizers manage tracks by dance type, build playlists, and display current and upcoming dances to the dance floor.

---

## Getting Started

### Music Directory

Before using Ready4Balfolk, you need to configure a music directory. Go to **Settings** and browse to the folder containing your music files.

### How your files are read

Tracks are discovered automatically from your music directory. There is **no required naming convention**, and nothing is assumed about how your library is arranged: loose files in one folder and a five-deep tree are both ordinary.

- **The dance** is recognised when a name from your dance list appears in the file name or in the tags, wherever it sits: `10. Hep Harz (Cercle).mp3`, `11-La Violette - valse 5tps.mp3`, or a dance written into the tags. Two sources agreeing is what makes an answer trustworthy, and when a file names two dances with nothing to separate them, nothing is assumed.
- **The artist** comes from the artist tags. A folder name is not read as an artist: the same level is an artist in one library and a country in the next.
- **The title** comes from the title tag, falling back to the file name with any leading track number taken off.

Anything not answered this way waits for you in **Review** rather than being filled in with a guess. A track is in your library or in review, never both: crossing over needs an artist, a title, a dance from the published list, and you having agreed to all three. An unreviewed library correctly shows no music.

Review is a fixture rather than a step of setup. Retag a file, rename one or drop new ones in and they come back on their own, with what you answered before kept. It is built for the keyboard: the least certain tracks come first, Enter answers one, and A answers everything left in its folder. Supported audio formats: MP3, MP2, MP1, WAV, OGG, AIFF, and FLAC.

### Advanced discovery

If your library *does* follow a shape, you can say so: **Settings → Advanced discovery**. What you state there outranks everything the application worked out for itself, because you know your library and it does not.

- **File name patterns.** `%d` dance, `%a` artist, `%t` title, `%n` track number, `%i` ignore, `%ex` extension; anything else has to be there exactly. `%d - %a - %t` reads `Mazurka - Naragonia - Idiosyncrasie.mp3`. A pattern has to match a whole name, and the first pattern in the list that does is the one that answers, so put the most specific first.
- **Folder levels.** Counted from the outside in. Saying level 1 is the artist reads that folder as the artist for every file deep enough to have one, and says nothing about the files that are not.
- **Tags.** Which tag fields hold which value. Left alone, artist and album artist are read as the artist, the title tag as the title, and no tag is read as the dance. A dance name from your list is still recognised inside any tag whatever you set here.

**A rule is a bulk approval**, which is the point of it: rather than answering two thousand files one at a time, you agree once to the rule that answers them. So you are shown what it does before you add it, in the numbers that matter — how many files it takes, what it makes of them, and how many would be left. Adding, removing or reordering a rule re-reads your library, because a rule is meant to answer the files already sitting in it.

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

Opens the settings screen where you can configure music directory, queue behavior, presentation displays, and theme.

### Review

Everything waiting for you, with a count of how many tracks that is. Nothing reaches your library without an artist, a title, a dance from the published list and your agreement, so this is where a library is made rather than a chore at the end of one. The least certain tracks come first; Enter answers one and A answers the rest of its folder.

When a track's dance is a name your list does not know, the row offers what it might have meant, "use for all N that say the same", and "that is not a dance". The bulk action sets the dance on all of them; each track still wants its own confirmation, because artists and titles are not shared. "Not a dance" clears junk like `trad` everywhere and remembers it, and those tracks still need a real dance.

---

## Playback

The playback panel shows what is currently playing and provides transport controls.

### Now Playing Display

- **Dance name**: Shown prominently at the top
- **Artist and title**: Displayed below the dance name as "Artist — Title"
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
- **Auto-track**: A randomly selected track, shown with a faded appearance and a recycling icon. Auto-tracks appear when the auto-queue feature is enabled and the queue is empty. They have two extra actions:
  - **Refresh**: Pick a different random track
  - **Pin**: Convert the auto-track into a regular track, keeping it in the queue permanently
- **Stop**: A marker where playback will pause until you manually continue. Shown with an orange highlight.
- **Delay**: A timed pause. Playback resumes automatically after the configured duration. Shown with a blue highlight.
- **Message**: A text announcement displayed on screen, optionally with a duration. Shown with a teal highlight.

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
- **Remove Selected**: Delete the currently selected queue item
- **Clear Queue**: Remove all items from the queue (with confirmation)

### Status Bar

At the bottom of the queue panel:

- **Item count**: Shows the number of items in the queue, or "Queue empty"
- **Finish time**: Estimated time when the playlist will finish, displayed as "Playlist finishes at HH:mm". If the queue contains a stop or a message without a duration, it shows "halts at" instead, since playback will pause at that point.

---

## History

The history view shows a log of items that have been played or skipped during the current session.

### Viewing History

Switch to the history view using the toggle button in the queue toolbar. Each entry shows:

- **Description**: The dance name (for tracks), message text, delay, or stop
- **Duration**: How long the item played
- **Status**: Whether it was finished or skipped

### History Toolbar

- **Toggle to Queue**: Switch back to the queue view
- **Export History**: Save the history to a CSV file. Useful for keeping records of what was played at an event.
- **Clear History**: Delete all history entries (with confirmation)

### Status Bar

At the bottom of the history panel:

- **Item count**: Number of history entries, or "No history"
- **Total duration**: Combined playback time of all history entries

---

## Track Catalog

The track catalog shows all discovered tracks in a searchable, sortable table.

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

### Toggle to Dance List

Use the toggle button in the toolbar to switch the right panel to the dance list.

---

## Dance List

The dance list is every balfolk dance and every name each one goes by. It comes from
[BigBalfolkList](https://tjvl.github.io/BigBalfolkList/) and Ready4Balfolk uses it exactly as
published: there is nothing to build, nothing to fill in, and nothing in the application edits it.

A copy is shipped with Ready4Balfolk, so it works the first time you open it with no internet. It
checks for a newer one each time it starts.

### What is in it

- **The names of a dance are equals.** Spelling is contested and this list does not take sides; the
  first name is simply the one shown in the application. A name belongs to exactly one dance, which
  is what lets Ready4Balfolk answer with one dance when it recognises a name in a filename.
- **Everything else is a tag**: where a dance comes from, which family it belongs to, whether it is
  danced as part of a suite. A dance can be Breton *and* a gavotte *and* part of a suite without
  being filed under one of them.

### Choosing what random picks from

The tags in the left-hand rail are the **pool**: click a tag to put it in, click it again to take it
out. A random pick, and the auto-queue, draw from the dances carrying any tag in the pool. With
nothing chosen the pool is every dance.

The toolbar always says what is being drawn from, because a tag is easy to click on the way past and
hard to notice afterwards. **Everything** empties the pool again.

Tags are sized by how many dances carry them, and clicking a tag on a card does the same thing as
clicking it in the rail.

### One particular dance

Click the **dice** on a dance to queue a random track of that dance, whatever the pool is set to.
A dance you own no tracks for says so instead, and can never come up in a random pick either.

### Searching

The search box matches every spelling of every dance, ignoring case, accents and punctuation, so
`hanterdro` finds *Hanter dro*.

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

## Settings

The settings screen lets you configure application behavior. All changes are saved automatically.

### Music Directory

The path to the folder containing your music files. Click **Browse** to select a folder. The application scans this directory recursively for audio files and extracts dance, artist, and title from file names.

### Maximum Queue Items

The maximum number of items allowed in the queue, between 1 and 100. When the queue is full, new items cannot be added until existing ones are played or removed.

### Delay Duration

The default duration (in seconds) for delay markers added to the queue, between 1 and 300 seconds. This value is used when you click "Enqueue Delay" in the queue toolbar.

### Presentation Displays

The number of presentation windows to show, between 0 and 10. Set to 0 to disable presentation windows entirely. Presentation windows are designed to be shown on projectors or external screens visible to dancers.

### Auto-queue Random Track

When enabled, a random track is automatically added to the queue when it becomes empty during playback. The auto-track appears with a faded style and can be refreshed (to pick a different track) or pinned (to keep it permanently). Auto-tracks are automatically removed when you manually add items to the queue.

### Allow Duplicate Tracks

When disabled, tracks that are currently playing, already in the queue, or already in the session history cannot be added to the queue again. This prevents the same track from being played twice in a session.

### Confirm Playback Actions

When enabled (the default), a confirmation dialog is shown before skipping to the next item, clearing the current track, or restarting playback. Disable this if you find the confirmations disruptive during a performance.

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
