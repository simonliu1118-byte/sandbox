#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

import package_portable


class PackagePortableTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp = tempfile.TemporaryDirectory(prefix="scratchgame-packager-test-")
        self.root = Path(self._temp.name)
        self.assets = self.root / "assets"
        self.assets.mkdir()
        self.required = [
            "Themes/Default/Frame/header_bg.png",
            "Themes/Default/Frame/footer_bg.png",
            "Audio/lose.wav",
        ]
        self.optional = ["Tickets/ThreeStar/ticket.png"]
        self.built_in = ["BuiltInPacks/Official.scratchpack"]

        for relative in [*self.required, *self.optional, *self.built_in]:
            path = self.assets / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes((relative + "\n").encode("utf-8") * 3)

        test_pack = self.assets / "TestPacks" / "NeverShip.scratchpack"
        test_pack.parent.mkdir(parents=True)
        test_pack.write_bytes(b"test-only")

        self.asset_manifest = self.root / "runtime-assets.json"
        self.asset_manifest.write_text(
            json.dumps(
                {
                    "schemaVersion": 1,
                    "sourceBaseline": "unit-test-baseline",
                    "required": [self._record(path) for path in self.required],
                    "optionalLegacy": [self._record(path) for path in self.optional],
                    "builtInPacks": [self._record(path) for path in self.built_in],
                },
                indent=2,
            ),
            encoding="utf-8",
        )

        self.scratchgame = self.root / "ScratchGame.exe"
        self.pack_editor = self.root / "PackEditor.exe"
        self.scratchgame.write_bytes(b"scratchgame-current")
        self.pack_editor.write_bytes(b"packeditor-current")

    def tearDown(self) -> None:
        self._temp.cleanup()

    def _record(self, relative: str) -> dict[str, object]:
        path = self.assets / relative
        data = path.read_bytes()
        return {
            "path": relative,
            "size": len(data),
            "sha256": hashlib.sha256(data).hexdigest(),
        }

    def _args(
        self,
        *,
        output: Path,
        assets: Path | None = None,
        assets_zip: Path | None = None,
        scratchgame: Path | None = None,
        pack_editor: Path | None = None,
    ):
        argv = [
            "--exe",
            str(scratchgame or self.scratchgame),
            "--pack-editor-exe",
            str(pack_editor or self.pack_editor),
            "--asset-manifest",
            str(self.asset_manifest),
            "--output",
            str(output),
            "--version",
            "0.5.3",
            "--build",
            "0",
        ]
        if assets_zip is not None:
            argv.extend(["--assets-zip", str(assets_zip)])
        else:
            argv.extend(["--assets", str(assets or self.assets)])
        return package_portable.parse_args(argv)

    def test_directory_source_builds_exact_whitelist(self) -> None:
        output = self.root / "directory-source.zip"
        count, _ = package_portable.build_package(
            self._args(output=output, assets=self.assets)
        )

        with zipfile.ZipFile(output, "r") as archive:
            names = set(archive.namelist())
            self.assertIn("ScratchGame/ScratchGame.exe", names)
            self.assertIn("ScratchGame/PackEditor.exe", names)
            self.assertIn("ScratchGame/PACKAGE_CONTENTS.txt", names)
            self.assertIn(
                "ScratchGame/BuiltInPacks/Official.scratchpack",
                names,
            )
            self.assertNotIn(
                "ScratchGame/TestPacks/NeverShip.scratchpack",
                names,
            )
            manifest = archive.read(
                "ScratchGame/PACKAGE_CONTENTS.txt"
            ).decode("utf-8")
            self.assertIn("VERSION: 0.5.3", manifest)
            self.assertIn("BUILD: 0", manifest)
            self.assertIn("unit-test-baseline", manifest)

        expected = 2 + len(self.required) + len(self.optional) + len(self.built_in) + 1
        self.assertEqual(expected, count)

    def test_portable_zip_can_be_asset_source_without_inheriting_old_exes(self) -> None:
        old_scratchgame = self.root / "old-ScratchGame.exe"
        old_pack_editor = self.root / "old-PackEditor.exe"
        old_scratchgame.write_bytes(b"old scratchgame")
        old_pack_editor.write_bytes(b"old packeditor")

        source_zip = self.root / "old-portable.zip"
        package_portable.build_package(
            self._args(
                output=source_zip,
                assets=self.assets,
                scratchgame=old_scratchgame,
                pack_editor=old_pack_editor,
            )
        )

        output = self.root / "rebuilt.zip"
        package_portable.build_package(
            self._args(output=output, assets_zip=source_zip)
        )

        with zipfile.ZipFile(output, "r") as archive:
            self.assertEqual(
                self.scratchgame.read_bytes(),
                archive.read("ScratchGame/ScratchGame.exe"),
            )
            self.assertEqual(
                self.pack_editor.read_bytes(),
                archive.read("ScratchGame/PackEditor.exe"),
            )
            self.assertNotEqual(
                old_scratchgame.read_bytes(),
                archive.read("ScratchGame/ScratchGame.exe"),
            )

    def test_missing_packeditor_is_rejected(self) -> None:
        output = self.root / "missing-editor.zip"
        missing = self.root / "missing-PackEditor.exe"
        with self.assertRaisesRegex(ValueError, "Missing required executable"):
            package_portable.build_package(
                self._args(
                    output=output,
                    assets=self.assets,
                    pack_editor=missing,
                )
            )
        self.assertFalse(output.exists())

    def test_asset_hash_mismatch_is_rejected(self) -> None:
        target = self.assets / self.required[0]
        target.write_bytes(target.read_bytes() + b"changed")

        output = self.root / "bad-assets.zip"
        with self.assertRaisesRegex(ValueError, "size mismatch|SHA-256 mismatch"):
            package_portable.build_package(
                self._args(output=output, assets=self.assets)
            )
        self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
