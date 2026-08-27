#!/usr/bin/env bash
#
# Regenerates scripts/smoke-test-media/, the audio the --smoke-test flag decodes to prove a build
# can actually play each supported format.
#
# The output is committed, like the icons are: CI decodes these files on every pull request, and
# generating them there would put ffmpeg on the critical path of every run, and windows-latest does
# not ship it. Run this only when the fixtures need to change, and commit the result.
#
#   scripts/generate-smoke-test-media.sh
#
# Each file is the same 1.5 s chromatic scale, A4 up to G#5, one semitone per 125 ms. A scale
# rather than a tone or noise so that a human can tell at a glance whether a decode came out right.
#
# There is deliberately no .mp1 fixture: nothing has encoded MPEG audio layer 1 for decades. BASS
# still decodes it and the app still offers it, so it is covered by the extension-list check in
# SmokeTest.cs instead. Nor is there a .aif one, which is byte for byte the same format as .aiff.
#
set -euo pipefail

if ! command -v ffmpeg &>/dev/null; then
  echo "ERROR: ffmpeg not found." >&2
  echo "  Arch:   sudo pacman -S ffmpeg" >&2
  echo "  Debian: sudo apt-get install ffmpeg" >&2
  exit 1
fi

OUT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/smoke-test-media"
RATE=16000
NOTE_SECONDS=0.125
FADE_SECONDS=0.008

# 440 * 2^(n/12) for the twelve semitones from A4.
NOTES=(440.00 466.16 493.88 523.25 554.37 587.33 622.25 659.26 698.46 739.99 783.99 830.61)

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

# A short fade on each end of every note, so the joins are not clicks. A click is broadband, and
# broadband content is exactly what makes a lossy encoder spend its bitrate badly at 32 kbit/s.
FADE_OUT_AT=$(awk -v n="$NOTE_SECONDS" -v f="$FADE_SECONDS" 'BEGIN { print n - f }')

for index in "${!NOTES[@]}"; do
  note=$(printf '%02d' "$index")
  ffmpeg -hide_banner -loglevel error -y \
    -f lavfi -i "sine=frequency=${NOTES[$index]}:duration=$NOTE_SECONDS:sample_rate=$RATE" \
    -ac 1 \
    -af "afade=t=in:st=0:d=$FADE_SECONDS,afade=t=out:st=$FADE_OUT_AT:d=$FADE_SECONDS" \
    "$TMP/note-$note.wav"
  echo "file '$TMP/note-$note.wav'" >> "$TMP/list.txt"
done

ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i "$TMP/list.txt" -c copy "$TMP/scale.wav"

mkdir -p "$OUT"

# Determinism, so that rerunning this produces no diff unless the audio actually changed:
# -map_metadata -1 drops the tags carried over from the input, and +bitexact stops the encoder
# stamping its own version into the file and pins the Ogg bitstream serial, which the muxer
# otherwise picks at random on every run.
encode() {
  local name=$1
  shift
  ffmpeg -hide_banner -loglevel error -y -i "$TMP/scale.wav" \
    -map_metadata -1 -fflags +bitexact -flags:a +bitexact "$@" "$OUT/$name"
  echo "  $name  $(stat -c%s "$OUT/$name") bytes"
}

# 22050 Hz for the lossy three: MP2 has no 16 kHz mode outside MPEG-2, and matching the rates
# across all three keeps the comparison between them honest.
encode scale.wav  -c:a pcm_s16le
encode scale.aiff -c:a pcm_s16be
encode scale.flac -c:a flac
encode scale.mp3  -ar 22050 -c:a libmp3lame -b:a 32k
encode scale.mp2  -ar 22050 -c:a mp2 -b:a 32k
encode scale.ogg  -ar 22050 -c:a libvorbis -b:a 32k

echo "Done. Commit the result."
