from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date, datetime
from pathlib import Path
from typing import Any


@dataclass(slots=True)
class TrackPoint:
    latitude: float
    longitude: float
    utc_time: datetime | None = None
    device_time: datetime | None = None
    effective_local_time: datetime | None = None
    detected_timezone_id: str | None = None
    effective_timezone_id: str | None = None
    timezone_source: str = "auto"
    altitude_m: float | None = None
    speed_kmh: float | None = None
    course_degrees: float | None = None
    satellites: int | None = None
    raw_line_number: int | None = None
    raw_fields_json: str | None = None


@dataclass(slots=True)
class TrackSegmentData:
    index: int
    points: list[TrackPoint] = field(default_factory=list)
    assigned_date: date | None = None
    manual_date: date | None = None
    manual_timezone_id: str | None = None


@dataclass(slots=True)
class ParsedTrack:
    name: str
    source_type: str
    source_path: Path
    segments: list[TrackSegmentData]
    time_data_status: str = "original"
    timezone_mode: str = "auto"
    fixed_timezone_id: str | None = None
    assigned_date: date | None = None
    skipped_lines: list[tuple[int, str]] = field(default_factory=list)
    metadata: dict[str, Any] = field(default_factory=dict)

    @property
    def point_count(self) -> int:
        return sum(len(segment.points) for segment in self.segments)


@dataclass(slots=True)
class TrackSummary:
    id: int
    trip_id: int
    trip_name: str
    display_name: str
    source_name: str
    source_type: str
    assigned_date: date | None
    first_local_date: date | None
    last_local_date: date | None
    time_data_status: str
    timezone_mode: str
    fixed_timezone_id: str | None
    notes: str
    sort_order: int
    is_checked: bool
    segment_count: int
    point_count: int


@dataclass(slots=True)
class TripRecord:
    id: int
    name: str
    start_date: date | None
    end_date: date | None
    notes: str
    sort_order: int
    is_system: bool = False


@dataclass(slots=True)
class FilterCriteria:
    date_from: date | None = None
    date_to: date | None = None
    place_ids: list[int] = field(default_factory=list)
    companion_ids: list[int] = field(default_factory=list)
    activity_ids: list[int] = field(default_factory=list)
    category_operator: str = "AND"
    place_match: str = "ANY"
    companion_match: str = "ANY"
    activity_match: str = "ANY"


@dataclass(slots=True)
class ImportReport:
    imported_track_ids: list[int] = field(default_factory=list)
    skipped_files: list[tuple[Path, str]] = field(default_factory=list)
    source_reports: list[dict[str, Any]] = field(default_factory=list)
