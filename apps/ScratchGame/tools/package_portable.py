#!/usr/bin/env python3
"""Build and verify a complete ScratchGame portable ZIP.

Runtime artwork/audio intentionally live outside the repository. The packager
accepts either an unpacked runtime-asset directory or a previously approved
portable/asset ZIP. It selects only files declared by runtime-assets.json, so
old executables and unrelated undeclared files are never inherited.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import tempfile
import zipfile
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Iterable

DEFAULT_ASSET_MANIFEST = Path(__file__).resolve().parents[1] / "runtime-assets.json"


@dataclass(frozen=True)
class AssetRecord:
    path: str
    size: int
    sha256: str


@dataclass(frozen=True)
class AssetManifest:
    source_baseline: str
    required: tuple[AssetRecord, ...]
    optional_legacy: tuple[AssetRecord, ...]
    built_in_packs: tuple[AssetRecord, ...]
    test_packs: tuple[AssetRecord, ...]


@dataclass(frozen=True)
class AssetSource:
    root: Path
    description: str


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_zip_member(archive: zipfile.ZipFile, name: str) -> str:
    digest = hashlib.sha256()
    with archive.open(name, "r") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def parse_record(raw: object, section: str) -> AssetRecord:
    if not isinstance(raw, dict):
        raise ValueError(f"{section} entries must be JSON objects.")

    path = raw.get("path")
    size = raw.get("size")
    sha256 = raw.get("sha256")

    if not isinstance(path, str) or not path or "\\" in path:
        raise ValueError(f"Invalid asset path in {section}: {path!r}")
    pure = PurePosixPath(path)
    if pure.is_absolute() or any(part in {"", ".", ".."} for part in pure.parts):
        raise ValueError(f"Unsafe asset path in {section}: {path!r}")
    if not isinstance(size, int) or size <= 0:
        raise ValueError(f"Invalid asset size for {path}: {size!r}")
    if (
        not isinstance(sha256, str)
        or len(sha256) != 64
        or any(ch not in "0123456789abcdefABCDEF" for ch in sha256)
    ):
        raise ValueError(f"Invalid SHA-256 for {path}.")

    return AssetRecord(path=path, size=size, sha256=sha256.lower())


def load_asset_manifest(path: Path) -> AssetManifest:
    if not path.is_file():
        raise ValueError(f"Runtime asset manifest not found: {path}")

    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"Cannot read runtime asset manifest: {exc}") from exc

    if not isinstance(raw, dict) or raw.get("schemaVersion") != 1:
        raise ValueError("runtime-assets.json must use schemaVersion 1.")

    baseline = raw.get("sourceBaseline")
    if not isinstance(baseline, str) or not baseline.strip():
        raise ValueError("runtime-assets.json requires a non-empty sourceBaseline.")

    required = tuple(parse_record(item, "required") for item in raw.get("required", []))
    optional = tuple(
        parse_record(item, "optionalLegacy")
        for item in raw.get("optionalLegacy", [])
    )
    built_in = tuple(
        parse_record(item, "builtInPacks")
        for item in raw.get("builtInPacks", [])
    )
    test_packs = tuple(
        parse_record(item, "testPacks")
        for item in raw.get("testPacks", [])
    )

    if not required:
        raise ValueError("runtime-assets.json must declare at least one required asset.")

    all_paths = [
        record.path
        for record in (*required, *optional, *built_in, *test_packs)
    ]
    if len(all_paths) != len(set(all_paths)):
        raise ValueError("runtime-assets.json contains duplicate paths.")

    for record in built_in:
        if (
            not record.path.startswith("BuiltInPacks/")
            or not record.path.lower().endswith(".scratchpack")
        ):
            raise ValueError(
                f"Built-in pack must live under BuiltInPacks/*.scratchpack: {record.path}"
            )

    for record in test_packs:
        if (
            not record.path.startswith("TestPacks/")
            or not record.path.lower().endswith(".scratchpack")
        ):
            raise ValueError(
                f"Test pack must live under TestPacks/*.scratchpack: {record.path}"
            )

    return AssetManifest(
        source_baseline=baseline.strip(),
        required=required,
        optional_legacy=optional,
        built_in_packs=built_in,
        test_packs=test_packs,
    )


def validate_file(path: Path, record: AssetRecord) -> None:
    if not path.is_file():
        raise ValueError(f"Missing runtime asset: {record.path}")
    actual_size = path.stat().st_size
    if actual_size != record.size:
        raise ValueError(
            f"Runtime asset size mismatch: {record.path} "
            f"(expected {record.size}, got {actual_size})"
        )
    actual_sha = sha256_file(path)
    if actual_sha != record.sha256:
        raise ValueError(
            f"Runtime asset SHA-256 mismatch: {record.path} "
            f"(expected {record.sha256}, got {actual_sha})"
        )


def find_asset_zip_prefix(names: set[str], required_paths: tuple[str, ...]) -> str | None:
    first = required_paths[0]
    candidates: set[str] = set()

    if first in names:
        candidates.add("")

    suffix = "/" + first
    for name in names:
        if name.endswith(suffix):
            candidates.add(name[: -len(first)])

    valid = [
        prefix
        for prefix in sorted(candidates)
        if all(prefix + relative in names for relative in required_paths)
    ]
    if len(valid) == 1:
        return valid[0]
    return None


def extract_asset_zip(
    source_zip: Path,
    destination: Path,
    manifest: AssetManifest,
) -> AssetSource:
    if not source_zip.is_file() or source_zip.stat().st_size == 0:
        raise ValueError(f"Asset ZIP does not exist or is empty: {source_zip}")

    with zipfile.ZipFile(source_zip, "r") as archive:
        file_names = {
            info.filename
            for info in archive.infolist()
            if not info.is_dir()
        }
        required_paths = tuple(record.path for record in manifest.required)
        prefix = find_asset_zip_prefix(file_names, required_paths)
        if prefix is None:
            raise ValueError(
                "Asset ZIP must contain exactly one complete runtime asset tree. "
                "Expected the required files either at ZIP root or under one package folder."
            )

        selected: list[AssetRecord] = list(manifest.required)
        selected.extend(
            record
            for record in manifest.optional_legacy
            if prefix + record.path in file_names
        )
        selected.extend(manifest.built_in_packs)
        selected.extend(manifest.test_packs)

        for record in selected:
            source_name = prefix + record.path
            if source_name not in file_names:
                if record in manifest.optional_legacy:
                    continue
                raise ValueError(f"Asset ZIP is missing declared file: {record.path}")

            target = destination / PurePosixPath(record.path)
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(source_name, "r") as src, target.open("wb") as dst:
                shutil.copyfileobj(src, dst)
            validate_file(target, record)

    return AssetSource(
        root=destination,
        description=f"ZIP:{source_zip.name}",
    )


def validate_asset_directory(root: Path, manifest: AssetManifest) -> AssetSource:
    if not root.is_dir():
        raise ValueError(f"Asset directory does not exist: {root}")

    for record in manifest.required:
        validate_file(root / PurePosixPath(record.path), record)

    for record in manifest.optional_legacy:
        path = root / PurePosixPath(record.path)
        if path.exists():
            validate_file(path, record)

    for record in manifest.built_in_packs:
        validate_file(root / PurePosixPath(record.path), record)

    for record in manifest.test_packs:
        validate_file(root / PurePosixPath(record.path), record)

    return AssetSource(root=root, description=f"DIR:{root.name}")


def collect_asset_records(
    source: AssetSource,
    manifest: AssetManifest,
) -> tuple[AssetRecord, ...]:
    records: list[AssetRecord] = list(manifest.required)

    for record in manifest.optional_legacy:
        if (source.root / PurePosixPath(record.path)).is_file():
            records.append(record)

    records.extend(manifest.built_in_packs)
    records.extend(manifest.test_packs)
    return tuple(records)


def validate_folder_name(folder_name: str) -> None:
    candidate = PurePosixPath(folder_name)
    if (
        not folder_name
        or candidate.is_absolute()
        or len(candidate.parts) != 1
        or candidate.parts[0] in {".", ".."}
        or "\\" in folder_name
    ):
        raise ValueError("--folder-name must be one safe folder name.")


def build_package_manifest(
    *,
    copied: Iterable[str],
    version: str | None,
    build: str | None,
    asset_source: str,
    asset_manifest: Path,
    asset_manifest_sha256: str,
    source_baseline: str,
) -> str:
    lines = [
        "ScratchGame portable package",
        "Required runtime resources verified before packaging.",
    ]
    if version:
        lines.append(f"VERSION: {version}")
    if build is not None:
        lines.append(f"BUILD: {build}")
    lines.extend(
        [
            f"Runtime asset baseline: {source_baseline}",
            f"Asset source: {asset_source}",
            f"Asset manifest: {asset_manifest.name}",
            f"Asset manifest SHA-256: {asset_manifest_sha256}",
            "",
        ]
    )
    lines.extend(copied)
    lines.append("")
    return "\n".join(lines)


def verify_output(
    output: Path,
    folder_name: str,
    expected_sources: dict[str, Path],
    manifest_bytes: bytes,
) -> tuple[int, str]:
    expected_relative = set(expected_sources)
    expected_relative.add("PACKAGE_CONTENTS.txt")
    expected_archive = {f"{folder_name}/{name}" for name in expected_relative}

    with zipfile.ZipFile(output, "r") as archive:
        infos = [info for info in archive.infolist() if not info.is_dir()]
        names = [info.filename for info in infos]

        if len(names) != len(set(names)):
            raise ValueError("Portable ZIP contains duplicate file names.")

        actual = set(names)
        missing = sorted(expected_archive - actual)
        unexpected = sorted(actual - expected_archive)
        if missing or unexpected:
            details = []
            if missing:
                details.append("missing=" + ", ".join(missing))
            if unexpected:
                details.append("unexpected=" + ", ".join(unexpected))
            raise ValueError("Portable ZIP structure mismatch: " + "; ".join(details))

        forbidden = [
            name
            for name in names
            if name.endswith("/TEST_STEPS.txt")
            or name.endswith("/NOT_A_PORTABLE_PACKAGE.txt")
        ]
        if forbidden:
            raise ValueError(
                "Portable ZIP inherited test/build-only files: "
                + ", ".join(sorted(forbidden))
            )

        for relative, source in expected_sources.items():
            archived = f"{folder_name}/{relative}"
            if sha256_zip_member(archive, archived) != sha256_file(source):
                raise ValueError(f"Packaged bytes do not match source: {relative}")

        manifest_name = f"{folder_name}/PACKAGE_CONTENTS.txt"
        if archive.read(manifest_name) != manifest_bytes:
            raise ValueError("PACKAGE_CONTENTS.txt changed while packaging.")

    return len(expected_archive), sha256_file(output)


def build_package(args: argparse.Namespace) -> tuple[int, str]:
    scratchgame_exe = args.exe.resolve()
    pack_editor_exe = args.pack_editor_exe.resolve()
    asset_manifest_path = args.asset_manifest.resolve()
    output = args.output.resolve()
    validate_folder_name(args.folder_name)

    missing_exes = [
        str(path)
        for path in (scratchgame_exe, pack_editor_exe)
        if not path.is_file() or path.stat().st_size == 0
    ]
    if missing_exes:
        raise ValueError(
            "Missing required executable(s):\n"
            + "\n".join(f"  - {item}" for item in missing_exes)
        )
    if scratchgame_exe == pack_editor_exe:
        raise ValueError("ScratchGame.exe and PackEditor.exe must be different input files.")

    asset_manifest = load_asset_manifest(asset_manifest_path)
    asset_manifest_sha = sha256_file(asset_manifest_path)

    with tempfile.TemporaryDirectory(prefix="scratchgame-package-") as temp_value:
        temp = Path(temp_value)

        if args.assets is not None:
            asset_source = validate_asset_directory(
                args.assets.resolve(),
                asset_manifest,
            )
        else:
            asset_source = extract_asset_zip(
                args.assets_zip.resolve(),
                temp / "_asset-source",
                asset_manifest,
            )

        asset_records = collect_asset_records(asset_source, asset_manifest)
        package_root = temp / args.folder_name
        package_root.mkdir(parents=True)

        sources: dict[str, Path] = {
            "ScratchGame.exe": scratchgame_exe,
            "PackEditor.exe": pack_editor_exe,
        }
        for record in asset_records:
            sources[record.path] = asset_source.root / PurePosixPath(record.path)

        for relative, source in sources.items():
            destination = package_root / PurePosixPath(relative)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)

        package_manifest_text = build_package_manifest(
            copied=sources.keys(),
            version=args.version,
            build=args.build,
            asset_source=asset_source.description,
            asset_manifest=asset_manifest_path,
            asset_manifest_sha256=asset_manifest_sha,
            source_baseline=asset_manifest.source_baseline,
        )
        package_manifest_bytes = package_manifest_text.encode("utf-8")
        (package_root / "PACKAGE_CONTENTS.txt").write_bytes(package_manifest_bytes)

        output.parent.mkdir(parents=True, exist_ok=True)
        if output.exists():
            output.unlink()

        with zipfile.ZipFile(
            output,
            "w",
            compression=zipfile.ZIP_DEFLATED,
            compresslevel=9,
        ) as archive:
            for path in sorted(package_root.rglob("*")):
                if path.is_file():
                    archive.write(path, path.relative_to(temp).as_posix())

        return verify_output(
            output,
            args.folder_name,
            sources,
            package_manifest_bytes,
        )


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--exe",
        required=True,
        type=Path,
        help="Published ScratchGame.exe",
    )
    parser.add_argument(
        "--pack-editor-exe",
        required=True,
        type=Path,
        help="Published PackEditor.exe",
    )

    asset_group = parser.add_mutually_exclusive_group(required=True)
    asset_group.add_argument(
        "--assets",
        type=Path,
        help="Directory containing the external runtime asset tree",
    )
    asset_group.add_argument(
        "--assets-zip",
        type=Path,
        help="Approved portable/asset ZIP containing one complete runtime asset tree",
    )

    parser.add_argument(
        "--asset-manifest",
        type=Path,
        default=DEFAULT_ASSET_MANIFEST,
        help="JSON manifest containing exact size/SHA-256 for packageable assets",
    )
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--folder-name", default="ScratchGame")
    parser.add_argument("--version")
    parser.add_argument("--build")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        file_count, digest = build_package(args)
    except (OSError, ValueError, zipfile.BadZipFile) as exc:
        print(f"Portable package aborted: {exc}")
        return 2

    print(f"Created: {args.output.resolve()}")
    print(f"Verified files: {file_count}")
    print(f"SHA-256: {digest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
