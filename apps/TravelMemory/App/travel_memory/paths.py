from __future__ import annotations

import os
import sys
from dataclasses import dataclass
from pathlib import Path


def application_root() -> Path:
    """Return the immutable application/source root, independent of writable data overrides."""
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parent.parent


def bundled_sample_paths() -> list[Path]:
    samples = application_root() / "Samples"
    return [samples / "260530.txt", samples / "260602.txt"]


@dataclass(frozen=True, slots=True)
class AppPaths:
    root: Path
    data: Path
    database: Path
    config: Path
    settings: Path
    cache: Path
    map_cache: Path
    logs: Path
    log_file: Path
    error_log_file: Path
    diagnostics: Path
    resources: Path
    map_resources: Path
    timezone_resources: Path
    geography_resources: Path
    runtime: Path
    backups: Path
    migration_backups: Path
    restore_backups: Path

    @classmethod
    def discover(cls) -> "AppPaths":
        override = os.environ.get("TRAVEL_MEMORY_ROOT", "").strip()
        if override:
            root = Path(override).expanduser().resolve()
        elif getattr(sys, "frozen", False):
            root = Path(sys.executable).resolve().parent
        else:
            root = Path(__file__).resolve().parent.parent

        package_resources = Path(__file__).resolve().parent / "resources"
        installed_resources = root / "Resources"
        resources = installed_resources if installed_resources.exists() else package_resources
        data = root / "Data"
        config = root / "Config"
        cache = root / "Cache"
        logs = root / "Logs"
        diagnostics = root / "Diagnostics"
        backups = data / "Backups"
        return cls(
            root=root,
            data=data,
            database=data / "TM.db",
            config=config,
            settings=config / "settings.json",
            cache=cache,
            map_cache=cache / "MapCache",
            logs=logs,
            log_file=logs / "TravelMemory.log",
            error_log_file=logs / "TravelMemory_error.log",
            diagnostics=diagnostics,
            resources=resources,
            map_resources=resources / "Map",
            timezone_resources=resources / "Timezone",
            geography_resources=resources / "Geography",
            runtime=root / "Runtime",
            backups=backups,
            migration_backups=data / "MigrationBackups",
            restore_backups=data / "RestoreBackups",
        )

    def ensure(self) -> None:
        for path in (
            self.data,
            self.config,
            self.cache,
            self.map_cache,
            self.logs,
            self.diagnostics,
            self.runtime,
            self.backups,
            self.migration_backups,
            self.restore_backups,
        ):
            path.mkdir(parents=True, exist_ok=True)
