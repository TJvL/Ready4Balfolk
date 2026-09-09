#!/usr/bin/env python3
"""Every string the application shows has to exist in both languages, and has to be shown by
something.

The Dutch translation is currently complete, and nothing kept it that way: adding a string to
UiStrings.resx and forgetting UiStrings.nl.resx falls back to the English text at runtime, which
looks like a bug nobody reported rather than a build that failed.

Compares the data keys of each .resx against its .nl.resx and reports both directions. A key only
in Dutch is just as wrong: it is a string that was renamed or removed on one side.

It also fails on a key nothing reads. A heading or a hint can survive a screen being redesigned
around it, fully translated in both languages, because the parity check above only ever compares
the two resx files to each other and neither one notices that nobody asks for the value any more.
A key counts as read only if it shows up as one of the two idioms that actually fetch a resource:
a UiStrings.TheKey or DomainStrings.TheKey member access, or a bare string literal whose entire
contents are the key name, which is how a converter that looks a resource up by name (an icon key
such as IconFormatWav is the same idiom, just not a resx one) finds it. A key merely mentioned in a
comment, a variable name, an XML attribute name, or as part of a longer string is not a reader:
none of those ever ask the resource system for the value, so a key with no other reader is dead
regardless of how often its name is typed nearby.
"""

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

PAIRS = [
    ("Ready4Balfolk.UI/Resources/UiStrings.resx", "Ready4Balfolk.UI/Resources/UiStrings.nl.resx"),
    ("Ready4Balfolk.Domain/Resources/DomainStrings.resx",
     "Ready4Balfolk.Domain/Resources/DomainStrings.nl.resx"),
]

SOURCE_GLOBS = ("*.cs", "*.axaml")

# Build output and the SDK's own restore cache both nest a copy of the source tree; walking them
# would let a key be "read" by the very generated file that proves it never is.
IGNORED_DIR_PARTS = {"obj", "bin"}

IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")

# A UiStrings.TheKey or DomainStrings.TheKey member access. This is matched against the whole file
# text (.axaml included), because a XAML markup extension such as
# `{x:Static res:UiStrings.MainWindow_Title}` puts exactly this text inside an attribute value.
MEMBER_ACCESS = re.compile(r"\b(?:UiStrings|DomainStrings)\.([A-Za-z_][A-Za-z0-9_]*)")

# Comments and string/char literals in C#, as one alternation so finditer walks the file once and
# never mistakes a "//" inside a string (an "http://" URL, say) for the start of a line comment.
CS_COMMENT_OR_LITERAL = re.compile(
    r"//[^\n]*"                       # line comment
    r"|/\*.*?\*/"                     # block comment
    r"|@\$?\"(?:\"\"|[^\"])*\""       # verbatim (and verbatim-interpolated) string
    r"|\$?\"(?:\\.|[^\"\\\n])*\""     # ordinary (and interpolated) string
    r"|'(?:\\.|[^'\\\n])*'",          # char literal
    re.DOTALL,
)

# A single, non-nested interpolation hole inside a $"..." string, e.g. the UiStrings.Queue_Foo in
# $" - {UiStrings.Queue_Foo}". Its contents are ordinary C# expression code, not string content, so
# a member access inside one is a real read and must go back into the member-access scan below.
INTERPOLATION_HOLE = re.compile(r"\{([^{}]*)\}")

# XML comments in .axaml. Avalonia attribute values are always plain quoted text (no block
# comments can appear inside one), so stripping these is the only thing a .axaml file needs before
# the member-access and bare-literal scans below run over what is left.
XML_COMMENT = re.compile(r"<!--.*?-->", re.DOTALL)


def keys(path: Path) -> set[str]:
    """The data keys of a resx. ElementTree ignores comments, so the template examples in the
    header do not count as entries."""
    root = ET.parse(path).getroot()
    return {
        element.get("name")
        for element in root.findall("data")
        if element.get("name") is not None
    }


AXAML_QUOTED = re.compile(r'"([^"]*)"')


def _cs_readers(text: str) -> set[str]:
    """Member-access and bare-literal reads in one .cs file's text. Comments never reach either
    check: they are one of the alternatives CS_COMMENT_OR_LITERAL matches, so the loop below drops
    them along with everything that lies between one match and the next non-comment, non-literal
    stretch of actual code."""
    read: set[str] = set()
    code_spans: list[str] = []
    last = 0
    for match in CS_COMMENT_OR_LITERAL.finditer(text):
        code_spans.append(text[last:match.start()])
        last = match.end()
        token = match.group(0)
        if token.startswith("//") or token.startswith("/*"):
            continue  # a comment: mentions a key by name here, but never fetches it
        if token[0] in "\"$@":
            inner = token[token.index('"') + 1: -1]
            if token[0] == "$" or token.startswith("@$"):
                # Interpolated: the holes are code, not string content, and must still be scanned
                # for member accesses; the surrounding literal text can never itself be a bare-key
                # match once it contains a hole, so there is nothing else to add for this token.
                code_spans.extend(INTERPOLATION_HOLE.findall(inner))
            elif IDENTIFIER.fullmatch(inner):
                read.add(inner)
    code_spans.append(text[last:])
    read.update(MEMBER_ACCESS.findall("".join(code_spans)))
    return read


def _axaml_readers(text: str) -> set[str]:
    """Member-access and bare-literal reads in one .axaml file's text, after dropping XML
    comments so a key mentioned only inside one is not mistaken for a reader."""
    code = XML_COMMENT.sub("", text)
    read = set(MEMBER_ACCESS.findall(code))
    for value in AXAML_QUOTED.findall(code):
        if IDENTIFIER.fullmatch(value):
            read.add(value)
    return read


def referenced_identifiers(repo: Path) -> set[str]:
    """Every resx key name the application's source actually reads, .cs and .axaml alike, skipping
    generated designer files. A key counts only via a UiStrings.TheKey/DomainStrings.TheKey member
    access or a bare string literal that is exactly the key name; see the module docstring."""
    read: set[str] = set()
    for pattern in SOURCE_GLOBS:
        for path in repo.rglob(pattern):
            if IGNORED_DIR_PARTS & set(path.relative_to(repo).parts):
                continue
            if path.name.endswith(".Designer.cs"):
                continue
            text = path.read_text(encoding="utf-8")
            if pattern == "*.cs":
                read.update(_cs_readers(text))
            else:
                read.update(_axaml_readers(text))
    return read


def main() -> int:
    repo = Path(__file__).resolve().parent.parent
    failed = False
    read = referenced_identifiers(repo)

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

        for key in sorted(english - read):
            print(f"ERROR: {english_name} defines '{key}', which no .cs or .axaml file reads")
            failed = True

        if english == dutch:
            print(f"{english_name}: {len(english)} strings, both languages complete.")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
