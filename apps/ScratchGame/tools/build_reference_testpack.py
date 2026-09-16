#!/usr/bin/env python3
"""Build the canonical ThreeStar-Test.scratchpack used by portable packaging."""
from __future__ import annotations

import argparse
import hashlib
import json
import zipfile
from pathlib import Path

DEFAULT_SOURCE = Path(__file__).resolve().parents[1] / "reference-packs" / "ThreeStar-Test"
FIXED_TIMESTAMP = (1980, 1, 1, 0, 0, 0)


def canonical_json_bytes(path: Path) -> bytes:
    value = json.loads(path.read_text(encoding="utf-8"))
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")


def write_member(archive: zipfile.ZipFile, name: str, data: bytes) -> None:
    info = zipfile.ZipInfo(name, date_time=FIXED_TIMESTAMP)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.create_system = 3
    info.external_attr = 0o100644 << 16
    archive.writestr(
        info,
        data,
        compress_type=zipfile.ZIP_DEFLATED,
        compresslevel=9,
    )


def build(source: Path, output: Path) -> tuple[int, str]:
    members = [
        ("manifest.json", source / "manifest.json"),
        ("ticket.json", source / "ticket.json"),
    ]
    for _, path in members:
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError(f"Missing reference-pack source: {path}")

    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        output.unlink()

    with zipfile.ZipFile(output, "w") as archive:
        for name, path in members:
            write_member(archive, name, canonical_json_bytes(path))

    data = output.read_bytes()
    return len(data), hashlib.sha256(data).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        size, digest = build(args.source.resolve(), args.output.resolve())
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Reference TestPack build aborted: {exc}")
        return 2
    print(f"Created: {args.output.resolve()}")
    print(f"Bytes: {size}")
    print(f"SHA-256: {digest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
