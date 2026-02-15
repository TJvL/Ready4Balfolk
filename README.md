# Ready4Balfolk

A desktop application for managing a balfolk dance night with recorded music. Build a queue of tracks, organise dances into categories, and display what's playing to your dancers on a presentation screen.

Tracks are discovered from a music directory using the naming convention `Dance - Artist - Title.mp3`. Only MP3 files are supported.

## Audio Backend

Ready4Balfolk uses [BASS](https://www.un4seen.com/) (via ManagedBass) as its audio backend.

## Installation

Download the latest version from the [Releases](../../releases) page.

| Platform | Options |
|----------|---------|
| **Windows** | Installer (`.exe`) or portable archive (`.zip`) |
| **Linux** | Flatpak (`.flatpak`) or portable archive (`.tar.gz`) |
| **macOS** | Disk image (`.dmg`) or portable archive (`.tar.gz`) |

The portable builds require no installation — just extract and run. The installers add Start Menu/desktop shortcuts (Windows), desktop integration (Linux Flatpak), or a drag-to-Applications experience (macOS).

> **macOS note:** The app is not signed with an Apple Developer certificate. On first launch, right-click the app and choose **Open** to bypass the Gatekeeper warning.

## Documentation

- [User Help](documentation/help.md) — how to use the application
- [Development Guide](documentation/development.md) — contributor guidance on the codebase structure and conventions

## Issues

Found a bug or have a feature suggestion? Please [open a GitHub issue](../../issues).
