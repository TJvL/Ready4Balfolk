# Development Guide

This document explains the layered architecture, naming conventions, and patterns used in Ready4Balfolk so that new contributors know where to add code and how to follow existing conventions.

## Overview

Ready4Balfolk is a five-project Avalonia desktop application for managing and playing back folk dance music queues.

| Project | Purpose | Key dependencies |
|---------|---------|-----------------|
| `Ready4Balfolk.Domain` | Models, stores, services. No UI dependencies | DynamicData, ManagedBass, Microsoft.Data.Sqlite, TagLibSharp |
| `Ready4Balfolk.UI` | Views, ViewModels, converters, UI services. Hosts the other two | Avalonia 12, ReactiveUI, ReactiveUI.SourceGenerators |
| `Ready4Balfolk.Web` | The presentation display and the phone remote, served from inside the app | ASP.NET Core (via `FrameworkReference`), SignalR |
| `Ready4Balfolk.Tests` | Unit, integration and ViewModel tests | xunit.v3 on Microsoft.Testing.Platform, NSubstitute, System.IO.Abstractions.TestingHelpers |
| `Ready4Balfolk.E2E` | End-to-end scenarios that drive the real application headless, one process per scenario | xunit.v3 on Microsoft.Testing.Platform, Avalonia.Headless, Microsoft.Playwright |

**Key principles:**

- **Reactive-first**: state flows as `IObservable<T>` from Domain stores/services; the UI subscribes and reacts.
- **Immutable models**: all Domain models are sealed records; mutations produce new instances.
- **Interface-driven**: every store and stateful service has an interface for testability and DI. Pure functions of their input (`DanceListReader`, `DanceListValidation`, `AudioContentHasher`, `TrackClaims`, `NightReport` and the like) are static classes instead, and are tested by calling them.
- **MVVM with compiled bindings**: Views bind to ViewModels; `x:DataType` is set on every view.

---

## Domain Layer (`Ready4Balfolk.Domain/`)

### Models

All models are **sealed records**, organised by subdirectory. **Every model that is stored names each stored member with `[JsonPropertyName]`**: `Dance` and `DanceList` because their names are BigBalfolkList's, and the settings (`ApplicationSettings` and the records it is made of) and the history entries in the spelling they have always been written under. `settings.json` is read skipping what it does not recognise, so a rename without a pinned name puts a setting back to its default without a word; a history entry is the only copy there is of an evening, and a rename would read every older night back with that member empty. Tests in `SettingsStoreTests` and `QueueHistoryStoreTests` hold the stored names and fail on any read-back member that has none.

| Directory | Contents |
|-----------|----------|
| `Tracks/` | `Track`: file path, dance, artist, title, length. Carries `OriginalDance` for re-resolution. |
| `QueueItems/` | `IQueueItem` interface + seven implementations: `TrackQueueItem`, `DelayQueueItem`, `MessageQueueItem`, `StopQueueItem`, `AutoTrackQueueItem`, `EndOfNightQueueItem` and `GapQueueItem`. `EndOfNightQueueItem` is the file named in the settings and deliberately not a `TrackQueueItem`: it is not in the library and must never enter it. `GapQueueItem` is the standard gap between two dances while it runs, and is only ever the current item, never a queued one. |
| `Dances/` | `DanceList` -> `Dance`, exactly as BigBalfolkList publishes it: a top-level `Tags` vocabulary and a flat list of `{slug, names, tags}`. A dance's identity is its `Slug`; its `Names` are a flat set of equals whose first entry is what gets displayed; everything else is a tag, so nothing is filed under one grouping at the expense of another. There is no hierarchy and no weight. `DanceListIndex` is the folded-name-to-slug lookup built over a list, `DanceListProblems` is what validation reports, and `DanceListStatus`/`DanceListUpdate` say where the list came from and what came of asking for a newer one. |
| `Settings/` | `ApplicationSettings` and what it is made of: `ApplicationTheme`, `ApplicationLanguage`, `WindowState`, `EqualizerSettings`, `DisplayTemplates`, and the declared discovery rules (`DiscoverySettings`, `TagTrust`, `TagField`, `FolderRole`). |
| `History/` | `QueueHistoryEntry` (abstract, `[JsonPolymorphic]`) with `TrackHistoryEntry`, `MessageHistoryEntry`, `DelayHistoryEntry`, `StopHistoryEntry`, `EndOfNightHistoryEntry`, each carrying `StartedAt`, `FinishedAt` and a `CompletionStatus` of `Finished`, `Skipped` or `FileMissing`. `QueueHistory` is one night: `Id`, `StartedAt`, `EndedAt` and the entries; `NightSummary` is the little a list of nights shows. |
| `Presentation/` | `PresentationState` and the `PresentationItem`s in it: what a presentation surface draws, reduced once by `PresentationStateService` for the window and the browser alike. |

**To add a new model:** create a `sealed record` in the appropriate subdirectory. If it is serialised polymorphically, add `[JsonPolymorphic]` + `[JsonDerivedType]` on the base type. A member of a model that persists gets a `[JsonPropertyName]` when it is added, and its C# name can then change freely; the stored name changes only together with what reads the old one back.

### Stores

Stores own **persisted state** and publish it as observables. There is no one shape they all follow; the ones that publish a single value share this:

- **`BehaviorSubject<T>`** holds what is current, replays it to a new subscriber and broadcasts every change. `Current` reads it and `Observe()` exposes it.
- **`SemaphoreSlim(1, 1)`** serialises the store's own reads and writes, so a write cannot interleave with a load.
- **`ILoadableStore`** is the stores that load after the window opens (`IsLoading`), so a screen can say it is waiting rather than showing an empty list as though it were the answer.
- **The directory comes from `IApplicationSettingsDirectory`**, never a path built at the call site, so a test and a scenario each point a store at their own.

| Store | Holds | How it loads and writes |
|-------|-------|-------------------------|
| `SettingsStore` | `ApplicationSettings`, as `settings.json` | Reads the file **in its constructor**, because a setting is needed while the application is being composed. Never throws there: an unreadable file is kept beside the real one as `.corrupt` and the defaults are used. Writes through `UpdateAsync(Func<T, T>)`, a pure transformation applied under the semaphore and persisted, indented so the file stays editable by hand. |
| `DanceListStore` | The published dance list, as `dance_list.json` | `LoadAsync` reads the cached copy; `RefreshAsync` and `UpdateFromFileAsync` replace it whole. No `UpdateAsync`: the list is read-only vocabulary. Also exposes an `Index`, rebuilt *before* the new list is published, so a subscriber reacting to a change never reads a lookup built from the list it just replaced. |
| `QueueHistoryStore` | The evenings, in `history.sqlite` | `LoadAsync` opens the running night; entries are appended with `AddAsync`. See Nights below. |
| `SqliteLibraryIndex` | What is in the music directory, in `library.sqlite` | `OpenAsync`, then queried and written by `TrackStore`. See Library index below. |
| `TrackStore` | The library as the application plays from it | A DynamicData `SourceList<Track>` rather than a subject, exposed through `Connect()` for collection binding. `ApplyAsync(TrackLibraryConfiguration)` hands it the music directory, the declared rules and the dance rule as one value, so they cannot be applied in an order that scans the library twice. `LibraryWatcher` notices changes on disk and the store decides what each is worth. |

**To add a new store:**

1. Create `IXxxStore` in `Stores/{Feature}/` and `XxxStore` beside it. Take `IApplicationSettingsDirectory` for where it writes, hold its state in a `BehaviorSubject<T>` behind `Current` and `Observe()`, and serialise its own I/O with a `SemaphoreSlim(1, 1)`.
2. If it loads after the window opens, implement `ILoadableStore` and give it a `LoadAsync(CancellationToken)`; if it is needed while the application is composed, read in the constructor as `SettingsStore` does, and never throw there.
3. Register it in `ConfigureServices` in `ApplicationComposition.cs` as a singleton: `services.AddSingleton<IXxxStore, XxxStore>();`.
4. Add its `LoadAsync` to the loads `ApplicationStartup.Run` starts once the main window has opened, with an English log line and a `UiStrings` text for its failure.

### Discovery: claims and corroboration

`Services/Discovery/` decides what a track is, in three steps that are deliberately separable: `TrackDiscoveryService.Gather` opens the file, `TrackClaims.Collect` asks everything that can speak about it what it says, and `TrackInformationResolver.Decide` answers each field from those claims plus the dance list. Only the first touches a disk, so the other two re-run whenever the list or the settings change and are tested without a file existing.

**Everything is a claim: a field, a value, a source, and a trust.** One currency for all of it, and the whole of `Claim`.

- **Claims are raw.** A dance claim carries the text somebody wrote, not a slug: turning text into a dance is the list's job, and a claim the list does not recognise is still a claim. That unrecognised value is exactly what parks a track in review and what 21 identical misspellings group by.
- **Nothing is discarded silently.** Losing claims, and values refused as ripper placeholders, stay on the resolution. A wrong source is only visible next to what it beat, and "the artist tag says Unknown Artist" is a different thing to look at than "there is no artist tag".
- **Three tiers, and the top one that spoke is the only one considered** (`ClaimTrust`): `Declared` (a discovery setting the user filled in), `Measured` (calibration over the library's own strings), `Observed` (this file's tags and name). A tier is not a vote to be weighed: a user who declares a rule has taken responsibility for it, so a declaration replaces the tags rather than arguing with them, and is not "corroborated" by a weaker source agreeing. `Declared` and `Observed` are produced; `Measured` is not, because calibration proposes a rule on the rules panel and what the user accepts is claimed as declared.
- **`DecisionReason` keeps the several meanings of a blank apart**: `NoClaim`, `Unusable`, `Contested` are three different situations, and the review screen has to tell a person which one it is looking at.
- **Independence is per `ClaimSourceKind`**, not per claim: the title tag and the comment tag are one kind between them, because the same ripper wrote both in the same pass and a dance appearing in both proves nothing.

**The library root is a black box.** Nothing about its shape may be assumed: not that the first folder is an artist, not that the deepest one is an album, not that a file name has fields in it, not that there are folders at all. A real library puts the dance in brackets (`10. Hep Harz (Cercle)`), after a trailing dash (`11-La Violette - valse 5tps`), or nowhere at all, and the `Dance - Artist - Title` split this code used to apply produced a dance column of track numbers and band names.

How each field is then decided:

- **The dance is decided by agreement**, because it is the one field with a real vocabulary behind it. Two independent kinds agreeing wins; one kind alone still answers when nothing contradicts it; two dances with nothing to separate them answer **nothing**, because inventing a confident answer is the failure the feature exists to prevent.
- **Artist and title are decided in order**, because nothing can check them. An album artist and a performer disagreeing is ordinary rather than a contest, so within the winning tier the first usable claim answers, and the order the collector emits them in *is* the trust order. See Claims below.
- **Brackets break a dance tie**, and nothing else does. `ClaimSource.IsDeliberate` says somebody wrote this as a statement about the track; a dance-shaped word in a sentence is an accident of language. Where the brackets say nothing either, the answer is nothing.
- **Folder agreement fills gaps only.** A `Folder` claim is dropped the moment any other kind resolves, so a folder of mazurkas with one scottish in it keeps the scottish, and it never corroborates: it is computed from sibling file names, so counting it as a second source is counting one source twice. The folder is a grouping and no more: `TrackEvidence.FolderKey` claims nothing about it being an album.
- **Matching is on whole words** (`DanceNameScanner`), longest name first: "Bourrée 3 temps" beats the "Bourrée" inside it, and "Andro" must not match inside "Androgyne".
- **Names are compared on a match key, not on their spelling** (`DanceWords`). The published file carries two word lists: a number word becomes its digit and glue is dropped, so `Bourrée à trois temps`, `Bourrée in 3`, `Bourrée 3t` and `Bourrée 3` are one key and one dance. The same pass runs over a file name before scanning it, which is what lets a library written in French, Dutch or German match at all. Measured on the reference library: 921 file names carried a recognisable dance without it, 972 with, and 9 answers changed: 8 of them corrections, `Valse 8 temps` having been filed as a 3-time waltz.
- **Glue is stepped over rather than ending a match**, so `valse à 3 temps` finds `valse 3`. The cost is that glue no longer separates: `Valse de la mazurka` reads as `valse mazurka`. That is the same trade the list makes on its own names, and a word that is not glue still ends a match, so `Bourrée du Berry 3 temps` is not `Bourrée 3 temps`.
- **Genre is not evidence.** Measured on the reference library, of 400 resolved tracks only 69 carry a genre at all and the whole set of values is `Music`, `Folk`, `Balfolk`, `Breton`; across ~530 files a genre supplied a dance name once. It is not read at all.
- **Undeclared, the artist comes from tags and the title from the title tag or the whole file name.** Neither is taken from a path segment or a file name field unless the user declared what that level or field means, because that is exactly what an unconfigured library cannot say. `ArtistNames` blocks ripper placeholders (`Unknown Artist`, `Various Artists`, digits-only): dances are a closed set the dance list defines and get a whitelist, artists are open and get a blocklist.

**Claims.** `Services/Discovery/Claims/` holds one `IClaimDiscovery` per thing that can speak about a file, and `TrackClaims.Collect` asks them in this order:

1. `PatternClaimDiscovery`: the first declared file name pattern that matches the whole name. `Declared`.
2. `FolderClaimDiscovery`: the folder levels the user gave a role, where the file is deep enough to have them. `Declared`.
3. `DanceClaimDiscovery`: a declared dance tag or custom tag, read whole (`Declared`); names from the list found in the title, album and comment tags and in the file name, bracketed or not, and the dance the rest of the folder agreed on (`Observed`).
4. `TagClaimDiscovery`: the tag fields for artist and title, in the declared order (`Declared`) or the built-in one (`Observed`).
5. `FilenameTitleDiscovery`: the whole file name as a title, with a leading track number taken off. `Observed`.

The tier decides first and the order only inside it: a declared pattern beats a declared folder role, and both beat a declared tag order, while any declaration beats every observed claim wherever it was emitted.

The claims themselves are not stored. The index keeps where each field's answer came from and why (`dance_kind`, `dance_detail`, `dance_reason` and the same for artist and title), which is what review shows next to a value.

### Declared settings: the informed greenlight

`DiscoverySettings` is what the user has stated about their library: ordered file name patterns, a role per folder level, and which tag fields speak for which field. Empty by default, and that default is the honest one. `DeclaredDiscovery.Compile` turns it into the compiled form a scan runs with, and everything it yields is claimed at `ClaimTrust.Declared`.

- **Each mechanism is switched on separately, and starts off.** Most libraries are read by one of them, and four sections of settings for three that do not apply is the most overwhelming part of the setup. `InForce()` is the same settings with the switched-off ones taken out, and it is what `DeclaredDiscovery.Compile`, `Calibration` and every preview are given: what a switched-off section holds is kept so it can be switched back on, and until then it does nothing. The wizard step will not be passed until one of the four is ticked.

- **A declaration is a bulk approval.** Code can measure that strings agree; only a person can say a rule is right. Once they have, the code stops hedging and powers through: which is the only way 2685 files get answered in an evening rather than never.
- **So the greenlight has to be informed**, and `DeclarationPreview` is what makes it one: how many files a pattern takes, what it makes of a sample of them, and which ones it leaves. The screen measures a draft against the **leftovers** rather than the whole library, because that is the pile the next rule is actually aimed at.
- **A pattern is refused rather than half-understood** (`PatternProblem`): two fields with no literal between them, the same field twice, a token that is not a token. A rule that quietly means something other than what it looks like is the opposite of the bargain.
- **Each field stops at the next literal, and the last one takes the rest**, which is the only reading that makes `%a - %t` mean what a person expects of `Bal O'Gadjo - Le badaud - Live`.
- **The default tag order is a guess and is claimed as one.** `TagTrust` holds null per field for "the built-in default applies", which stays `Observed`; a list the user filled in is a declaration and is claimed at the top tier. An empty list is a real declaration too: "nothing in the tags speaks for this".
- **Trusting a tag field is not the same as finding a name in it.** A declared field is read whole and is the dance even when the list has never heard of it, which is what parks the track. Scanning any tag for a name from the list needs no declaration: the vocabulary recognising itself is not a guess about what a field means.
- **Changing the rules re-reads the library** (`TrackStore.ApplyAsync`), skipping the size-and-mtime shortcut. What the index holds was derived under the rules that just changed, so it cannot answer instead. The comparison is against the rules the index records it was last read under (`read_under`, written only after a load that reached every file), not against what the store started with, which is nothing: a start whose rules match what the index was read under takes the shortcut. The rules are compared as `InForce()`, so a pattern under a switch that is off is no reason to read anything again. Revoking the rule approvals clears the record too, so a re-read that is interrupted is done again on the next start.
- **The library settings reach the track store only once the dance list has loaded.** A file read before the list arrives is resolved against an empty vocabulary, and nothing reads it again until it changes.

On a 2685-file library with BigBalfolkList imported and nothing else configured, this answers the dance for something under half of it, a few hundred of those by folder agreement. Everything else answers with nothing, which is a real answer and the reason the review gate exists: the way that number goes up is a user declaring how their library is arranged, not this code guessing harder.

### Library index

`Stores/Library/` is the index of what is in the music directory, in SQLite (`library.sqlite`). It replaced a JSON duration cache, and its job is that **a startup which finds nothing changed opens no audio files at all**: verified on a 345-track library: first run 345 files read, second run 0.

- **`Microsoft.Data.Sqlite` appears in `SqliteLibraryIndex` and `QueueHistoryStore` and nowhere else.** Extracting a `.Data` project later should be a file move, not an untangling.
- `id INTEGER PRIMARY KEY` is an alias for the rowid, so there is no second index to maintain. **`content_hash BLOB UNIQUE` is the natural key** and what an upsert conflicts on, so a renamed or retagged file keeps its row along with everything the user decided about it.
- The hash is over **the audio only** (`AudioContentHasher`), so a file retagged by another program keeps its row. TagLib's invariant start/end positions say where the audio is, except for FLAC and AIFF, where they cover the metadata blocks and the ID3 chunk: there the hasher walks the file itself, to the first frame after the last metadata block and to the samples in the SSND chunk. For Ogg, TagLib's start can land inside the header pages a tagger lays out again on every save, so the hasher counts the Vorbis or Opus header packets to the first audio page, and leaves out each page header's sequence number and checksum, because a comment that grows onto another page renumbers every audio page behind it. It samples rather than reading everything: a slice from each end of the audio and its length. Nothing in the application writes tags into a file: a correction from the editor is stored as an approval in the library index (`TrackEditorService.ApplyAsync`), never written back to the tag.
- The **fast path is path + size + last-write-time**, held in a snapshot read once per scan. Hashing would be a better check and is what the row is keyed by, but it means opening the file, which is the cost the index exists to avoid.
- The index stores **the slug, not a name**, plus `original_dance` for the review screen to group identical unknown values by. The review count itself is the gate's: the track store publishes how many indexed tracks were held out of the library, so all three hold-back reasons count.
- **The file carries the shape it was laid out in, in `PRAGMA user_version`.** `SchemaVersion` in `SqliteLibraryIndex` goes up with any change to the schema, and an index stamped with anything else is deleted and laid out again rather than opened: an older stamp and a newer one alike, because going back a build has to work the first time. Only the `tracks` table is thrown away, because only it was worked out from the files and the scan that follows works it out again. **The approvals, the ignored values and the whole of `track_paths` are read out before the file goes and written back after**, all three or none of them. The paths have to come across or the rebuild is itself a way of losing a library: the missing-folder question is asked about the folders the indexed paths name, so an emptied `track_paths` asks nobody anything on a start with the drive unplugged, and the reconciliation that closes the scan then deletes every carried approval for pointing at no track. Every enum the index stores a member of has pinned numeric values, and a test holds them there: a member inserted in front of one would turn every answer into a different one, and the stamp cannot help, because the rebuild reads the old file back out by exactly those numbers.
- **`track_paths.available` is per path, and a scan never sets it on its own.** A scan cannot tell a drive that has not mounted from a folder emptied on purpose, so when the index holds tracks in a folder and the walk found no music in it, `TrackStore` asks through `IMissingFolderPrompt` before it writes anything, and only an answer marks rows unavailable. Unavailable rows are excluded from the published library, the review queue, folder agreement and the discovery statistics, and any write or scan that finds the file again clears the flag. Per path rather than per audio because one recording can sit on a local disk and on a NAS at once.

### Nights

`Stores/History/` is the evenings, in SQLite (`history.sqlite`), in its own file: the library index is derived and a scan puts it back, and a history is the only copy there is of an evening.

- **A night is a row and every entry is a row appended to it.** The JSON file this replaced was rewritten in full after every entry, truncated first and then serialised, so a machine that stopped inside that window left a partial file and the evening read as though it had never happened.
- **A night exists once something has happened in it.** `StartedAt` is set by the first entry, so nobody presses anything to begin one, and closing the application mid-evening does not begin a second.
- **`ended_at` is what makes it a finished night.** `EndNightAsync` sets it and publishes an empty night; nothing is deleted, which is why `QueueConsumptionService` can call it on its own the moment an `EndOfNightHistoryEntry` lands. `DeleteNightAsync` is the destructive one, and only a person calls it, on whichever night they are looking at.
- **A filed night is still reachable.** `ListNightsAsync` is summaries rather than nights, because a list of evenings is chosen from and only one of them is read; `ReadNightAsync` reads that one, and export and delete take an id. The screen used to be able to reach only the night that was running, so the account of an evening left the screen the moment it ended and the file grew for the life of the application with nothing anybody could do about it.
- **An entry records its finish as well as its start.** `RecordCurrentItemAsync` runs the moment an item stops being the current one, so what a room heard is the time between the two: a track's own length says how long it is, not how long it was played for.
- **Entries keep their polymorphic JSON as a `payload` column** rather than being flattened. `kind` and `started_at` are lifted back out of it for a person opening the file, so what an evening was made of can be read without parsing JSON; nothing in the application queries either, and a night is counted by its rows. The payload is read with the same lenient enum converter as the settings, so a `CompletionStatus` a later build adds costs an older build that one entry's status rather than the whole night.
- **Deleting a night is one transaction.** Its entries and its row go together or not at all, so a failure between the two cannot leave a night listed with nothing in it.
- **The file carries the shape it was laid out in, in `PRAGMA user_version`**, as the library index does. `SchemaVersion` in `QueueHistoryStore` goes up with any change to the schema, together with whatever carries the older nights across. A file stamped zero was written before the stamp existed and in exactly the current shape, so it is stamped and opened as it stands. **A file stamped with anything else is refused and left byte for byte as it was found**, newer or older: it is reported the way an unreadable database is, and nothing is written into it.
- An unreadable database is **logged and left alone**, unlike the library index, which deletes and rebuilds itself. `ApplicationStartup` asks once at startup about a night that was never ended and has been quiet for more than eight hours: a gap rather than a date, because a ball crossing midnight is normal. Starting fresh passes `LastActivityAt` to `EndNightAsync`, so the night is filed at the finish of its last entry rather than at the moment somebody answered, which can be days later.

### Services

Services hold **runtime state** and the logic that is not persistence: the queue, playback, random selection, track discovery, what the presentation shows. They consume stores and expose observables.

| Service | Responsibility |
|---------|---------------|
| `QueueService` | In-memory queue backed by `SourceList<IQueueItem>`. Delegates all validation to a `QueueGuard` (see below). Changes name the row rather than its position: `Move` and `Remove` take a `QueueItemId`, because every dance that ends renumbers the queue and a caller's position is always a snapshot. A row that has gone comes back as `QueueChangeResult.Gone`, which is the queue having moved on rather than the queue saying no. |
| `QueueConsumptionService` | Dequeues items, drives playback, tracks elapsed time, records history, and holds the gap between two dances. `GapQueueItem` is the gap while it runs: it is the *current* item and never a queued one, so every surface draws it the way it draws a delay, and nothing has to filter it out of the queue or the remote. Recording skips it, and no time is lost by that: entries carry a start and a finish, so the gap is the space between two rows. `TrackGaps` is the same rule for anything projecting when the evening ends. An advance that starts on its own arrives from somewhere else, a track ending on the audio library's callback thread or a countdown on a timer thread, so both go through the constructor's advance scheduler, which the application supplies as the UI thread. |
| `ManagedBassAudioPlaybackService` | `IAudioPlaybackService` over BASS: select, play, pause, restart, seek, preload the next file, and the equalizer chain. No volume: that is the mixing desk's. BASS is initialised in the constructor, and a library that will not load is reported through `AudioAvailability` rather than thrown, so the window still comes up. |
| `PreviewPlaybackService` | Plays one file so a person can hear what it is, for review. Refused outright while the queue is playing: there is one output and the room is on it. |
| `PresentationStateService` | Reduces the queue and the player to the `PresentationState` every presentation surface draws, once, so the desktop window and the browser cannot disagree. |
| `EndOfNightAudio` | The file the user nominated as the end of the night, checked before it is offered, so the button is disabled when the path stops resolving rather than failing in front of a room. |
| `NightReport`, `NightSpreadsheet` | One night written out for a rights organisation: an HTML document a person reads, and a CSV for an upload. The same three columns either way. Static. |
| `RandomTrackService` | Random selection over the dances a `RandomSelectionScope` reaches: a `Pool` of tags (empty means every dance) or one `SingleDance`. Every dance in the pool is equally likely and a dance's tracks share its share, so forty recordings of one waltz do not drown out the rest. Deduplicates against queue + history + currently playing, and groups tracks by slug so an unresolved track never takes part. |
| `DancePool` | The tags a pick draws from, held in memory and read by the dance panel, the auto-queue and the phone remote alike. Not persisted: it is a decision about tonight. |
| `TrackDiscoveryService` | Opens a file once and reports what it says about itself (`TrackEvidence`): filename, path segments, tags, duration, format, content hash. It decides nothing. |
| `AudioContentHasher` | SHA-256 over a slice from each end of the audio, tags excluded, plus its length, so a file retagged elsewhere keeps its row in the library index. Static. |
| `DanceListReader` | The one door the list comes through, from all three sources: the cached copy on disk, a fetch, and a file the user picked. Nothing is shipped with the build, so a machine nobody has fetched or imported on has no vocabulary and says so. Refuses anything that is not format version 4, is empty, or breaks validation. Static, because it is a pure function of the bytes. |
| `DanceListFeed` | Downloads the raw `dances.json` from the BigBalfolkList repository. Caching off: the reason to press update is that something was merged a minute ago. |
| `DanceListValidation` | Checks the invariants everything else rests on: a name belongs to exactly one dance, slugs are unique, and every tag a dance carries is declared at the top of the file. Static. |

**To add a new service:**

1. Create `IXxxService` and `XxxService` in `Services/{Feature}/`.
2. Inject stores or other services via the constructor.
3. Register in `ConfigureServices` in `ApplicationComposition.cs`: `services.AddSingleton<IXxxService, XxxService>();`.

### Queue Guard

The `QueueService` does not contain any validation logic itself. Instead, it delegates all policy decisions (whether an item can be added, moved, removed, or cleared) to an `IQueueGuard`. The guard is composed of pluggable `IQueueRule` instances, making it easy to add or remove constraints without modifying the service.

**Components:**

- **`IQueueRule`**: interface that each rule implements. Every method but `GetEvictionIndices` returns a nullable value: `null` means "no opinion" (defer to other rules), a non-null value means "I have a verdict". `GetEvictionIndices` returns a list, empty when the rule evicts nothing. Methods:
  - `GetPreAddRemovalPredicate(newItem, currentItems)`: returns an optional predicate identifying items that should be removed *before* the new item is evaluated. `EndOfNightRule` uses it to take the auto-track out from under the entry that ends the evening.
  - `EvaluateAdd(item, adjustedItems)`: returns a `QueueRuleVerdict` to allow or deny adding the item. Receives the queue *after* pre-add removals have been applied.
  - `GetEvictionIndices(currentItems)`: returns indices of items that should be evicted when settings or history change.
  - `CanRemove(item)`, `CanMove(item)`, `CanClear(currentItems)`: allow or deny the corresponding operation.
- **`QueueRuleVerdict`**: `sealed record(bool Allowed, string? Reason, QueueDenial Denial)` returned by `EvaluateAdd`.
- **`QueueDenial`**: why an entry was turned away: `Entry` (something about this entry), `Cutoff` (the evening's end time), `EveningEnded` (the queue is closed). The reason a person reads is the rule's own wording; this is for callers that must tell one no from another, such as the auto-queue ending the night when the cutoff refuses its next track.
- **`QueueAddResult`**: `sealed record(bool Allowed, string? RejectionReason, Func<IQueueItem, bool>? RemovalPredicate, QueueDenial Denial)` returned by `IQueueGuard.EvaluateAdd`. Combines the pre-add removal predicate with the final allow/deny decision. Created via `QueueAddResult.Allow(predicate?)` or `QueueAddResult.Deny(reason, denial?)`.
- **`QueueGuard`**: the `IQueueGuard` implementation. Accepts an ordered list of `IQueueRule` instances. Orchestrates evaluation in two phases:
  1. **Pre-add removal**: collects removal predicates from all rules and combines them with OR logic.
  2. **Add evaluation**: runs `EvaluateAdd` on each rule against the adjusted item list. First deny wins.
  For `CanRemove`, `CanMove`, and `CanClear`, first definitive answer wins; if no rule has an opinion, the default is `true`. For `GetEvictionIndices`, results from all rules are merged into a deduplicated set, sorted in descending order for safe back-to-front removal.
- **`QueueGuardBuilder`**: static factory that constructs a `QueueGuard` from `ApplicationSettings`. It always includes `EndOfNightRule` first and `AutoTrackRule`, conditionally adds `DuplicateTrackRule` based on the `AllowDuplicateTracksInQueue` setting, conditionally adds `QueueCutoffRule` based on `QueueCutoffEnabled`, and always adds `MaxItemsRule` last.

**Existing rules:**

| Rule | Purpose |
|------|---------|
| `EndOfNightRule` | Closes the queue once an `EndOfNightQueueItem` is queued or playing: every add is refused with `QueueDenial.EveningEnded`, including another one of itself. Removes the auto-track as the entry goes in, and refuses to move the entry, since anything after it would outlive the evening it ended. Removing it reopens the queue. |
| `AutoTrackRule` | Denies adding a second `AutoTrackQueueItem` while one is already queued. Evicts every auto-track when the auto-queue setting is off. Prevents moving or removing an auto-track, and refuses a clear once auto-tracks are all that is left. Emits no removal predicate: the auto-track sits at the tail alongside real requests rather than being displaced by them (`QueueService` keeps it last). |
| `DuplicateTrackRule` | Denies adding a track that already exists in the queue, is currently playing, or was already played (finished in history). Evicts duplicates when history changes or the setting is toggled. |
| `QueueCutoffRule` | Denies adding once the projection: the current item's remainder, plus the queued durations, plus the new item, would run past the configured time of day plus its grace. The auto-track is judged like any request, since exempting the thing that refills the queue would leave the evening running past the cutoff on its own. Suspended while a halt (a stop, or a message with no duration) is queued, because past one there is no end time to judge against. |
| `MaxItemsRule` | Denies adding a track when the queue already holds `maxItems` tracks. Evicts tail tracks when the max is reduced. Only `TrackQueueItem` counts: the auto-track, the end-of-night item, gaps, delays, messages and stop markers are all exempt, so none of them count against the limit or are ever evicted by it. |

**Reactive rebuilding:** `QueueService` subscribes to `ISettingsStore.Observe()` and rebuilds the guard via `QueueGuardBuilder.FromSettings()` whenever settings change. After rebuilding, it runs eviction to enforce the new rules immediately. It also subscribes to `IQueueHistoryStore.Observe()` (skipping the initial value) to evict items that become duplicates after a track finishes playing.

**To add a new queue rule:**

1. Create a class implementing `IQueueRule` in `Services/Queue/`. Return `null` from any method where the rule has no opinion.
2. Add the rule to the list in `QueueGuardBuilder.FromSettings()`, respecting the ordering (rules are evaluated in list order; first deny wins for adds, first definitive answer wins for can-operations).
3. Add unit tests for the rule in isolation (see `AutoTrackRuleTests`, `DuplicateTrackRuleTests`, `MaxItemsRuleTests` for examples).
4. If the rule interacts with other rules during eviction, add a combined test in `QueueGuardTests`.

### Helpers

`UnawaitedWork` is how work nothing can await is started: it runs the work and, when it fails, writes the English log line and shows the screen text (see Logging). A bare discard leaves the exception on a task nobody observes, and an `async void` rethrows it on the UI thread, which closes the application in the middle of an evening. Where a call site already hands `SafeFireAndForget` a handler of its own, that handler is the report and `UnawaitedWork` is not added on top.

`TagLibFileAbstraction` hands TagLib an `IFileInfo` instead of a path, so a tag read goes through the same `IFileSystem` as every other read and a test on a `MockFileSystem` never reaches the disk. File access goes through `System.IO.Abstractions` everywhere, with two agreed exceptions: `SqliteLibraryIndex` deleting its own database files, which SQLite opens on the real disk anyway, and `ManagedBassAudioPlaybackService` looking for the BASS native library next to the executable.

`TrackTextTemplate` is how a track is written as a line on the screens that write one, in the placeholders the file name patterns use, read the other way round. A helper rather than a service because it is a pure function of a template and a track, and because a queue item's own description is written through it: four surfaces render it and the rule about an empty field, that it takes its separator with it, has to be the same on all of them. `DisplayTemplates` in the settings holds one per surface. The catalogue is deliberately not one of them: it is a table sorted per field.

`StringNormalizer.Normalize(string)`: decomposes Unicode (FormD), strips diacritics (non-spacing marks), keeps only letters/digits/spaces, lowercases, and collapses whitespace. Used throughout for case-insensitive, accent-insensitive name matching (resolving a name to a dance, uniqueness checks, search filtering).

---

## UI Layer (`Ready4Balfolk.UI/`)

### Startup & DI

`Program.cs` picks the windowing backend (and, under `--smoke-test`, the no-sound audio device) and
hands the builder to `ApplicationComposition.Configure`, which is everything the application is apart
from the platform it is drawn on: a scenario run hands the same builder to a headless backend instead,
so what is registered is one description rather than two kept in step by hand.

`ApplicationComposition.Configure`:

1. Builds the Avalonia app with `UseReactiveUIWithMicrosoftDependencyResolver(services => ConfigureServices(services, options), withResolver: sp => App.UseServices(sp!), withReactiveUIBuilder: ...)`: the resolver bridges Microsoft DI into Splat so ReactiveUI's `ViewLocator` can resolve views, and the builder installs a `WithExceptionHandler` observer in place of the `RxApp.DefaultExceptionHandler` assignment ReactiveUI 23 removed.
2. `ConfigureServices(IServiceCollection, ApplicationOptions)` registers all stores, services, and ViewModels (mostly singletons).
3. `AfterSetup` sets the UI culture from the settings store, wires `FileLogSinkService` as Avalonia's log sink, and installs three more global exception handlers:
   - `AppDomain.CurrentDomain.UnhandledException` → log critical.
   - `TaskScheduler.UnobservedTaskException` → log error, mark observed.
   - `Dispatcher.UIThread.UnhandledException` → log error, mark handled so the window stays up instead of Avalonia tearing the loop down.

That is four exception handlers in total, none of them in `Program.cs` itself.

**`App.Services`** is a static `IServiceProvider` property on `App`, set once from `App.UseServices` and never afterwards. Code-behind uses it to resolve services: `App.Services.GetRequiredService<NavigationService>()`.

**To register a new service or ViewModel:** add a line in `ConfigureServices` in `ApplicationComposition.cs`. Use `AddSingleton` for shared state, `AddTransient` for per-resolution instances.

### Setup wizard

`Views/Wizard/` holds a first-run wizard shown when `ApplicationSettings.SetupCompleted` is false (and never during a smoke test, which has nobody to answer it). It is also reachable from Settings -> Library -> *Run setup again*. It is a screen inside the main window (`Screen.Setup`), not a window of its own.

- A step is a `WizardStepViewModel`: `Title`, `Explanation`, an optional `CanContinue` observable, and `EnterAsync`/`CommitAsync`. `SetupWizardViewModel` owns the ordered list, and its continue command follows `CurrentStep.CanContinue` through `.Switch()` so a step's opinion stops counting the moment it is left.
- Steps are registered `AddTransient`, so a second run starts from what is on disk rather than from the last visit. A step that wraps a singleton screen has to say so to that screen as well: `DiscoveryStepViewModel` tells the `DiscoveryViewModel` it shares with the rules panel to fill its switches from disk again, or a second run would show, and commit, whatever was left there unsaved. Each needs an `IViewFor<T>` registration for `ViewModelViewHost` to resolve it.
- Order: explain, fetch the dance list, point at the music, say how the library is arranged, then answer what could not be placed. The dance list comes first because it is the vocabulary everything else is said in, and its step has nothing to answer: it fetches the published list, or imports one from a file, and shows what arrived. It blocks until a list has arrived, because nothing ships with the build and a machine with no list can answer nothing at all; a hall with no wifi imports a `dances.json` from a stick instead. "How your library is arranged" cannot be skipped either: it will not be passed until one of the four discovery mechanisms is ticked, and declaring the shape before answering files one at a time is what makes the last step a pile of leftovers rather than the whole library.

### Views & ViewModels

Every feature lives in `Views/{Feature}/` containing:

- `{Feature}View.axaml`: XAML with `x:DataType="{Feature}ViewModel"`.
- `{Feature}View.axaml.cs`: code-behind extending `ReactiveUserControl<{Feature}ViewModel>`.
- `{Feature}ViewModel.cs`: ViewModel extending `ReactiveObject`.

Namespace: `Ready4Balfolk.UI.Views.{Feature}`.

Some features also include sub-item ViewModels (e.g. `TrackViewModel`, `DanceCardViewModel`, `TagChipViewModel`, `HistoryItemViewModel`) and converters. Converters used by more than one feature live in `Converters/`.

Three kinds of view do not follow the trio:

- **`NotificationOverlayView`** is a plain `UserControl` whose `x:DataType` is the `NotificationService` itself: the bars are the service's list, and a view model in between would only copy it.
- **`PresentationWindow`** is a plain `Window` over `PresentationDisplayViewModel`, one per display, opened and closed by `ApplicationStartup` rather than navigated to.
- **The dialogs** in `Views/Dialogs/` are `ReactiveWindow<T>`s, built and shown by the service that asks the question (`ConfirmationService`, `TrackEditorService`, `MissingFolderPromptService`, `DialogService`) rather than resolved from the container or from a view's code-behind. Every one of them is owned by the window `DialogOwner` holds.

**MainWindow** is the shell. Its `MainWindowViewModel` is handed the always-visible view models (toolbar, playback, equalizer, queue, catalogue) directly, and everything else as a `Lazy<T>`, or a `Func<T>` for the wizard, built on first navigation into a nullable `[Reactive]` property. Each screen is a `DockPanel` whose `IsVisible` follows `Navigation`, holding a `ViewModelViewHost` bound to that property, so the view is resolved through its `IViewFor<T>` registration once there is a view model to show. The `NotificationOverlayView` is always visible on top.

### Source Generators

`ReactiveUI.SourceGenerators` 3.2.0 provides three key attributes:

| Attribute | What it generates | Usage |
|-----------|------------------|-------|
| `[Reactive]` | Backing field + `RaiseAndSetIfChanged` in setter | `[Reactive] public partial string Name { get; set; }` |
| `[ObservableAsProperty]` | Readonly `_propHelper` field + `_prop` backing field | `[ObservableAsProperty] public partial string DisplayName { get; }` |
| `[ReactiveCommand]` | `ReactiveCommand` property wired to the decorated method | `[ReactiveCommand(CanExecute = nameof(CanDoIt))] private void DoIt() { }` |

**Gotchas:**

- The generated `_propHelper` field is `readonly`: it must be assigned **in the constructor**, not in a helper method called later.
- Use `.ToProperty(this, x => x.Prop)` (not `ToPropertyEx`) to get the helper, then assign to `_propHelper`.
- `CanExecute = nameof(Prop)` requires `Prop` to be an `IObservable<bool>` property or field.

### Compiled Bindings

Enabled globally via `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` in the `.csproj`. Every XAML file must set `x:DataType` to its ViewModel type.

**Fall back to `{ReflectionBinding}`** for **DataGrid columns** only: columns are not in the visual tree, so compiled bindings cannot resolve their DataContext. `TrackCatalogView` is the one place that needs it.

### Code-Behind

Code-behind is used only for UI mechanics that cannot be expressed declaratively. It **always delegates mutations to the ViewModel**: never modifies domain state directly.

Common cases:

| Pattern | Example |
|---------|---------|
| **Drag-drop reorder** | `QueueView.axaml.cs`: pointer tracking, `DragDrop.DoDragDropAsync`, drop indicator positioning. Calls `ViewModel.MoveItem()`. |
| **ContainerPrepared styling** | `QueueView.axaml.cs`: adds CSS class `"autoTrack"` to `ListBoxItem` containers for `AutoTrackQueueItem`. |
| **Focus management** | Various views: programmatic focus after inline edit starts. |
| **Navigation clicks** | `ToolbarView.axaml.cs`, `MainWindow.axaml.cs`: set `NavigationService.CurrentScreen`. |
| **Keys standing in for buttons** | `MainWindow.axaml.cs`, `QueueView.axaml.cs`: `CommandKeys.Press(command)`, which asks the command's `CanExecute` first, so a key is refused wherever its button is disabled. |

### Navigation

`NavigationService` holds a `[Reactive] Screen CurrentScreen` property and derived `[ObservableAsProperty]` booleans (`IsMainScreen`, `IsSettingsScreen`, `IsHelpScreen`, `IsReviewScreen`, `IsSetupScreen`). The main screen also has `IsHistoryMode` and `IsDanceListMode` toggles for switching between the Queue/History and TrackCatalog/DanceList panels.

```csharp
public enum Screen { Main, Settings, Help, Review, Setup }
```

**To add a new screen:**

1. Add a value to the `Screen` enum.
2. Add a derived `[ObservableAsProperty] public partial bool IsXxxScreen { get; }` and wire it in the constructor.
3. Create the view folder in `Views/{Feature}/` with the standard View + ViewModel.
4. In `ApplicationComposition.cs`, register the ViewModel, a `Lazy<XxxViewModel>` over it, and `IViewFor<XxxViewModel>` for its view.
5. On `MainWindowViewModel`, take the `Lazy<XxxViewModel>` and add a nullable `[Reactive] public partial XxxViewModel? Xxx { get; set; }`, filled the first time `CurrentScreen` becomes `Screen.Xxx`.
6. In `MainWindow.axaml`, add a `DockPanel` with `IsVisible="{Binding Navigation.IsXxxScreen}"` holding `<rxui:ViewModelViewHost ViewModel="{Binding Xxx}" />`.
7. Add a navigation button in the toolbar or appropriate location.

### UI Services

| Service | Purpose |
|---------|---------|
| `DialogOwner` | The one window every dialog and picker belongs to, set once in `ApplicationStartup.Run` and read by every service below that puts something up. |
| `ConfirmationService` | Shows a modal `ConfirmationDialogView`. Returns `Task<bool>`. |
| `DialogService` | The toolbars' dialogs: asking for a message to queue, and showing an address as a QR code. |
| `MissingFolderPromptService` | Implements the Domain's `IMissingFolderPrompt`: shows `MissingFoldersDialogView` for a scan that found no music where the index says there is some. Marshals onto the UI thread, since a scan does not run on it, and takes its owner window from `DialogOwner`. Keeping the tracks is what an unanswered question means. |
| `NotificationService` | Implements the Domain's `INotificationService`: the bars along the bottom of the window, a DynamicData `SourceList<NotificationItem>` bound to `NotificationOverlayView`. Asked from any thread and moves onto the UI thread itself. Auto-dismisses after 4 seconds, counted from when the window opens for anything said before it did. An error already on screen is not shown again beside itself. `NotificationSeverity` is `Information`, `Warning` or `Error`. |
| `FileLogSinkService` | Implements Avalonia's `ILogSink` to bridge framework logs into the Domain `ILoggerService`. Wired in `ApplicationComposition.cs` via `AfterSetup`. |

### Converters

Value converters follow a static instance pattern for use with `{x:Static}` in XAML:

```csharp
public sealed class DurationFormatConverter : IValueConverter
{
    public static readonly DurationFormatConverter Instance = new();
    // Convert / ConvertBack ...
}
```

```xml
Text="{Binding Length, Converter={x:Static local:DurationFormatConverter.Instance}}"
```

Existing converters: `BoolToStringConverter` and `WeightConverter` in `Converters/`; `DurationFormatConverter` and the multi-value `TrackTextConverter` in `Views/Queue/`; `SeverityToBrushConverter` in `Views/Notifications/`; `FolderRoleDisplayConverter` in `Views/Discovery/`; `ReviewStateBrushConverter` and `PickerZIndex` in `Views/Review/`; `LanguageDisplayConverter`, `MinutesOfDayConverter` and `ThemeDisplayConverter` in `Views/Settings/`; `AudioFormatToBrushConverter` and `AudioFormatToIconConverter` in `Views/TrackCatalog/`.

`BoolToStringConverter` is the exception: it carries its two labels as properties, so each use declares its own instance in XAML resources.

**To add a new converter:** create a class implementing `IValueConverter` with `public static readonly XxxConverter Instance = new();`. Place it in the feature folder where it is used.

### Presentation Windows

`ApplicationStartup` manages 0-10 presentation windows (for external displays). The count is driven by `ApplicationSettings.PresentationDisplayCount`. Each window's position, size, maximised, and borderless state is saved on exit and restored on startup. The `SyncPresentationWindows` method closes excess windows and opens new ones as the setting changes.

---

## Web Layer (`Ready4Balfolk.Web/`)

A **class library, not a web application**. The Avalonia app is the host and starts it on demand;
`FrameworkReference Microsoft.AspNetCore.App` is how a non-web project gets ASP.NET Core. Nothing in
the desktop app depends on the server running, and switching it off costs the app nothing.

It serves two pages and what they need: every file in `wwwroot/`, `/api/config`,
`/api/remote/login` and the two hubs. The files are embedded rather than copied next to the
executable (`GenerateEmbeddedFilesManifest`, because flatpak-builder stages a single published
directory and a self-contained publish should not depend on loose files surviving it):

| Page | Who opens it | Hub |
|------|--------------|-----|
| `display.html` | a browser on the projector machine, as an alternative to a presentation window | `DisplayHub`, read-only |
| `remote.html` | the DJ's phone | `RemoteHub`, can change things |

### It does not build its own services

`WebApplication.CreateSlimBuilder` builds its own `IServiceProvider`, so
`HostServiceForwarding.AddForwardedHostServices` registers the running app's singletons **as
instances**. Never replace one of those with `AddSingleton<TService, TImplementation>`: that
constructs a second queue and a second audio engine inside the web host, and the browser then
faithfully renders a queue that nothing is playing. It fails silently, which is why the forwarding
has its own test.

Everything `RemoteHub` can reach touches the queue or the audio engine, both driven from the UI
thread, so every hub method goes through `IRemoteCommandDispatcher` rather than running on the
threadpool thread SignalR handed it.

### The remote is guarded, the display is not

`RemoteAccessService` exchanges a PIN for a token once, and `RemoteHub.OnConnectedAsync` checks the
token. Checking only on the page that serves the form would leave the hub open, since anyone on the
network can open a socket directly without ever loading the page.

- PINs are six digits from `RandomNumberGenerator`, compared with `CryptographicOperations.FixedTimeEquals`.
- Five wrong attempts lock an address out for a minute; the lockout is per address.
- Tokens expire, slid forward on use, and changing the PIN or switching the remote off drops every issued token.

`PresentationWebServer.ApplyAsync` brings the listener into line with the settings, so switching the
server on or moving its port never needs a restart. There is no bind-to-loopback option: enabling the
server binds `IPAddress.Any`, so it is reachable on every interface the machine has from the moment
it is switched on.

---

## Cross-Cutting Concerns

### Logging

`ILoggerService` is the Domain logging abstraction with `LogAsync`, `DebugAsync`, `InfoAsync`, `WarningAsync`, `ErrorAsync`, `CriticalAsync`, and `ExportAsync` methods, and `Report` from `LoggerServiceExtensions`.

| Implementation | Behaviour |
|----------------|-----------|
| `FileLoggerService` | Writes to `app.log` in the app-data directory. Moves it to `app.log.1` and starts over when it exceeds 512 KB. Uses `SemaphoreSlim` for thread-safe writes. Has a configurable `MinimumLevel`. Exporting writes both halves, oldest first, with the user profile directory written as `~`. |
| `NoOpLoggerService` | Does nothing. Used in tests, and by `SettingsStore` when it is built without a logger. |

**Format:** `2025-01-15 14:30:00.123 [INFO] message`

**Usage:** inject `ILoggerService`. A level method returns a `Task` already running on the thread pool, so a caller discards it (`_ = logger.InfoAsync("message")`) unless it must know the line is written, as a test does. A failure is reported with `Report`, which returns nothing.

**The log and the screen are separate, and nothing couples them.**

- What is logged is English, written as a literal at the call site. Never a `UiStrings` or `DomainStrings` value: a log that changes language with the application is no use to whoever reads it, and the exported log goes into public issues.
- What is shown on screen comes from the resx files, like every other text the DJ reads, and goes through `INotificationService` (declared in the Domain, under `Services/Notifications`, so a domain service can tell the DJ; implemented by the UI's `NotificationService`).
- Logging never puts anything on screen, whatever its level. There is no stream of logged errors for anything to subscribe to.

A failure the DJ has to hear about is therefore said twice, in two texts:

| Where | Call |
|-------|------|
| Anywhere with a logger and the notifications | `logger.Report("English log line", notifications, UiStrings.Xxx_Failed, exception)` |
| Work nothing can await | `new UnawaitedWork(logger, notifications).Start("English log line", DomainStrings.Xxx_Failed, work)` |
| A view's event handler | `Handlers.Run("English log line", UiStrings.Xxx_Failed, work)` |
| A view model's command | `command.ReportFailures(logger, "English log line", notifications, UiStrings.Xxx_Failed)` |

`logger.Report("English log line", exception)` without the screen text writes the log and nothing else, for a failure whose own remarks say why the DJ need not hear about it. The screen text says what did not happen, in the DJ's terms, not what threw; an exception's message is written by whoever threw it, in their language, and only ever goes to the log. `python3 scripts/check-translations.py` refuses a resx string passed as the log line of any of these calls, or of `DebugAsync`, `InfoAsync`, `WarningAsync`, `ErrorAsync`, `CriticalAsync` or `LogAsync`.

### Exception Handling

Four global handlers, all installed in `ApplicationComposition.cs`, catch unhandled exceptions and route them to the logger with an English line saying which net caught it:

1. `AppDomain.CurrentDomain.UnhandledException`: CLR-level (critical). Logged only: the process is ending, and there is no window left to show anything in.
2. `TaskScheduler.UnobservedTaskException`: unobserved async failures (error, marked observed).
3. `Dispatcher.UIThread.UnhandledException`: the last net under the UI thread (error, marked handled so the window stays up instead of Avalonia tearing the loop down).
4. A `WithExceptionHandler` observer, which replaces the `RxApp.DefaultExceptionHandler` assignment removed in ReactiveUI 23 (error).

The last three also tell the DJ, with the one generic `UiStrings.App_SomethingWentWrong`: what reaches them is something nobody wrote a sentence for. `Program.Main` logs a fatal startup exception and shows nothing, since there is no window.

Every other failure the DJ sees is reported where it happens, with its own text, as described under Logging.

### Continuous Integration

`verify.yml` runs on every push to `main` and every pull request targeting it. `verify-packages.yml` runs on a pull request that touches packaging, and builds and smoke tests the packages themselves. `release.yml` is triggered by hand with a version and chains everything else: verify → build binaries → package (Flatpak, Inno Setup) → smoke test the packages → publish the release. macOS is not a build target.

Before opening a pull request, run what `verify.yml` runs:

```bash
dotnet build Ready4Balfolk.sln -c Release
dotnet format Ready4Balfolk.sln --verify-no-changes
python3 scripts/check-translations.py
dotnet test --project Ready4Balfolk.Tests/Ready4Balfolk.Tests.csproj -c Release
dotnet test --project Ready4Balfolk.E2E/Ready4Balfolk.E2E.csproj -c Release
```

The same five, because Release is stricter than Debug and CI builds Release. `CONTRIBUTING.md` and the pull request template list the same set.

It is **four jobs that run beside each other**, because a pull request goes green when the slowest one finishes rather than when the longest list of steps does:

- `test`, on Ubuntu and Windows. The tests have to run somewhere they could fail differently: `Directory.Build.targets` resolves the BASS natives from the host OS, and the paths the stores write to are not the same shape on Windows.
- `style`: `dotnet format --verify-no-changes` and `scripts/check-translations.py`, which compares the `.resx` key sets and `{0}` placeholders in both directions, compares each designer file with its resx, fails on a key nothing in the application reads (the tests do not count), fails on a resx string handed to the logger as its log line, and holds the two tables in `wwwroot/strings.js` to the same rules. A missing Dutch key falls back to English at runtime, which reads as a bug nobody reported rather than a build that failed. One platform for both: `.gitattributes` normalises line endings, so neither can answer differently per platform.
- `scenarios`: the end to end suite. Its own job above all because it is the leg that grows every time a scenario is written, and beside the others it grows on its own rather than on top of them.
- `verify`, which needs the other three. A matrix reports one check per leg, so requiring those directly means editing the ruleset every time one is split, and a leg nobody remembered to add is a leg that cannot block a merge.

The branch ruleset requires three checks, not just `verify`: `check-icons` and `build` (the packaging job in `build-binaries.yml`) also gate the merge, and both run outside `verify.yml`. `check-icons.yml` hashes `Ready4Balfolk.UI/Assets/icon.svg` against a stored hash on every push and pull request targeting `main`; editing the icon without regenerating its derived assets fails the check even though none of the five commands above touch it. Regenerate with `bash scripts/generate-icons.sh` (or `pwsh scripts/generate-icons.ps1` on Windows) and commit the result before opening a pull request that changes the icon.

No coverage is collected. It was, as an artifact on every run, and nothing ever read it or gated on it: it was storage paid for a number nobody looked at, and stored artifacts are what put the account over its quota and turned upload steps into random 403s.

**Native debug symbols are dropped from the output** (`DropNativeDebugSymbols` in `Directory.Build.props`). SkiaSharp and HarfBuzz ship a `.pdb` beside every native library for every runtime they support, and MSBuild copies them: `libSkiaSharp.pdb` alone is 81 MB and arrives once per Windows runtime in each project's output. It made the four outputs of this solution 2.2 GB, nearly all of it copying rather than compiling, and none of it usable on the machine doing the copying. Only the natives are stripped; the symbols of the code in this repository are what a stack trace is read from.

Two build-level gates are worth knowing about. `TreatWarningsAsErrors` does not reach the Avalonia XAML compiler, so `AVLN5001` (the obsolete-member warning) is listed in `WarningsAsErrors` separately. And `verify`, `build-binaries` and `check-icons` declare a `concurrency` group so a superseded push is cancelled, except on `main`, where a commit left with no verdict is worse than a slow one. `verify-packages` always cancels, `release` never does, and the three workflows that are only ever called (`build-flatpak`, `build-inno-setup`, `smoke-test-packages`) have none of their own: they run inside their caller's.

**The smoke test.** CI packages every artifact but cannot tell a healthy one from a broken one by looking. `Directory.Build.targets` takes the architecture of the BASS, BASSFLAC and BASS_FX natives from the `RuntimeIdentifier`, but the operating system always from the *host*, so a publish for another OS lands the wrong ones and still succeeds. And a native that is present is not the same as one that loads. Either way the failure only shows up when a user double-clicks it.

So the app can start itself for inspection:

```bash
./Ready4Balfolk.UI --smoke-test
```

`SmokeTest.Run` starts the application for real, waits for the main window, then asks `IAudioPlaybackService` whether BASS came up. Killing the app after a timeout would not do: BASS is loaded as the main window's view models are built, and a library that will not load is reported rather than thrown, so a build with no BASS at all reaches a running window quite happily. It then checks BASS_FX (`IsEqualizerAvailable`), checks every extension the app offers is registered, **decodes a file in each format**, **starts the presentation server and fetches the display page and its assets from it**, scans everything this run appended to `app.log` for `[ERROR]` and `[CRITICAL]`, prints the log if anything failed, and exits: `0` passed, `1` a check failed, `2` startup hung.

The decode matters because registering a plugin is not the same as being able to read a file with it. v1.1.0 shipped Windows builds with BASSFLAC present and unloadable, so `.flac` was silently missing from the catalogue for every Windows user.

The presentation server is the other half a package can drop. The display page and its scripts are embedded in `Ready4Balfolk.Web` and served out of the assembly, so a package that loses them starts perfectly and serves nothing; and a Flatpak whose manifest lost `--share=network` gets a sandbox with no network of its own, where the listener still binds inside the sandbox but no address the hall could reach exists at all. The check asks for `display.js`, `app.css`, `strings.js`, `remote.js` and `lib/signalr.min.js` as well as the page, because the page has a route of its own and only the files travel through the static file middleware a browser depends on. It drives `PresentationWebServer.ApplyAsync` directly rather than flipping the setting, so a local run leaves the DJ's own switch alone, and it asks for a port of its own so a machine already serving its display page is not a failure.

`scripts/smoke-test-media/` holds the fixtures: the same 1.5 s chromatic scale, A4 up to G♯5, encoded as `.wav`, `.aiff`, `.flac`, `.mp3`, `.mp2` and `.ogg`. They are committed rather than generated, for the same reason the icons are: CI decodes them on every pull request, and generating them there would put ffmpeg on the critical path of every run, which `windows-latest` does not ship. Regenerate with `scripts/generate-smoke-test-media.sh` and commit the result; the output is deterministic, so an unchanged scale produces no diff.

`.mp1` and `.aif` have no fixture. Nothing has encoded MPEG audio layer 1 for decades, and `.aif` is byte for byte the same format as `.aiff`; both are covered by the registered-extensions check instead.

Two things exist only for this mode. `ApplicationStartup` skips its exit confirmation dialog, since nobody is there to answer one; and BASS initialises against its "no sound" device, so the library, its plugins and the effect chain come up exactly as they would against real hardware on a runner that has no sound card. That keeps the check measuring whether the natives shipped rather than whether the machine can make a noise.

Run it the way CI does with the wrappers, which set up a headless display and unpick some platform-specific traps:

```bash
scripts/smoke-test.sh x11     publish/Ready4Balfolk.UI
scripts/smoke-test.sh wayland publish/Ready4Balfolk.UI          # needs xvfb / cage
scripts/smoke-test.sh x11     flatpak run io.github.tjvl.Ready4Balfolk
```

```powershell
pwsh scripts/smoke-test.ps1 publish\Ready4Balfolk.UI.exe
```

Both display servers are worth running, because `UseWaylandWithFallback` picks the backend at startup and X11 and Wayland are two different paths through Avalonia.

The portable builds are checked inside `build-binaries.yml`, so every pull request runs them. `smoke-test-packages.yml` goes further and installs the Flatpak and the Windows installer, then launches what the installer put on disk. That is the level that catches a native library present in `publish/` but never copied into the bundle. It gates the `release` job, so nothing reaches the Releases page without having been started at least once.

`verify-packages.yml` runs that same chain on a pull request, so a manifest or a `setup.iss` that no longer produces a working package fails the change rather than the release. It is filtered to the paths packaging is built from: `packaging/`, the icons the bundle and the installer carry, the smoke test scripts and their fixtures, and the workflow files themselves. Everything else skips it, because building a Flatpak is the better part of a quarter of an hour and no ordinary change should wait on one. It builds the binaries itself rather than borrowing the run that `build-binaries.yml` starts for the same pull request: artifacts belong to the run that produced them. It has no aggregate gate job, and must not be required by the branch ruleset, since a required check on a path filtered workflow never reports on the pull requests that skip it.

### Reactive Patterns

- **Domain → UI:** stores expose `IObservable<T>` via `BehaviorSubject.AsObservable()` or DynamicData `SourceList.Connect()`. ViewModels subscribe in the constructor, marshal to the UI thread with `.ObserveOn(RxSchedulers.MainThreadScheduler)`, and collect subscriptions in a `CompositeDisposable` that is disposed when the ViewModel is disposed.
- **DynamicData collections:** `service.Connect()` → `.ObserveOn(RxSchedulers.MainThreadScheduler)` → `.Bind(out _items)` → `.Subscribe()`. The resulting `ReadOnlyObservableCollection<T>` is bound to the view's `ItemsSource`.
- **Derived properties:** `this.WhenAnyValue(x => x.Prop).Select(...)` piped to `.ToProperty(this, x => x.DerivedProp)` to produce `[ObservableAsProperty]` values.
- **Disposal:** all subscriptions are added to `CompositeDisposable` via `.DisposeWith(_disposables)`. ViewModels implement `IDisposable`.

### Thread Safety

| Mechanism | Where used |
|-----------|-----------|
| `SemaphoreSlim(1, 1)` | All stores: serialises file I/O. `FileLoggerService`: serialises log writes. |
| `ObserveOn(RxSchedulers.MainThreadScheduler)` | All ViewModel subscriptions that touch UI-bound properties or collections. |
| `ObserveOn(TaskPoolScheduler.Default)` | Work that must stay off the UI thread, such as `TrackStore` re-resolving every track when the dance list changes. |

---

## How To: Add a New Feature (Checklist)

1. **Model**: add sealed records in `Domain/Models/{Feature}/` if new data types are needed.
2. **Store** (if persistent state): create `IXxxStore` + `XxxStore` in `Domain/Stores/{Feature}/`, as described under Stores.
3. **Service** (if runtime logic): create `IXxxService` + `XxxService` in `Domain/Services/{Feature}/`.
4. **Register**: add store/service to `ConfigureServices` in `ApplicationComposition.cs`. Add the store's `LoadAsync` to the loads in `ApplicationStartup.Run` if it loads after the window opens.
5. **ViewModel**: create `{Feature}ViewModel : ReactiveObject` in `UI/Views/{Feature}/`. Use `[Reactive]`, `[ObservableAsProperty]`, `[ReactiveCommand]`. Subscribe to stores/services in the constructor, dispose in `Dispose()`.
6. **View**: create `{Feature}View.axaml` + `.axaml.cs` extending `ReactiveUserControl<{Feature}ViewModel>`. Set `x:DataType`. Use compiled bindings.
7. **Register ViewModel**: add to `ApplicationComposition.cs` as singleton. A top-level screen also needs a `Lazy<T>` and an `IViewFor<T>`; see To add a new screen.
8. **Navigation**: add to `Screen` enum, wire `IsXxxScreen`, add the `DockPanel` and its `ViewModelViewHost` in `MainWindow.axaml`, add toolbar button.
9. **Converters**: if needed, add with the static `Instance` pattern in the feature folder.
10. **Strings**: add the English text to `UiStrings.resx`, the Dutch to `UiStrings.nl.resx`, and the property to `UiStrings.Designer.cs`. The designer file is written by hand, not generated, so no IDE tool may regenerate it; `scripts/check-translations.py` fails when the three fall out of step. The same rule holds for `DomainStrings`, for the browser pages' `wwwroot/strings.js`, and for the two manuals, `help.md` and `help.nl.md`: nothing is written in one language only.

### Dutch glossary

One Dutch word per thing, in the app, the remote's `strings.js` and the Dutch manual alike. A new string uses these, and so do its compounds and inflections ("Onderbrekingsduur", "een getimede onderbreking").

| Thing | Dutch | Not |
|---|---|---|
| Holding playback (the transport button) | Pauze / pauzeren | |
| The queue item that holds the room for a while (English "Delay"), and the standard time between dances (English "delay between tracks") | Onderbreking | Pauze |
| Starting the playing track from the top (English "Restart") | Opnieuw | |
| Swapping an auto-picked track for another (English "Reroll") | Ander nummer | Opnieuw |
| Putting a stop, an onderbreking or a message in the queue | aanvragen (Stop aanvragen, Onderbreking aanvragen, Bericht aanvragen) | toevoegen |
| The review screen and what waits there | Nakijken ("wacht op je bij Nakijken") | review, reviewrij |
| The list of dances | dansenlijst | danslijst |

"Opnieuw" as an ordinary word in a sentence ("opnieuw ingelezen", "Opnieuw verbinden") is not the Restart label and is fine. `HelpManualTests` holds the Dutch manual to this table.
