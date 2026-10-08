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

And it fails on a resx string handed to the logger as the line it writes down. The log and the
screen are separate (#301): what is logged is English, written as a literal where it is logged, and
what is shown comes from the resx files. A UiStrings or DomainStrings value in the log is a log that
changes language with the application, and treating the log line as the notice is how the Dutch
application came to show English error bars. The check reads the application's own source (not the
tests, which may well name a resx string beside a logger call to say it is not what was logged) and
looks at the one argument of each logging call that becomes the log line.

Only the application's own projects count as readers. A key the tests still name, after the screen
that showed it has gone, is a key the DJ never sees, and a test reading it proves only that the
string exists.

The two designer files are written by hand, one property per key, and are compared with their
English resx: a property whose key has gone returns null at runtime instead of failing the build,
and a key with no property cannot be read at all.

Every {0} placeholder has to appear in both languages. A Dutch string that lost one drops the value
it was meant to show, and one that gained a {1} the code never passes throws when it is formatted.

The browser pages keep their own strings, in Ready4Balfolk.Web/wwwroot/strings.js, as an English and
a Dutch table. Those are held to the same rules: the same keys in both, the same placeholders, and
no key that nothing reads. A page reads one through t("key") or R4B.t("key"), and the hub reads one
by sending a RemoteRefusal code that the page passes straight to t(), so every such code also has to
be a key in both tables.
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

# The application's own projects, the only ones whose reads and whose logging count. The tests are
# left out of both: a test that names a string proves only that the string exists, and a test can
# perfectly well put a resx string beside a logger call, to assert that it is not what was logged.
APPLICATION_PROJECTS = ("Ready4Balfolk.Domain", "Ready4Balfolk.UI", "Ready4Balfolk.Web")

DESIGNERS = {
    "Ready4Balfolk.UI/Resources/UiStrings.resx": "Ready4Balfolk.UI/Resources/UiStrings.Designer.cs",
    "Ready4Balfolk.Domain/Resources/DomainStrings.resx":
        "Ready4Balfolk.Domain/Resources/DomainStrings.Designer.cs",
}

STRINGS_JS = "Ready4Balfolk.Web/wwwroot/strings.js"
WEB_SCRIPTS = "Ready4Balfolk.Web/wwwroot"
REFUSALS = "Ready4Balfolk.Web/Contracts/PresentationDtos.cs"

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


def entries(path: Path) -> dict[str, str]:
    """The data entries of a resx, key to text. ElementTree ignores comments, so the template
    examples in the header do not count as entries."""
    root = ET.parse(path).getroot()
    return {
        element.get("name"): element.findtext("value") or ""
        for element in root.findall("data")
        if element.get("name") is not None
    }


# A composite format item: {0}, {1,-8} or {0:d MMMM yyyy}. Escaped braces are removed first, so
# "{{0}}", which formats as the literal text {0}, is not one.
FORMAT_ITEM = re.compile(r"\{(\d+)[^{}]*\}")


def placeholders(text: str) -> set[str]:
    """The indices of the format items in a string."""
    return set(FORMAT_ITEM.findall(text.replace("{{", "").replace("}}", "")))


def placeholder_mismatches(english: dict[str, str], dutch: dict[str, str], where: str) -> list[str]:
    """Every key whose two languages do not ask for the same values."""
    problems = []
    for key in sorted(english.keys() & dutch.keys()):
        theirs, ours = placeholders(english[key]), placeholders(dutch[key])
        if theirs != ours:
            problems.append(
                f"ERROR: {where} '{key}' has placeholders {sorted(theirs)} in English and "
                f"{sorted(ours)} in Dutch")
    return problems


DESIGNER_PROPERTY = re.compile(
    r"public\s+static\s+string\s+([A-Za-z_][A-Za-z0-9_]*)\s*=>\s*"
    r"ResourceManager\.GetString\(\s*\"([^\"]*)\"")
DESIGNER_DECLARATION = re.compile(r"public\s+static\s+string\s+([A-Za-z_][A-Za-z0-9_]*)")


def designer_mismatches(designer: Path, english: set[str], where: str) -> list[str]:
    """Where the hand-written designer file and its English resx disagree. Each property has to
    fetch the key it is named after, every key has to have a property, and every property a key."""
    text = designer.read_text(encoding="utf-8")
    problems = []
    fetched = {}
    for name, key in DESIGNER_PROPERTY.findall(text):
        if name != key:
            problems.append(f"ERROR: {where} property '{name}' fetches the key '{key}'")
        fetched[name] = key
    for name in DESIGNER_DECLARATION.findall(text):
        if name not in fetched:
            # A string property in any other shape would drop out of the comparison below unseen.
            problems.append(
                f"ERROR: {where} declares '{name}' in a shape the check cannot read; write it as "
                f"public static string {name} => ResourceManager.GetString(\"{name}\", Culture)!;")
    keys_fetched = set(fetched.values())
    for key in sorted(keys_fetched - english):
        problems.append(f"ERROR: {where} reads '{key}', which its resx does not define")
    for key in sorted(english - keys_fetched):
        problems.append(f"ERROR: {where} has no property for '{key}', which its resx defines")
    return problems


JS_BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.DOTALL)
JS_LINE_COMMENT = re.compile(r"^\s*//[^\n]*$", re.MULTILINE)
JS_TABLE_OPEN = re.compile(r"^\s*(?:var|let|const)\s+TABLE\s*=\s*\{\s*$")
JS_LANGUAGE_OPEN = re.compile(r"^\s*([a-z]{2})\s*:\s*\{\s*$")
JS_LANGUAGE_CLOSE = re.compile(r"^\s*\},?\s*$")
JS_ENTRY = re.compile(r'^\s*([A-Za-z_$][A-Za-z0-9_$]*)\s*:\s*"((?:\\.|[^"\\])*)"\s*,?\s*$')

# A page asks for a string as t("key") or R4B.t("key"); remote.js binds t to R4B.t.
JS_READ = re.compile(r"""\bt\(\s*["']([A-Za-z_$][A-Za-z0-9_$]*)["']""")


def js_without_comments(text: str) -> str:
    """The script with its comments dropped, so a key named in one is not mistaken for a read."""
    return JS_LINE_COMMENT.sub("", JS_BLOCK_COMMENT.sub("", text))


def strings_js_tables(path: Path) -> tuple[dict[str, dict[str, str]], list[str]]:
    """The language tables of strings.js, language to key to text, and every line inside the table
    the check could not read. A line it cannot read is an error rather than a skip: a string
    written in some other shape would otherwise drop out of every comparison here unseen."""
    tables: dict[str, dict[str, str]] = {}
    problems = []
    in_table, language = False, None
    for line in js_without_comments(path.read_text(encoding="utf-8")).splitlines():
        if not in_table:
            in_table = bool(JS_TABLE_OPEN.match(line))
            continue
        if not line.strip():
            continue
        if language is None:
            opened = JS_LANGUAGE_OPEN.match(line)
            if opened:
                language = opened.group(1)
                tables[language] = {}
            elif line.strip().startswith("}"):
                break
            else:
                problems.append(f"ERROR: {STRINGS_JS} has a table line the check cannot read: {line.strip()}")
            continue
        if JS_LANGUAGE_CLOSE.match(line):
            language = None
            continue
        entry = JS_ENTRY.match(line)
        if entry:
            tables[language][entry.group(1)] = entry.group(2)
        else:
            problems.append(f"ERROR: {STRINGS_JS} has a {language} line the check cannot read: {line.strip()}")
    return tables, problems


def remote_refusals(path: Path) -> set[str]:
    """The codes the hub sends a phone when it refuses a command, each the key of a string."""
    text = path.read_text(encoding="utf-8")
    start = text.find("class RemoteRefusal")
    if start < 0:
        return set()
    end = text.find("\n}", start)
    return set(re.findall(r'const\s+string\s+\w+\s*=\s*"([^"]*)"', text[start:end]))


def strings_js_problems(repo: Path) -> list[str]:
    """Everything wrong with the browser pages' own strings; see the module docstring."""
    tables, problems = strings_js_tables(repo / STRINGS_JS)
    english, dutch = tables.get("en", {}), tables.get("nl", {})
    if not english or not dutch:
        return problems + [f"ERROR: {STRINGS_JS} has no en and nl table the check can read"]
    for language in sorted(tables.keys() - {"en", "nl"}):
        problems.append(f"ERROR: {STRINGS_JS} has a '{language}' table the check does not compare")

    for key in sorted(english.keys() - dutch.keys()):
        problems.append(f"ERROR: {STRINGS_JS} has no Dutch for '{key}'")
    for key in sorted(dutch.keys() - english.keys()):
        problems.append(f"ERROR: {STRINGS_JS} has Dutch for '{key}', which the English table does not define")
    problems.extend(placeholder_mismatches(english, dutch, STRINGS_JS))

    read: set[str] = set()
    for script in sorted((repo / WEB_SCRIPTS).glob("*.js")):
        read.update(JS_READ.findall(js_without_comments(script.read_text(encoding="utf-8"))))
    refusals = remote_refusals(repo / REFUSALS)
    if not refusals:
        problems.append(f"ERROR: {REFUSALS} has no RemoteRefusal codes the check can read")
    for code in sorted(refusals - (english.keys() & dutch.keys())):
        problems.append(f"ERROR: RemoteRefusal sends '{code}', which {STRINGS_JS} does not word in both languages")
    read |= refusals

    for key in sorted(english.keys() - read):
        problems.append(f"ERROR: {STRINGS_JS} defines '{key}', which no page reads")
    if not problems:
        print(f"{STRINGS_JS}: {len(english)} strings, both languages complete.")
    return problems


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
    """Every resx key name the application's own source actually reads, .cs and .axaml alike,
    skipping the designer files and the test projects. A key counts only via a
    UiStrings.TheKey/DomainStrings.TheKey member access or a bare string literal that is exactly
    the key name; see the module docstring."""
    read: set[str] = set()
    for pattern in SOURCE_GLOBS:
        for path in repo.rglob(pattern):
            parts = path.relative_to(repo).parts
            if IGNORED_DIR_PARTS & set(parts) or parts[0] not in APPLICATION_PROJECTS:
                continue
            if path.name.endswith(".Designer.cs"):
                continue
            text = path.read_text(encoding="utf-8")
            if pattern == "*.cs":
                read.update(_cs_readers(text))
            else:
                read.update(_axaml_readers(text))
    return read


# Each call that writes a log line, and the position of the argument that becomes it. Report,
# UnawaitedWork.Start, Handlers.Run and ReportFailures take the English line and, separately, the
# resx text the DJ is shown; only the first of those is the log's.
LOG_LINE_ARGUMENT = {
    "DebugAsync": 0,
    "InfoAsync": 0,
    "WarningAsync": 0,
    "ErrorAsync": 0,
    "CriticalAsync": 0,
    "LogAsync": 1,
    "Report": 0,
    "Start": 0,
    "Run": 0,
    "ReportFailures": 1,
}

LOGGER_CALL = re.compile(
    r"\bHandlers\.(Run)\s*\("
    r"|\.(Start|Report|ReportFailures|DebugAsync|InfoAsync|WarningAsync|ErrorAsync|CriticalAsync|LogAsync)\s*\(")

RESX_ACCESS = re.compile(r"\b(?:UiStrings|DomainStrings)\.[A-Za-z_][A-Za-z0-9_]*")

CHAR_LITERAL = re.compile(r"'(?:\\.|[^'\\\n])*'")

# What the rule has to see, and what it has to leave alone. Run before every scan, so a change to
# the scanner that stops it seeing the thing it exists for fails here rather than passing quietly.
LOGGER_RULE_EXAMPLES = [
    ('loggerService.Report(DomainStrings.Audio_OutputGone, new InvalidOperationException(detail));', 1),
    ('_unawaited.Start(DomainStrings.Queue_AdvanceFailed, () => AdvanceAsync(item, ranOut: true));', 1),
    ('Handlers.Run(UiStrings.Review_PreviewFailed, () => ViewModel.TogglePreviewAsync(selected));', 1),
    ('_ = logger.ErrorAsync(string.Format(CultureInfo.CurrentCulture, UiStrings.Queue_X, name), e);', 1),
    ('_ = logger.WarningAsync($"Refused: {DomainStrings.DanceList_Invalid}");', 1),
    ('command.ReportFailures(logger, UiStrings.Wizard_BackFailed, notifications, UiStrings.Wizard_BackFailed);', 1),
    ('logger.Report("Failed to save settings", notifications, DomainStrings.Settings_SaveFailed, e);', 0),
    ('Handlers.Run("Failed to preview the track", UiStrings.Review_PreviewFailed, async () => { });', 0),
    ('command.ReportFailures(logger, "Failed to go back a step", notifications, UiStrings.Wizard_BackFailed);', 0),
    ('// logger.Report(UiStrings.Review_PreviewFailed, e);', 0),
    ('_ = logger.InfoAsync("UiStrings.Review_PreviewFailed is only text here");', 0),
    ('_ = logger.InfoAsync($"{count} loaded, then {{UiStrings.X}} as text");', 0),
    ('logger.Report("""a " raw UiStrings.X string""", e);', 0),
]


def _starts_string(text: str, at: int) -> bool:
    """Whether a string literal, of any kind, opens at this position."""
    i = at
    while i < len(text) and text[i] in "$@" and i - at < 4:
        i += 1
    return i < len(text) and text[i] == '"'


def _blank(out: list[str], start: int, end: int) -> None:
    for k in range(start, end):
        if out[k] != "\n":
            out[k] = " "


def _skip_string(text: str, start: int, out: list[str]) -> int:
    """Blanks the string literal that opens at `start`, keeps the code in its interpolation holes,
    and returns where the literal ends."""
    i, n = start, len(text)
    while text[i] in "$@":
        i += 1
    prefix = text[start:i]

    quotes = 0
    while i + quotes < n and text[i + quotes] == '"':
        quotes += 1
    if quotes >= 3:
        # A raw string literal, closed by as many quotes as opened it. Its holes are blanked with
        # the rest, which costs nothing: nothing in this application logs through one.
        close = text.find('"' * quotes, i + quotes)
        end = n if close < 0 else close + quotes
        _blank(out, start, end)
        return end

    verbatim, interpolated = "@" in prefix, "$" in prefix
    _blank(out, start, i + 1)
    j = i + 1
    while j < n:
        if verbatim and text.startswith('""', j):
            _blank(out, j, j + 2)
            j += 2
        elif not verbatim and text[j] == "\\":
            _blank(out, j, j + 2)
            j += 2
        elif text[j] == '"':
            _blank(out, j, j + 1)
            return j + 1
        elif interpolated and text.startswith("{{", j):
            _blank(out, j, j + 2)
            j += 2
        elif interpolated and text[j] == "{":
            # A hole, which is code: kept as it is, up to the brace that closes it.
            _blank(out, j, j + 1)
            depth, j = 1, j + 1
            while j < n and depth:
                if _starts_string(text, j):
                    j = _skip_string(text, j, out)
                    continue
                if text[j] == "{":
                    depth += 1
                elif text[j] == "}":
                    depth -= 1
                    if depth == 0:
                        _blank(out, j, j + 1)
                j += 1
        else:
            _blank(out, j, j + 1)
            j += 1
    return n


def code_only(text: str) -> str:
    """The C# with every comment and the contents of every string and char literal blanked out,
    so what is left is the code alone, where it was. The code in an interpolation hole is kept."""
    out = list(text)
    i, n = 0, len(text)
    while i < n:
        if text.startswith("//", i):
            end = text.find("\n", i)
            end = n if end < 0 else end
            _blank(out, i, end)
            i = end
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            end = n if end < 0 else end + 2
            _blank(out, i, end)
            i = end
        elif text[i] == "'":
            match = CHAR_LITERAL.match(text, i)
            end = match.end() if match else i + 1
            _blank(out, i, end)
            i = end
        elif _starts_string(text, i):
            i = _skip_string(text, i, out)
        else:
            i += 1
    return "".join(out)


def _arguments(code: str, open_paren: int) -> list[tuple[int, int]]:
    """Where each top-level argument of the call whose parenthesis opens at open_paren lies."""
    spans, depth, start = [], 0, open_paren + 1
    for i in range(open_paren + 1, len(code)):
        c = code[i]
        if c in "([{":
            depth += 1
        elif c in ")]}":
            if depth == 0:
                if code[start:i].strip():
                    spans.append((start, i))
                return spans
            depth -= 1
        elif c == "," and depth == 0:
            spans.append((start, i))
            start = i + 1
    return spans


def logged_resx_strings(text: str) -> list[tuple[int, str, str]]:
    """Every resx string handed to a logging call as its log line, as (line, call, member access)."""
    code = code_only(text)
    found = []
    for call in LOGGER_CALL.finditer(code):
        name = call.group(1) or call.group(2)
        spans = _arguments(code, call.end() - 1)
        index = LOG_LINE_ARGUMENT[name]
        if index >= len(spans):
            continue
        start, end = spans[index]
        for access in RESX_ACCESS.finditer(code, start, end):
            found.append((code.count("\n", 0, access.start()) + 1, name, access.group(0)))
    return found


def the_logger_rule_sees_its_own_examples() -> bool:
    """Runs the rule over its examples, and says which of them it got wrong."""
    sound = True
    for example, expected in LOGGER_RULE_EXAMPLES:
        if len(logged_resx_strings(example)) != expected:
            verb = "misses" if expected else "flags"
            print(f"ERROR: the logger rule {verb} its own example: {example}")
            sound = False
    return sound


def resx_strings_in_the_log(repo: Path) -> list[str]:
    """Every place the application hands a resx string to the logger as the line it writes."""
    problems = []
    for project in APPLICATION_PROJECTS:
        for path in sorted((repo / project).rglob("*.cs")):
            if IGNORED_DIR_PARTS & set(path.relative_to(repo).parts) or path.name.endswith(".Designer.cs"):
                continue
            for line, call, access in logged_resx_strings(path.read_text(encoding="utf-8")):
                problems.append(
                    f"ERROR: {path.relative_to(repo).as_posix()}:{line} hands {access} to {call} as the "
                    "line it logs. The log is English, a literal where it is logged; the resx text is "
                    "what the DJ is shown, passed beside it.")
    return problems


def main() -> int:
    repo = Path(__file__).resolve().parent.parent
    failed = False
    read = referenced_identifiers(repo)

    if not the_logger_rule_sees_its_own_examples():
        failed = True

    logged = resx_strings_in_the_log(repo)
    for problem in logged:
        print(problem)
        failed = True

    for english_name, dutch_name in PAIRS:
        english_path, dutch_path = repo / english_name, repo / dutch_name
        if not english_path.exists() or not dutch_path.exists():
            print(f"ERROR: missing {english_path if not english_path.exists() else dutch_path}")
            failed = True
            continue

        english_text, dutch_text = entries(english_path), entries(dutch_path)
        english, dutch = set(english_text), set(dutch_text)

        for key in sorted(english - dutch):
            print(f"ERROR: {dutch_name} has no translation for '{key}'")
            failed = True

        for key in sorted(dutch - english):
            print(f"ERROR: {dutch_name} translates '{key}', which {english_name} does not define")
            failed = True

        for key in sorted(english - read):
            print(f"ERROR: {english_name} defines '{key}', which nothing in the application reads")
            failed = True

        designer_name = DESIGNERS[english_name]
        for problem in (placeholder_mismatches(english_text, dutch_text, english_name)
                        + designer_mismatches(repo / designer_name, english, designer_name)):
            print(problem)
            failed = True

        if english == dutch:
            print(f"{english_name}: {len(english)} strings, both languages complete.")

    for problem in strings_js_problems(repo):
        print(problem)
        failed = True

    if not logged:
        print("No resx string is handed to the logger as the line it writes.")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
