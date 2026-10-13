from __future__ import annotations

import csv
import json
import math
from collections import Counter, OrderedDict
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable, TextIO

from ..models import ParsedTrack, TrackPoint, TrackSegmentData
from ..operations import CancellationToken, ProgressCallback, ProgressUpdate
from .timezone_service import TimeZoneService

RITI_TIME_FORMAT = "%Y/%m/%d %H:%M:%S"
NEW_SEGMENT_SECONDS = 15 * 60
RITI_PARSER_VERSION = "RITI-PY-1.2"


def dddmm_to_decimal(value: float) -> float:
    """將 DDDMM.MMMMM／DDMM.MMMMM 度分格式轉為十進位角度。"""
    degree = int(value // 100)
    minutes = value - degree * 100
    if minutes < 0 or minutes >= 60:
        raise ValueError("度分格式的分欄位超出 0～60")
    return degree + minutes / 60.0


def decode_riti_coordinate(raw_first: float, raw_second: float) -> tuple[float, float]:
    """解碼 RITI TXT 前兩欄，回傳 (latitude, longitude)。

    RITI 檔案實際欄值含裝置編碼；以下公式由 260530/260602 樣本硬性驗證。
    解碼結果為 WGS84 十進位度，不再做四捨五入。
    """
    if not math.isfinite(raw_first) or not math.isfinite(raw_second):
        raise ValueError("座標不是有限數值")
    if raw_first < 0 or raw_second < 0:
        raise ValueError("座標不得為負的編碼值")
    longitude = ((raw_first % 31000.0) - 17.0) / 31.0
    latitude = ((raw_second % 3100.0) - 17.0) / 31.0
    if not (-90.0 <= latitude <= 90.0 and -180.0 <= longitude <= 180.0):
        raise ValueError("解碼後座標超出有效範圍")
    return latitude, longitude


def _nullable_float(fields: list[str], index: int) -> float | None:
    if index >= len(fields):
        return None
    value = fields[index].strip()
    if not value or value == "-":
        return None
    try:
        return float(value)
    except ValueError:
        return None


def _nullable_int(fields: list[str], index: int) -> int | None:
    value = _nullable_float(fields, index)
    return int(value) if value is not None else None


def parse_riti_stream(
    stream: TextIO,
    source_path: Path,
    timezone_service: TimeZoneService,
    token: CancellationToken | None = None,
    progress: ProgressCallback | None = None,
    total_rows_hint: int = 0,
) -> ParsedTrack:
    points: list[TrackPoint] = []
    skipped: list[tuple[int, str]] = []
    reader = csv.reader(stream)
    total_rows = 0
    token = token or CancellationToken()
    for line_number, fields in enumerate(reader, start=1):
        token.raise_if_cancelled()
        total_rows += 1
        if progress and (line_number == 1 or line_number % 250 == 0):
            progress(
                ProgressUpdate(
                    line_number,
                    total_rows_hint,
                    f"正在解析 RITI：{line_number:,} / {total_rows_hint:,} 列"
                    if total_rows_hint
                    else f"正在解析 RITI：第 {line_number:,} 列",
                )
            )
        if not fields or all(not field.strip() for field in fields):
            skipped.append((line_number, "空白列"))
            continue
        if len(fields) < 8:
            skipped.append((line_number, "欄位不足"))
            continue
        try:
            raw_first = float(fields[0].strip())
            raw_second = float(fields[1].strip())
            latitude, longitude = decode_riti_coordinate(raw_first, raw_second)
        except ValueError:
            skipped.append((line_number, "座標欄位無法解析"))
            continue
        try:
            utc_naive = datetime.strptime(fields[2].strip(), RITI_TIME_FORMAT)
            utc_time = utc_naive.replace(tzinfo=timezone.utc)
        except ValueError:
            skipped.append((line_number, "UTC 時間無法解析"))
            continue
        try:
            device_time = datetime.strptime(fields[7].strip(), RITI_TIME_FORMAT)
        except ValueError:
            skipped.append((line_number, "裝置時間無法解析"))
            continue

        timezone_id = timezone_service.detect_for_point(latitude, longitude)
        effective_local = timezone_service.to_local(utc_time, timezone_id)
        points.append(
            TrackPoint(
                latitude=latitude,
                longitude=longitude,
                utc_time=utc_time,
                device_time=device_time,
                effective_local_time=effective_local,
                detected_timezone_id=timezone_id,
                effective_timezone_id=timezone_id,
                timezone_source="auto",
                speed_kmh=_nullable_float(fields, 3),
                course_degrees=_nullable_float(fields, 4),
                satellites=_nullable_int(fields, 6),
                altitude_m=_nullable_float(fields, 12) if len(fields) >= 14 else _nullable_float(fields, 11),
                raw_line_number=line_number,
                raw_fields_json=json.dumps(fields, ensure_ascii=False, separators=(",", ":")),
            )
        )

    segments: list[TrackSegmentData] = []
    current: list[TrackPoint] = []
    previous_utc: datetime | None = None
    for point in points:
        if (
            current
            and previous_utc is not None
            and point.utc_time is not None
            and (point.utc_time - previous_utc).total_seconds() > NEW_SEGMENT_SECONDS
        ):
            segments.append(_make_segment(len(segments), current))
            current = []
        current.append(point)
        previous_utc = point.utc_time
    if current:
        segments.append(_make_segment(len(segments), current))

    assigned_date = segments[0].assigned_date if segments else None
    if progress:
        progress(ProgressUpdate(total_rows, total_rows_hint or total_rows, f"RITI 解析完成：{total_rows:,} 列"))
    return ParsedTrack(
        name=source_path.stem,
        source_type="RITI_TXT",
        source_path=source_path,
        segments=segments,
        assigned_date=assigned_date,
        skipped_lines=skipped,
        metadata={
            "parser_version": RITI_PARSER_VERSION,
            "total_rows": total_rows,
            "valid_points": len(points),
            "skipped_rows": len(skipped),
            "segment_count": len(segments),
            "segment_sizes": [len(segment.points) for segment in segments],
            "skip_reasons": dict(Counter(reason for _, reason in skipped)),
        },
    )


def _make_segment(index: int, points: list[TrackPoint]) -> TrackSegmentData:
    assigned = points[0].effective_local_time.date() if points and points[0].effective_local_time else None
    return TrackSegmentData(index=index, points=list(points), assigned_date=assigned)


def parse_riti_file(
    path: Path,
    timezone_service: TimeZoneService,
    token: CancellationToken | None = None,
    progress: ProgressCallback | None = None,
) -> ParsedTrack:
    path = Path(path)
    last_error: UnicodeDecodeError | None = None
    for encoding in ("utf-8-sig", "cp950", "shift_jis"):
        try:
            if progress:
                progress(ProgressUpdate(0, 0, f"正在準備解析 {path.name}"))
            with path.open("r", encoding=encoding, newline="") as stream:
                total_rows = sum(1 for _ in stream)
                token = token or CancellationToken()
                token.raise_if_cancelled()
            with path.open("r", encoding=encoding, newline="") as stream:
                parsed = parse_riti_stream(
                    stream,
                    path,
                    timezone_service,
                    token,
                    progress,
                    total_rows_hint=total_rows,
                )
                parsed.metadata["text_encoding"] = encoding
                return parsed
        except UnicodeDecodeError as exc:
            last_error = exc
    assert last_error is not None
    raise last_error


def split_riti_track_by_local_date(parsed: ParsedTrack) -> list[ParsedTrack]:
    """將多日 RITI 來源拆成每日軌跡，同一 segment 不因午夜而切斷。

    segment 若跨越午夜，依最後一個有本地時間的定位點日期歸類，符合跨日
    軌跡以最後點日期顯示的既定規則。
    """
    groups: OrderedDict[object, list[TrackSegmentData]] = OrderedDict()
    for segment in parsed.segments:
        local_date = None
        for point in reversed(segment.points):
            if point.effective_local_time is not None:
                local_date = point.effective_local_time.date()
                break
        group_date = local_date or segment.assigned_date or parsed.assigned_date
        groups.setdefault(group_date, []).append(segment)

    if len(groups) <= 1:
        return [parsed]

    tracks: list[ParsedTrack] = []
    for track_index, (group_date, segments) in enumerate(groups.items()):
        daily_segments = [
            TrackSegmentData(
                index=index,
                points=list(segment.points),
                assigned_date=segment.assigned_date,
                manual_date=segment.manual_date,
                manual_timezone_id=segment.manual_timezone_id,
            )
            for index, segment in enumerate(segments)
        ]
        metadata = {
            "parser_version": parsed.metadata.get("parser_version", RITI_PARSER_VERSION),
            "text_encoding": parsed.metadata.get("text_encoding", ""),
            "total_rows": int(parsed.metadata.get("total_rows", 0)) if track_index == 0 else 0,
            "valid_points": sum(len(segment.points) for segment in daily_segments),
            "skipped_rows": len(parsed.skipped_lines) if track_index == 0 else 0,
            "segment_count": len(daily_segments),
            "segment_sizes": [len(segment.points) for segment in daily_segments],
            "split_date": group_date.isoformat() if group_date else None,
        }
        tracks.append(
            ParsedTrack(
                name=parsed.name,
                source_type=parsed.source_type,
                source_path=parsed.source_path,
                segments=daily_segments,
                time_data_status=parsed.time_data_status,
                timezone_mode=parsed.timezone_mode,
                fixed_timezone_id=parsed.fixed_timezone_id,
                assigned_date=group_date,
                skipped_lines=list(parsed.skipped_lines) if track_index == 0 else [],
                metadata=metadata,
            )
        )
    return tracks


def validate_reference_files(paths: Iterable[Path]) -> dict[str, dict[str, object]]:
    service = TimeZoneService()
    expected = {
        "260530.txt": (21, 290, 36.40144679, 136.41705700),
        "260602.txt": (482, 25, 36.71915400, 137.16535174),
    }
    reports: dict[str, dict[str, object]] = {}
    for path in paths:
        result = parse_riti_file(Path(path), service)
        first = result.segments[0].points[0] if result.segments and result.segments[0].points else None
        exp = expected.get(Path(path).name)
        passed = bool(
            first
            and exp
            and result.point_count == exp[0]
            and len(result.skipped_lines) == exp[1]
            and abs(first.latitude - exp[2]) < 1e-8
            and abs(first.longitude - exp[3]) < 1e-8
        )
        reports[Path(path).name] = {
            "passed": passed,
            "valid_points": result.point_count,
            "skipped_rows": len(result.skipped_lines),
            "first_latitude": first.latitude if first else None,
            "first_longitude": first.longitude if first else None,
        }
    return reports
