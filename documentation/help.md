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
- **Right column**: Track catalog or dance tree editor

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

### Dance Synonyms

Opens the dance synonyms editor, where you can define alternative names for dances to group tracks under a single dance type.

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
- **Queue Random Track**: Add a randomly selected track based on the current marked selection in the dance tree. The random selection respects weights and scope.
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

### Toggle to Dance Tree

Use the toggle button in the toolbar to switch the right panel to the dance tree editor.

---

## Dance Tree

The dance tree provides a hierarchical view of dance categories, used to organize dances and control random track selection.

### Structure

The tree is organized into categories (branches) and dances (leaves):

- **Categories** can contain other categories and dances, forming a hierarchy
- **Dances** are the leaf nodes, representing a specific dance type
- Each entry shows its name and the number of matching tracks in parentheses, e.g. "Mazurka (42)"

### Marking for Random Selection

Click the dice icon next to any entry to **mark** it for random selection. The marked entry determines the scope when using "Queue Random Track":

- **Mark the root**: Random selection draws from all dances in the entire tree
- **Mark a category**: Random selection draws from all dances in that category and its subcategories
- **Mark a single dance**: Random selection only picks tracks for that specific dance

The marked entry is highlighted to show it is active.

### Weights

Each category and dance has a **weight** value that influences random selection probability. A higher weight means that entry is more likely to be chosen. The effective probability of a dance being selected is proportional to its weight multiplied by its number of available tracks.

To edit weights, select an entry and click the edit button. A numeric spinner appears next to the name where you can adjust the weight.

### Editing the Tree

Select an entry to reveal action buttons:

- **Add Category**: Create a new subcategory within the selected category
- **Add Dance**: Create a new dance entry within the selected category
- **Edit**: Enter edit mode to rename the entry and adjust its weight
- **Confirm**: Save changes when in edit mode
- **Delete**: Remove the entry (and all its children, if it's a category)
- **Cancel**: Discard changes when in edit mode

### Toolbar

- **Toggle to Track List**: Switch back to the track catalog view
- **Undo** (Ctrl+Z): Revert the last edit. Hover over the button to see a description of the action that will be undone.
- **Redo** (Ctrl+Y): Re-apply the last undone edit. Hover over the button to see a description.
- **Import**: Load a dance tree from a JSON file, replacing the current tree
- **Export**: Save the current tree to a JSON file for backup or sharing

---

## Dance Synonyms

The dance synonyms editor lets you define alternative names for dances. When a track's dance name matches a synonym, it is grouped under the main dance name. This is useful when your music collection uses different spellings or regional names for the same dance.

Entries are displayed as cards in a flowing multi-column layout. Each card shows the main dance name at the top and its synonyms as tags below.

### Managing Entries

- **Add**: Click the **+** button in the toolbar to create a new entry. The entry is created with a default name and immediately enters edit mode so you can type the name.
- **Edit name**: Click the **pencil** icon on a card to enter edit mode. The name becomes an editable text field, focused and fully selected. While editing, all other cards are disabled.
  - Press **Enter** or click the **check** icon to confirm the rename.
  - Press **Escape** or click the **X** icon to cancel and revert to the original name.
  - Cancelling a newly added entry undoes the add entirely.
- **Delete**: Click the **trash** icon on a card (only visible when not in edit mode) to remove the entry and all its synonyms.

### Managing Synonyms

Synonyms appear as tags below the main dance name.

- **Add synonym**: Click the **+** button at the end of the synonym tags to show an inline text field. While adding, all other cards are disabled.
  - Type the synonym, then press **Enter** or click the **check** icon to confirm.
  - Press **Escape** or click the **X** icon to cancel.
- **Remove synonym**: Click the **X** button on any synonym tag to remove it.

### Toolbar

- **Back**: Return to the main screen
- **Undo** (Ctrl+Z): Revert the last change. The tooltip shows which action will be undone.
- **Redo** (Ctrl+Y): Re-apply the last undone change. The tooltip shows which action will be redone.
- **Import**: Load synonyms from a JSON file, replacing the current set (with confirmation)
- **Export**: Save the current synonyms to a JSON file for backup or sharing
- **Add**: Create a new dance entry

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
