#!/usr/bin/env python3
"""Build a ScratchGame portable ZIP and refuse incomplete runtime assets."""
from __future__ import annotations

import argparse
import shutil
import tempfile
import zipfile
from pathlib import Path

REQUIRED_ASSETS = (
    "Themes/Default/Frame/header_bg.png",
    "Themes/Default/Frame/footer_bg.png",
    "Themes/Default/Stage/stage_bg.png",
    "Tickets/ThreeStar/ticket.png",
    "Tickets/ThreeStar/ticket-100.png",
    "Tickets/ThreeStar/silver-star.png",
    "Audio/small-win-manual.wav",
    "Audio/small-win-auto.wav",
    "Audio/big-win-manual.wav",
    "Audio/big-win-auto.wav",
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--exe", required=True, type=Path)
    parser.add_argument("--assets", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--folder-name", default="ScratchGame")
    args = parser.parse_args()

    exe = args.exe.resolve()
    assets = args.assets.resolve()
    output = args.output.resolve()

    missing: list[str] = []
    if not exe.is_file() or exe.stat().st_size == 0:
        missing.append(str(exe))
    for relative in REQUIRED_ASSETS:
        path = assets / relative
        if not path.is_file() or path.stat().st_size == 0:
            missing.append(relative)

    if missing:
        print("Portable package aborted. Missing required runtime files:")
        for item in missing:
            print(f"  - {item}")
        return 2

    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        output.unlink()

    with tempfile.TemporaryDirectory(prefix="scratchgame-package-") as temp:
        package_root = Path(temp) / args.folder_name
        package_root.mkdir(parents=True)
        shutil.copy2(exe, package_root / "ScratchGame.exe")

        for relative in REQUIRED_ASSETS:
            source = assets / relative
            destination = package_root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)

        manifest = package_root / "PACKAGE_CONTENTS.txt"
        manifest.write_text(
            "ScratchGame portable package\n"
            "Required runtime resources verified before packaging.\n\n"
            + "\n".join(["ScratchGame.exe", *REQUIRED_ASSETS])
            + "\n",
            encoding="utf-8",
        )

        with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
            for path in sorted(package_root.rglob("*")):
                if path.is_file():
                    archive.write(path, path.relative_to(Path(temp)))

    print(f"Created: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
