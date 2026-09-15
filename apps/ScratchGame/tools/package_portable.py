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
    "BuiltInAssets/Tickets/gameType1/01-red.png",
    "BuiltInAssets/Tickets/gameType1/01-blue.png",
    "BuiltInAssets/Tickets/gameType1/02.png",
    "BuiltInAssets/Foils/brushed-silver-plain.png",
    "BuiltInAssets/Foils/brushed-silver-three-star.png",
    "UI/grant-overlay-01.png",
    "Audio/small-win-manual.wav",
    "Audio/small-win-auto.wav",
    "Audio/big-win-manual.wav",
    "Audio/big-win-auto.wav",
    "Audio/wallet-grant.wav",
    "Audio/lose.wav",
)

# Pre-V0.4 local databases may still reference these legacy ticket assets.
# They are copied when available, but are no longer part of the V0.4 ticket-definition pipeline.
OPTIONAL_LEGACY_ASSETS = (
    "Tickets/ThreeStar/ticket.png",
    "Tickets/ThreeStar/ticket-100.png",
    "Tickets/ThreeStar/silver-star.png",
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

        copied: list[str] = ["ScratchGame.exe"]
        for relative in REQUIRED_ASSETS:
            source = assets / relative
            destination = package_root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            copied.append(relative)

        for relative in OPTIONAL_LEGACY_ASSETS:
            source = assets / relative
            if not source.is_file() or source.stat().st_size == 0:
                continue
            destination = package_root / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            copied.append(relative)

        built_in_packs = assets / "BuiltInPacks"
        if built_in_packs.is_dir():
            for source in sorted(built_in_packs.glob("*.scratchpack")):
                if not source.is_file() or source.stat().st_size == 0:
                    continue
                relative = Path("BuiltInPacks") / source.name
                destination = package_root / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, destination)
                copied.append(relative.as_posix())

        manifest = package_root / "PACKAGE_CONTENTS.txt"
        manifest.write_text(
            "ScratchGame portable package\n"
            "Required runtime resources verified before packaging.\n\n"
            + "\n".join(copied)
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
