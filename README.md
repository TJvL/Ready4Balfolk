# Ready4Balfolk

A desktop application for managing a balfolk dance night with recorded music. Build a queue of tracks, draw at random from the dances you feel like playing, and display what's playing to your dancers on a presentation screen.

The dances themselves come from [BigBalfolkList](https://tjvl.github.io/BigBalfolkList/), used exactly as published: every dance and every name it goes by, with tags for where it comes from, which family it belongs to and whether it is danced in a suite. Ready4Balfolk fetches it for you and never edits it, so there is no list to build before you start.

Tracks are discovered from a music directory without requiring any naming convention: Ready4Balfolk looks for those dance names anywhere in a filename or its tags, reads the artist from the folder the file is filed under, and asks you about whatever it does not recognise rather than guessing. Supported audio formats: MP3, MP2, MP1, WAV, OGG, AIFF, and FLAC.

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
