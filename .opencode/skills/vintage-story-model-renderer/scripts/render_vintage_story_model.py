#!/usr/bin/env python3
"""Compatibility CLI for the reusable Vintage Story model renderer package."""

import sys
from pathlib import Path

SCRIPT_DIRECTORY = str(Path(__file__).resolve().parent)
if SCRIPT_DIRECTORY not in sys.path:
    sys.path.insert(0, SCRIPT_DIRECTORY)

from vintage_story_model_renderer import *  # noqa: F403


if __name__ == "__main__":
    main()  # noqa: F405
