from __future__ import annotations

import logging
import shutil
import sqlite3
from datetime import datetime, timedelta
from pathlib import Path

from ..database import Database
from ..operations import CancellationToken, ProgressCallback
from ..paths import AppPaths
from ..settings import AppSettings


class BackupService:
    def __init__(self, database: Database, paths: AppPaths, settings: AppSettings, logger: logging.Logger) -> None:
        self.database = database
        self.paths = paths
        self.settings = settings
        self.logger = logger

    def automatic_backup(
        self,
        force: bool = False,
        token: CancellationToken | None = None,
        progress: ProgressCallback | None = None,
    ) -> Path | None:
        directory = self._automatic_directory()
        interval = max(1, int(self.settings.automatic_backup_interval_days or 3))
        latest = self._latest(directory.glob("TM_auto_*.db"))
        if not force and latest is not None:
            age = datetime.now() - datetime.fromtimestamp(latest.stat().st_mtime)
            if age < timedelta(days=interval):
                return None
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        target = directory / f"TM_auto_{stamp}.db"
        self.database.backup_to(target, token, progress)
        keep = max(1, int(self.settings.automatic_backup_keep or 5))
        self._retain(directory.glob("TM_auto_*.db"), keep)
        self.logger.info("Automatic backup created: %s", target)
        return target

    def manual_backup(
        self,
        target: Path,
        token: CancellationToken | None = None,
        progress: ProgressCallback | None = None,
    ) -> Path:
        path = self.database.backup_to(Path(target), token, progress)
        self.logger.info("Manual backup created: %s", path)
        return path

    def restore(self, source: Path) -> Path:
        source = Path(source)
        if not source.exists():
            raise FileNotFoundError(source)
        with sqlite3.connect(f"file:{source.resolve().as_posix()}?mode=ro", uri=True) as connection:
            result = str(connection.execute("PRAGMA integrity_check").fetchone()[0])
            if result.lower() != "ok":
                raise sqlite3.DatabaseError(f"備份資料庫 integrity_check 失敗：{result}")
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        restore_dir = self.database.path.parent / "RestoreBackups"
        restore_dir.mkdir(parents=True, exist_ok=True)
        before = restore_dir / f"TM_before_restore_{stamp}.db"
        self.database.checkpoint()
        self.database.backup_to(before)
        self._retain(restore_dir.glob("*.db"), 2)
        temporary = self.database.path.with_suffix(".restore.tmp")
        temporary.unlink(missing_ok=True)
        shutil.copy2(source, temporary)
        with sqlite3.connect(temporary) as connection:
            result = str(connection.execute("PRAGMA integrity_check").fetchone()[0])
            if result.lower() != "ok":
                temporary.unlink(missing_ok=True)
                raise sqlite3.DatabaseError(f"還原暫存資料庫 integrity_check 失敗：{result}")
        temporary.replace(self.database.path)
        for suffix in ("-wal", "-shm", "-journal"):
            sidecar = Path(str(self.database.path) + suffix)
            if sidecar.exists() and sidecar.stat().st_size == 0:
                sidecar.unlink(missing_ok=True)
        self.logger.warning("Database restored from %s; previous=%s", source, before)
        return before

    def status(self) -> dict[str, object]:
        directory = self._automatic_directory()
        items = sorted(directory.glob("TM_auto_*.db"), key=lambda path: path.stat().st_mtime, reverse=True)
        interval = max(1, int(self.settings.automatic_backup_interval_days or 3))
        latest = items[0] if items else None
        next_due = datetime.fromtimestamp(latest.stat().st_mtime) + timedelta(days=interval) if latest else datetime.now()
        quick_ok, quick_detail = self.database.quick_check()
        return {
            "database_path": str(self.database.path),
            "database_size_bytes": self.database.path.stat().st_size if self.database.path.exists() else 0,
            "quick_check_ok": quick_ok,
            "quick_check": quick_detail,
            "backup_directory": str(directory),
            "automatic_count": len(items),
            "latest_automatic": str(latest) if latest else "",
            "next_due": next_due.isoformat(timespec="seconds"),
        }

    def _automatic_directory(self) -> Path:
        configured = (self.settings.google_drive_sync_directory or self.settings.backup_directory).strip()
        directory = Path(configured).expanduser() if configured else self.database.path.parent / "Backups"
        directory.mkdir(parents=True, exist_ok=True)
        return directory

    @staticmethod
    def _latest(paths) -> Path | None:
        items = sorted(list(paths), key=lambda path: path.stat().st_mtime, reverse=True)
        return items[0] if items else None

    @staticmethod
    def _retain(paths, keep: int) -> None:
        items = sorted(list(paths), key=lambda path: path.stat().st_mtime, reverse=True)
        for path in items[keep:]:
            path.unlink(missing_ok=True)
