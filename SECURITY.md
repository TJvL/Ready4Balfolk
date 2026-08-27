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
accounts, no server component you connect to, and it sends nothing anywhere. Two parts are worth
naming.

**The embedded web server is off by default.** When switched on it binds to the local machine, or to
the network if you ask it to, and serves two pages: a presentation display and a phone remote. It
speaks plain HTTP, so anyone able to read traffic on that network can read what is on the screen and
the remote's token. That is deliberate for a page whose whole content is being projected onto a wall
in the same room. Do not put it on a network you do not trust.

The remote is guarded by a six-digit PIN exchanged once for a token, with a per-address lockout after
five wrong attempts. The PIN protects against someone idly poking at the port, not against a
determined attacker with time on the same network.

**The dance list is fetched over HTTPS** from BigBalfolkList and replaced wholesale. A first run with
no network uses the copy shipped inside the binary.

## Out of scope

- The BASS audio libraries, which are third-party binaries fetched at build time from un4seen.com.
  Report problems in those upstream.
- Anything requiring an attacker to already be able to run code as your user, or to write to your
  music directory.
