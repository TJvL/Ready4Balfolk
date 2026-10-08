#!/usr/bin/env bash
#
# Regenerates the audio the end to end scenarios play that the smoke test media cannot stand in for:
# today only Ready4Balfolk.E2E/Fixtures/seek-test.mp3.
#
# Committed for the same reason the smoke test media is (see generate-smoke-test-media.sh): CI
# reads it on every pull request and windows-latest does not ship ffmpeg. Run this only when the
# fixture needs to change, and commit the result.
#
#   scripts/generate-e2e-media.sh
#
# seek-test.mp3 is a 40 s 440 Hz tone for ClickingTheSeekBarTwiceAsksOnce. It has to be long: the
# scenario asserts that playback landed near the halfway point, and on the 1.5 s smoke test scale
# ordinary playback drifts through that window by itself, so a seek that did nothing still passed.
# It is kept out of scripts/smoke-test-media because the smoke test expects every clip there to be
# the 1.5 s scale.
#
set -euo pipefail

if ! command -v ffmpeg &>/dev/null; then
  echo "ERROR: ffmpeg not found." >&2
  echo "  Arch:   sudo pacman -S ffmpeg" >&2
  echo "  Debian: sudo apt-get install ffmpeg" >&2
  exit 1
fi

OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Ready4Balfolk.E2E/Fixtures"

# Determinism, as in generate-smoke-test-media.sh: -map_metadata -1 drops the tags carried over
# from the input, and +bitexact stops the encoder stamping its own version into the file.
ffmpeg -hide_banner -loglevel error -y \
  -f lavfi -i "sine=frequency=440:duration=40:sample_rate=22050" \
  -ac 1 -map_metadata -1 -fflags +bitexact -flags:a +bitexact \
  -c:a libmp3lame -b:a 32k "$OUT/seek-test.mp3"
echo "  seek-test.mp3  $(stat -c%s "$OUT/seek-test.mp3") bytes"

echo "Done. Commit the result."
