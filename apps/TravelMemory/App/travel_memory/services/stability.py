from __future__ import annotations

import json
import shutil
from datetime import datetime
from pathlib import Path

from ..database import Database
from ..operations import OperationJournal
from ..paths import AppPaths


class PreflightError(RuntimeError):
    pass


def verify_runtime_environment(paths: AppPaths, minimum_free_bytes: int = 256 * 1024 * 1024) -> None:
    paths.ensure()
    for directory in (paths.data, paths.config, paths.logs, paths.diagnostics, paths.runtime):
        probe = directory / ".write_test.tmp"
        try:
            probe.write_bytes(b"TravelMemory")
            probe.unlink()
        except OSError as exc:
            raise PreflightError(f"資料夾無法寫入：{directory}\n{exc}") from exc
    free = shutil.disk_usage(paths.root).free
    if free < minimum_free_bytes:
        raise PreflightError(f"磁碟剩餘空間不足：僅剩 {free / 1024 / 1024:.1f} MiB")


def recover_after_unclean_shutdown(paths: AppPaths, database: Database, journal: OperationJournal) -> Path | None:
    unfinished = journal.unfinished()
    if unfinished is None:
        return None
    actions: list[str] = []
    try:
        database.checkpoint()
        actions.append("WAL checkpoint：完成")
    except Exception as exc:
        actions.append(f"WAL checkpoint：失敗（{exc}）")
    quick_ok, quick_detail = database.quick_check()
    integrity_ok, integrity_detail = database.integrity_check()
    actions.append(f"SQLite quick_check：{'PASS' if quick_ok else 'FAIL'}（{quick_detail}）")
    actions.append(f"SQLite integrity_check：{'PASS' if integrity_ok else 'FAIL'}（{integrity_detail}）")
    removed: list[str] = []
    for directory in (paths.data, paths.cache, paths.runtime):
        if not directory.exists():
            continue
        for pattern in ("*.tmp", "*.restore.tmp", "*.export.tmp"):
            for candidate in directory.rglob(pattern):
                try:
                    candidate.unlink()
                    removed.append(str(candidate))
                except OSError:
                    pass
    actions.append(f"清除程式暫存檔：{len(removed)} 個")
    report = paths.diagnostics / f"TravelMemory_Recovery_{datetime.now():%Y%m%d_%H%M%S}.txt"
    lines = [
        "旅行記憶異常關閉復原報告",
        "=" * 72,
        "中斷作業：" + json.dumps(unfinished, ensure_ascii=False),
        *actions,
    ]
    report.write_text("\n".join(lines), encoding="utf-8")
    return report
