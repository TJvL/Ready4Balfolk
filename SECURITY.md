# Security

## Reporting

Report a vulnerability through
[GitHub's private advisory form](https://github.com/TJvL/Ready4Balfolk/security/advisories/new)
rather than a public issue, and give it a few days for a first reply. This is a spare-time project,
so there is no on-call rotation behind it.

## Supported versions

The latest release only. There are no maintenance branches.

## What the threat model actually is

Ready4Balfolk is a desktop application that reads a music directory and plays audio. It has no
accounts, no server component you connect to, and it sends nothing anywhere by itself. Three parts
are worth naming.

**The embedded web server is off by default.** When switched on it binds to the local machine, or to
the network if you ask it to, and serves two pages: a presentation display and a phone remote. It
speaks plain HTTP, so anyone able to read traffic on that network can read what is on the screen and
the remote's token. That is deliberate for a page whose whole content is being projected onto a wall
in the same room. Do not put it on a network you do not trust.

The remote is guarded by a six-digit PIN exchanged once for a token, with a per-address lockout after
five wrong attempts. The PIN protects against someone idly poking at the port, not against a
determined attacker with time on the same network. The remote names every search hit by the full
path of its file, which is how a phone says which track to queue, so a device that has the PIN can
read where the music sits on the disk as well as what is in it.

**Two files leave the machine, and only because you send them.** The exported log and an exported
night are files the user saves and hands to somebody: the log to a public bug report, the night to
an organiser. The log names files rather than spelling out where they live, and the user profile
directory is written as a tilde in the exported copy, not in the file on disk. An exported night
carries the dances, artists, titles and times and not the path of any file.

What an exception carried into a log line is not scrubbed beyond that tilde. The text of an error is
written by whoever threw it, and an operating system that could not open a file routinely names that
file in full, so a library kept somewhere other than under the user profile directory is spelled out
in the export. Read an exported log before posting it in public.

**The dance list is fetched over HTTPS** from BigBalfolkList and replaced wholesale. No copy is
shipped inside the binary, so a machine that never goes online gets its list from a `dances.json`
the user imports, which is parsed and validated exactly like a downloaded one.

## Out of scope

- The BASS audio libraries, which are third-party binaries fetched at build time from un4seen.com.
  Report problems in those upstream.
- Anything requiring an attacker to already be able to run code as your user, or to write to your
  music directory.
