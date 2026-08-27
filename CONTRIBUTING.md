# Contributing

Thanks for looking. Ready4Balfolk is a desktop application for running a balfolk night with recorded
music, and it is used in front of real rooms, so the bar for "does it start" is high and the bar for
"does it interrupt the DJ" is higher.

## Before you write code

[`documentation/development.md`](documentation/development.md) is the guide to how the code is laid
out and why. It is worth reading before adding anything, particularly the layering rules and the
queue guard.

For anything larger than a fix, open an issue first. It is much cheaper to disagree about an
approach in an issue than in a finished pull request.

## Building and running

```bash
dotnet build Ready4Balfolk.sln -c Release
dotnet test --project Ready4Balfolk.Tests/Ready4Balfolk.Tests.csproj -c Release
```

The audio natives (BASS, BASSFLAC, BASS_FX) are downloaded by `Directory.Build.targets` on first
build. Set `BassSkipDownload=true` to build offline from whatever is already in `build/bass-native`.

## What CI will check

Run these three before opening a pull request, because Release is stricter than Debug and CI runs
Release:

```bash
dotnet build Ready4Balfolk.sln -c Release
dotnet format Ready4Balfolk.sln --verify-no-changes
python3 scripts/check-translations.py
```

- **Warnings are errors in Release.** That includes `AVLN5001` from the Avalonia XAML compiler.
- **Formatting is enforced.** `dotnet format` decides, not your editor.
- **Both languages, always.** A new user-facing string goes in `UiStrings.resx` *and*
  `UiStrings.nl.resx`. A missing Dutch key silently falls back to English at runtime.
- **Every artifact is launched.** CI publishes each platform and starts it, then installs the
  Flatpak and the Windows installer and starts those too. A build that cannot start never merges.

## Conventions

- **Conventional commits**, with a scope where there is an obvious one: `fix(tracks):`, `feat(web):`,
  `chore(ci):`. The pull request title becomes the squash commit subject, so make it the real one.
- **Say why, not what.** The code says what it does. Comments and XML docs in this repository explain
  the decision behind it, usually the failure that prompted it. Match that.
- **Label your pull request.** Release notes are generated from labels (`.github/release.yml`), so an
  unlabelled pull request lands in "Other changes".
- **Tests that would have caught it.** For a bug fix, the test should fail without the fix. If it
  cannot, say so in the pull request rather than letting the name imply otherwise.

## Dances come from BigBalfolkList

The dance list is [BigBalfolkList](https://github.com/TJvL/BigBalfolkList), used exactly as
published. Ready4Balfolk never edits it. A dance that is missing, or named wrongly, is a change to
make there, not here.
