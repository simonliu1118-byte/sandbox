from __future__ import annotations

import html
import os
from pathlib import Path
from typing import Iterable, Sequence, TextIO

from ..database import Database
from ..operations import CancellationToken, ProgressCallback, ProgressUpdate


def _xml(value: object) -> str:
    return html.escape(str(value), quote=True)


def _utc(value: object) -> str:
    text = str(value or "")
    return text[:-6] + "Z" if text.endswith("+00:00") else text


class ExportService:
    """Streaming GPX/KML writer backed directly by SQLite cursors."""

    def __init__(self, database: Database) -> None:
        self.database = database

    def export_gpx(
        self,
        track_ids: Sequence[int],
        target: Path,
        token: CancellationToken | None = None,
        progress: ProgressCallback | None = None,
    ) -> Path:
        return self._export(track_ids, target, "gpx", token, progress)

    def export_kml(
        self,
        track_ids: Sequence[int],
        target: Path,
        token: CancellationToken | None = None,
        progress: ProgressCallback | None = None,
    ) -> Path:
        return self._export(track_ids, target, "kml", token, progress)

    def _export(
        self,
        track_ids: Sequence[int],
        target: Path,
        kind: str,
        token: CancellationToken | None,
        progress: ProgressCallback | None,
    ) -> Path:
        ids = list(dict.fromkeys(int(value) for value in track_ids))
        if not ids:
            raise ValueError("沒有可匯出的軌跡")
        target = Path(target)
        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(target.name + ".tmp")
        temporary.unlink(missing_ok=True)
        token = token or CancellationToken()
        placeholders = ",".join("?" for _ in ids)
        try:
            with self.database.connect(readonly=True) as connection:
                total = int(
                    connection.execute(
                        f"""
                        SELECT COUNT(*) FROM track_points p
                        JOIN track_segments s ON s.id=p.segment_id
                        WHERE s.track_id IN ({placeholders})
                        """,
                        ids,
                    ).fetchone()[0]
                )
                tracks = connection.execute(
                    f"""
                    SELECT k.id,COALESCE(k.custom_name,k.source_name,'未命名軌跡') AS name,
                           k.assigned_date,k.notes,t.name AS trip_name
                    FROM tracks k JOIN trips t ON t.id=k.trip_id
                    WHERE k.id IN ({placeholders})
                    ORDER BY k.assigned_date,k.sort_order,k.id
                    """,
                    ids,
                ).fetchall()
                with temporary.open("w", encoding="utf-8", newline="\n") as handle:
                    if kind == "gpx":
                        self._write_gpx(connection, tracks, handle, token, progress, total)
                    else:
                        self._write_kml(connection, tracks, handle, token, progress, total)
                    handle.flush()
                    os.fsync(handle.fileno())
            token.raise_if_cancelled()
            temporary.replace(target)
            return target
        except Exception:
            temporary.unlink(missing_ok=True)
            raise

    def _write_gpx(self, connection, tracks: Iterable, handle: TextIO, token, progress, total: int) -> None:
        handle.write('<?xml version="1.0" encoding="UTF-8"?>\n')
        handle.write('<gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1" creator="Travel Memory 1.0.0">\n')
        done = 0
        for track in tracks:
            token.raise_if_cancelled()
            handle.write("  <trk>\n")
            handle.write(f"    <name>{_xml(track['name'])}</name>\n")
            if track["notes"]:
                handle.write(f"    <desc>{_xml(track['notes'])}</desc>\n")
            segments = connection.execute(
                "SELECT id,segment_index,assigned_date FROM track_segments WHERE track_id=? ORDER BY segment_index",
                (int(track["id"]),),
            )
            for segment in segments:
                handle.write("    <trkseg>\n")
                cursor = connection.execute(
                    """
                    SELECT latitude,longitude,altitude_m,utc_time FROM track_points
                    WHERE segment_id=? ORDER BY point_index
                    """,
                    (int(segment["id"]),),
                )
                while True:
                    rows = cursor.fetchmany(2000)
                    if not rows:
                        break
                    for point in rows:
                        token.raise_if_cancelled()
                        handle.write(
                            f'      <trkpt lat="{float(point["latitude"]):.10f}" '
                            f'lon="{float(point["longitude"]):.10f}">\n'
                        )
                        if point["altitude_m"] is not None:
                            handle.write(f"        <ele>{float(point['altitude_m']):.3f}</ele>\n")
                        if point["utc_time"]:
                            handle.write(f"        <time>{_xml(_utc(point['utc_time']))}</time>\n")
                        handle.write("      </trkpt>\n")
                        done += 1
                    if progress:
                        progress(ProgressUpdate(done, total, f"正在輸出 GPX：{done:,}/{total:,}"))
                handle.write("    </trkseg>\n")
            handle.write("  </trk>\n")
        handle.write("</gpx>\n")

    def _write_kml(self, connection, tracks: Iterable, handle: TextIO, token, progress, total: int) -> None:
        handle.write('<?xml version="1.0" encoding="UTF-8"?>\n')
        handle.write('<kml xmlns="http://www.opengis.net/kml/2.2"><Document>\n')
        done = 0
        for track in tracks:
            token.raise_if_cancelled()
            handle.write(f"  <Folder><name>{_xml(track['name'])}</name>\n")
            segments = connection.execute(
                "SELECT id,segment_index,assigned_date FROM track_segments WHERE track_id=? ORDER BY segment_index",
                (int(track["id"]),),
            )
            for segment in segments:
                handle.write(
                    f"    <Placemark><name>{_xml(track['name'])} - {int(segment['segment_index']) + 1}</name>"
                    "<LineString><tessellate>1</tessellate><coordinates>\n"
                )
                cursor = connection.execute(
                    """
                    SELECT latitude,longitude,altitude_m FROM track_points
                    WHERE segment_id=? ORDER BY point_index
                    """,
                    (int(segment["id"]),),
                )
                while True:
                    rows = cursor.fetchmany(2000)
                    if not rows:
                        break
                    for point in rows:
                        token.raise_if_cancelled()
                        altitude = float(point["altitude_m"] or 0.0)
                        handle.write(
                            f"{float(point['longitude']):.10f},{float(point['latitude']):.10f},{altitude:.3f} "
                        )
                        done += 1
                    if progress:
                        progress(ProgressUpdate(done, total, f"正在輸出 KML：{done:,}/{total:,}"))
                handle.write("\n</coordinates></LineString></Placemark>\n")
            handle.write("  </Folder>\n")
        handle.write("</Document></kml>\n")
