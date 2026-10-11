#!/usr/bin/env python3
"""Every name the documents and the code comments use has to exist in the code they describe.

The development guide has been brought back in line with the code twice in a month, the manuals
once, the contributor files once, and comments beside code that no longer did what the comment said
were found twice. Each round was a person reading and comparing. Nothing in verify.yml read a
document against the code it describes, so the next drift was a matter of time, and it had already
happened again: the guide named a discovery class the code had renamed.

What is held, and to what:

- Every backticked name in README.md, CONTRIBUTING.md, SECURITY.md and documentation/*.md that has
  the shape of a code name (an identifier, a dotted member path, a generic, a call, a file or a
  path) is typed somewhere in the code, or names a file in the repository, or a workflow or a job in
  one. A backticked command line, a flag, a placeholder such as %d or a format string is not a code
  name and is left alone. The failure this catches is a rename: afterwards the old name is nowhere.
  In the two manuals only a file name counts: a manual is written for the DJ, and a backticked word
  there is a tag name or something to type into the search box, not a type in this code.
- Every relative link in those files resolves to a file or a directory in the repository. GitHub's
  own ../../releases and ../../issues are the repository's, not files, and are allowed by name.
- A line-number citation, Foo.cs:123, fails wherever it is written, in a document or in a code
  comment. It is the first thing to go stale, and an issue is where one of those belongs.
- Every backticked name in a C# comment, a line comment or an XML summary, is held to the same rule
  as the documents, over the source of every project. A comment is read by the next person to touch
  the code, which is exactly who a name that no longer exists sends on an errand.
- The keyboard tables in both manuals name every key the main screen's, the library's, the queue's
  and the review screen's handlers switch on, and no key they do not. The media keys are described in prose rather
  than in a table, which is where a reader with such a keyboard looks, and are allowed by name.

A name counts as typed in the code when it appears outside a comment in a source file of any project:
the documents are describing the code, so the code, not the documents, is what they are held to.
String contents count, because a resource key or a stored name is typed as a string and is a name
nonetheless.
"""

import re
import subprocess
import sys
from pathlib import Path

DOCUMENTS = ["README.md", "CONTRIBUTING.md", "SECURITY.md", "documentation"]
MANUALS = {"en": "documentation/help.md", "nl": "documentation/help.nl.md"}

# Where names are typed: every project's source, the scripts, the workflows and the build files.
SOURCE_SUFFIXES = {".cs", ".axaml", ".js", ".py", ".sh", ".ps1", ".yml", ".yaml", ".csproj",
                   ".props", ".targets", ".sln", ".json", ".resx", ".toml", ".xml", ".html", ".css"}
IGNORED_DIR_PARTS = {"obj", "bin", ".git", ".claude", "node_modules"}

# Names the documents use that no source file types, and that are right all the same: placeholders
# the guide uses to describe a shape, and third-party names used as words rather than as code.
ALLOWED_NAMES = {
    # Placeholders the development guide describes shapes with. Anything with Xxx in it is one too.
    "Feature", "Foo", "Bar", "T", "TheKey", "n", "_propHelper", "_prop", "Prop", "TService",
    "TImplementation", "service",
    # Third-party names the documents and the comments use as words, which the code never types:
    # ReactiveUI's, a Wayland protocol's, a SkiaSharp file's, and BigBalfolkList's script.
    "ViewLocator", "ToPropertyEx", "RxApp.DefaultExceptionHandler", "libSkiaSharp.pdb",
    "WaylandPlatformOptions", "app_id", "xdg_toplevel.set_app_id", "convert.py",
}

# A relative link target that is GitHub's rather than a file: the two the README uses.
ALLOWED_LINKS = {"../../releases", "../../issues"}

# The keys the manuals describe in prose rather than in a table, and the one handler that is not a
# screen's keyboard but a picker's, which the manual describes as a list to arrow through.
PROSE_KEYS = {"MediaPlayPause", "MediaNextTrack", "MediaPreviousTrack"}

# The handlers whose keys the manuals' tables must name, and the manuals' names for each key. A key
# missing from the table for its language is a key the reader never learns.
KEY_HANDLERS = [
    "Ready4Balfolk.UI/MainWindow.axaml.cs",
    "Ready4Balfolk.UI/Views/Queue/QueueView.axaml.cs",
    "Ready4Balfolk.UI/Views/TrackCatalog/TrackCatalogView.axaml.cs",
    "Ready4Balfolk.UI/Views/Review/ReviewView.axaml.cs",
]
KEY_NAMES = {
    "en": {"Space": "Space", "Right": "Right", "Left": "Left", "Up": "Up", "Down": "Down",
           "Enter": "Enter", "Delete": "Delete", "Tab": "Tab", "Escape": "Escape", "F": "F", "Z": "Z"},
    "nl": {"Space": "Spatie", "Right": "Rechts", "Left": "Links", "Up": "Omhoog", "Down": "Omlaag",
           "Enter": "Enter", "Delete": "Delete", "Tab": "Tab", "Escape": "Escape", "F": "F", "Z": "Z"},
}

BACKTICKED = re.compile(r"`([^`\n]+)`")
XML_C = re.compile(r"<c>([^<\n]+)</c>")
IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")

# What a backticked text has to look like to be a code name at all. Anything else is a command, a
# flag, a format or a sentence, and is nobody's name.
MEMBER_PATH = re.compile(
    r"^\[?[A-Za-z_][A-Za-z0-9_]*(?:<[A-Za-z0-9_, ]+>)?(?:\.[A-Za-z_][A-Za-z0-9_]*(?:<[A-Za-z0-9_, ]+>)?)*"
    r"(?:\(\))?\]?$")
FILE_NAME = re.compile(r"^\w[\w./-]*\.(?:cs|axaml|js|py|sh|ps1|yml|yaml|csproj|props|targets|sln|json|resx|md|txt|svg|png|ico|toml|xml|html|css)$")
# A code comment or doc line that cites a line number: a file name, a colon, digits.
LINE_CITATION = re.compile(r"\b[\w.-]+\.(?:cs|axaml|js|py|sh|ps1|yml|md|json|resx)\s*:\s*\d+\b")
LINK = re.compile(r"\]\(([^)\s]+)\)")

CS_COMMENT = re.compile(r"//[^\n]*|/\*.*?\*/", re.DOTALL)
CS_STRING_OR_COMMENT = re.compile(
    r"//[^\n]*|/\*.*?\*/|@\$?\"(?:\"\"|[^\"])*\"|\$?\"(?:\\.|[^\"\\\n])*\"", re.DOTALL)
HASH_COMMENT = re.compile(r"(?m)#[^\n]*$")
JS_COMMENT = re.compile(r"//[^\n]*|/\*.*?\*/", re.DOTALL)
XML_COMMENT = re.compile(r"<!--.*?-->", re.DOTALL)
KEY_TOKEN = re.compile(r"\bKey\.([A-Z][A-Za-z0-9]*)")
TABLE_ROW = re.compile(r"^\|\s*([^|]+?)\s*\|", re.MULTILINE)


def tracked_files(repo: Path) -> list[Path]:
    out = subprocess.run(["git", "-C", str(repo), "ls-files", "-z"], check=True, capture_output=True)
    return [repo / name for name in out.stdout.decode("utf-8").split("\0") if name]


def source_files(files: list[Path], repo: Path) -> list[Path]:
    return [
        path for path in files
        if path.suffix in SOURCE_SUFFIXES
        and not IGNORED_DIR_PARTS & set(path.relative_to(repo).parts)
        and not path.name.endswith(".min.js")
    ]


def without_comments(path: Path, text: str) -> str:
    """The source with its comments blanked, so a name that lives only in a comment is not typed."""
    if path.suffix == ".cs":
        return CS_COMMENT.sub(" ", text)
    if path.suffix in {".js", ".css"}:
        return JS_COMMENT.sub(" ", text)
    if path.suffix in {".py", ".sh", ".ps1", ".yml", ".yaml", ".toml"}:
        return HASH_COMMENT.sub(" ", text)
    if path.suffix in {".axaml", ".xml", ".resx", ".html", ".csproj", ".props", ".targets"}:
        return XML_COMMENT.sub(" ", text)
    return text


def typed_code(sources: list[Path]) -> str:
    """Every source file with its comments blanked, as one text."""
    return "\n".join(
        without_comments(path, path.read_text(encoding="utf-8", errors="replace")) for path in sources)


def typed_names(code: str) -> set[str]:
    """Every identifier typed outside a comment, in any source file."""
    return set(IDENTIFIER.findall(code))


def known_paths(files: list[Path], repo: Path) -> set[str]:
    """Every tracked file, by its full relative path, its name, and every directory above it."""
    known: set[str] = set()
    for path in files:
        relative = path.relative_to(repo)
        known.add(relative.as_posix())
        known.add(relative.name)
        for parent in relative.parents:
            if parent.as_posix() != ".":
                known.add(parent.as_posix())
                known.add(parent.as_posix() + "/")
                known.add(parent.name)
    return known


def workflow_names(files: list[Path], repo: Path) -> set[str]:
    """The jobs and the names of every workflow, which the guide refers to by name."""
    names: set[str] = set()
    for path in files:
        if path.relative_to(repo).parts[:2] == (".github", "workflows"):
            text = path.read_text(encoding="utf-8")
            names.update(re.findall(r"(?m)^  ([A-Za-z0-9_-]+):\s*$", text))
            names.update(re.findall(r"(?m)^name:\s*(.+?)\s*$", text))
    return names


def is_code_name(text: str) -> bool:
    return bool(MEMBER_PATH.match(text) or FILE_NAME.match(text))


class Known:
    """What the repository knows a name by: the code, the tracked files and the workflows."""

    def __init__(self, repo: Path, files: list[Path]) -> None:
        sources = source_files(files, repo)
        self.count = len(sources)
        self.code = typed_code(sources)
        self.typed = typed_names(self.code)
        self.paths = known_paths(files, repo)
        self.workflows = workflow_names(files, repo)

    def unknown_parts(self, text: str) -> list[str]:
        """The parts of a backticked code name that nothing in the repository knows."""
        stripped = text.strip("[]")
        if stripped in self.paths or stripped in self.workflows or stripped in ALLOWED_NAMES:
            return []
        if FILE_NAME.match(stripped):
            # A file the repository holds, or one the application writes and so types as a string.
            return [] if Path(stripped).name in self.paths or stripped in self.code else [stripped]
        return [
            part for part in IDENTIFIER.findall(stripped)
            if part not in self.typed and part not in ALLOWED_NAMES and part not in self.paths
            and "Xxx" not in part
        ]


def document_files(repo: Path) -> list[Path]:
    documents: list[Path] = []
    for name in DOCUMENTS:
        path = repo / name
        documents.extend(sorted(path.glob("*.md")) if path.is_dir() else [path])
    return documents


def names_in_documents(repo: Path, known: Known) -> list[str]:
    problems = []
    manuals = {repo / name for name in MANUALS.values()}
    for document in document_files(repo):
        where = document.relative_to(repo).as_posix()
        text = document.read_text(encoding="utf-8")
        for match in BACKTICKED.finditer(text):
            name = match.group(1)
            if not is_code_name(name) or (document in manuals and not FILE_NAME.match(name)):
                continue
            for part in known.unknown_parts(name):
                line = text.count("\n", 0, match.start()) + 1
                problems.append(f"ERROR: {where}:{line} names `{name}`, and nothing in the code is called {part}")
        for match in LINK.finditer(text):
            target = match.group(1)
            if target.startswith(("http://", "https://", "mailto:", "#")) or target in ALLOWED_LINKS:
                continue
            target_path = (document.parent / target.split("#", 1)[0]).resolve()
            if not target_path.exists():
                line = text.count("\n", 0, match.start()) + 1
                problems.append(f"ERROR: {where}:{line} links to {target}, which does not exist")
        for match in LINE_CITATION.finditer(text):
            line = text.count("\n", 0, match.start()) + 1
            problems.append(f"ERROR: {where}:{line} cites a line number, {match.group(0)}, which is stale the moment the file is edited")
    return problems


def comment_spans(path: Path, text: str) -> list[tuple[int, str]]:
    """Each comment in a C# file with the offset it starts at, strings left out so a // inside one
    is not a comment."""
    spans = []
    for match in CS_STRING_OR_COMMENT.finditer(text):
        token = match.group(0)
        if token.startswith("//") or token.startswith("/*"):
            spans.append((match.start(), token))
    return spans


def names_in_comments(repo: Path, files: list[Path], known: Known) -> list[str]:
    problems = []
    for path in source_files(files, repo):
        if path.suffix != ".cs" or path.name.endswith(".Designer.cs"):
            continue
        where = path.relative_to(repo).as_posix()
        text = path.read_text(encoding="utf-8")
        for offset, comment in comment_spans(path, text):
            for pattern in (BACKTICKED, XML_C):
                for match in pattern.finditer(comment):
                    name = match.group(1)
                    if not is_code_name(name):
                        continue
                    for part in known.unknown_parts(name):
                        line = text.count("\n", 0, offset + match.start()) + 1
                        problems.append(f"ERROR: {where}:{line} names `{name}` in a comment, and nothing in the code is called {part}")
            for match in LINE_CITATION.finditer(comment):
                line = text.count("\n", 0, offset + match.start()) + 1
                problems.append(f"ERROR: {where}:{line} cites a line number in a comment, {match.group(0)}")
    return problems


def handler_keys(repo: Path) -> set[str]:
    keys: set[str] = set()
    for name in KEY_HANDLERS:
        text = CS_COMMENT.sub(" ", (repo / name).read_text(encoding="utf-8"))
        keys.update(KEY_TOKEN.findall(text))
    return keys - PROSE_KEYS


def table_keys(manual: str) -> set[str]:
    """Every key named in the first column of a keyboard table: a cell such as "Ctrl+Up, Ctrl+Down"
    names Up and Down, and the modifiers are the table's business."""
    keys: set[str] = set()
    for cell in TABLE_ROW.findall(manual):
        if cell.strip() in {"", "---"} or cell.startswith("-"):
            continue
        for key in re.split(r"[,+]", cell):
            key = key.strip()
            if key and key not in {"Ctrl", "Shift", "Alt"}:
                keys.add(key)
    return keys


def keyboard_problems(repo: Path) -> list[str]:
    problems = []
    handled = handler_keys(repo)
    for language, name in MANUALS.items():
        manual = (repo / name).read_text(encoding="utf-8")
        named = table_keys(manual)
        wording = KEY_NAMES[language]
        for key in sorted(handled):
            if key not in wording:
                problems.append(f"ERROR: the handlers switch on Key.{key}, which this check has no {language} name for")
            elif wording[key] not in named:
                problems.append(f"ERROR: {name} has no keyboard row for {wording[key]} (Key.{key}), which a handler answers")
        code_names = {wording[key] for key in handled if key in wording}
        for key in sorted(named - code_names):
            if IDENTIFIER.fullmatch(key) and key in wording.values():
                problems.append(f"ERROR: {name} has a keyboard row for {key}, which no handler switches on")
    return problems


def main() -> int:
    repo = Path(__file__).resolve().parent.parent
    files = tracked_files(repo)
    known = Known(repo, files)

    problems = (names_in_documents(repo, known)
                + names_in_comments(repo, files, known)
                + keyboard_problems(repo))
    for problem in problems:
        print(problem)
    if not problems:
        print(f"Every name the documents and the comments use exists in the code ({known.count} source files read).")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
