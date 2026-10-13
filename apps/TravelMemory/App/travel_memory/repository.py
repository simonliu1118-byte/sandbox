from __future__ import annotations

import hashlib
import json
import sqlite3
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Iterable, Sequence
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

from .database import Database
from .models import FilterCriteria, ParsedTrack, TrackSummary, TripRecord
from .operations import CancellationToken, ProgressCallback, ProgressUpdate
from .services.timezone_service import TimeZoneService


class DuplicateImportError(RuntimeError):
    pass


class Repository:
    def __init__(self, database: Database, timezone_service: TimeZoneService) -> None:
        self.database = database
        self.timezone_service = timezone_service

    # ---------- Trips ----------
    def list_trips(self) -> list[TripRecord]:
        with self.database.connect() as connection:
            rows = connection.execute(
                "SELECT * FROM trips ORDER BY is_system DESC, sort_order, name COLLATE NOCASE, id"
            ).fetchall()
        return [self._trip(row) for row in rows]

    def get_trip(self, trip_id: int) -> sqlite3.Row | None:
        with self.database.connect() as connection:
            return connection.execute("SELECT * FROM trips WHERE id=?", (trip_id,)).fetchone()

    def get_unclassified_trip_id(self) -> int:
        with self.database.connect() as connection:
            row = connection.execute("SELECT id FROM trips WHERE is_system=1 ORDER BY id LIMIT 1").fetchone()
            if not row:
                raise RuntimeError("未分類旅程不存在")
            return int(row[0])

    def create_trip(
        self,
        name: str,
        start_date: date | None = None,
        end_date: date | None = None,
        notes: str = "",
        place_ids: Sequence[int] = (),
        companion_ids: Sequence[int] = (),
        activity_ids: Sequence[int] = (),
    ) -> int:
        name = name.strip() or "未命名旅程"
        if start_date and end_date and start_date > end_date:
            raise ValueError("旅程開始日期不得晚於結束日期")
        now = self._now()
        with self.database.transaction() as connection:
            sort_order = int(connection.execute("SELECT COALESCE(MAX(sort_order),0)+1 FROM trips").fetchone()[0])
            cursor = connection.execute(
                """
                INSERT INTO trips(name,start_date,end_date,notes,sort_order,is_system,created_at_utc,updated_at_utc)
                VALUES(?,?,?,?,?,0,?,?)
                """,
                (name, self._date(start_date), self._date(end_date), notes.strip(), sort_order, now, now),
            )
            trip_id = int(cursor.lastrowid)
            self._replace_relations(connection, "trip_places", "trip_id", trip_id, "place_id", place_ids)
            self._replace_relations(connection, "trip_companions", "trip_id", trip_id, "companion_id", companion_ids)
            self._replace_relations(connection, "trip_activities", "trip_id", trip_id, "activity_id", activity_ids)
            return trip_id

    def update_trip(
        self,
        trip_id: int,
        name: str,
        start_date: date | None,
        end_date: date | None,
        notes: str,
        place_ids: Sequence[int],
        companion_ids: Sequence[int],
        activity_ids: Sequence[int],
    ) -> None:
        row = self.get_trip(trip_id)
        if not row:
            raise ValueError("旅程不存在")
        clean_name = name.strip() or "未命名旅程"
        if start_date and end_date and start_date > end_date:
            raise ValueError("旅程開始日期不得晚於結束日期")
        with self.database.transaction() as connection:
            connection.execute(
                "UPDATE trips SET name=?,start_date=?,end_date=?,notes=?,updated_at_utc=? WHERE id=?",
                (clean_name, self._date(start_date), self._date(end_date), notes.strip(), self._now(), trip_id),
            )
            self._replace_relations(connection, "trip_places", "trip_id", trip_id, "place_id", place_ids)
            self._replace_relations(connection, "trip_companions", "trip_id", trip_id, "companion_id", companion_ids)
            self._replace_relations(connection, "trip_activities", "trip_id", trip_id, "activity_id", activity_ids)

    def rename_trip(self, trip_id: int, name: str) -> None:
        with self.database.transaction() as connection:
            connection.execute(
                "UPDATE trips SET name=?,updated_at_utc=? WHERE id=?",
                (name.strip() or "未命名旅程", self._now(), trip_id),
            )

    def delete_trip(self, trip_id: int) -> None:
        with self.database.transaction() as connection:
            row = connection.execute("SELECT is_system FROM trips WHERE id=?", (trip_id,)).fetchone()
            if not row:
                return
            if bool(row[0]):
                raise ValueError("未分類旅程不可刪除")
            count = int(connection.execute("SELECT COUNT(*) FROM tracks WHERE trip_id=?", (trip_id,)).fetchone()[0])
            if count:
                raise ValueError("旅程仍有軌跡，請先移動或刪除軌跡")
            connection.execute("DELETE FROM trips WHERE id=?", (trip_id,))

    def trip_relation_ids(self, trip_id: int) -> dict[str, list[int]]:
        with self.database.connect() as connection:
            return {
                "places": [int(r[0]) for r in connection.execute("SELECT place_id FROM trip_places WHERE trip_id=?", (trip_id,))],
                "companions": [int(r[0]) for r in connection.execute("SELECT companion_id FROM trip_companions WHERE trip_id=?", (trip_id,))],
                "activities": [int(r[0]) for r in connection.execute("SELECT activity_id FROM trip_activities WHERE trip_id=?", (trip_id,))],
            }

    # ---------- Tracks ----------
    def list_tracks(self, search: str = "", criteria: FilterCriteria | None = None) -> list[TrackSummary]:
        criteria = criteria or FilterCriteria()
        where = ["1=1"]
        params: list[object] = []
        if search.strip():
            token = f"%{search.strip().lower()}%"
            where.append("(LOWER(t.name) LIKE ? OR LOWER(COALESCE(k.custom_name,k.source_name,'')) LIKE ?)")
            params.extend([token, token])
        if criteria.date_from:
            where.append("DATE(COALESCE(last_local_date,k.assigned_date)) >= DATE(?)")
            params.append(criteria.date_from.isoformat())
        if criteria.date_to:
            where.append("DATE(COALESCE(first_local_date,k.assigned_date)) <= DATE(?)")
            params.append(criteria.date_to.isoformat())

        category_conditions: list[str] = []
        for kind, ids, match in (
            ("place", criteria.place_ids, criteria.place_match),
            ("companion", criteria.companion_ids, criteria.companion_match),
            ("activity", criteria.activity_ids, criteria.activity_match),
        ):
            if ids:
                condition, condition_params = self._tag_filter(kind, ids, match)
                category_conditions.append(condition)
                params.extend(condition_params)
        if category_conditions:
            operator = " OR " if criteria.category_operator.upper() == "OR" else " AND "
            where.append("(" + operator.join(category_conditions) + ")")

        sql = f"""
        SELECT k.*,t.name AS trip_name,
               COALESCE(k.custom_name,k.source_name,'未命名軌跡') AS display_name,
               COALESCE(i.source_type,'') AS source_type,
               COALESCE(i.source_filename,'') AS source_filename,
               stats.segment_count,stats.point_count,stats.first_local_date,stats.last_local_date
        FROM tracks k
        JOIN trips t ON t.id=k.trip_id
        LEFT JOIN import_sources i ON i.id=k.import_source_id
        LEFT JOIN track_stats stats ON stats.track_id=k.id
        WHERE {' AND '.join(where)}
        ORDER BY t.is_system DESC,t.sort_order,t.name COLLATE NOCASE,
                 COALESCE(k.assigned_date,stats.first_local_date,'9999-12-31'),k.sort_order,k.id
        """
        with self.database.connect() as connection:
            rows = connection.execute(sql, params).fetchall()
        return [self._track(row) for row in rows]

    def get_track(self, track_id: int) -> sqlite3.Row | None:
        with self.database.connect() as connection:
            return connection.execute(
                """
                SELECT k.*,COALESCE(k.custom_name,k.source_name,'未命名軌跡') AS display_name,
                       COALESCE(i.source_type,'') AS source_type,COALESCE(i.source_filename,'') AS source_filename
                FROM tracks k LEFT JOIN import_sources i ON i.id=k.import_source_id WHERE k.id=?
                """,
                (track_id,),
            ).fetchone()

    def track_relation_ids(self, track_id: int) -> dict[str, list[int]]:
        with self.database.connect() as connection:
            return {
                "places": [int(r[0]) for r in connection.execute("SELECT place_id FROM track_places WHERE track_id=?", (track_id,))],
                "companions": [int(r[0]) for r in connection.execute("SELECT companion_id FROM track_companions WHERE track_id=?", (track_id,))],
                "activities": [int(r[0]) for r in connection.execute("SELECT activity_id FROM track_activities WHERE track_id=?", (track_id,))],
            }

    def update_track(
        self,
        track_id: int,
        name: str,
        assigned_date: date | None,
        timezone_mode: str,
        fixed_timezone_id: str | None,
        notes: str,
        place_ids: Sequence[int],
        companion_ids: Sequence[int],
        activity_ids: Sequence[int],
    ) -> None:
        if timezone_mode not in {"auto", "fixed"}:
            raise ValueError("時區模式錯誤")
        if timezone_mode == "fixed" and not fixed_timezone_id:
            raise ValueError("固定時區模式必須指定 IANA 時區")
        if timezone_mode == "fixed":
            try:
                ZoneInfo(str(fixed_timezone_id))
            except ZoneInfoNotFoundError as exc:
                raise ValueError(f"無效的 IANA 時區：{fixed_timezone_id}") from exc
        with self.database.transaction() as connection:
            connection.execute(
                """
                UPDATE tracks SET custom_name=?,assigned_date=?,assigned_date_source=?,timezone_mode=?,
                    fixed_timezone_id=?,notes=?,updated_at_utc=? WHERE id=?
                """,
                (
                    name.strip() or None,
                    self._date(assigned_date),
                    "manual" if assigned_date else "auto",
                    timezone_mode,
                    fixed_timezone_id if timezone_mode == "fixed" else None,
                    notes.strip(),
                    self._now(),
                    track_id,
                ),
            )
            self._replace_relations(connection, "track_places", "track_id", track_id, "place_id", place_ids)
            self._replace_relations(connection, "track_companions", "track_id", track_id, "companion_id", companion_ids)
            self._replace_relations(connection, "track_activities", "track_id", track_id, "activity_id", activity_ids)
            points = connection.execute(
                """
                SELECT p.id,p.latitude,p.longitude,p.utc_time FROM track_points p
                JOIN track_segments s ON s.id=p.segment_id WHERE s.track_id=?
                """,
                (track_id,),
            ).fetchall()
            for point in points:
                if not point["utc_time"]:
                    continue
                utc_value = self._parse_datetime(point["utc_time"])
                if timezone_mode == "fixed":
                    tzid = str(fixed_timezone_id)
                    source = "fixed"
                else:
                    tzid = self.timezone_service.detect_for_point(float(point["latitude"]), float(point["longitude"]))
                    source = "auto"
                local_value = self.timezone_service.to_local(utc_value, tzid)
                connection.execute(
                    """
                    UPDATE track_points SET detected_timezone_id=CASE WHEN ?='auto' THEN ? ELSE detected_timezone_id END,
                        effective_timezone_id=?,effective_local_time=?,timezone_source=? WHERE id=?
                    """,
                    (source, tzid, tzid, local_value.isoformat(sep=" ", timespec="seconds"), source, int(point["id"])),
                )
            self._refresh_track_stats(connection, track_id)

    def rename_track(self, track_id: int, name: str) -> None:
        with self.database.transaction() as connection:
            connection.execute(
                "UPDATE tracks SET custom_name=?,updated_at_utc=? WHERE id=?",
                (name.strip() or None, self._now(), track_id),
            )

    def move_track(self, track_id: int, trip_id: int) -> None:
        with self.database.transaction() as connection:
            connection.execute(
                "UPDATE tracks SET trip_id=?,updated_at_utc=? WHERE id=?",
                (trip_id, self._now(), track_id),
            )

    def delete_track(self, track_id: int) -> None:
        with self.database.transaction() as connection:
            connection.execute("DELETE FROM tracks WHERE id=?", (track_id,))

    def set_checked(self, track_id: int, checked: bool) -> None:
        with self.database.transaction() as connection:
            connection.execute("UPDATE tracks SET is_checked=? WHERE id=?", (1 if checked else 0, track_id))

    def clear_checked(self) -> None:
        with self.database.transaction() as connection:
            connection.execute("UPDATE tracks SET is_checked=0 WHERE is_checked<>0")

    def checked_track_ids(self) -> list[int]:
        with self.database.connect() as connection:
            return [int(row[0]) for row in connection.execute("SELECT id FROM tracks WHERE is_checked=1 ORDER BY id")]

    # ---------- Import ----------
    def import_tracks(
        self,
        source_path: Path,
        source_type: str,
        parsed_tracks: Sequence[ParsedTrack],
        trip_id: int,
        token: CancellationToken | None = None,
        progress: ProgressCallback | None = None,
    ) -> list[int]:
        source_path = Path(source_path)
        token = token or CancellationToken()
        token.raise_if_cancelled()
        file_hash_builder = hashlib.sha256()
        content_hash_builder = hashlib.sha256()
        with source_path.open("rb") as handle:
            while True:
                chunk = handle.read(1024 * 1024)
                if not chunk:
                    break
                token.raise_if_cancelled()
                file_hash_builder.update(chunk)
                content_hash_builder.update(chunk.replace(b"\r\n", b"\n"))
        file_hash = file_hash_builder.hexdigest()
        content_hash = content_hash_builder.hexdigest()
        now = self._now()
        ids: list[int] = []
        total_points = sum(item.point_count for item in parsed_tracks)
        written_points = 0
        aggregate = {
            "total_rows": sum(int(item.metadata.get("total_rows", item.point_count)) for item in parsed_tracks),
            "valid_points": sum(item.point_count for item in parsed_tracks),
            "skipped_rows": sum(len(item.skipped_lines) for item in parsed_tracks),
            "segment_count": sum(len(item.segments) for item in parsed_tracks),
            "parser_version": ",".join(sorted({str(item.metadata.get("parser_version", "")) for item in parsed_tracks if item.metadata.get("parser_version")})),
            "tracks": [item.metadata for item in parsed_tracks],
        }
        with self.database.transaction() as connection:
            duplicate = connection.execute(
                "SELECT id FROM import_sources WHERE source_type=? AND (source_hash_sha256=? OR content_hash_sha256=?)",
                (source_type, file_hash, content_hash),
            ).fetchone()
            if duplicate:
                raise DuplicateImportError(f"檔案已匯入：{source_path.name}")
            source_cursor = connection.execute(
                """
                INSERT INTO import_sources(
                    source_type,source_filename,source_hash_sha256,content_hash_sha256,source_full_path,imported_at_utc,
                    source_size_bytes,total_rows,valid_points,skipped_rows,segment_count,parser_version,parse_report_json)
                VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)
                """,
                (
                    source_type, source_path.name, file_hash, content_hash, str(source_path.resolve()), now,
                    source_path.stat().st_size, aggregate["total_rows"], aggregate["valid_points"],
                    aggregate["skipped_rows"], aggregate["segment_count"], aggregate["parser_version"],
                    json.dumps(aggregate, ensure_ascii=False, separators=(",", ":")),
                ),
            )
            source_id = int(source_cursor.lastrowid)
            next_sort = int(connection.execute("SELECT COALESCE(MAX(sort_order),0)+1 FROM tracks WHERE trip_id=?", (trip_id,)).fetchone()[0])
            for offset, parsed in enumerate(parsed_tracks):
                token.raise_if_cancelled()
                if parsed.time_data_status == "missing" and (not parsed.assigned_date or parsed.timezone_mode != "fixed"):
                    # Untimed GPX is allowed, but must be completed by the user before normal use.
                    parsed.timezone_mode = "fixed"
                    parsed.fixed_timezone_id = parsed.fixed_timezone_id or "Etc/UTC"
                cursor = connection.execute(
                    """
                    INSERT INTO tracks(trip_id,import_source_id,custom_name,source_name,assigned_date,assigned_date_source,
                        time_data_status,timezone_mode,fixed_timezone_id,notes,sort_order,is_checked,created_at_utc,updated_at_utc)
                    VALUES(?,?,NULL,?,?, 'auto',?,?,?,'',?,0,?,?)
                    """,
                    (
                        trip_id, source_id, parsed.name, self._date(parsed.assigned_date), parsed.time_data_status,
                        parsed.timezone_mode, parsed.fixed_timezone_id, next_sort + offset, now, now,
                    ),
                )
                track_id = int(cursor.lastrowid)
                ids.append(track_id)
                for segment in parsed.segments:
                    token.raise_if_cancelled()
                    seg_cursor = connection.execute(
                        """
                        INSERT INTO track_segments(track_id,segment_index,assigned_date,manual_date,manual_timezone_id,created_at_utc)
                        VALUES(?,?,?,?,?,?)
                        """,
                        (
                            track_id, segment.index, self._date(segment.assigned_date), self._date(segment.manual_date),
                            segment.manual_timezone_id, now,
                        ),
                    )
                    segment_id = int(seg_cursor.lastrowid)
                    rows = []
                    for point_index, point in enumerate(segment.points):
                        if point_index % 2000 == 0:
                            token.raise_if_cancelled()
                        rows.append(
                            (
                                segment_id, point_index, point.latitude, point.longitude, point.altitude_m,
                                self._datetime(point.utc_time), self._datetime(point.device_time, utc=False),
                                point.detected_timezone_id, point.effective_timezone_id,
                                self._datetime(point.effective_local_time, utc=False), point.timezone_source,
                                point.speed_kmh, point.course_degrees, point.satellites, point.raw_line_number,
                                point.raw_fields_json,
                            )
                        )
                    connection.executemany(
                        """
                        INSERT INTO track_points(segment_id,point_index,latitude,longitude,altitude_m,utc_time,device_time,
                            detected_timezone_id,effective_timezone_id,effective_local_time,timezone_source,speed_kmh,
                            course_degrees,satellites,raw_line_number,raw_fields_json)
                        VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                        """,
                        rows,
                    )
                    written_points += len(rows)
                    if progress:
                        progress(
                            ProgressUpdate(
                                written_points,
                                total_points,
                                f"正在寫入資料庫：{written_points:,} / {total_points:,} 點",
                            )
                        )
                self._refresh_track_stats(connection, track_id)
        return ids

    # ---------- Map / Export ----------
    def load_track_data(self, track_ids: Sequence[int]) -> list[dict]:
        if not track_ids:
            return []
        placeholders = ",".join("?" for _ in track_ids)
        with self.database.connect() as connection:
            tracks = connection.execute(
                f"""
                SELECT k.id,k.trip_id,t.name AS trip_name,COALESCE(k.custom_name,k.source_name,'未命名軌跡') AS name,
                       k.assigned_date,k.notes,k.time_data_status,k.timezone_mode,k.fixed_timezone_id,
                       COALESCE(i.source_type,'') AS source_type,COALESCE(i.source_filename,'') AS source_filename
                FROM tracks k JOIN trips t ON t.id=k.trip_id
                LEFT JOIN import_sources i ON i.id=k.import_source_id
                WHERE k.id IN ({placeholders}) ORDER BY k.assigned_date,k.sort_order,k.id
                """,
                list(track_ids),
            ).fetchall()
            result: list[dict] = []
            for track in tracks:
                segments = []
                seg_rows = connection.execute(
                    "SELECT * FROM track_segments WHERE track_id=? ORDER BY segment_index", (int(track["id"]),)
                ).fetchall()
                for segment in seg_rows:
                    point_rows = connection.execute(
                        "SELECT * FROM track_points WHERE segment_id=? ORDER BY point_index", (int(segment["id"]),)
                    ).fetchall()
                    points = [
                        {
                            "lat": float(point["latitude"]),
                            "lon": float(point["longitude"]),
                            "altitude": point["altitude_m"],
                            "utc": point["utc_time"],
                            "device": point["device_time"],
                            "local": point["effective_local_time"],
                            "timezone": point["effective_timezone_id"],
                            "speed": point["speed_kmh"],
                            "course": point["course_degrees"],
                            "satellites": point["satellites"],
                            "index": int(point["point_index"]),
                        }
                        for point in point_rows
                    ]
                    segments.append({"index": int(segment["segment_index"]), "assigned_date": segment["assigned_date"], "points": points})
                result.append(
                    {
                        "id": int(track["id"]), "trip_id": int(track["trip_id"]), "trip_name": str(track["trip_name"]),
                        "name": str(track["name"]), "date": track["assigned_date"], "notes": str(track["notes"]),
                        "source_type": str(track["source_type"]), "source_filename": str(track["source_filename"]),
                        "segments": segments,
                    }
                )
        return result

    def track_bounds(self, track_id: int) -> list[float] | None:
        """Use all stored points, not display sampling, for viewport bounds."""
        with self.database.connect(readonly=True) as connection:
            row = connection.execute(
                """SELECT MIN(p.longitude),MIN(p.latitude),MAX(p.longitude),MAX(p.latitude)
                   FROM track_points p JOIN track_segments s ON s.id=p.segment_id
                   WHERE s.track_id=?""", (track_id,),
            ).fetchone()
        return [float(value) for value in row] if row and row[0] is not None else None

    def map_payload(
        self,
        track_ids: Sequence[int],
        max_points: int = 30000,
        gap_seconds: int = 120,
    ) -> list[dict]:
        ids = list(dict.fromkeys(int(value) for value in track_ids))
        if not ids:
            return []
        placeholders = ",".join("?" for _ in ids)
        palette = ["#2F80ED", "#27AE60", "#F2994A", "#9B51E0", "#EB5757", "#219653", "#56CCF2", "#6FCF97"]
        date_colors: dict[str, str] = {}
        result: list[dict] = []
        with self.database.connect(readonly=True) as connection:
            tracks = connection.execute(
                f"""
                SELECT k.id,k.assigned_date,COALESCE(k.custom_name,k.source_name,'未命名軌跡') AS name,
                       COUNT(p.id) AS point_count,
                       MIN(p.longitude) AS west,MIN(p.latitude) AS south,
                       MAX(p.longitude) AS east,MAX(p.latitude) AS north
                FROM tracks k
                LEFT JOIN track_segments s ON s.track_id=k.id
                LEFT JOIN track_points p ON p.segment_id=s.id
                WHERE k.id IN ({placeholders})
                GROUP BY k.id
                ORDER BY k.assigned_date,k.sort_order,k.id
                """,
                ids,
            ).fetchall()
            for track in tracks:
                original_count = int(track["point_count"] or 0)
                stride = max(1, (original_count + max(1, max_points) - 1) // max(1, max_points))
                segments: list[dict] = []
                drawn_count = 0
                seg_rows = connection.execute(
                    "SELECT id,segment_index,assigned_date FROM track_segments WHERE track_id=? ORDER BY segment_index",
                    (int(track["id"]),),
                ).fetchall()
                for segment in seg_rows:
                    maximum = int(
                        connection.execute(
                            "SELECT COALESCE(MAX(point_index),0) FROM track_points WHERE segment_id=?",
                            (int(segment["id"]),),
                        ).fetchone()[0]
                    )
                    rows = connection.execute(
                        """
                        WITH ordered AS (
                            SELECT p.*,
                                   LAG(utc_time) OVER (ORDER BY point_index) AS previous_utc,
                                   LEAD(utc_time) OVER (ORDER BY point_index) AS next_utc
                            FROM track_points p WHERE segment_id=?
                        ), marked AS (
                            SELECT *,
                                CASE WHEN utc_time IS NOT NULL AND previous_utc IS NOT NULL
                                     AND (julianday(utc_time)-julianday(previous_utc))*86400.0 > ?
                                THEN 1 ELSE 0 END AS gap_before,
                                CASE WHEN utc_time IS NOT NULL AND next_utc IS NOT NULL
                                     AND (julianday(next_utc)-julianday(utc_time))*86400.0 > ?
                                THEN 1 ELSE 0 END AS gap_after
                            FROM ordered
                        )
                        SELECT * FROM marked
                        WHERE point_index=0 OR point_index=? OR point_index % ?=0 OR gap_before=1 OR gap_after=1
                        ORDER BY point_index
                        """,
                        (int(segment["id"]), gap_seconds, gap_seconds, maximum, stride),
                    ).fetchall()
                    points = [
                        {
                            "lat": float(point["latitude"]),
                            "lon": float(point["longitude"]),
                            "utc": point["utc_time"],
                            "timezone": point["effective_timezone_id"],
                            "local": point["effective_local_time"],
                            "index": int(point["point_index"]),
                            "gap_before": bool(point["gap_before"]),
                        }
                        for point in rows
                    ]
                    drawn_count += len(points)
                    segments.append(
                        {
                            "index": int(segment["segment_index"]),
                            "assigned_date": segment["assigned_date"],
                            "points": points,
                        }
                    )
                date_key = str(track["assigned_date"] or "no-date")
                if date_key not in date_colors:
                    date_colors[date_key] = palette[len(date_colors) % len(palette)]
                result.append(
                    {
                        "id": int(track["id"]),
                        "name": str(track["name"]),
                        "date": track["assigned_date"],
                        "color": date_colors[date_key],
                        "segments": segments,
                        "segment_count": len(segments),
                        "point_count": original_count,
                        "drawn_point_count": drawn_count,
                        "overview": drawn_count < original_count,
                        "bounds": [float(track[key]) for key in ("west", "south", "east", "north")] if original_count else None,
                    }
                )
        return result

    # ---------- Merge ----------
    def merge_tracks(self, track_ids: Sequence[int], name: str) -> int:
        ordered = list(dict.fromkeys(int(value) for value in track_ids))
        if len(ordered) < 2:
            raise ValueError("至少選取兩筆軌跡")
        source_data = self.load_track_data(ordered)
        if len(source_data) != len(ordered):
            raise ValueError("部分軌跡不存在")
        trip_ids = {int(item["trip_id"]) for item in source_data}
        if len(trip_ids) != 1:
            raise ValueError("只有同一旅程內的軌跡可以合併")
        trip_id = next(iter(trip_ids))
        now = self._now()
        dates = [item["date"] for item in source_data if item.get("date")]
        assigned_date = min(dates) if dates else None
        statuses = {str(self.get_track(int(item["id"]))["time_data_status"]) for item in source_data}
        merged_status = next(iter(statuses)) if len(statuses) == 1 else "mixed"
        with self.database.transaction() as connection:
            sort_order = int(connection.execute("SELECT COALESCE(MIN(sort_order),0) FROM tracks WHERE id IN (%s)" % ",".join("?" * len(ordered)), ordered).fetchone()[0])
            cursor = connection.execute(
                """
                INSERT INTO tracks(trip_id,import_source_id,custom_name,source_name,assigned_date,assigned_date_source,
                    time_data_status,timezone_mode,fixed_timezone_id,notes,sort_order,is_checked,created_at_utc,updated_at_utc)
                VALUES(?,NULL,?,NULL,?,'auto',?,'auto',NULL,'',?,1,?,?)
                """,
                (trip_id, name.strip() or "合併軌跡", assigned_date, merged_status, sort_order, now, now),
            )
            new_track_id = int(cursor.lastrowid)
            new_segment_index = 0
            for origin_order, track in enumerate(source_data):
                connection.execute(
                    """
                    INSERT INTO track_merge_origins(track_id,origin_order,source_track_id,source_display_name,source_filename,source_type)
                    VALUES(?,?,?,?,?,?)
                    """,
                    (new_track_id, origin_order, track["id"], track["name"], track["source_filename"], track["source_type"]),
                )
                source_segments = connection.execute(
                    "SELECT * FROM track_segments WHERE track_id=? ORDER BY segment_index", (track["id"],)
                ).fetchall()
                for source_segment in source_segments:
                    seg_cursor = connection.execute(
                        """
                        INSERT INTO track_segments(track_id,segment_index,assigned_date,manual_date,manual_timezone_id,created_at_utc)
                        VALUES(?,?,?,?,?,?)
                        """,
                        (new_track_id, new_segment_index, source_segment["assigned_date"], source_segment["manual_date"], source_segment["manual_timezone_id"], now),
                    )
                    new_segment_index += 1
                    new_segment_id = int(seg_cursor.lastrowid)
                    connection.execute(
                        """
                        INSERT INTO track_points(segment_id,point_index,latitude,longitude,altitude_m,utc_time,device_time,
                            detected_timezone_id,effective_timezone_id,effective_local_time,timezone_source,speed_kmh,
                            course_degrees,satellites,raw_line_number,raw_fields_json)
                        SELECT ?,point_index,latitude,longitude,altitude_m,utc_time,device_time,detected_timezone_id,
                            effective_timezone_id,effective_local_time,timezone_source,speed_kmh,course_degrees,satellites,
                            raw_line_number,raw_fields_json FROM track_points WHERE segment_id=? ORDER BY point_index
                        """,
                        (new_segment_id, int(source_segment["id"])),
                    )
            for table, column in (("track_places", "place_id"), ("track_companions", "companion_id"), ("track_activities", "activity_id")):
                connection.execute(
                    f"INSERT OR IGNORE INTO {table}(track_id,{column}) SELECT ?,{column} FROM {table} WHERE track_id IN ({','.join('?' for _ in ordered)})",
                    [new_track_id, *ordered],
                )
            self._refresh_track_stats(connection, new_track_id)
            connection.execute(f"DELETE FROM tracks WHERE id IN ({','.join('?' for _ in ordered)})", ordered)
            return new_track_id

    # ---------- Reference data ----------
    def list_places(self, search: str = "", limit: int = 500) -> list[sqlite3.Row]:
        params: list[object] = []
        where = ""
        if search.strip():
            token = f"%{search.strip().lower()}%"
            where = "WHERE LOWER(display_name) LIKE ? OR LOWER(full_name) LIKE ? OR LOWER(search_names) LIKE ?"
            params.extend([token, token, token])
        params.append(limit)
        with self.database.connect() as connection:
            return connection.execute(
                f"SELECT * FROM places {where} ORDER BY CASE place_type WHEN 'country' THEN 0 WHEN 'admin1' THEN 1 ELSE 2 END,full_name LIMIT ?",
                params,
            ).fetchall()

    def list_companion_groups(self) -> list[sqlite3.Row]:
        with self.database.connect() as connection:
            return connection.execute("SELECT * FROM companion_groups ORDER BY is_system DESC,sort_order,name").fetchall()

    def list_companions(self) -> list[sqlite3.Row]:
        with self.database.connect() as connection:
            return connection.execute(
                """
                SELECT c.*,g.name AS group_name,g.sort_order AS group_sort FROM companions c
                JOIN companion_groups g ON g.id=c.group_id ORDER BY g.is_system DESC,g.sort_order,c.sort_order,c.name
                """
            ).fetchall()

    def list_activities(self) -> list[sqlite3.Row]:
        with self.database.connect() as connection:
            return connection.execute("SELECT * FROM activities ORDER BY sort_order,name").fetchall()

    def save_companion_group(self, group_id: int | None, name: str, sort_order: int) -> int:
        name = name.strip()
        if not name:
            raise ValueError("旅伴群組名稱不可空白")
        with self.database.transaction() as connection:
            if group_id:
                connection.execute("UPDATE companion_groups SET name=?,sort_order=? WHERE id=? AND is_system=0", (name, sort_order, group_id))
                return group_id
            cursor = connection.execute("INSERT INTO companion_groups(name,sort_order,is_system) VALUES(?,?,0)", (name, sort_order))
            return int(cursor.lastrowid)

    def save_companion(self, companion_id: int | None, group_id: int, name: str, notes: str, sort_order: int) -> int:
        name = name.strip()
        if not name:
            raise ValueError("旅伴名稱不可空白")
        with self.database.transaction() as connection:
            if companion_id:
                connection.execute("UPDATE companions SET group_id=?,name=?,notes=?,sort_order=? WHERE id=?", (group_id, name, notes.strip(), sort_order, companion_id))
                return companion_id
            cursor = connection.execute("INSERT INTO companions(group_id,name,notes,sort_order) VALUES(?,?,?,?)", (group_id, name, notes.strip(), sort_order))
            return int(cursor.lastrowid)

    def save_activity(self, activity_id: int | None, name: str, notes: str, sort_order: int) -> int:
        name = name.strip()
        if not name:
            raise ValueError("活動名稱不可空白")
        with self.database.transaction() as connection:
            if activity_id:
                connection.execute("UPDATE activities SET name=?,notes=?,sort_order=? WHERE id=?", (name, notes.strip(), sort_order, activity_id))
                return activity_id
            cursor = connection.execute("INSERT INTO activities(name,notes,sort_order) VALUES(?,?,?)", (name, notes.strip(), sort_order))
            return int(cursor.lastrowid)

    def delete_reference(self, table: str, record_id: int) -> None:
        allowed = {"companion_groups", "companions", "activities"}
        if table not in allowed:
            raise ValueError("不允許的資料表")
        with self.database.transaction() as connection:
            if table == "companion_groups":
                row = connection.execute("SELECT is_system FROM companion_groups WHERE id=?", (record_id,)).fetchone()
                if row and bool(row[0]):
                    raise ValueError("系統群組不可刪除")
            connection.execute(f"DELETE FROM {table} WHERE id=?", (record_id,))

    @staticmethod
    def _refresh_track_stats(connection: sqlite3.Connection, track_id: int) -> None:
        connection.execute(
            """
            INSERT INTO track_stats(track_id,segment_count,point_count,first_local_date,last_local_date,updated_at_utc)
            SELECT k.id,COUNT(DISTINCT s.id),COUNT(p.id),
                   MIN(DATE(COALESCE(p.effective_local_time,s.assigned_date,k.assigned_date))),
                   MAX(DATE(COALESCE(p.effective_local_time,s.assigned_date,k.assigned_date))),?
            FROM tracks k
            LEFT JOIN track_segments s ON s.track_id=k.id
            LEFT JOIN track_points p ON p.segment_id=s.id
            WHERE k.id=? GROUP BY k.id
            ON CONFLICT(track_id) DO UPDATE SET
                segment_count=excluded.segment_count,point_count=excluded.point_count,
                first_local_date=excluded.first_local_date,last_local_date=excluded.last_local_date,
                updated_at_utc=excluded.updated_at_utc
            """,
            (Repository._now(), track_id),
        )

    # ---------- Helpers ----------
    def _tag_filter(self, kind: str, ids: Sequence[int], match: str) -> tuple[str, list[object]]:
        placeholders = ",".join("?" for _ in ids)
        if kind == "place":
            relation = """
                SELECT DISTINCT linked.id FROM places selected
                JOIN places linked ON (
                    (selected.place_type='country' AND linked.country_id=selected.country_id) OR
                    (selected.place_type='admin1' AND linked.country_id=selected.country_id AND linked.admin1_id=selected.admin1_id) OR
                    (selected.place_type='city' AND linked.country_id=selected.country_id AND linked.admin1_id=selected.admin1_id AND linked.city_id=selected.city_id)
                ) WHERE selected.id IN ({ids})
            """.format(ids=placeholders)
            linked = f"SELECT place_id AS id FROM track_places WHERE track_id=k.id UNION SELECT place_id FROM trip_places WHERE trip_id=k.trip_id"
        elif kind == "companion":
            relation = f"SELECT id FROM companions WHERE id IN ({placeholders})"
            linked = "SELECT companion_id AS id FROM track_companions WHERE track_id=k.id UNION SELECT companion_id FROM trip_companions WHERE trip_id=k.trip_id"
        else:
            relation = f"SELECT id FROM activities WHERE id IN ({placeholders})"
            linked = "SELECT activity_id AS id FROM track_activities WHERE track_id=k.id UNION SELECT activity_id FROM trip_activities WHERE trip_id=k.trip_id"
        if match.upper() == "ALL":
            condition = f"(SELECT COUNT(DISTINCT id) FROM ({linked}) WHERE id IN ({relation})) >= ?"
            return condition, [*ids, len(ids)]
        condition = f"EXISTS(SELECT 1 FROM ({linked}) WHERE id IN ({relation}))"
        return condition, list(ids)

    @staticmethod
    def _replace_relations(connection: sqlite3.Connection, table: str, owner_column: str, owner_id: int, value_column: str, values: Iterable[int]) -> None:
        connection.execute(f"DELETE FROM {table} WHERE {owner_column}=?", (owner_id,))
        unique = list(dict.fromkeys(int(value) for value in values))
        connection.executemany(
            f"INSERT INTO {table}({owner_column},{value_column}) VALUES(?,?)",
            [(owner_id, value) for value in unique],
        )

    @staticmethod
    def _trip(row: sqlite3.Row) -> TripRecord:
        return TripRecord(
            id=int(row["id"]), name=str(row["name"]), start_date=Repository._parse_date(row["start_date"]),
            end_date=Repository._parse_date(row["end_date"]), notes=str(row["notes"]),
            sort_order=int(row["sort_order"]), is_system=bool(row["is_system"]),
        )

    @staticmethod
    def _track(row: sqlite3.Row) -> TrackSummary:
        return TrackSummary(
            id=int(row["id"]), trip_id=int(row["trip_id"]), trip_name=str(row["trip_name"]),
            display_name=str(row["display_name"]), source_name=str(row["source_filename"] or row["source_name"] or ""),
            source_type=str(row["source_type"]), assigned_date=Repository._parse_date(row["assigned_date"]),
            first_local_date=Repository._parse_date(row["first_local_date"]), last_local_date=Repository._parse_date(row["last_local_date"]),
            time_data_status=str(row["time_data_status"]), timezone_mode=str(row["timezone_mode"]),
            fixed_timezone_id=row["fixed_timezone_id"], notes=str(row["notes"]), sort_order=int(row["sort_order"]),
            is_checked=bool(row["is_checked"]), segment_count=int(row["segment_count"] or 0), point_count=int(row["point_count"] or 0),
        )

    @staticmethod
    def _now() -> str:
        return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

    @staticmethod
    def _date(value: date | None) -> str | None:
        return value.isoformat() if value else None

    @staticmethod
    def _datetime(value: datetime | None, utc: bool = True) -> str | None:
        if value is None:
            return None
        if utc:
            if value.tzinfo is None:
                value = value.replace(tzinfo=timezone.utc)
            return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")
        return value.replace(tzinfo=None).isoformat(sep=" ", timespec="seconds")

    @staticmethod
    def _parse_date(value: object) -> date | None:
        if not value:
            return None
        try:
            return date.fromisoformat(str(value)[:10])
        except ValueError:
            return None

    @staticmethod
    def _parse_datetime(value: str) -> datetime:
        text = value.replace("Z", "+00:00")
        result = datetime.fromisoformat(text)
        return result if result.tzinfo else result.replace(tzinfo=timezone.utc)
