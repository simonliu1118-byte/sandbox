from __future__ import annotations

import csv
import logging
import sqlite3
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable, Iterator

from .operations import CancellationToken, ProgressUpdate
from .paths import AppPaths

SCHEMA_VERSION = 12

SCHEMA = r"""
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS app_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS trips (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    start_date TEXT NULL,
    end_date TEXT NULL,
    notes TEXT NOT NULL DEFAULT '',
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_system INTEGER NOT NULL DEFAULT 0,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS import_sources (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source_type TEXT NOT NULL,
    source_filename TEXT NOT NULL,
    source_hash_sha256 TEXT NOT NULL,
    content_hash_sha256 TEXT NULL,
    source_full_path TEXT NULL,
    imported_at_utc TEXT NOT NULL,
    source_size_bytes INTEGER NULL,
    total_rows INTEGER NULL,
    valid_points INTEGER NULL,
    skipped_rows INTEGER NULL,
    segment_count INTEGER NULL,
    parser_version TEXT NULL,
    parse_report_json TEXT NULL,
    UNIQUE(source_hash_sha256, source_type)
);

CREATE TABLE IF NOT EXISTS tracks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    trip_id INTEGER NOT NULL,
    import_source_id INTEGER NULL,
    custom_name TEXT NULL,
    source_name TEXT NULL,
    assigned_date TEXT NULL,
    assigned_date_source TEXT NOT NULL DEFAULT 'auto',
    time_data_status TEXT NOT NULL DEFAULT 'original',
    timezone_mode TEXT NOT NULL DEFAULT 'auto',
    fixed_timezone_id TEXT NULL,
    notes TEXT NOT NULL DEFAULT '',
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_checked INTEGER NOT NULL DEFAULT 0,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    FOREIGN KEY(trip_id) REFERENCES trips(id) ON DELETE RESTRICT,
    FOREIGN KEY(import_source_id) REFERENCES import_sources(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS track_segments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    track_id INTEGER NOT NULL,
    segment_index INTEGER NOT NULL,
    assigned_date TEXT NULL,
    manual_date TEXT NULL,
    manual_timezone_id TEXT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE,
    UNIQUE(track_id, segment_index)
);

CREATE TABLE IF NOT EXISTS track_points (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    segment_id INTEGER NOT NULL,
    point_index INTEGER NOT NULL,
    latitude REAL NOT NULL,
    longitude REAL NOT NULL,
    altitude_m REAL NULL,
    utc_time TEXT NULL,
    device_time TEXT NULL,
    detected_timezone_id TEXT NULL,
    effective_timezone_id TEXT NULL,
    effective_local_time TEXT NULL,
    timezone_source TEXT NOT NULL DEFAULT 'auto',
    speed_kmh REAL NULL,
    course_degrees REAL NULL,
    satellites INTEGER NULL,
    raw_line_number INTEGER NULL,
    raw_fields_json TEXT NULL,
    FOREIGN KEY(segment_id) REFERENCES track_segments(id) ON DELETE CASCADE,
    UNIQUE(segment_id, point_index)
);

CREATE TABLE IF NOT EXISTS companion_groups (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_system INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS companions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    group_id INTEGER NOT NULL,
    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    notes TEXT NOT NULL DEFAULT '',
    sort_order INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(group_id) REFERENCES companion_groups(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS trip_companions (
    trip_id INTEGER NOT NULL,
    companion_id INTEGER NOT NULL,
    PRIMARY KEY(trip_id, companion_id),
    FOREIGN KEY(trip_id) REFERENCES trips(id) ON DELETE CASCADE,
    FOREIGN KEY(companion_id) REFERENCES companions(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS track_companions (
    track_id INTEGER NOT NULL,
    companion_id INTEGER NOT NULL,
    PRIMARY KEY(track_id, companion_id),
    FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE,
    FOREIGN KEY(companion_id) REFERENCES companions(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS activities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL COLLATE NOCASE UNIQUE,
    notes TEXT NOT NULL DEFAULT '',
    sort_order INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS trip_activities (
    trip_id INTEGER NOT NULL,
    activity_id INTEGER NOT NULL,
    PRIMARY KEY(trip_id, activity_id),
    FOREIGN KEY(trip_id) REFERENCES trips(id) ON DELETE CASCADE,
    FOREIGN KEY(activity_id) REFERENCES activities(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS track_activities (
    track_id INTEGER NOT NULL,
    activity_id INTEGER NOT NULL,
    PRIMARY KEY(track_id, activity_id),
    FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE,
    FOREIGN KEY(activity_id) REFERENCES activities(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS places (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    place_type TEXT NOT NULL,
    external_id TEXT NOT NULL,
    country_id TEXT NULL,
    admin1_id TEXT NULL,
    city_id TEXT NULL,
    display_name TEXT NOT NULL,
    full_name TEXT NOT NULL,
    latitude REAL NULL,
    longitude REAL NULL,
    timezone_id TEXT NULL,
    search_names TEXT NOT NULL DEFAULT '',
    data_source TEXT NOT NULL DEFAULT '',
    UNIQUE(place_type, external_id)
);

CREATE TABLE IF NOT EXISTS trip_places (
    trip_id INTEGER NOT NULL,
    place_id INTEGER NOT NULL,
    PRIMARY KEY(trip_id, place_id),
    FOREIGN KEY(trip_id) REFERENCES trips(id) ON DELETE CASCADE,
    FOREIGN KEY(place_id) REFERENCES places(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS track_places (
    track_id INTEGER NOT NULL,
    place_id INTEGER NOT NULL,
    PRIMARY KEY(track_id, place_id),
    FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE,
    FOREIGN KEY(place_id) REFERENCES places(id) ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS track_stats (
    track_id INTEGER PRIMARY KEY,
    segment_count INTEGER NOT NULL DEFAULT 0,
    point_count INTEGER NOT NULL DEFAULT 0,
    first_local_date TEXT NULL,
    last_local_date TEXT NULL,
    updated_at_utc TEXT NOT NULL,
    FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS track_merge_origins (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    track_id INTEGER NOT NULL,
    origin_order INTEGER NOT NULL,
    source_track_id INTEGER NULL,
    source_display_name TEXT NOT NULL,
    source_filename TEXT NOT NULL DEFAULT '',
    source_type TEXT NOT NULL DEFAULT '',
    FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE,
    UNIQUE(track_id, origin_order)
);

CREATE INDEX IF NOT EXISTS idx_import_sources_content_hash ON import_sources(source_type, content_hash_sha256);
CREATE INDEX IF NOT EXISTS idx_tracks_trip_date ON tracks(trip_id, assigned_date);
CREATE INDEX IF NOT EXISTS idx_tracks_checked ON tracks(is_checked);
CREATE INDEX IF NOT EXISTS idx_track_stats_dates ON track_stats(first_local_date,last_local_date);
CREATE INDEX IF NOT EXISTS idx_segments_track ON track_segments(track_id, segment_index);
CREATE INDEX IF NOT EXISTS idx_points_segment ON track_points(segment_id, point_index);
CREATE INDEX IF NOT EXISTS idx_points_utc ON track_points(utc_time);
CREATE INDEX IF NOT EXISTS idx_points_local ON track_points(effective_local_time);
CREATE INDEX IF NOT EXISTS idx_places_country ON places(country_id, admin1_id, city_id);
CREATE INDEX IF NOT EXISTS idx_places_names ON places(display_name, full_name);
"""


class Database:
    def __init__(self, database_path: Path, paths: AppPaths, logger: logging.Logger) -> None:
        self.path = Path(database_path)
        self.paths = paths
        self.logger = logger

    def connect(self, readonly: bool = False) -> sqlite3.Connection:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        if readonly:
            connection = sqlite3.connect(
                f"file:{self.path.resolve().as_posix()}?mode=ro", uri=True, timeout=30, isolation_level=None
            )
        else:
            connection = sqlite3.connect(self.path, timeout=30, isolation_level=None)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA foreign_keys=ON")
        if not readonly:
            connection.execute("PRAGMA journal_mode=WAL")
            connection.execute("PRAGMA synchronous=NORMAL")
        connection.execute("PRAGMA busy_timeout=30000")
        return connection

    @contextmanager
    def transaction(self) -> Iterator[sqlite3.Connection]:
        connection = self.connect()
        try:
            connection.execute("BEGIN IMMEDIATE")
            yield connection
            connection.commit()
        except Exception:
            connection.rollback()
            raise
        finally:
            connection.close()

    def initialize(self) -> None:
        self.paths.ensure()
        new_database = not self.path.exists()
        old_version = 0 if new_database else self._schema_version()
        if not new_database:
            self._migration_backup_if_needed()
        with self.connect() as connection:
            connection.executescript(SCHEMA)
            self._ensure_column(connection, "track_points", "raw_fields_json", "TEXT NULL")
            for column, ddl in (
                ("source_size_bytes", "INTEGER NULL"),
                ("total_rows", "INTEGER NULL"),
                ("valid_points", "INTEGER NULL"),
                ("skipped_rows", "INTEGER NULL"),
                ("segment_count", "INTEGER NULL"),
                ("parser_version", "TEXT NULL"),
                ("parse_report_json", "TEXT NULL"),
            ):
                self._ensure_column(connection, "import_sources", column, ddl)
            self._ensure_relation_tables(connection)
            if old_version < 12:
                self._split_existing_multiday_riti_tracks(connection)
            connection.execute(
                "INSERT INTO app_meta(key,value) VALUES('schema_version',?) "
                "ON CONFLICT(key) DO UPDATE SET value=excluded.value",
                (str(SCHEMA_VERSION),),
            )
            now = self._utc_now()
            connection.execute(
                """
                INSERT INTO trips(name,start_date,end_date,notes,sort_order,is_system,created_at_utc,updated_at_utc)
                SELECT '未分類',NULL,NULL,'',-100000,1,?,?
                WHERE NOT EXISTS(SELECT 1 FROM trips WHERE is_system=1)
                """,
                (now, now),
            )
            connection.execute(
                """
                INSERT INTO companion_groups(name,sort_order,is_system)
                SELECT '未分組',-100000,1
                WHERE NOT EXISTS(SELECT 1 FROM companion_groups WHERE is_system=1)
                """
            )
            self._backfill_track_stats(connection)
            connection.commit()
        self.seed_geography_if_empty()
        self.logger.info("Database initialized: %s", self.path)

    def _schema_version(self) -> int:
        try:
            with sqlite3.connect(self.path) as connection:
                row = connection.execute("SELECT value FROM app_meta WHERE key='schema_version'").fetchone()
                return int(row[0]) if row else 0
        except (sqlite3.Error, ValueError):
            return 0

    def _split_existing_multiday_riti_tracks(self, connection: sqlite3.Connection) -> None:
        """將舊版已匯入的多日 RITI 單一軌跡升級為每日軌跡。"""
        tracks = connection.execute(
            """
            SELECT k.* FROM tracks k
            JOIN import_sources i ON i.id=k.import_source_id
            WHERE i.source_type='RITI_TXT' AND k.assigned_date_source='auto'
            ORDER BY k.id
            """
        ).fetchall()
        now = self._utc_now()
        split_count = 0
        created_count = 0
        for track in tracks:
            segments = connection.execute(
                """
                SELECT s.*,
                       COALESCE(
                           (SELECT DATE(p.effective_local_time) FROM track_points p
                            WHERE p.segment_id=s.id AND p.effective_local_time IS NOT NULL
                            ORDER BY p.point_index DESC LIMIT 1),
                           s.assigned_date,
                           ?
                       ) AS group_date
                FROM track_segments s WHERE s.track_id=? ORDER BY s.segment_index,s.id
                """,
                (track["assigned_date"], int(track["id"])),
            ).fetchall()
            groups: dict[str | None, list[sqlite3.Row]] = {}
            for segment in segments:
                groups.setdefault(segment["group_date"], []).append(segment)
            if len(groups) <= 1:
                continue

            split_count += 1
            for group_index, (group_date, group_segments) in enumerate(groups.items()):
                if group_index == 0:
                    target_track_id = int(track["id"])
                    connection.execute(
                        "UPDATE tracks SET assigned_date=?,updated_at_utc=? WHERE id=?",
                        (group_date, now, target_track_id),
                    )
                else:
                    cursor = connection.execute(
                        """
                        INSERT INTO tracks(
                            trip_id,import_source_id,custom_name,source_name,assigned_date,assigned_date_source,
                            time_data_status,timezone_mode,fixed_timezone_id,notes,sort_order,is_checked,
                            created_at_utc,updated_at_utc
                        ) VALUES(?,?,?,?,?,'auto',?,?,?,?,?,?,?,?)
                        """,
                        (
                            int(track["trip_id"]), track["import_source_id"], track["custom_name"],
                            track["source_name"], group_date, track["time_data_status"], track["timezone_mode"],
                            track["fixed_timezone_id"], track["notes"], int(track["sort_order"]),
                            int(track["is_checked"]), track["created_at_utc"], now,
                        ),
                    )
                    target_track_id = int(cursor.lastrowid)
                    created_count += 1
                    for table, relation_column in (
                        ("track_places", "place_id"),
                        ("track_companions", "companion_id"),
                        ("track_activities", "activity_id"),
                    ):
                        connection.execute(
                            f"INSERT OR IGNORE INTO {table}(track_id,{relation_column}) "
                            f"SELECT ?,{relation_column} FROM {table} WHERE track_id=?",
                            (target_track_id, int(track["id"])),
                        )

                segment_ids = [int(segment["id"]) for segment in group_segments]
                for temporary_index, segment_id in enumerate(segment_ids, start=1):
                    connection.execute(
                        "UPDATE track_segments SET track_id=?,segment_index=? WHERE id=?",
                        (target_track_id, -temporary_index, segment_id),
                    )
                for final_index, segment_id in enumerate(segment_ids):
                    connection.execute(
                        "UPDATE track_segments SET segment_index=? WHERE id=?",
                        (final_index, segment_id),
                    )

        if split_count:
            self.logger.info(
                "Database migration v12 split multiday RITI tracks: sources=%s new_tracks=%s",
                split_count,
                created_count,
            )

    def _ensure_column(self, connection: sqlite3.Connection, table: str, column: str, ddl: str) -> None:
        columns = {row[1] for row in connection.execute(f"PRAGMA table_info({table})")}
        if column not in columns:
            connection.execute(f"ALTER TABLE {table} ADD COLUMN {column} {ddl}")

    def _ensure_relation_tables(self, connection: sqlite3.Connection) -> None:
        connection.executescript(
            """
            CREATE TABLE IF NOT EXISTS track_companions (
                track_id INTEGER NOT NULL, companion_id INTEGER NOT NULL,
                PRIMARY KEY(track_id,companion_id),
                FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE,
                FOREIGN KEY(companion_id) REFERENCES companions(id) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS track_activities (
                track_id INTEGER NOT NULL, activity_id INTEGER NOT NULL,
                PRIMARY KEY(track_id,activity_id),
                FOREIGN KEY(track_id) REFERENCES tracks(id) ON DELETE CASCADE,
                FOREIGN KEY(activity_id) REFERENCES activities(id) ON DELETE CASCADE
            );
            """
        )

    def _backfill_track_stats(self, connection: sqlite3.Connection) -> None:
        now = self._utc_now()
        connection.execute(
            """
            INSERT INTO track_stats(track_id,segment_count,point_count,first_local_date,last_local_date,updated_at_utc)
            SELECT k.id,COUNT(DISTINCT s.id),COUNT(p.id),
                   MIN(DATE(COALESCE(p.effective_local_time,s.assigned_date,k.assigned_date))),
                   MAX(DATE(COALESCE(p.effective_local_time,s.assigned_date,k.assigned_date))),?
            FROM tracks k
            LEFT JOIN track_segments s ON s.track_id=k.id
            LEFT JOIN track_points p ON p.segment_id=s.id
            GROUP BY k.id
            ON CONFLICT(track_id) DO UPDATE SET
                segment_count=excluded.segment_count,point_count=excluded.point_count,
                first_local_date=excluded.first_local_date,last_local_date=excluded.last_local_date,
                updated_at_utc=excluded.updated_at_utc
            """,
            (now,),
        )

    def _migration_backup_if_needed(self) -> None:
        try:
            with sqlite3.connect(self.path) as connection:
                row = connection.execute(
                    "SELECT value FROM app_meta WHERE key='schema_version'"
                ).fetchone()
                old_version = int(row[0]) if row else 0
        except (sqlite3.Error, ValueError):
            old_version = 0
        if old_version >= SCHEMA_VERSION:
            return
        self.paths.migration_backups.mkdir(parents=True, exist_ok=True)
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")
        target = self.paths.migration_backups / f"TM_before_v{SCHEMA_VERSION}_{stamp}.db"
        self.backup_to(target)
        self._retain(self.paths.migration_backups.glob("*.db"), 2)

    def seed_geography_if_empty(self) -> None:
        with self.connect() as connection:
            count = int(connection.execute("SELECT COUNT(*) FROM places").fetchone()[0])
            if count:
                return
        source = self.paths.geography_resources / "places_seed.csv"
        if not source.exists():
            self.logger.warning("Geography seed missing: %s", source)
            return
        rows: list[tuple[object, ...]] = []
        with source.open("r", encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            for item in reader:
                rows.append(
                    (
                        item["place_type"], item["external_id"], item.get("country_id") or None,
                        item.get("admin1_id") or None, item.get("city_id") or None,
                        item["display_name"], item["full_name"],
                        self._float_or_none(item.get("latitude")),
                        self._float_or_none(item.get("longitude")),
                        item.get("timezone_id") or None, item.get("search_names") or "",
                        item.get("source") or "",
                    )
                )
        with self.transaction() as connection:
            connection.executemany(
                """
                INSERT OR IGNORE INTO places(
                    place_type,external_id,country_id,admin1_id,city_id,display_name,full_name,
                    latitude,longitude,timezone_id,search_names,data_source)
                VALUES(?,?,?,?,?,?,?,?,?,?,?,?)
                """,
                rows,
            )
        self.logger.info("Geography seeded: %s rows", len(rows))

    def checkpoint(self) -> None:
        if not self.path.exists():
            return
        with self.connect() as connection:
            connection.execute("PRAGMA wal_checkpoint(TRUNCATE)")

    def quick_check(self) -> tuple[bool, str]:
        try:
            with self.connect(readonly=True) as connection:
                result = str(connection.execute("PRAGMA quick_check").fetchone()[0])
            return result.lower() == "ok", result
        except sqlite3.Error as exc:
            return False, str(exc)

    def integrity_check(self) -> tuple[bool, str]:
        try:
            with self.connect(readonly=True) as connection:
                result = str(connection.execute("PRAGMA integrity_check").fetchone()[0])
            return result.lower() == "ok", result
        except sqlite3.Error as exc:
            return False, str(exc)

    def backup_to(
        self,
        target: Path,
        token: CancellationToken | None = None,
        progress: Callable[[ProgressUpdate], None] | None = None,
    ) -> Path:
        target = Path(target)
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_suffix(target.suffix + ".tmp")
        temporary.unlink(missing_ok=True)
        token = token or CancellationToken()
        source = sqlite3.connect(self.path, timeout=30)
        destination = sqlite3.connect(temporary)
        completed_pages = 0

        def report(status: int, remaining: int, total: int) -> None:
            nonlocal completed_pages
            token.raise_if_cancelled()
            completed_pages = max(0, total - remaining)
            if progress:
                progress(ProgressUpdate(completed_pages, max(total, 1), "正在備份資料庫"))

        try:
            source.backup(destination, pages=256, progress=report, sleep=0.01)
            token.raise_if_cancelled()
            destination.commit()
            check = destination.execute("PRAGMA integrity_check").fetchone()[0]
            if str(check).lower() != "ok":
                raise sqlite3.DatabaseError(f"Backup integrity_check failed: {check}")
        except Exception:
            temporary.unlink(missing_ok=True)
            raise
        finally:
            destination.close()
            source.close()
        temporary.replace(target)
        return target

    @staticmethod
    def _retain(paths: object, keep: int) -> None:
        items = sorted(list(paths), key=lambda p: p.stat().st_mtime, reverse=True)
        for path in items[max(0, keep):]:
            path.unlink(missing_ok=True)

    @staticmethod
    def _utc_now() -> str:
        return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

    @staticmethod
    def _float_or_none(value: str | None) -> float | None:
        try:
            return float(value) if value not in (None, "") else None
        except ValueError:
            return None
