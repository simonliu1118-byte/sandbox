import logging
import sys
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'App'))
try:
    from PySide6.QtCore import Signal
    from PySide6.QtWidgets import QApplication, QWidget, QPushButton, QToolBar
except ImportError:
    raise unittest.SkipTest('PySide6 unavailable; UI tests require Windows CI')
from travel_memory.settings import AppSettings
from travel_memory.ui.main_window import MainWindow
from travel_memory.ui.dialogs import SettingsDialog


class FakeMap(QWidget):
    status_changed = Signal(str, str)
    track_selected = Signal(int)
    online_restored = Signal()

    def __init__(self, *args):
        super().__init__()
        self.calls = []

    def set_provider(self, value): self.calls.append(('provider', value))
    def set_tracks(self, value, **options): self.calls.append(('tracks', options))
    def select_track(self, value): self.calls.append(('selection', value))
    def fit_track(self, value, bounds): self.calls.append(('fit', value, bounds))
    def shutdown(self): pass


class UITest(unittest.TestCase):
    @classmethod
    def setUpClass(cls): cls.app = QApplication.instance() or QApplication([])

    def setUp(self):
        self.repo = MagicMock()
        for name in ('list_tracks', 'list_trips', 'list_companion_groups', 'list_companions', 'list_activities', 'checked_track_ids', 'map_payload'):
            getattr(self.repo, name).return_value = []
        self.settings = AppSettings()

    def test_toolbar_menu_order_and_states(self):
        with patch('travel_memory.ui.main_window.MapWidget', FakeMap):
            w = MainWindow(self.repo, MagicMock(), MagicMock(), MagicMock(), self.settings, MagicMock(), MagicMock(), Path('.'), Path('.'), logging.getLogger('test'))
        toolbar = w.findChild(QToolBar)
        buttons = [toolbar.widgetForAction(a) for a in toolbar.actions()]
        menus = [b for b in buttons if isinstance(b, QPushButton)]
        self.assertEqual([b.text() for b in menus], ['旅程', '軌跡', '匯入', '匯出', '設定'])
        self.assertEqual([a.text() for a in menus[1].menu().actions()], ['合併軌跡', '重新命名軌跡', '刪除軌跡'])
        w._update_menu_actions()
        self.assertFalse(w.rename_track_action.isEnabled())
        self.assertFalse(w.merge_action.isEnabled())
        for b, tip in [(w.points_action, '顯示定位點'), (w.endpoints_action, '顯示起訖點')]:
            self.assertEqual(b.toolTip(), tip)
            self.assertTrue(b.isCheckable())
            self.assertFalse(b.icon().isNull())
        w.refresh_map()
        self.assertFalse(w.map_widget.calls[-1][1]['fit'])
        self.repo.track_bounds.return_value = [1., 2., 3., 4.]
        w.locate_track(5)
        self.assertEqual(w.map_widget.calls[-1], ('fit', 5, [1., 2., 3., 4.]))
        w.close()

    def test_settings_direct_tab(self):
        for index in range(5):
            dialog = SettingsDialog(self.settings, self.repo, MagicMock(), tab_index=index)
            self.assertEqual(dialog.tabs.currentIndex(), index)
            dialog.close()


if __name__ == '__main__': unittest.main()
