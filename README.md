# Ready4Balfolk

A desktop application for managing a balfolk dance night with recorded music. Build a queue of tracks, organise dances into categories, and display what's playing to your dancers on a presentation screen.

Tracks are discovered from a music directory using the naming convention `Dance - Artist - Title`. Supported audio formats: MP3, MP2, MP1, WAV, OGG, AIFF, and FLAC.

## Audio Backend

Ready4Balfolk uses [BASS](https://www.un4seen.com/) (via ManagedBass) as its audio backend.

## Installation

Download the latest version from the [Releases](../../releases) page.

| Platform | Options |
|----------|---------|
| **Windows** | Installer (`.exe`) or portable archive (`.zip`) |
| **Linux** | Flatpak (`.flatpak`) or portable archive (`.tar.gz`) |

The portable builds require no installation — just extract and run. The installers add Start Menu/desktop shortcuts (Windows) or desktop integration (Linux Flatpak).

Every release artifact is launched by CI before it is published, so a build that cannot start never reaches this page.

> **macOS is not supported.** Releases up to and including v1.1.0 shipped an unsigned `.dmg`; there are no newer ones. Nothing in the code is deliberately Windows- and Linux-only, so a `dotnet publish -r osx-arm64` may well still work — under the GPL you are free to build it yourself, but it is neither tested nor released.

## Documentation

- [User Help](documentation/help.md) — how to use the application
- [Development Guide](documentation/development.md) — contributor guidance on the codebase structure and conventions

## Issues

Found a bug or have a feature suggestion? Please [open a GitHub issue](../../issues).
