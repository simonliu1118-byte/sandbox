import logging
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'App'))
from travel_memory.database import Database
from travel_memory.paths import AppPaths
from travel_memory.repository import Repository
from travel_memory.models import ParsedTrack, TrackPoint, TrackSegmentData
from travel_memory.services.timezone_service import TimeZoneService


class BoundsTest(unittest.TestCase):
    def test_bounds_include_unsampled_extremes(self):
        with tempfile.TemporaryDirectory() as tmp:
            old = os.environ.get('TRAVEL_MEMORY_ROOT')
            os.environ['TRAVEL_MEMORY_ROOT'] = tmp
            try:
                paths = AppPaths.discover()
            finally:
                if old is None: os.environ.pop('TRAVEL_MEMORY_ROOT', None)
                else: os.environ['TRAVEL_MEMORY_ROOT'] = old
            db = Database(paths.database, paths, logging.getLogger('test'))
            db.initialize()
            repo = Repository(db, TimeZoneService())
            source = Path(tmp) / 'synthetic.txt'
            source.write_text('synthetic fixture, no personal location records')
            track = ParsedTrack('synthetic', 'GPX', source, [TrackSegmentData(0, [
                TrackPoint(1, 2), TrackPoint(-20, -40), TrackPoint(3, 4),
                TrackPoint(40, 70), TrackPoint(5, 6),
            ])])
            ids = repo.import_tracks(source, 'GPX', [track], repo.get_unclassified_trip_id())
            self.assertEqual(repo.track_bounds(ids[0]), [-40., -20., 70., 40.])
            self.assertEqual(repo.map_payload(ids, max_points=1)[0]['bounds'], [-40., -20., 70., 40.])
            self.assertIsNone(repo.track_bounds(-1))


if __name__ == '__main__': unittest.main()
