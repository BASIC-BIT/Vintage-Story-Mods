"""Load the relaxed JSON syntax used by Vintage Story asset definitions."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any


_UNQUOTED_KEY = re.compile(r'(?P<prefix>[{,]\s*)(?P<key>[A-Za-z_$][A-Za-z0-9_$-]*)(?=\s*:)')
_TRAILING_COMMA = re.compile(r",(?=\s*[}\]])")


def _strip_comments(text: str) -> str:
    result: list[str] = []
    index = 0
    quote: str | None = None
    escaped = False
    while index < len(text):
        character = text[index]
        following = text[index + 1] if index + 1 < len(text) else ""
        if quote is not None:
            result.append(character)
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == quote:
                quote = None
            index += 1
            continue
        if character in {'"', "'"}:
            quote = character
            result.append(character)
            index += 1
            continue
        if character == "/" and following == "/":
            index += 2
            while index < len(text) and text[index] not in "\r\n":
                index += 1
            continue
        if character == "/" and following == "*":
            index += 2
            while index + 1 < len(text) and text[index:index + 2] != "*/":
                index += 1
            index += 2
            continue
        result.append(character)
        index += 1
    return "".join(result)


def load_vintage_story_json(path: Path) -> dict[str, Any]:
    """Accept strict JSON plus comments, unquoted keys, and trailing commas."""

    text = path.read_text(encoding="utf-8")
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        relaxed = _strip_comments(text)
        relaxed = _UNQUOTED_KEY.sub(r'\g<prefix>"\g<key>"', relaxed)
        relaxed = _TRAILING_COMMA.sub("", relaxed)
        return json.loads(relaxed)
