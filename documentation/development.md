# Development Guide

This document explains the layered architecture, naming conventions, and patterns used in Ready4Balfolk so that new contributors know where to add code and how to follow existing conventions.

## Overview

Ready4Balfolk is a two-project Avalonia desktop application for managing and playing back folk dance music queues.

| Project | Purpose | Key dependencies |
|---------|---------|-----------------|
| `Ready4Balfolk.Domain` | Models, stores, services — no UI dependencies | System.Reactive, DynamicData, ManagedBass, System.Text.Json |
| `Ready4Balfolk.UI` | Views, ViewModels, converters, UI services | Avalonia 11, ReactiveUI, ReactiveUI.SourceGenerators |

**Key principles:**

- **Reactive-first** — state flows as `IObservable<T>` from Domain stores/services; the UI subscribes and reacts.
- **Immutable models** — all Domain models are sealed records; mutations produce new instances.
- **Interface-driven** — every store and service has an interface for testability and DI.
- **MVVM with compiled bindings** — Views bind to ViewModels; `x:DataType` is set on every view.

---

## Domain Layer (`Ready4Balfolk.Domain/`)

### Models

All models are **sealed records** with `[JsonPropertyName]` attributes for persistence. They are organised by subdirectory:

| Directory | Contents |
|-----------|----------|
| `Tracks/` | `Track` — file path, dance, artist, title, length. Carries `OriginalDance` for re-resolution. |
| `QueueItems/` | `IQueueItem` interface + five implementations: `TrackQueueItem`, `DelayQueueItem`, `MessageQueueItem`, `StopQueueItem`, `AutoTrackQueueItem`. |
| `Tree/` | `DanceBranch` (recursive children + leaf dances) and `DanceLeaf`. Each has a `Weight` for probability-based random selection. |
| `Synonyms/` | `DanceMainName` (canonical name + list of `DanceSynonym`). |
| `Settings/` | `ApplicationSettings`, `ApplicationTheme` enum, `WindowState`. |
| `History/` | `QueueHistoryEntry` (abstract, `[JsonPolymorphic]`) with `TrackHistoryEntry`, `MessageHistoryEntry`, `DelayHistoryEntry`, `StopHistoryEntry`. `QueueHistory` wraps the entry list. |

**To add a new model:** create a `sealed record` in the appropriate subdirectory. Add `[JsonPropertyName]` attributes if it will be serialised. If it is polymorphic, add `[JsonPolymorphic]` + `[JsonDerivedType]` on the base type.

### Stores

Stores manage **persisted state** with thread-safe, reactive updates. Every store follows the same pattern:

```
Interface:  IXxxStore  →  Current, Observe(), LoadAsync(), UpdateAsync(Func<T,T>)
Impl:       XxxStore   →  BehaviorSubject<T> + SemaphoreSlim + JSON file I/O
```

**Pattern elements:**

- **`BehaviorSubject<T>`** — holds the current value, replays last value to new subscribers, broadcasts changes.
- **`SemaphoreSlim(1, 1)`** — binary semaphore serialising file access so concurrent calls don't corrupt the JSON file.
- **`UpdateAsync(Func<T, T>)`** — caller passes a pure transformation function; the store atomically applies it, updates the subject, and persists to disk.
- **JSON I/O** — `JsonSerializer.SerializeAsync` with `WriteIndented = true` for human-editable files. Stores accept a `DirectoryInfo` at construction (the app-data directory).

The `TrackStore` is different: it uses a DynamicData `SourceList<Track>` instead of `BehaviorSubject`, exposes `Connect()` for reactive collection binding, integrates a `FileSystemWatcher` for live directory monitoring, and uses `Task.WhenEach()` for streaming track discovery.

**To add a new store:**

1. Create `IXxxStore` in `Stores/{Feature}/` with `Current`, `Observe()`, `LoadAsync()`, `UpdateAsync()`.
2. Create `XxxStore` implementing the interface. Use `BehaviorSubject<T>`, `SemaphoreSlim`, and JSON serialisation as in existing stores.
3. Register in `Program.cs` as a singleton: `services.AddSingleton<IXxxStore>(_ => new XxxStore(DataDirectory));`.
4. Call `LoadAsync()` in `App.axaml.cs` inside the `MainWindow.Opened` handler.

### Services

Services hold **ephemeral runtime state** and operational logic — queue management, playback orchestration, random selection, synonym resolution, track discovery. They consume stores and expose reactive observables.

| Service | Responsibility |
|---------|---------------|
| `QueueService` | In-memory queue backed by `SourceList<IQueueItem>`. Delegates all validation to a `QueueGuard` (see below). |
| `QueueConsumptionService` | Dequeues items, drives playback, tracks elapsed time, records history. |
| `AudioPlaybackService` | ManagedBass wrapper for audio playback (play, pause, seek, volume). |
| `RandomTrackService` | Weighted random selection from the dance tree, with deduplication against queue + history + currently playing. |
| `SynonymResolutionService` | Maintains an in-memory `Dictionary<string, string>` (normalised name → canonical name). Rebuilds atomically via `Interlocked.Exchange` when synonyms change. |
| `TrackDiscoveryService` | Reads audio metadata (TagLib) to produce `Track` records. |

**To add a new service:**

1. Create `IXxxService` and `XxxService` in `Services/{Feature}/`.
2. Inject stores or other services via the constructor.
3. Register in `Program.cs`: `services.AddSingleton<IXxxService, XxxService>();`.

### Queue Guard

The `QueueService` does not contain any validation logic itself. Instead, it delegates all policy decisions — whether an item can be added, moved, removed, or cleared — to an `IQueueGuard`. The guard is composed of pluggable `IQueueRule` instances, making it easy to add or remove constraints without modifying the service.

**Components:**

- **`IQueueRule`** — interface that each rule implements. Every method returns a nullable value: `null` means "no opinion" (defer to other rules), a non-null value means "I have a verdict". Methods:
  - `GetPreAddRemovalPredicate(newItem, currentItems)` — returns an optional predicate identifying items that should be removed *before* the new item is evaluated (e.g. removing AutoTrack placeholders when a real track is added).
  - `EvaluateAdd(item, adjustedItems)` — returns a `QueueRuleVerdict` to allow or deny adding the item. Receives the queue *after* pre-add removals have been applied.
  - `GetEvictionIndices(currentItems)` — returns indices of items that should be evicted when settings or history change.
  - `CanRemove(item)`, `CanMove(item)`, `CanClear(currentItems)` — allow or deny the corresponding operation.
- **`QueueRuleVerdict`** — `sealed record(bool Allowed, string? Reason)` returned by `EvaluateAdd`.
- **`QueueAddResult`** — `sealed record(bool Allowed, string? RejectionReason, Func<IQueueItem, bool>? RemovalPredicate)` returned by `IQueueGuard.EvaluateAdd`. Combines the pre-add removal predicate with the final allow/deny decision. Created via `QueueAddResult.Allow(predicate?)` or `QueueAddResult.Deny(reason)`.
- **`QueueGuard`** — the `IQueueGuard` implementation. Accepts an ordered list of `IQueueRule` instances. Orchestrates evaluation in two phases:
  1. **Pre-add removal** — collects removal predicates from all rules and combines them with OR logic.
  2. **Add evaluation** — runs `EvaluateAdd` on each rule against the adjusted item list. First deny wins.
  For `CanRemove`, `CanMove`, and `CanClear`, first definitive answer wins; if no rule has an opinion, the default is `true`. For `GetEvictionIndices`, results from all rules are merged into a deduplicated set, sorted in descending order for safe back-to-front removal.
- **`QueueGuardBuilder`** — static factory that constructs a `QueueGuard` from `ApplicationSettings`. It always includes `AutoTrackRule`, conditionally adds `DuplicateTrackRule` based on the `AllowDuplicateTracksInQueue` setting, and always adds `MaxItemsRule` last.

**Existing rules:**

| Rule | Purpose |
|------|---------|
| `AutoTrackRule` | When a non-auto item is added, emits a removal predicate for all `AutoTrackQueueItem`s. Denies adding an `AutoTrackQueueItem` to a non-empty queue. Prevents moving, removing, or clearing auto-track items. |
| `DuplicateTrackRule` | Denies adding a track that already exists in the queue, is currently playing, or was already played (finished in history). Evicts duplicates when history changes or the setting is toggled. |
| `MaxItemsRule` | Denies adding when the queue is at capacity. Evicts tail items when the max is reduced. |

**Reactive rebuilding:** `QueueService` subscribes to `ISettingsStore.Observe()` and rebuilds the guard via `QueueGuardBuilder.FromSettings()` whenever settings change. After rebuilding, it runs eviction to enforce the new rules immediately. It also subscribes to `IQueueHistoryStore.Observe()` (skipping the initial value) to evict items that become duplicates after a track finishes playing.

**To add a new queue rule:**

1. Create a class implementing `IQueueRule` in `Services/Queue/`. Return `null` from any method where the rule has no opinion.
2. Add the rule to the list in `QueueGuardBuilder.FromSettings()`, respecting the ordering (rules are evaluated in list order; first deny wins for adds, first definitive answer wins for can-operations).
3. Add unit tests for the rule in isolation (see `AutoTrackRuleTests`, `DuplicateTrackRuleTests`, `MaxItemsRuleTests` for examples).
4. If the rule interacts with other rules during eviction, add a combined test in `QueueGuardTests`.

### Editor System

The editor system implements **undo/redo** via the Command pattern combined with immutable transforms.

**Components:**

- **`IEditorAction`** — interface with `ExecuteAsync()`, `UndoAsync()`, and `Description`.
- **`EditorHistoryService`** — manages two stacks (`_undoStack`, `_redoStack`). Exposes `CanUndo`, `CanRedo`, `UndoDescription`, `RedoDescription` as `IObservable<T>` via `BehaviorSubject`. Executing a new action clears the redo stack.
- **`DanceTreeAction` / `DanceSynonymAction`** — concrete `IEditorAction` implementations using static factory methods. Each captures a `_before` snapshot, applies a pure transform via `Store.UpdateAsync()`, and undoes by restoring `_before`. An optional `_validate` closure is checked before execution.
- **`DanceTreeTransforms` / `DanceSynonymTransforms`** — static classes containing pure functions that transform immutable data structures. Tree transforms use recursive `ReplaceBranchAtDepth` with `int[]` path-based navigation and record `with` expressions.

**To add a new editor action:**

1. Add a static factory method on `DanceTreeAction` or `DanceSynonymAction` (e.g. `public static DanceTreeAction MoveLeaf(...)`).
2. Write the pure transform function in the corresponding `*Transforms` class.
3. Optionally add a `_validate` closure for pre-execution validation.
4. Call it from the ViewModel via `editorHistoryService.DoActionAsync(DanceTreeAction.MoveLeaf(store, ...))`.

### Helpers

`StringNormalizer.Normalize(string)` — decomposes Unicode (FormD), strips diacritics (non-spacing marks), keeps only letters/digits/spaces, lowercases, and collapses whitespace. Used throughout for case-insensitive, accent-insensitive name matching (synonym resolution, uniqueness checks, search filtering).

---

## UI Layer (`Ready4Balfolk.UI/`)

### Startup & DI

`Program.cs` is the entry point:

1. Creates the `FileLoggerService` singleton (writes to `~/.local/share/Ready4Balfolk/`).
2. Installs three global exception handlers:
   - `AppDomain.CurrentDomain.UnhandledException` → log critical.
   - `TaskScheduler.UnobservedTaskException` → log error, mark observed.
   - `RxApp.DefaultExceptionHandler` → log unhandled Rx exceptions.
3. Builds the Avalonia app with `UseReactiveUIWithMicrosoftDependencyResolver(ConfigureServices, withResolver: sp => App.Services = sp!)` — this bridges Microsoft DI into Splat so ReactiveUI's `ViewLocator` can resolve views.
4. `ConfigureServices(IServiceCollection)` registers all stores, services, and ViewModels (mostly singletons).
5. `AfterSetup` wires `FileLogSinkService` as Avalonia's log sink.

**`App.Services`** is a static `IServiceProvider` property on `App`. Code-behind uses it to resolve services: `App.Services.GetRequiredService<NavigationService>()`.

**To register a new service or ViewModel:** add a line in `ConfigureServices` in `Program.cs`. Use `AddSingleton` for shared state, `AddTransient` for per-resolution instances.

### Views & ViewModels

Every feature lives in `Views/{Feature}/` containing:

- `{Feature}View.axaml` — XAML with `x:DataType="{Feature}ViewModel"`.
- `{Feature}View.axaml.cs` — code-behind extending `ReactiveUserControl<{Feature}ViewModel>`.
- `{Feature}ViewModel.cs` — ViewModel extending `ReactiveObject`.

Namespace: `Ready4Balfolk.UI.Views.{Feature}`.

Some features also include sub-item ViewModels (e.g. `TrackViewModel`, `DanceSynonymEntryViewModel`, `HistoryItemViewModel`), node types (e.g. `DanceCategoryNode`, `DanceItem`), and converters.

**MainWindow** is the shell. Its `MainWindowViewModel` receives all sub-ViewModels via constructor injection. Navigation uses `IsVisible` bindings on `Panel` children — one panel per screen, all stacked. The `NotificationOverlayView` is always visible on top.

### Source Generators

`ReactiveUI.SourceGenerators` 2.6.1 provides three key attributes:

| Attribute | What it generates | Usage |
|-----------|------------------|-------|
| `[Reactive]` | Backing field + `RaiseAndSetIfChanged` in setter | `[Reactive] public partial string Name { get; set; }` |
| `[ObservableAsProperty]` | Readonly `_propHelper` field + `_prop` backing field | `[ObservableAsProperty] public partial string DisplayName { get; }` |
| `[ReactiveCommand]` | `ReactiveCommand` property wired to the decorated method | `[ReactiveCommand(CanExecute = nameof(CanDoIt))] private void DoIt() { }` |

**Gotchas:**

- The generated `_propHelper` field is `readonly` — it must be assigned **in the constructor**, not in a helper method called later.
- Use `.ToProperty(this, x => x.Prop)` (not `ToPropertyEx`) to get the helper, then assign to `_propHelper`.
- `CanExecute = nameof(Prop)` requires `Prop` to be an `IObservable<bool>` property or field.

### Compiled Bindings

Enabled globally via `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` in the `.csproj`. Every XAML file must set `x:DataType` to its ViewModel type.

**Fall back to `{ReflectionBinding}`** in two cases:

- **DataGrid columns** — columns are not in the visual tree, so compiled bindings cannot resolve their DataContext.
- **TreeView `IsExpanded` style setters** — style bindings cannot use compiled bindings for two-way sync.

### Code-Behind

Code-behind is used only for UI mechanics that cannot be expressed declaratively. It **always delegates mutations to the ViewModel** — never modifies domain state directly.

Common cases:

| Pattern | Example |
|---------|---------|
| **Drag-drop reorder** | `QueueView.axaml.cs` — pointer tracking, `DragDrop.DoDragDropAsync`, drop indicator positioning. Calls `ViewModel.MoveItem()`. |
| **TreeView expansion sync** | `DanceTreeView.axaml.cs` — static class handler on `TreeViewItem.IsExpandedProperty.Changed` writes back to `DanceCategoryNode.IsExpanded`. |
| **ContainerPrepared styling** | `QueueView.axaml.cs` — adds CSS class `"autoTrack"` to `ListBoxItem` containers for `AutoTrackQueueItem`. |
| **Focus management** | Various views — programmatic focus after inline edit starts. |
| **Navigation clicks** | `ToolbarView.axaml.cs`, `MainWindow.axaml.cs` — set `NavigationService.CurrentScreen`. |

### Navigation

`NavigationService` holds a `[Reactive] Screen CurrentScreen` property and derived `[ObservableAsProperty]` booleans (`IsMainScreen`, `IsSettingsScreen`, `IsHelpScreen`, `IsSynonymsScreen`). The main screen also has `IsHistoryMode` and `IsTreeViewMode` toggles for switching between Queue/History and TrackCatalog/DanceTree panels.

```csharp
public enum Screen { Main, Settings, Help, Synonyms }
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

## Cross-Cutting Concerns

### Logging

`ILoggerService` is the Domain logging abstraction with `LogAsync`, `DebugAsync`, `InfoAsync`, `WarningAsync`, `ErrorAsync`, `CriticalAsync`, and `ExportAsync` methods.

| Implementation | Behaviour |
|----------------|-----------|
| `FileLoggerService` | Writes to `app.log` in the app-data directory. Deletes and restarts the file when it exceeds 512 KB. Uses `SemaphoreSlim` for thread-safe writes. Has a configurable `MinimumLevel`. |
| `NoOpLoggerService` | Does nothing — used in tests. |

**Format:** `2025-01-15 14:30:00.123 [INFO] message`

**Usage:** inject `ILoggerService` and call `await logger.InfoAsync("message")`. Logging is fire-and-forget (the `Task` offloads to a background thread).

### Exception Handling

Three global handlers in `Program.cs` catch unhandled exceptions and route them to the logger:

1. `AppDomain.CurrentDomain.UnhandledException` — CLR-level (critical).
2. `TaskScheduler.UnobservedTaskException` — unobserved async failures (error, marked observed).
3. `RxApp.DefaultExceptionHandler` — unhandled Rx pipeline errors (error).

UI-level errors (e.g. failed editor actions, missing tracks) are shown to the user via `NotificationService.Show(message, Severity.Error)`.

### Continuous Integration

`verify.yml` builds and runs the tests on Ubuntu and Windows on every push and pull request. `release.yml` is triggered by hand with a version and chains everything else: verify → build binaries → package (Flatpak, Inno Setup) → smoke test the packages → publish the release. macOS is not a build target.

**The smoke test.** CI packages every artifact but cannot tell a healthy one from a broken one by looking. `Directory.Build.targets` picks the BASS, BASSFLAC and BASS_FX natives from the *host* OS rather than from the `RuntimeIdentifier`, so a publish that lands the wrong ones, or none, still succeeds — and the failure only shows up when a user double-clicks it.

So the app can start itself for inspection:

```bash
./Ready4Balfolk.UI --smoke-test
```

`SmokeTest.Run` starts the application for real, waits for the main window, then resolves `IAudioPlaybackService` — which is what loads BASS, and is why killing the app after a timeout would not do: the service is a lazy singleton that nothing on the startup path touches, so a build with no BASS at all reaches a running window quite happily. It then checks BASS_FX (`IsEqualizerAvailable`), checks every extension the app offers is registered, **decodes a file in each format**, scans everything this run appended to `app.log` for `[ERROR]` and `[CRITICAL]`, prints the log if anything failed, and exits: `0` passed, `1` a check failed, `2` startup hung.

The decode matters because registering a plugin is not the same as being able to read a file with it. v1.1.0 shipped Windows builds with BASSFLAC present and unloadable, so `.flac` was silently missing from the catalogue for every Windows user.

`scripts/smoke-test-media/` holds the fixtures: the same 1.5 s chromatic scale, A4 up to G♯5, encoded as `.wav`, `.aiff`, `.flac`, `.mp3`, `.mp2` and `.ogg`. They are committed rather than generated, for the same reason the icons are — CI decodes them on every pull request, and generating them there would put ffmpeg on the critical path of every run, which `windows-latest` does not ship. Regenerate with `scripts/generate-smoke-test-media.sh` and commit the result; the output is deterministic, so an unchanged scale produces no diff.

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

The portable builds are checked inside `build-binaries.yml`, so every pull request runs them. `smoke-test-packages.yml` goes further and installs the Flatpak and the Windows installer, then launches what the installer put on disk — that is the level that catches a native library present in `publish/` but never copied into the bundle. It gates the `release` job, so nothing reaches the Releases page without having been started at least once.

### Reactive Patterns

- **Domain → UI:** stores expose `IObservable<T>` via `BehaviorSubject.AsObservable()` or DynamicData `SourceList.Connect()`. ViewModels subscribe in the constructor, marshal to the UI thread with `.ObserveOn(RxApp.MainThreadScheduler)`, and collect subscriptions in a `CompositeDisposable` that is disposed when the ViewModel is disposed.
- **DynamicData collections:** `service.Connect()` → `.ObserveOn(RxApp.MainThreadScheduler)` → `.Bind(out _items)` → `.Subscribe()`. The resulting `ReadOnlyObservableCollection<T>` is bound to the view's `ItemsSource`.
- **Derived properties:** `this.WhenAnyValue(x => x.Prop).Select(...)` piped to `.ToProperty(this, x => x.DerivedProp)` to produce `[ObservableAsProperty]` values.
- **Disposal:** all subscriptions are added to `CompositeDisposable` via `.DisposeWith(_disposables)`. ViewModels implement `IDisposable`.

### Thread Safety

| Mechanism | Where used |
|-----------|-----------|
| `SemaphoreSlim(1, 1)` | All stores — serialises file I/O. `FileLoggerService` — serialises log writes. |
| `Interlocked.Exchange` | `SynonymResolutionService` — atomically swaps the lookup dictionary when synonyms change. |
| `ObserveOn(RxApp.MainThreadScheduler)` | All ViewModel subscriptions that touch UI-bound properties or collections. |
| `ObserveOn(TaskPoolScheduler.Default)` | Domain services that rebuild caches off the UI thread (e.g. synonym lookup). |

---

## How To: Add a New Feature (Checklist)

1. **Model** — add sealed records in `Domain/Models/{Feature}/` if new data types are needed.
2. **Store** (if persistent state) — create `IXxxStore` + `XxxStore` in `Domain/Stores/{Feature}/`. Follow the `BehaviorSubject` + `SemaphoreSlim` + JSON pattern.
3. **Service** (if runtime logic) — create `IXxxService` + `XxxService` in `Domain/Services/{Feature}/`.
4. **Register** — add store/service to `Program.cs` `ConfigureServices`. Call `store.LoadAsync()` in `App.axaml.cs` if it persists data.
5. **ViewModel** — create `{Feature}ViewModel : ReactiveObject` in `UI/Views/{Feature}/`. Use `[Reactive]`, `[ObservableAsProperty]`, `[ReactiveCommand]`. Subscribe to stores/services in the constructor, dispose in `Dispose()`.
6. **View** — create `{Feature}View.axaml` + `.axaml.cs` extending `ReactiveUserControl<{Feature}ViewModel>`. Set `x:DataType`. Use compiled bindings.
7. **Register ViewModel** — add to `Program.cs` as singleton. Add as a property on `MainWindowViewModel` if it is a top-level screen.
8. **Navigation** — add to `Screen` enum, wire `IsXxxScreen`, add `IsVisible` panel in `MainWindow.axaml`, add toolbar button.
9. **Converters** — if needed, add with the static `Instance` pattern in the feature folder.
10. **Editor actions** — if the feature edits tree/synonym data, add factory methods on `DanceTreeAction` / `DanceSynonymAction` and pure transforms in the `*Transforms` class.
