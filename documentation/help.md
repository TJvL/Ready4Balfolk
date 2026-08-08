# Ready4Balfolk Help

Ready4Balfolk is a music queue management application designed for balfolk dance events. It helps dancers/organizers manage tracks by dance type, build playlists, and display current and upcoming dances to the dance floor.

---

## Getting Started

### Music Directory

Before using Ready4Balfolk, you need to configure a music directory. Go to **Settings** and browse to the folder containing your music files.

### File Naming Convention

Tracks are discovered automatically from your music directory. Files should follow this naming pattern:

**Dance - Artist - Title.ext**

For example: `Mazurka - Duo Absynthe - La Java Bleue.mp3`

The application splits the file name on dashes to extract the dance name, artist, and title. Supported audio formats: MP3, MP2, MP1, WAV, OGG, AIFF, and FLAC.

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
- **Queue Random Track**: Add a randomly selected track, from whatever the dance list is currently scoped to. Weights are respected.
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

The dance list is your own list of the dances you play and the names each one goes by. Nothing is built into the application: you build the list once when you first run Ready4Balfolk, by importing a published list or starting empty, and after that it is yours to edit. The list is also what random selection reads, so there is no second structure to keep in step with it.

### Categories and dances

- **Categories** hold dances and other categories. An imported list starts with a category per region, and a category inside it for a family or suite.
- **Dances** live in a category. Each one carries the names it goes by; the first is the one shown everywhere in the application.

Select anything on the left to edit it on the right.

### Names

A dance can go by several names, because the spelling of a balfolk dance is genuinely contested. All of them mean the same dance, and none is treated as the correct one.

- **Add a spelling**: type it in the box under the list of names and press Enter.
- **Choose which one is shown**: click the **up arrow** next to a spelling to move it to the top. Nothing else moves, because everything in the application refers to the dance itself rather than to its name.
- **Remove a spelling**: click the **trash** icon. A dance always keeps at least one name.

**A name can only ever mean one dance.** Adding a spelling that another dance already answers to is refused, and the message says which dance has it. That is what lets Ready4Balfolk answer with one dance when it recognises a name in a filename.

### Weights

Categories and dances both carry a **weight**, and a random pick is weighted by the category's weight multiplied by the dance's. A higher weight comes up more often; **zero means never**, and a category weighted zero takes everything inside it with it.

### Choosing what random picks from

Click the **dice** icon on any row to scope random selection to it:

- **A category**: picks from the dances in it and in everything under it.
- **A single dance**: picks only tracks for that dance.
- Click the dice again, or use **Whole list** in the toolbar, to go back to picking from everything.

The toolbar always says what the current scope is, because the dice is easy to hit by accident and hard to notice afterwards.

### Editing

- **Add category**: creates a new top-level category. With a category selected, **Add a category inside this one** nests one.
- **Add a dance**: type its name in the box on a selected category and press Enter.
- **Rename**: edit the name of the selected category and press Enter. Two categories in the same place cannot share a name.
- **Delete**: removes the selected category or dance. Deleting a category takes the dances in it with it, and it says how many first.
- **Undo** / **Redo**: every edit can be undone. Hover a button to see which change it will undo.
- **Import** / **Export**: replace the whole list from a file, or save it for backup or sharing. Importing cannot be undone, so it asks first.

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
