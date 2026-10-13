from __future__ import annotations

import importlib.util
import json
import platform
import sys
from datetime import datetime
from pathlib import Path

from .. import __version__
from ..database import SCHEMA_VERSION, Database
from ..paths import AppPaths, bundled_sample_paths
from .riti_parser import validate_reference_files


def run_self_test(paths: AppPaths, database: Database) -> tuple[bool, Path]:
    checks: list[tuple[str, bool, str]] = []
    version_tuple = tuple(map(int, platform.python_version_tuple()[:2]))
    checks.append(("Python 3.12", version_tuple == (3, 12), platform.python_version()))
    checks.append(("64-bit runtime", platform.architecture()[0] == "64bit", platform.architecture()[0]))
    checks.append(("PySide6", importlib.util.find_spec("PySide6") is not None, "module"))
    checks.append(("timezonefinder", importlib.util.find_spec("timezonefinder") is not None, "module"))
    checks.append(("Map HTML", (paths.map_resources / "index.html").exists(), str(paths.map_resources / "index.html")))
    checks.append(("World fallback", (paths.map_resources / "world.svg").exists(), str(paths.map_resources / "world.svg")))
    checks.append(("Geography seed", (paths.geography_resources / "places_seed.csv").exists(), str(paths.geography_resources)))
    ok, detail = database.quick_check()
    checks.append(("SQLite quick_check", ok, detail))
    integrity_ok, integrity_detail = database.integrity_check()
    checks.append(("SQLite integrity_check", integrity_ok, integrity_detail))
    with database.connect(readonly=True) as connection:
        row = connection.execute("SELECT value FROM app_meta WHERE key='schema_version'").fetchone()
        actual_schema = int(row[0]) if row else 0
    checks.append(("SQLite schema", actual_schema == SCHEMA_VERSION, f"{actual_schema}/{SCHEMA_VERSION}"))

    samples = bundled_sample_paths()
    if all(path.exists() for path in samples):
        reports = validate_reference_files(samples)
        for name, item in reports.items():
            checks.append((f"RITI {name}", bool(item["passed"]), json.dumps(item, ensure_ascii=False)))
    else:
        checks.append(("RITI samples", False, "Samples/260530.txt 或 260602.txt 不存在"))

    passed = all(item[1] for item in checks)
    paths.diagnostics.mkdir(parents=True, exist_ok=True)
    output = paths.diagnostics / f"TravelMemory_SelfTest_{datetime.now():%Y%m%d_%H%M%S}.txt"
    lines = [
        f"旅行記憶 {__version__} 安裝檢查",
        f"Executable: {sys.executable}",
        "=" * 72,
    ]
    lines.extend(f"{'PASS' if success else 'FAIL'} | {name} | {detail}" for name, success, detail in checks)
    lines.append("=" * 72)
    lines.append(f"整體結果：{'PASS' if passed else 'FAIL'}")
    output.write_text("\n".join(lines), encoding="utf-8")
    return passed, output


def write_release_check(paths: AppPaths, database: Database, output: Path) -> bool:
    passed, self_test = run_self_test(paths, database)
    content = self_test.read_text(encoding="utf-8")
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        f"TravelMemory Release Check\nVersion: {__version__}\nSchema: {SCHEMA_VERSION}\n\n{content}\n",
        encoding="utf-8",
    )
    return passed


def run_webengine_smoke(paths: AppPaths, output: Path) -> bool:
    """Load the real local map page in QtWebEngine and verify the JS health contract."""
    from PySide6.QtCore import QEventLoop, QTimer
    from PySide6.QtWidgets import QApplication
    from ..ui.map_widget import MapWidget

    app = QApplication.instance() or QApplication([])
    view = MapWidget(paths.map_resources, paths.map_cache)
    loop = QEventLoop()
    result: dict[str, object] = {"passed": False, "detail": "timeout"}

    def finish(passed: bool, detail: object) -> None:
        result["passed"] = passed
        result["detail"] = detail
        loop.quit()

    def loaded(ok: bool) -> None:
        if not ok:
            finish(False, "loadFinished=false")
            return
        view.set_provider("fallback")
        view.set_tracks(
            [
                {
                    "id": 1,
                    "name": "smoke",
                    "date": "2026-01-01",
                    "color": "#2F80ED",
                    "segments": [
                        {
                            "index": 0,
                            "points": [
                                {"lat": 25.0, "lon": 121.0, "gap_before": False},
                                {"lat": 25.1, "lon": 121.1, "gap_before": False},
                            ],
                        }
                    ],
                }
            ]
        )

        def check() -> None:
            view.page().runJavaScript(
                "window.tmHealth ? window.tmHealth() : null",
                lambda value: finish(bool(isinstance(value, dict) and value.get("ready")), value),
            )

        QTimer.singleShot(800, check)

    view.loadFinished.connect(loaded)
    QTimer.singleShot(15000, lambda: finish(False, "timeout"))
    view.show()
    loop.exec()
    view.shutdown()
    view.close()
    app.processEvents()
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        "TravelMemory QtWebEngine Smoke Test\n"
        f"Version: {__version__}\n"
        f"Result: {'PASS' if result['passed'] else 'FAIL'}\n"
        f"Detail: {json.dumps(result['detail'], ensure_ascii=False)}\n",
        encoding="utf-8",
    )
    return bool(result["passed"])
