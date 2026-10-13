from __future__ import annotations

import logging
from pathlib import Path
from typing import Iterable

from ..models import ImportReport
from ..operations import CancellationToken, OperationCancelled, ProgressCallback, ProgressUpdate
from ..repository import DuplicateImportError, Repository
from .gpx_service import parse_gpx_file
from .riti_parser import parse_riti_file, split_riti_track_by_local_date
from .timezone_service import TimeZoneService


class ImportService:
    def __init__(self, repository: Repository, timezone_service: TimeZoneService, logger: logging.Logger) -> None:
        self.repository = repository
        self.timezone_service = timezone_service
        self.logger = logger

    def import_riti(
        self,
        paths: Iterable[Path],
        trip_id: int,
        token: CancellationToken | None = None,
        progress: ProgressCallback | None = None,
    ) -> ImportReport:
        report = ImportReport()
        token = token or CancellationToken()
        path_list = list(map(Path, paths))
        for file_index, path in enumerate(path_list, start=1):
            token.raise_if_cancelled()
            if progress:
                progress(ProgressUpdate(file_index - 1, len(path_list), f"正在匯入 {path.name}"))
            try:
                parsed = parse_riti_file(path, self.timezone_service, token, progress)
                if parsed.point_count == 0:
                    report.skipped_files.append((path, "沒有有效定位點"))
                    continue
                parsed_tracks = split_riti_track_by_local_date(parsed)
                if progress:
                    progress(ProgressUpdate(0, parsed.point_count, f"正在寫入 {path.name} 的定位點"))
                ids = self.repository.import_tracks(path, "RITI_TXT", parsed_tracks, trip_id, token, progress)
                report.imported_track_ids.extend(ids)
                report.source_reports.append({"path": str(path), "track_count": len(parsed_tracks), **parsed.metadata})
                self.logger.info("RITI imported: %s tracks=%s points=%s", path, len(ids), parsed.point_count)
            except OperationCancelled:
                raise
            except DuplicateImportError as exc:
                report.skipped_files.append((path, str(exc)))
            except Exception as exc:
                self.logger.exception("RITI import failed: %s", path)
                report.skipped_files.append((path, str(exc)))
        if progress:
            progress(ProgressUpdate(len(path_list), len(path_list), "RITI 匯入完成"))
        return report

    def import_gpx(
        self,
        paths: Iterable[Path],
        trip_id: int,
        token: CancellationToken | None = None,
        progress: ProgressCallback | None = None,
    ) -> ImportReport:
        report = ImportReport()
        token = token or CancellationToken()
        path_list = list(map(Path, paths))
        for file_index, path in enumerate(path_list, start=1):
            token.raise_if_cancelled()
            if progress:
                progress(ProgressUpdate(file_index - 1, len(path_list), f"正在匯入 {path.name}"))
            try:
                parsed_tracks = parse_gpx_file(path, self.timezone_service, token, progress)
                if not parsed_tracks:
                    report.skipped_files.append((path, "GPX 沒有可匯入的 trk/trkseg"))
                    continue
                ids = self.repository.import_tracks(path, "GPX", parsed_tracks, trip_id, token, progress)
                report.imported_track_ids.extend(ids)
                report.source_reports.append(
                    {"path": str(path), "track_count": len(parsed_tracks), "point_count": sum(x.point_count for x in parsed_tracks)}
                )
                self.logger.info("GPX imported: %s tracks=%s", path, len(ids))
            except OperationCancelled:
                raise
            except DuplicateImportError as exc:
                report.skipped_files.append((path, str(exc)))
            except Exception as exc:
                self.logger.exception("GPX import failed: %s", path)
                report.skipped_files.append((path, str(exc)))
        if progress:
            progress(ProgressUpdate(len(path_list), len(path_list), "GPX 匯入完成"))
        return report
