from __future__ import annotations

import math
import os
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

from .. import __version__
from ..models import ParsedTrack, TrackPoint, TrackSegmentData
from ..operations import CancellationToken, ProgressCallback, ProgressUpdate
from .timezone_service import TimeZoneService

GPX_NS = "http://www.topografix.com/GPX/1/1"
GPX_PARSER_VERSION = "GPX-PY-1.4"


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _child_text(element: ET.Element, name: str) -> str | None:
    for child in element:
        if _local_name(child.tag) == name:
            return child.text.strip() if child.text else None
    return None


def _parse_iso_time(text: str | None) -> datetime | None:
    if not text:
        return None
    value = text.strip()
    if value.endswith("Z"):
        value = value[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(value)
        if parsed.tzinfo is None:
            parsed = parsed.replace(tzinfo=timezone.utc)
        return parsed.astimezone(timezone.utc)
    except ValueError:
        return None


def _parse_track_element(
    trk: ET.Element,
    path: Path,
    track_index: int,
    timezone_service: TimeZoneService,
    token: CancellationToken,
    progress: ProgressCallback | None,
) -> ParsedTrack | None:
    name = _child_text(trk, "name") or f"{path.stem}-{track_index + 1}"
    segments: list[TrackSegmentData] = []
    timed_points = 0
    untimed_points = 0
    invalid_points = 0
    point_count = 0
    for segment_index, trkseg in enumerate(child for child in trk if _local_name(child.tag) == "trkseg"):
        points: list[TrackPoint] = []
        for trkpt in (child for child in trkseg if _local_name(child.tag) == "trkpt"):
            token.raise_if_cancelled()
            try:
                latitude = float(trkpt.attrib["lat"])
                longitude = float(trkpt.attrib["lon"])
                if not math.isfinite(latitude) or not math.isfinite(longitude):
                    raise ValueError
                if not (-90 <= latitude <= 90 and -180 <= longitude <= 180):
                    raise ValueError
            except (KeyError, ValueError):
                invalid_points += 1
                continue
            altitude = None
            ele = _child_text(trkpt, "ele")
            try:
                parsed_altitude = float(ele) if ele is not None else None
                altitude = parsed_altitude if parsed_altitude is None or math.isfinite(parsed_altitude) else None
            except ValueError:
                pass
            utc_time = _parse_iso_time(_child_text(trkpt, "time"))
            timezone_id = None
            local_time = None
            if utc_time is not None:
                timed_points += 1
                timezone_id = timezone_service.detect_for_point(latitude, longitude)
                local_time = timezone_service.to_local(utc_time, timezone_id)
            else:
                untimed_points += 1
            points.append(
                TrackPoint(
                    latitude=latitude,
                    longitude=longitude,
                    altitude_m=altitude,
                    utc_time=utc_time,
                    effective_local_time=local_time,
                    detected_timezone_id=timezone_id,
                    effective_timezone_id=timezone_id,
                )
            )
            point_count += 1
            if progress and point_count % 2000 == 0:
                progress(ProgressUpdate(point_count, 0, f"正在解析 GPX：{point_count:,} 點"))
        if points:
            assigned_date = next(
                (point.effective_local_time.date() for point in points if point.effective_local_time is not None),
                None,
            )
            segments.append(TrackSegmentData(segment_index, points, assigned_date=assigned_date))
    if not segments:
        return None
    if timed_points and untimed_points:
        time_status = "mixed"
    elif timed_points:
        time_status = "original"
    else:
        time_status = "missing"
    return ParsedTrack(
        name=name,
        source_type="GPX",
        source_path=path,
        segments=segments,
        time_data_status=time_status,
        assigned_date=segments[0].assigned_date,
        metadata={
            "parser_version": GPX_PARSER_VERSION,
            "track_index": track_index,
            "segment_count": len(segments),
            "valid_points": point_count,
            "invalid_points": invalid_points,
            "timed_points": timed_points,
            "untimed_points": untimed_points,
        },
    )


def parse_gpx_file(
    path: Path,
    timezone_service: TimeZoneService,
    token: CancellationToken | None = None,
    progress: ProgressCallback | None = None,
) -> list[ParsedTrack]:
    path = Path(path)
    token = token or CancellationToken()
    tracks: list[ParsedTrack] = []
    track_index = 0
    for event, element in ET.iterparse(path, events=("end",)):
        token.raise_if_cancelled()
        if _local_name(element.tag) != "trk":
            continue
        parsed = _parse_track_element(element, path, track_index, timezone_service, token, progress)
        if parsed is not None:
            tracks.append(parsed)
        track_index += 1
        element.clear()
    return tracks


def _atomic_tree_write(tree: ET.ElementTree, target: Path) -> Path:
    target = Path(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(target.name + ".tmp")
    temporary.unlink(missing_ok=True)
    try:
        tree.write(temporary, encoding="utf-8", xml_declaration=True)
        with temporary.open("rb") as handle:
            os.fsync(handle.fileno())
        temporary.replace(target)
        return target
    except Exception:
        temporary.unlink(missing_ok=True)
        raise


def write_gpx(tracks: list[dict], target: Path) -> Path:
    ET.register_namespace("", GPX_NS)
    root = ET.Element(f"{{{GPX_NS}}}gpx", {"version": "1.1", "creator": f"Travel Memory {__version__}"})
    metadata = ET.SubElement(root, f"{{{GPX_NS}}}metadata")
    ET.SubElement(metadata, f"{{{GPX_NS}}}name").text = target.stem
    for track in tracks:
        trk = ET.SubElement(root, f"{{{GPX_NS}}}trk")
        ET.SubElement(trk, f"{{{GPX_NS}}}name").text = str(track["name"])
        if track.get("notes"):
            ET.SubElement(trk, f"{{{GPX_NS}}}desc").text = str(track["notes"])
        for segment in track.get("segments", []):
            trkseg = ET.SubElement(trk, f"{{{GPX_NS}}}trkseg")
            for point in segment.get("points", []):
                trkpt = ET.SubElement(
                    trkseg,
                    f"{{{GPX_NS}}}trkpt",
                    {"lat": format(float(point["lat"]), ".10f"), "lon": format(float(point["lon"]), ".10f")},
                )
                if point.get("altitude") is not None:
                    ET.SubElement(trkpt, f"{{{GPX_NS}}}ele").text = format(float(point["altitude"]), ".3f")
                if point.get("utc"):
                    value = str(point["utc"])
                    if value.endswith("+00:00"):
                        value = value[:-6] + "Z"
                    ET.SubElement(trkpt, f"{{{GPX_NS}}}time").text = value
    tree = ET.ElementTree(root)
    ET.indent(tree, space="  ")
    return _atomic_tree_write(tree, Path(target))


def write_kml(tracks: list[dict], target: Path) -> Path:
    ns = "http://www.opengis.net/kml/2.2"
    ET.register_namespace("", ns)
    root = ET.Element(f"{{{ns}}}kml")
    document = ET.SubElement(root, f"{{{ns}}}Document")
    ET.SubElement(document, f"{{{ns}}}name").text = target.stem
    colors = ["ff2f80ed", "ff27ae60", "fff2994a", "ff9b51e0", "ffeb5757", "ff219653"]
    for index, track in enumerate(tracks):
        style_id = f"track-{index}"
        style = ET.SubElement(document, f"{{{ns}}}Style", {"id": style_id})
        line_style = ET.SubElement(style, f"{{{ns}}}LineStyle")
        ET.SubElement(line_style, f"{{{ns}}}color").text = colors[index % len(colors)]
        ET.SubElement(line_style, f"{{{ns}}}width").text = "3"
        folder = ET.SubElement(document, f"{{{ns}}}Folder")
        ET.SubElement(folder, f"{{{ns}}}name").text = str(track["name"])
        for segment_index, segment in enumerate(track.get("segments", [])):
            placemark = ET.SubElement(folder, f"{{{ns}}}Placemark")
            ET.SubElement(placemark, f"{{{ns}}}name").text = f"{track['name']} - {segment_index + 1}"
            ET.SubElement(placemark, f"{{{ns}}}styleUrl").text = f"#{style_id}"
            line = ET.SubElement(placemark, f"{{{ns}}}LineString")
            ET.SubElement(line, f"{{{ns}}}tessellate").text = "1"
            coordinates = []
            for point in segment.get("points", []):
                altitude = float(point.get("altitude") or 0.0)
                coordinates.append(f"{float(point['lon']):.10f},{float(point['lat']):.10f},{altitude:.3f}")
            ET.SubElement(line, f"{{{ns}}}coordinates").text = " ".join(coordinates)
    tree = ET.ElementTree(root)
    ET.indent(tree, space="  ")
    return _atomic_tree_write(tree, Path(target))
