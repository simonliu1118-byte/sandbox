from __future__ import annotations

import json
import shutil
from dataclasses import asdict, dataclass, fields
from pathlib import Path

from .paths import AppPaths


@dataclass(slots=True)
class AppSettings:
    database_path: str = ""
    backup_directory: str = ""
    google_drive_sync_directory: str = ""
    automatic_backup_keep: int = 5
    automatic_backup_interval_days: int = 3
    map_provider: str = "auto"
    show_points: bool = True
    show_endpoints: bool = True
    show_segment_connectors: bool = True
    line_width: float = 2.4
    point_size: float = 3.0
    gap_display_seconds: int = 120
    max_map_points: int = 30000
    map_cache_max_bytes: int = 536870912
    last_trip_id: int | None = None
    window_geometry: str = ""
    splitter_state: str = ""


class SettingsService:
    def __init__(self, paths: AppPaths) -> None:
        self.paths = paths

    def load(self) -> AppSettings:
        self.paths.ensure()
        path = self.paths.settings
        if not path.exists():
            settings = AppSettings(database_path="")
            self.save(settings)
            return settings
        try:
            raw = json.loads(path.read_text(encoding="utf-8"))
            valid = {item.name for item in fields(AppSettings)}
            settings = AppSettings(**{k: v for k, v in raw.items() if k in valid})
            return settings
        except Exception:
            broken = path.with_name(path.name + ".broken")
            try:
                shutil.copy2(path, broken)
            except OSError:
                pass
            backup = path.with_suffix(".json.bak")
            if backup.exists():
                try:
                    raw = json.loads(backup.read_text(encoding="utf-8"))
                    valid = {item.name for item in fields(AppSettings)}
                    recovered = AppSettings(**{k: v for k, v in raw.items() if k in valid})
                    self.save(recovered)
                    return recovered
                except Exception:
                    pass
            settings = AppSettings(database_path="")
            self.save(settings)
            return settings

    def save(self, settings: AppSettings) -> None:
        self.paths.ensure()
        path = self.paths.settings
        backup = path.with_suffix(".json.bak")
        temporary = path.with_suffix(".json.tmp")
        if path.exists():
            shutil.copy2(path, backup)
        temporary.write_text(
            json.dumps(asdict(settings), ensure_ascii=False, indent=2), encoding="utf-8"
        )
        temporary.replace(path)
