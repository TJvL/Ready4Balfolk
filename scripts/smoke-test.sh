#!/usr/bin/env bash
#
# Launches Ready4Balfolk with --smoke-test under a headless display server and exits with the
# application's own status. Used by CI, and the quickest way to reproduce a CI failure locally.
#
#   scripts/smoke-test.sh x11     publish/Ready4Balfolk.UI
#   scripts/smoke-test.sh wayland publish/Ready4Balfolk.UI
#   scripts/smoke-test.sh x11     flatpak run io.github.tjvl.Ready4Balfolk
#
# Both sessions are worth running: the app selects its backend at startup (UseWaylandWithFallback),
# so X11 and Wayland are two different code paths through Avalonia and only one of them is what a
# given user will get.
#
# Needs xvfb (x11) or cage (wayland) on PATH:
#   sudo apt-get install -y xvfb cage libx11-6 libice6 libsm6 libfontconfig1 fonts-dejavu-core
#
set -euo pipefail

if [ $# -lt 2 ]; then
  echo "usage: $0 x11|wayland <command> [args...]" >&2
  exit 64
fi

session=$1
shift

case "$session" in
  x11)
    exec xvfb-run -a "$@" --smoke-test
    ;;

  wayland)
    export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/tmp/ready4balfolk-smoke-test}"
    mkdir -p "$XDG_RUNTIME_DIR"
    chmod 700 "$XDG_RUNTIME_DIR"

    # A CI runner has neither a GPU nor input devices, and wlroots has to be told so or cage never
    # comes up at all.
    export WLR_BACKENDS=headless
    export WLR_LIBINPUT_NO_DEVICES=1

    # cage's own exit status says nothing about the application: it reliably segfaults tearing down
    # its headless backend once the client is gone, long after the client decided anything. Carry
    # the real status out in a file, pre-seeded so that "cage never ran the app" reads as a failure
    # rather than as an empty success.
    SMOKE_STATUS_FILE=$(mktemp)
    export SMOKE_STATUS_FILE
    echo 70 > "$SMOKE_STATUS_FILE"

    cage -- sh -c '"$@" --smoke-test; echo $? > "$SMOKE_STATUS_FILE"' sh "$@" || true

    status=$(cat "$SMOKE_STATUS_FILE")
    rm -f "$SMOKE_STATUS_FILE"
    exit "$status"
    ;;

  *)
    echo "unknown session type '$session', expected x11 or wayland" >&2
    exit 64
    ;;
esac
