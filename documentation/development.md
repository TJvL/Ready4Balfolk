# Development Guide

This document explains the layered architecture, naming conventions, and patterns used in Ready4Balfolk so that new contributors know where to add code and how to follow existing conventions.

## Overview

Ready4Balfolk is a four-project Avalonia desktop application for managing and playing back folk dance music queues.

| Project | Purpose | Key dependencies |
|---------|---------|-----------------|
| `Ready4Balfolk.Domain` | Models, stores, services. No UI dependencies | System.Reactive, DynamicData, ManagedBass, System.Text.Json, Microsoft.Data.Sqlite |
| `Ready4Balfolk.UI` | Views, ViewModels, converters, UI services. Hosts the other two | Avalonia 12, ReactiveUI, ReactiveUI.SourceGenerators |
| `Ready4Balfolk.Web` | The presentation display and the phone remote, served from inside the app | ASP.NET Core (via `FrameworkReference`), SignalR |
| `Ready4Balfolk.Tests` | Unit, integration and ViewModel tests | xunit.v3 on Microsoft.Testing.Platform, NSubstitute, System.IO.Abstractions.TestingHelpers |

**Key principles:**

- **Reactive-first**: state flows as `IObservable<T>` from Domain stores/services; the UI subscribes and reacts.
- **Immutable models**: all Domain models are sealed records; mutations produce new instances.
- **Interface-driven**: every store and service has an interface for testability and DI.
- **MVVM with compiled bindings**: Views bind to ViewModels; `x:DataType` is set on every view.

---

## Domain Layer (`Ready4Balfolk.Domain/`)

### Models

All models are **sealed records** with `[JsonPropertyName]` attributes for persistence. They are organised by subdirectory:

| Directory | Contents |
|-----------|----------|
| `Tracks/` | `Track`: file path, dance, artist, title, length. Carries `OriginalDance` for re-resolution. |
| `QueueItems/` | `IQueueItem` interface + six implementations: `TrackQueueItem`, `DelayQueueItem`, `MessageQueueItem`, `StopQueueItem`, `AutoTrackQueueItem`, `EndOfNightQueueItem`. The last is the file named in the settings and deliberately not a `TrackQueueItem`: it is not in the library and must never enter it. |
| `Dances/` | `DanceList` -> `Dance`, exactly as BigBalfolkList publishes it: a top-level `Tags` vocabulary and a flat list of `{slug, names, tags}`. A dance's identity is its `Slug`; its `Names` are a flat set of equals whose first entry is what gets displayed; everything else is a tag, so nothing is filed under one grouping at the expense of another. There is no hierarchy and no weight. `DanceListIndex` is the folded-name-to-slug lookup built over a list, `DanceListProblems` is what validation reports, and `DanceListStatus`/`DanceListUpdate` say where the list came from and what came of asking for a newer one. |
| `Settings/` | `ApplicationSettings`, `ApplicationTheme` enum, `WindowState`. |
| `History/` | `QueueHistoryEntry` (abstract, `[JsonPolymorphic]`) with `TrackHistoryEntry`, `MessageHistoryEntry`, `DelayHistoryEntry`, `StopHistoryEntry`, `EndOfNightHistoryEntry`, each carrying `StartedAt`, `FinishedAt` and a `CompletionStatus` of `Finished`, `Skipped` or `FileMissing`. `QueueHistory` is one night: `Id`, `StartedAt`, `EndedAt` and the entries; `NightSummary` is the little a list of nights shows. |

**To add a new model:** create a `sealed record` in the appropriate subdirectory. Add `[JsonPropertyName]` attributes if it will be serialised. If it is polymorphic, add `[JsonPolymorphic]` + `[JsonDerivedType]` on the base type.

### Stores

Stores manage **persisted state** with thread-safe, reactive updates. Every store follows the same pattern:

```
Interface:  IXxxStore  →  Current, Observe(), LoadAsync(), UpdateAsync(Func<T,T>)
Impl:       XxxStore   →  BehaviorSubject<T> + SemaphoreSlim + persistence
```

**Pattern elements:**

- **`BehaviorSubject<T>`**: holds the current value, replays last value to new subscribers, broadcasts changes.
- **`SemaphoreSlim(1, 1)`**: binary semaphore serialising access so concurrent calls cannot interleave a read with a write.
- **`UpdateAsync(Func<T, T>)`**: caller passes a pure transformation function; the store atomically applies it, updates the subject, and persists to disk.
- **Persistence**: the settings store is JSON (`JsonSerializer.SerializeAsync` with `WriteIndented = true`, so the file stays human-editable). The library index and the night history are SQLite; see those sections below. Stores accept a `DirectoryInfo` at construction (the app-data directory).

The `TrackStore` is different: it uses a DynamicData `SourceList<Track>` instead of `BehaviorSubject`, exposes `Connect()` for reactive collection binding, integrates a `FileSystemWatcher` for live directory monitoring, and uses `Task.WhenEach()` for streaming track discovery.

### Discovery: claims and corroboration

`Services/Discovery/` decides what a track is, in three steps that are deliberately separable: `TrackDiscoveryService.Gather` opens the file, `TrackClaims.Collect` asks everything that can speak about it what it says, and `TrackInformationResolver.Decide` answers each field from those claims plus the dance list. Only the first touches a disk, so the other two re-run whenever the list or the settings change and are tested without a file existing.

**Everything is a claim: a field, a value, a source, and a trust.** One currency for all of it, and the whole of `Claim`.

- **Claims are raw.** A dance claim carries the text somebody wrote, not a slug: turning text into a dance is the list's job, and a claim the list does not recognise is still a claim. That unrecognised value is exactly what parks a track in review and what 21 identical misspellings group by.
- **Nothing is discarded silently.** Losing claims, and values refused as ripper placeholders, stay on the resolution. A wrong source is only visible next to what it beat, and "the artist tag says Unknown Artist" is a different thing to look at than "there is no artist tag".
- **Three tiers, and the top one that spoke is the only one considered** (`ClaimTrust`): `Declared` (a discovery setting the user filled in), `Measured` (calibration over the library's own strings), `Observed` (this file's tags and name). A tier is not a vote to be weighed: a user who declares a rule has taken responsibility for it, so a declaration replaces the tags rather than arguing with them, and is not "corroborated" by a weaker source agreeing. Only `Observed` is produced today; the tiers above it arrive with declared settings and calibration.
- **`DecisionReason` keeps the several meanings of a blank apart**: `NoClaim`, `Unusable`, `Contested` are three different situations, and the review screen has to tell a person which one it is looking at.
- **Independence is per `ClaimSourceKind`**, not per claim: the title tag and the comment tag are one kind between them, because the same ripper wrote both in the same pass and a dance appearing in both proves nothing.

**The library root is a black box.** Nothing about its shape may be assumed: not that the first folder is an artist, not that the deepest one is an album, not that a file name has fields in it, not that there are folders at all. A real library puts the dance in brackets (`10. Hep Harz (Cercle)`), after a trailing dash (`11-La Violette - valse 5tps`), or nowhere at all, and the `Dance - Artist - Title` split this code used to apply produced a dance column of track numbers and band names.

How each field is then decided:

- **The dance is decided by agreement**, because it is the one field with a real vocabulary behind it. Two independent kinds agreeing wins; one kind alone still answers when nothing contradicts it; two dances with nothing to separate them answer **nothing**, because inventing a confident answer is the failure the feature exists to prevent.
- **Artist and title are decided in order**, because nothing can check them. An album artist and a performer disagreeing is ordinary rather than a contest, so the first usable claim answers and the order the collector emits them in *is* the trust order (album artist before artist, title tag before file name). Step 3 makes that order declarable.
- **Brackets break a dance tie**, and nothing else does. `ClaimSource.IsDeliberate` says somebody wrote this as a statement about the track; a dance-shaped word in a sentence is an accident of language. Where the brackets say nothing either, the answer is nothing.
- **Folder agreement fills gaps only.** A `Folder` claim is dropped the moment any other kind resolves, so a folder of mazurkas with one scottish in it keeps the scottish, and it never corroborates: it is computed from sibling file names, so counting it as a second source is counting one source twice. The folder is a grouping and no more: `TrackEvidence.FolderKey` claims nothing about it being an album.
- **Matching is on whole words** (`DanceNameScanner`), longest name first: "Bourrée 3 temps" beats the "Bourrée" inside it, and "Andro" must not match inside "Androgyne".
- **Names are compared on a match key, not on their spelling** (`DanceWords`). The published file carries two word lists: a number word becomes its digit and glue is dropped, so `Bourrée à trois temps`, `Bourrée in 3`, `Bourrée 3t` and `Bourrée 3` are one key and one dance. The same pass runs over a file name before scanning it, which is what lets a library written in French, Dutch or German match at all. Measured on the reference library: 921 file names carried a recognisable dance without it, 972 with, and 9 answers changed: 8 of them corrections, `Valse 8 temps` having been filed as a 3-time waltz.
- **Glue is stepped over rather than ending a match**, so `valse à 3 temps` finds `valse 3`. The cost is that glue no longer separates: `Valse de la mazurka` reads as `valse mazurka`. That is the same trade the list makes on its own names, and a word that is not glue still ends a match, so `Bourrée du Berry 3 temps` is not `Bourrée 3 temps`.
- **Genre is not evidence.** Measured on the reference library, of 400 resolved tracks only 69 carry a genre at all and the whole set of values is `Music`, `Folk`, `Balfolk`, `Breton`; across ~530 files a genre supplied a dance name once. It is not read at all.
- **The artist comes from tags, the title from the title tag or the whole file name.** Neither is taken from a path segment or a file name field, because which level or field means what is exactly what an unconfigured library cannot say. `ArtistNames` still blocks ripper placeholders (`Unknown Artist`, `Various Artists`, digits-only): dances are a closed set the dance list defines and get a whitelist, artists are open and get a blocklist.

Claims live only as long as the resolution today. Storing them so a review screen can show where a value came from after a restart is the schema work in step 4.

### Declared settings: the informed greenlight

`DiscoverySettings` is what the user has stated about their library: ordered file name patterns, a role per folder level, and which tag fields speak for which field. Empty by default, and that default is the honest one. `DeclaredDiscovery.Compile` turns it into the compiled form a scan runs with, and everything it yields is claimed at `ClaimTrust.Declared`.

- **Each mechanism is switched on separately, and starts off.** Most libraries are read by one of them, and four sections of settings for three that do not apply is the most overwhelming part of the setup. `InForce()` is the same settings with the switched-off ones taken out, and it is what `DeclaredDiscovery.Compile`, `Calibration` and every preview are given: what a switched-off section holds is kept so it can be switched back on, and until then it does nothing. The wizard step will not be passed until one of the four is ticked.

- **A declaration is a bulk approval.** Code can measure that strings agree; only a person can say a rule is right. Once they have, the code stops hedging and powers through: which is the only way 2685 files get answered in an evening rather than never.
- **So the greenlight has to be informed**, and `DeclarationPreview` is what makes it one: how many files a pattern takes, what it makes of a sample of them, and which ones it leaves. The screen measures a draft against the **leftovers** rather than the whole library, because that is the pile the next rule is actually aimed at.
- **A pattern is refused rather than half-understood** (`PatternProblem`): two fields with no literal between them, the same field twice, a token that is not a token. A rule that quietly means something other than what it looks like is the opposite of the bargain.
- **Each field stops at the next literal, and the last one takes the rest**, which is the only reading that makes `%a - %t` mean what a person expects of `Bal O'Gadjo - Le badaud - Live`.
- **The default tag order is a guess and is claimed as one.** `TagTrust` holds null per field for "the built-in default applies", which stays `Observed`; a list the user filled in is a declaration and is claimed at the top tier. An empty list is a real declaration too: "nothing in the tags speaks for this".
- **Trusting a tag field is not the same as finding a name in it.** A declared field is read whole and is the dance even when the list has never heard of it, which is what parks the track. Scanning any tag for a name from the list needs no declaration: the vocabulary recognising itself is not a guess about what a field means.
- **Changing the rules re-reads the library** (`TrackStore.DiscoverySettings`), skipping the size-and-mtime shortcut. What the index holds was derived under the rules that just changed, so it cannot answer instead. `App` sets the declarations *before* the music directory for the same reason: the other order scans everything twice.

On a 2685-file library with BigBalfolkList imported and nothing else configured, this answers the dance for something under half of it, a few hundred of those by folder agreement. Everything else answers with nothing, which is a real answer and the reason the review gate exists: the way that number goes up is a user declaring how their library is arranged, not this code guessing harder.

### Library index

`Stores/Library/` is the index of what is in the music directory, in SQLite (`library.sqlite`). It replaced a JSON duration cache, and its job is that **a startup which finds nothing changed opens no audio files at all**: verified on a 345-track library: first run 345 files read, second run 0.

- **`Microsoft.Data.Sqlite` appears in `SqliteLibraryIndex` and `QueueHistoryStore` and nowhere else.** Extracting a `.Data` project later should be a file move, not an untangling.
- `id INTEGER PRIMARY KEY` is an alias for the rowid, so there is no second index to maintain. **`content_hash BLOB UNIQUE` is the natural key** and what an upsert conflicts on, so a renamed or retagged file keeps its row along with everything the user decided about it.
- The hash is over **the audio stream only** (`AudioContentHasher`, using TagLib's invariant start/end positions). The application writes tags into files itself, and a whole-file hash would make every one of its own edits look like a new track.
- The **fast path is path + size + last-write-time**, held in a snapshot read once per scan. Hashing would be a better check and is what the row is keyed by, but it means opening the file, which is the cost the index exists to avoid.
- The index stores **the slug, not a name**, plus `original_dance` for the review screen to group identical unknown values by. The review count itself is the gate's: the track store publishes how many indexed tracks were held out of the library, so all three hold-back reasons count.

### Nights

`Stores/History/` is the evenings, in SQLite (`history.sqlite`), in its own file: the library index is derived and a scan puts it back, and a history is the only copy there is of an evening.

- **A night is a row and every entry is a row appended to it.** The JSON file this replaced was rewritten in full after every entry, truncated first and then serialised, so a machine that stopped inside that window left a partial file and the evening read as though it had never happened.
- **A night exists once something has happened in it.** `StartedAt` is set by the first entry, so nobody presses anything to begin one, and closing the application mid-evening does not begin a second.
- **`ended_at` is what makes it a finished night.** `EndNightAsync` sets it and publishes an empty night; nothing is deleted, which is why `QueueConsumptionService` can call it on its own the moment an `EndOfNightHistoryEntry` lands. `DeleteNightAsync` is the destructive one, and only a person calls it, on whichever night they are looking at.
- **A filed night is still reachable.** `ListNightsAsync` is summaries rather than nights, because a list of evenings is chosen from and only one of them is read; `ReadNightAsync` reads that one, and export and delete take an id. The screen used to be able to reach only the night that was running, so the account of an evening left the screen the moment it ended and the file grew for the life of the application with nothing anybody could do about it.
- **An entry records its finish as well as its start.** `RecordCurrentItemAsync` runs the moment an item stops being the current one, so what a room heard is the time between the two: a track's own length says how long it is, not how long it was played for.
- **Entries keep their polymorphic JSON as a `payload` column** rather than being flattened. `kind` is lifted back out of that payload so it cannot drift from it, and so counting what an evening was made of costs no parsing.
- An unreadable database is **logged and left alone**, unlike the library index, which deletes and rebuilds itself. `App` asks once at startup about a night that was never ended and has been quiet for more than eight hours: a gap rather than a date, because a ball crossing midnight is normal. Starting fresh passes `LastActivityAt` to `EndNightAsync`, so the night is filed at the finish of its last entry rather than at the moment somebody answered, which can be days later.

`TrackTextTemplate` is how a track is written on the screens that write one as a line, in the
placeholders the file name patterns already use, read the other way round: a pattern takes a name
apart, a template puts one together. It lives in the domain because four surfaces render it and the
rule about a field with nothing in it, that it takes its separator with it, has to be the same on
all of them. `DisplayTemplates` in the settings holds one per surface, defaulting to what each said
before it existed. The catalogue is deliberately not one of them: it is a table sorted per field.

`DanceListStore` owns `dance_list.json` and additionally exposes an `Index`. The index is rebuilt *before* the new list is published, so a subscriber reacting to a change never reads a lookup built from the list it just replaced.

**To add a new store:**

1. Create `IXxxStore` in `Stores/{Feature}/` with `Current`, `Observe()`, `LoadAsync()`, `UpdateAsync()`.
2. Create `XxxStore` implementing the interface. Use `BehaviorSubject<T>`, `SemaphoreSlim`, and JSON serialisation as in existing stores.
3. Register in `Program.cs` as a singleton: `services.AddSingleton<IXxxStore>(_ => new XxxStore(DataDirectory));`.
4. Call `LoadAsync()` in `App.axaml.cs` inside the `MainWindow.Opened` handler.

### Services

Services hold **ephemeral runtime state** and operational logic, queue management, playback orchestration, random selection, synonym resolution, track discovery. They consume stores and expose reactive observables.

| Service | Responsibility |
|---------|---------------|
| `QueueService` | In-memory queue backed by `SourceList<IQueueItem>`. Delegates all validation to a `QueueGuard` (see below). |
| `QueueConsumptionService` | Dequeues items, drives playback, tracks elapsed time, records history, and holds the gap between two dances. `GapQueueItem` is the gap while it runs: it is the *current* item and never a queued one, so every surface draws it the way it draws a delay, and nothing has to filter it out of the queue, the remote or the row indices a move works on. Recording skips it, and no time is lost by that: entries carry a start and a finish, so the gap is the space between two rows. `TrackGaps` is the same rule for anything projecting when the evening ends. |
| `AudioPlaybackService` | ManagedBass wrapper for audio playback (play, pause, seek, volume). |
| `RandomTrackService` | Random selection over the dances a `RandomSelectionScope` reaches: a `Pool` of tags (empty means every dance) or one `SingleDance`. Every dance in the pool is equally likely and a dance's tracks share its share, so forty recordings of one waltz do not drown out the rest. Deduplicates against queue + history + currently playing, and groups tracks by slug so an unresolved track never takes part. |
| `DancePool` | The tags a pick draws from, held in memory and read by the dance panel, the auto-queue and the phone remote alike. Not persisted: it is a decision about tonight. |
| `TrackDiscoveryService` | Opens a file once and reports what it says about itself (`TrackEvidence`): filename, path segments, tags, duration, format, content hash. It decides nothing. |
| `AudioContentHasher` | SHA-256 over the audio between the tags, so the application's own tag edits do not move a row in the library index. |
| `DanceListReader` | The one door the list comes through, from all three sources: the cached copy on disk, a fetch, and a file the user picked. Nothing is shipped with the build, so a machine nobody has fetched or imported on has no vocabulary and says so. Refuses anything that is not format version 4, is empty, or breaks validation. Static, because it is a pure function of the bytes. |
| `DanceListFeed` | Downloads the raw `dances.json` from the BigBalfolkList repository. Caching off: the reason to press update is that something was merged a minute ago. |
| `DanceListValidation` | Checks the invariants everything else rests on: a name belongs to exactly one dance, slugs are unique, and every tag a dance carries is declared at the top of the file. |

**To add a new service:**

1. Create `IXxxService` and `XxxService` in `Services/{Feature}/`.
2. Inject stores or other services via the constructor.
3. Register in `Program.cs`: `services.AddSingleton<IXxxService, XxxService>();`.

### Queue Guard

The `QueueService` does not contain any validation logic itself. Instead, it delegates all policy decisions (whether an item can be added, moved, removed, or cleared) to an `IQueueGuard`. The guard is composed of pluggable `IQueueRule` instances, making it easy to add or remove constraints without modifying the service.

**Components:**

- **`IQueueRule`**: interface that each rule implements. Every method returns a nullable value: `null` means "no opinion" (defer to other rules), a non-null value means "I have a verdict". Methods:
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
| `MaxItemsRule` | Denies adding when the queue is at capacity. Evicts tail items when the max is reduced. The auto-track is exempt: it is a placeholder for an empty slot, so it neither counts against the limit nor gets evicted by it. |

**Reactive rebuilding:** `QueueService` subscribes to `ISettingsStore.Observe()` and rebuilds the guard via `QueueGuardBuilder.FromSettings()` whenever settings change. After rebuilding, it runs eviction to enforce the new rules immediately. It also subscribes to `IQueueHistoryStore.Observe()` (skipping the initial value) to evict items that become duplicates after a track finishes playing.

**To add a new queue rule:**

1. Create a class implementing `IQueueRule` in `Services/Queue/`. Return `null` from any method where the rule has no opinion.
2. Add the rule to the list in `QueueGuardBuilder.FromSettings()`, respecting the ordering (rules are evaluated in list order; first deny wins for adds, first definitive answer wins for can-operations).
3. Add unit tests for the rule in isolation (see `AutoTrackRuleTests`, `DuplicateTrackRuleTests`, `MaxItemsRuleTests` for examples).
4. If the rule interacts with other rules during eviction, add a combined test in `QueueGuardTests`.

### Helpers

`StringNormalizer.Normalize(string)`: decomposes Unicode (FormD), strips diacritics (non-spacing marks), keeps only letters/digits/spaces, lowercases, and collapses whitespace. Used throughout for case-insensitive, accent-insensitive name matching (resolving a name to a dance, uniqueness checks, search filtering).

---

## UI Layer (`Ready4Balfolk.UI/`)

### Startup & DI

`Program.cs` is the entry point:

1. Creates the `FileLoggerService` singleton (writes to `~/.local/share/Ready4Balfolk/`).
2. Installs three global exception handlers:
   - `AppDomain.CurrentDomain.UnhandledException` → log critical.
   - `TaskScheduler.UnobservedTaskException` → log error, mark observed.
   - `RxApp.DefaultExceptionHandler` → log unhandled Rx exceptions.
3. Builds the Avalonia app with `UseReactiveUIWithMicrosoftDependencyResolver(ConfigureServices, withResolver: sp => App.Services = sp!)`: this bridges Microsoft DI into Splat so ReactiveUI's `ViewLocator` can resolve views.
4. `ConfigureServices(IServiceCollection)` registers all stores, services, and ViewModels (mostly singletons).
5. `AfterSetup` wires `FileLogSinkService` as Avalonia's log sink.

**`App.Services`** is a static `IServiceProvider` property on `App`. Code-behind uses it to resolve services: `App.Services.GetRequiredService<NavigationService>()`.

**To register a new service or ViewModel:** add a line in `ConfigureServices` in `Program.cs`. Use `AddSingleton` for shared state, `AddTransient` for per-resolution instances.

### Setup wizard

`Views/Wizard/` holds a first-run wizard shown when `ApplicationSettings.SetupCompleted` is false (and never during a smoke test, which has nobody to answer it). It is also reachable from Settings -> Troubleshooting -> *Run setup again*.

- A step is a `WizardStepViewModel`: `Title`, `Explanation`, an optional `CanContinue` observable, and `EnterAsync`/`CommitAsync`. `SetupWizardViewModel` owns the ordered list, and its continue command follows `CurrentStep.CanContinue` through `.Switch()` so a step's opinion stops counting the moment it is left.
- Steps are registered `AddTransient`, so a second run starts from what is on disk rather than from the last visit. Each needs an `IViewFor<T>` registration for `ViewModelViewHost` to resolve it.
- The wizard is modal over the main window, so it takes over confirmation ownership for as long as it is open (`ConfirmationService.UseOwner`). Without that, a confirmation raised from a step is parented to a window the user cannot reach: it closes immediately and reads as a button that does nothing.
- Order: explain, fetch the dance list, point at the music, then answer what could not be placed. The dance list comes first because it is the vocabulary everything else is said in, and its step asks nothing: it fetches the published list and shows what arrived. It never blocks either, because the copy shipped with the build is a perfectly good list and a hall with no wifi is an ordinary place to start in.

### Views & ViewModels

Every feature lives in `Views/{Feature}/` containing:

- `{Feature}View.axaml`: XAML with `x:DataType="{Feature}ViewModel"`.
- `{Feature}View.axaml.cs`: code-behind extending `ReactiveUserControl<{Feature}ViewModel>`.
- `{Feature}ViewModel.cs`: ViewModel extending `ReactiveObject`.

Namespace: `Ready4Balfolk.UI.Views.{Feature}`.

Some features also include sub-item ViewModels (e.g. `TrackViewModel`, `DanceCardViewModel`, `TagChipViewModel`, `HistoryItemViewModel`) and converters. Converters used by more than one feature live in `Converters/`.

**MainWindow** is the shell. Its `MainWindowViewModel` receives all sub-ViewModels via constructor injection. Navigation uses `IsVisible` bindings on `Panel` children, one panel per screen, all stacked. The `NotificationOverlayView` is always visible on top.

### Source Generators

`ReactiveUI.SourceGenerators` 2.6.1 provides three key attributes:

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

**Fall back to `{ReflectionBinding}`** in two cases:

- **DataGrid columns**: columns are not in the visual tree, so compiled bindings cannot resolve their DataContext.
- **TreeView `IsExpanded` style setters**: style bindings cannot use compiled bindings for two-way sync.

### Code-Behind

Code-behind is used only for UI mechanics that cannot be expressed declaratively. It **always delegates mutations to the ViewModel**: never modifies domain state directly.

Common cases:

| Pattern | Example |
|---------|---------|
| **Drag-drop reorder** | `QueueView.axaml.cs`: pointer tracking, `DragDrop.DoDragDropAsync`, drop indicator positioning. Calls `ViewModel.MoveItem()`. |
| **ContainerPrepared styling** | `QueueView.axaml.cs`: adds CSS class `"autoTrack"` to `ListBoxItem` containers for `AutoTrackQueueItem`. |
| **Focus management** | Various views: programmatic focus after inline edit starts. |
| **Navigation clicks** | `ToolbarView.axaml.cs`, `MainWindow.axaml.cs`: set `NavigationService.CurrentScreen`. |

### Navigation

`NavigationService` holds a `[Reactive] Screen CurrentScreen` property and derived `[ObservableAsProperty]` booleans (`IsMainScreen`, `IsSettingsScreen`, `IsHelpScreen`). The main screen also has `IsHistoryMode` and `IsDanceListMode` toggles for switching between the Queue/History and TrackCatalog/DanceList panels.

```csharp
public enum Screen { Main, Settings, Help }
```

**To add a new screen:**

1. Add a value to the `Screen` enum.
2. Add a derived `[ObservableAsProperty] public partial bool IsXxxScreen { get; }` and wire it in the constructor.
3. Create the view folder in `Views/{Feature}/` with the standard View + ViewModel.
4. Register the ViewModel in `Program.cs`.
5. Add the ViewModel as a property on `MainWindowViewModel` (injected via constructor).
6. Add a `Panel` in `MainWindow.axaml` with `IsVisible="{Binding Navigation.IsXxxScreen}"`.
7. Add a navigation button in the toolbar or appropriate location.

### UI Services

| Service | Purpose |
|---------|---------|
| `ConfirmationService` | Shows a modal `ConfirmationDialogView`. Requires `SetOwner(Window)` to be called once at startup (done in `App.axaml.cs`). Returns `Task<bool>`. |
| `NotificationService` | Toast notifications using a DynamicData `SourceList<NotificationItem>` bound to `NotificationOverlayView`. Auto-dismisses after 4 seconds. Supports `Information`, `Warning`, `Error` severity. |
| `FileLogSinkService` | Implements Avalonia's `ILogSink` to bridge framework logs into the Domain `ILoggerService`. Wired in `Program.cs` via `AfterSetup`. |

### Converters

All value converters follow a static instance pattern for use with `{x:Static}` in XAML:

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

Existing converters: `DurationFormatConverter`, `MarkedBrushConverter`, `WeightConverter`, `SeverityToBrushConverter`, `BoolToStringConverter`.

**To add a new converter:** create a class implementing `IValueConverter` with `public static readonly XxxConverter Instance = new();`. Place it in the feature folder where it is used.

### Presentation Windows

`App.axaml.cs` manages 0–10 presentation windows (for external displays). The count is driven by `ApplicationSettings.PresentationDisplayCount`. Each window's position, size, maximised, and borderless state is saved on exit and restored on startup. The `SyncPresentationWindows` method closes excess windows and opens new ones as the setting changes.

---

## Web Layer (`Ready4Balfolk.Web/`)

A **class library, not a web application**. The Avalonia app is the host and starts it on demand;
`FrameworkReference Microsoft.AspNetCore.App` is how a non-web project gets ASP.NET Core. Nothing in
the desktop app depends on the server running, and switching it off costs the app nothing.

It serves two pages, both from `wwwroot/` and both embedded rather than copied next to the
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
server on, moving its port or opening it to the network never needs a restart.

---

## Cross-Cutting Concerns

### Logging

`ILoggerService` is the Domain logging abstraction with `LogAsync`, `DebugAsync`, `InfoAsync`, `WarningAsync`, `ErrorAsync`, `CriticalAsync`, and `ExportAsync` methods.

| Implementation | Behaviour |
|----------------|-----------|
| `FileLoggerService` | Writes to `app.log` in the app-data directory. Deletes and restarts the file when it exceeds 512 KB. Uses `SemaphoreSlim` for thread-safe writes. Has a configurable `MinimumLevel`. |
| `NoOpLoggerService` | Does nothing: used in tests. |

**Format:** `2025-01-15 14:30:00.123 [INFO] message`

**Usage:** inject `ILoggerService` and call `await logger.InfoAsync("message")`. Logging is fire-and-forget (the `Task` offloads to a background thread).

### Exception Handling

Three global handlers in `Program.cs` catch unhandled exceptions and route them to the logger:

1. `AppDomain.CurrentDomain.UnhandledException`: CLR-level (critical).
2. `TaskScheduler.UnobservedTaskException`: unobserved async failures (error, marked observed).
3. `RxApp.DefaultExceptionHandler`: unhandled Rx pipeline errors (error).

UI-level errors (e.g. a failed refresh, missing tracks) are shown to the user via `NotificationService.Show(message, Severity.Error)`.

### Continuous Integration

`verify.yml` runs on every push and pull request. `release.yml` is triggered by hand with a version and chains everything else: verify → build binaries → package (Flatpak, Inno Setup) → smoke test the packages → publish the release. macOS is not a build target.

It is **four jobs that run beside each other**, because a pull request goes green when the slowest one finishes rather than when the longest list of steps does:

- `test`, on Ubuntu and Windows. The tests have to run somewhere they could fail differently: `Directory.Build.targets` resolves the BASS natives from the host OS, and the paths the stores write to are not the same shape on Windows.
- `style`: `dotnet format --verify-no-changes` and `scripts/check-translations.py`, which compares the `.resx` key sets in both directions. A missing Dutch key falls back to English at runtime, which reads as a bug nobody reported rather than a build that failed. One platform for both: `.gitattributes` normalises line endings, so neither can answer differently per platform.
- `scenarios`: the end to end suite. Its own job above all because it is the leg that grows every time a scenario is written, and beside the others it grows on its own rather than on top of them.
- `verify`, which needs the other three and is the only name the branch ruleset requires. A matrix reports one check per leg, so requiring those directly means editing the ruleset every time one is split, and a leg nobody remembered to add is a leg that cannot block a merge.

Coverage is collected as cobertura in the `test` job and uploaded as an artifact. It is deliberately **not** gated on a threshold; the artifact is there to be read.

**Native debug symbols are dropped from the output** (`DropNativeDebugSymbols` in `Directory.Build.props`). SkiaSharp and HarfBuzz ship a `.pdb` beside every native library for every runtime they support, and MSBuild copies them: `libSkiaSharp.pdb` alone is 81 MB and arrives once per Windows runtime in each project's output. It made the four outputs of this solution 2.2 GB, nearly all of it copying rather than compiling, and none of it usable on the machine doing the copying. Only the natives are stripped; the symbols of the code in this repository are what a stack trace is read from.

Two build-level gates are worth knowing about. `TreatWarningsAsErrors` does not reach the Avalonia XAML compiler, so `AVLN5001` (the obsolete-member warning) is listed in `WarningsAsErrors` separately. And every workflow declares a `concurrency` group so a superseded push is cancelled, except on `main`, where a commit left with no verdict is worse than a slow one.

**The smoke test.** CI packages every artifact but cannot tell a healthy one from a broken one by looking. `Directory.Build.targets` picks the BASS, BASSFLAC and BASS_FX natives from the *host* OS rather than from the `RuntimeIdentifier`, so a publish that lands the wrong ones, or none, still succeeds, and the failure only shows up when a user double-clicks it.

So the app can start itself for inspection:

```bash
./Ready4Balfolk.UI --smoke-test
```

`SmokeTest.Run` starts the application for real, waits for the main window, then resolves `IAudioPlaybackService`: which is what loads BASS, and is why killing the app after a timeout would not do: the service is a lazy singleton that nothing on the startup path touches, so a build with no BASS at all reaches a running window quite happily. It then checks BASS_FX (`IsEqualizerAvailable`), checks every extension the app offers is registered, **decodes a file in each format**, scans everything this run appended to `app.log` for `[ERROR]` and `[CRITICAL]`, prints the log if anything failed, and exits: `0` passed, `1` a check failed, `2` startup hung.

The decode matters because registering a plugin is not the same as being able to read a file with it. v1.1.0 shipped Windows builds with BASSFLAC present and unloadable, so `.flac` was silently missing from the catalogue for every Windows user.

`scripts/smoke-test-media/` holds the fixtures: the same 1.5 s chromatic scale, A4 up to G♯5, encoded as `.wav`, `.aiff`, `.flac`, `.mp3`, `.mp2` and `.ogg`. They are committed rather than generated, for the same reason the icons are: CI decodes them on every pull request, and generating them there would put ffmpeg on the critical path of every run, which `windows-latest` does not ship. Regenerate with `scripts/generate-smoke-test-media.sh` and commit the result; the output is deterministic, so an unchanged scale produces no diff.

`.mp1` and `.aif` have no fixture. Nothing has encoded MPEG audio layer 1 for decades, and `.aif` is byte for byte the same format as `.aiff`; both are covered by the registered-extensions check instead.

Two things exist only for this mode. `App` skips its exit confirmation dialog, since nobody is there to answer one; and BASS initialises against its "no sound" device, so the library, its plugins and the effect chain come up exactly as they would against real hardware on a runner that has no sound card. That keeps the check measuring whether the natives shipped rather than whether the machine can make a noise.

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

### Reactive Patterns

- **Domain → UI:** stores expose `IObservable<T>` via `BehaviorSubject.AsObservable()` or DynamicData `SourceList.Connect()`. ViewModels subscribe in the constructor, marshal to the UI thread with `.ObserveOn(RxApp.MainThreadScheduler)`, and collect subscriptions in a `CompositeDisposable` that is disposed when the ViewModel is disposed.
- **DynamicData collections:** `service.Connect()` → `.ObserveOn(RxApp.MainThreadScheduler)` → `.Bind(out _items)` → `.Subscribe()`. The resulting `ReadOnlyObservableCollection<T>` is bound to the view's `ItemsSource`.
- **Derived properties:** `this.WhenAnyValue(x => x.Prop).Select(...)` piped to `.ToProperty(this, x => x.DerivedProp)` to produce `[ObservableAsProperty]` values.
- **Disposal:** all subscriptions are added to `CompositeDisposable` via `.DisposeWith(_disposables)`. ViewModels implement `IDisposable`.

### Thread Safety

| Mechanism | Where used |
|-----------|-----------|
| `SemaphoreSlim(1, 1)` | All stores: serialises file I/O. `FileLoggerService`: serialises log writes. |
| `ObserveOn(RxApp.MainThreadScheduler)` | All ViewModel subscriptions that touch UI-bound properties or collections. |
| `ObserveOn(TaskPoolScheduler.Default)` | Work that must stay off the UI thread, such as `TrackStore` re-resolving every track when the dance list changes. |

---

## How To: Add a New Feature (Checklist)

1. **Model**: add sealed records in `Domain/Models/{Feature}/` if new data types are needed.
2. **Store** (if persistent state): create `IXxxStore` + `XxxStore` in `Domain/Stores/{Feature}/`. Follow the `BehaviorSubject` + `SemaphoreSlim` + JSON pattern.
3. **Service** (if runtime logic): create `IXxxService` + `XxxService` in `Domain/Services/{Feature}/`.
4. **Register**: add store/service to `Program.cs` `ConfigureServices`. Call `store.LoadAsync()` in `App.axaml.cs` if it persists data.
5. **ViewModel**: create `{Feature}ViewModel : ReactiveObject` in `UI/Views/{Feature}/`. Use `[Reactive]`, `[ObservableAsProperty]`, `[ReactiveCommand]`. Subscribe to stores/services in the constructor, dispose in `Dispose()`.
6. **View**: create `{Feature}View.axaml` + `.axaml.cs` extending `ReactiveUserControl<{Feature}ViewModel>`. Set `x:DataType`. Use compiled bindings.
7. **Register ViewModel**: add to `Program.cs` as singleton. Add as a property on `MainWindowViewModel` if it is a top-level screen.
8. **Navigation**: add to `Screen` enum, wire `IsXxxScreen`, add `IsVisible` panel in `MainWindow.axaml`, add toolbar button.
9. **Converters**: if needed, add with the static `Instance` pattern in the feature folder.
10. **Strings**: add the English text to `UiStrings.resx`, the Dutch to `UiStrings.nl.resx`, and the property to `UiStrings.Designer.cs`. The three are kept in step by hand.
