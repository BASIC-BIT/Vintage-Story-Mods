"""Executable entry point for rendering and optional visual judging."""

from __future__ import annotations

import sys
from pathlib import Path

if __package__:
    from .cli import main as render_main
    from .judge import main as judge_main
else:
    scripts_directory = str(Path(__file__).resolve().parent.parent)
    if scripts_directory not in sys.path:
        sys.path.insert(0, scripts_directory)
    from vintage_story_model_renderer.cli import main as render_main
    from vintage_story_model_renderer.judge import main as judge_main


def main() -> None:
    if len(sys.argv) > 1 and sys.argv[1] == "judge":
        judge_main(sys.argv[2:])
        return
    render_main(sys.argv[1:])


if __name__ == "__main__":
    main()
