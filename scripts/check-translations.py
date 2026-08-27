#!/usr/bin/env python3
"""Every string the application shows has to exist in both languages.

The Dutch translation is currently complete, and nothing kept it that way: adding a string to
UiStrings.resx and forgetting UiStrings.nl.resx falls back to the English text at runtime, which
looks like a bug nobody reported rather than a build that failed.

Compares the data keys of each .resx against its .nl.resx and reports both directions. A key only
in Dutch is just as wrong: it is a string that was renamed or removed on one side.
"""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

PAIRS = [
    ("Ready4Balfolk.UI/Resources/UiStrings.resx", "Ready4Balfolk.UI/Resources/UiStrings.nl.resx"),
    ("Ready4Balfolk.Domain/Resources/DomainStrings.resx",
     "Ready4Balfolk.Domain/Resources/DomainStrings.nl.resx"),
]


def keys(path: Path) -> set[str]:
    """The data keys of a resx. ElementTree ignores comments, so the template examples in the
    header do not count as entries."""
    root = ET.parse(path).getroot()
    return {
        element.get("name")
        for element in root.findall("data")
        if element.get("name") is not None
    }


def main() -> int:
    repo = Path(__file__).resolve().parent.parent
    failed = False

    for english_name, dutch_name in PAIRS:
        english_path, dutch_path = repo / english_name, repo / dutch_name
        if not english_path.exists() or not dutch_path.exists():
            print(f"ERROR: missing {english_path if not english_path.exists() else dutch_path}")
            failed = True
            continue

        english, dutch = keys(english_path), keys(dutch_path)

        for key in sorted(english - dutch):
            print(f"ERROR: {dutch_name} has no translation for '{key}'")
            failed = True

        for key in sorted(dutch - english):
            print(f"ERROR: {dutch_name} translates '{key}', which {english_name} does not define")
            failed = True

        if english == dutch:
            print(f"{english_name}: {len(english)} strings, both languages complete.")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
