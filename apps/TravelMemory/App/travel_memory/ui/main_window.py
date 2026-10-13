from __future__ import annotations

import base64
from datetime import date
from pathlib import Path

from PySide6.QtCore import QByteArray, Qt, QTimer, QThread, QSize
from PySide6.QtGui import QAction, QCloseEvent, QIcon, QPixmap
from PySide6.QtWidgets import (
    QFileDialog,
    QHBoxLayout,
    QInputDialog,
    QLabel,
    QMainWindow,
    QMenu,
    QMessageBox,
    QPushButton,
    QSplitter,
    QToolBar,
    QTreeWidget,
    QTreeWidgetItem,
    QVBoxLayout,
    QWidget,
    QLineEdit,
    QProgressDialog,
    QToolButton,
)

from .. import APP_NAME, __version__
from ..models import FilterCriteria
from ..repository import Repository
from ..services.backup_service import BackupService
from ..services.export_service import ExportService
from ..services.import_service import ImportService
from ..operations import OperationJournal
from ..settings import AppSettings, SettingsService
from .dialogs import FilterDialog, SettingsDialog, TrackEditorDialog, TripEditorDialog
from .map_widget import MapWidget
from .workers import TaskHandle, TaskWorker

KIND_ROLE = Qt.ItemDataRole.UserRole + 10
ID_ROLE = Qt.ItemDataRole.UserRole + 11
TRIP_ID_ROLE = Qt.ItemDataRole.UserRole + 12


class MainWindow(QMainWindow):
    def __init__(
        self,
        repository: Repository,
        import_service: ImportService,
        export_service: ExportService,
        backup_service: BackupService,
        settings: AppSettings,
        settings_service: SettingsService,
        operation_journal: OperationJournal,
        map_directory: Path,
        map_cache_directory: Path,
        logger,
    ) -> None:
        super().__init__()
        self.repository = repository
        self.import_service = import_service
        self.export_service = export_service
        self.backup_service = backup_service
        self.operation_journal = operation_journal
        self._active_task: TaskHandle | None = None
        self._active_progress: QProgressDialog | None = None
        self._closing_after_cancel = False
        self.app_settings = settings
        self.settings_service = settings_service
        self.logger = logger
        self.criteria = FilterCriteria()
        self._loading_tree = False
        self._build_ui(map_directory, map_cache_directory)
        self._restore_window_state()
        self.refresh_tree()
        self.map_widget.set_provider(self.app_settings.map_provider)
        self.refresh_map(fit=True)

    def _build_ui(self, map_directory: Path, map_cache_directory: Path) -> None:
        self.setWindowTitle(f"{APP_NAME}  {__version__}")
        self.resize(1320, 820)
        toolbar = QToolBar("主要工具列")
        toolbar.setMovable(False)
        self.addToolBar(toolbar)

        trip_menu = QMenu(self)
        trip_menu.addAction("新增旅程", self.add_trip)
        self.rename_trip_action = trip_menu.addAction("重新命名旅程", self.rename_selected_trip)
        self.delete_trip_action = trip_menu.addAction("刪除旅程", self.delete_selected_trip)
        trip_button = QPushButton("旅程")
        trip_button.setMenu(trip_menu); toolbar.addWidget(trip_button)
        track_menu = QMenu(self)
        self.merge_action = track_menu.addAction("合併軌跡", self.merge_checked)
        self.rename_track_action = track_menu.addAction("重新命名軌跡", self.rename_selected_track)
        self.delete_track_action = track_menu.addAction("刪除軌跡", self.delete_selected_track)
        track_button = QPushButton("軌跡")
        track_button.setMenu(track_menu); toolbar.addWidget(track_button)
        trip_menu.aboutToShow.connect(self._update_menu_actions)
        track_menu.aboutToShow.connect(self._update_menu_actions)

        import_menu = QMenu(self)
        import_riti = import_menu.addAction("RITI TXT")
        import_gpx = import_menu.addAction("GPX")
        import_riti.triggered.connect(self.import_riti)
        import_gpx.triggered.connect(self.import_gpx)
        import_button = QPushButton("匯入")
        import_button.setMenu(import_menu)
        toolbar.addWidget(import_button)

        export_menu = QMenu(self)
        export_gpx = export_menu.addAction("GPX")
        export_kml = export_menu.addAction("KML")
        export_gpx.triggered.connect(lambda: self.export_checked("gpx"))
        export_kml.triggered.connect(lambda: self.export_checked("kml"))
        export_button = QPushButton("匯出")
        export_button.setMenu(export_menu)
        toolbar.addWidget(export_button)

        settings_menu = QMenu(self)
        for index, label in enumerate(("一般", "軌跡／地圖", "資料庫備份還原", "旅伴設定", "活動設定")):
            settings_menu.addAction(label, lambda checked=False, i=index: self.open_settings(i))
        settings_button = QPushButton("設定")
        settings_button.setMenu(settings_menu); toolbar.addWidget(settings_button)
        toolbar.addSeparator()

        points = self._display_button("顯示定位點", '<circle cx="12" cy="12" r="4" fill="currentColor"/><path d="M12 2v4M12 18v4M2 12h4M18 12h4"/>', self.app_settings.show_points)
        endpoints = self._display_button("顯示起訖點", '<path d="M5 21V3l10 4-10 4M16 17h5M18.5 14.5v5"/>', self.app_settings.show_endpoints)
        points.toggled.connect(self._map_option_changed); endpoints.toggled.connect(self._map_option_changed)
        self.points_action = points; self.endpoints_action = endpoints
        toolbar.addWidget(points); toolbar.addWidget(endpoints)

        central = QWidget()
        central_layout = QVBoxLayout(central)
        central_layout.setContentsMargins(6, 6, 6, 6)
        self.splitter = QSplitter(Qt.Orientation.Horizontal)
        left = QWidget(); left_layout = QVBoxLayout(left); left_layout.setContentsMargins(0, 0, 4, 0)
        search_row = QHBoxLayout()
        self.search = QLineEdit(); self.search.setPlaceholderText("搜尋旅程名稱或軌跡名稱")
        filter_button = QPushButton("篩選")
        clear_filter = QPushButton("清除篩選")
        search_row.addWidget(self.search, 1); search_row.addWidget(filter_button); search_row.addWidget(clear_filter)
        left_layout.addLayout(search_row)
        self.tree = QTreeWidget()
        self.tree.setHeaderLabels(["旅程／日期／軌跡", "點數"])
        self.tree.setColumnWidth(0, 330)
        self.tree.setContextMenuPolicy(Qt.ContextMenuPolicy.CustomContextMenu)
        left_layout.addWidget(self.tree, 1)
        clear_checked = QPushButton("清除勾選")
        left_layout.addWidget(clear_checked)
        self.map_widget = MapWidget(map_directory, map_cache_directory)
        self.splitter.addWidget(left); self.splitter.addWidget(self.map_widget)
        self.splitter.setStretchFactor(0, 0); self.splitter.setStretchFactor(1, 1)
        self.splitter.setSizes([430, 890])
        central_layout.addWidget(self.splitter)
        self.setCentralWidget(central)

        self.map_status = QLabel("地圖連線：載入中")
        self.statusBar().addPermanentWidget(self.map_status)
        self.filter_status = QLabel("")
        self.statusBar().addWidget(self.filter_status, 1)

        self.search.textChanged.connect(self.refresh_tree)
        filter_button.clicked.connect(self.open_filter)
        clear_filter.clicked.connect(self.clear_filter)
        clear_checked.clicked.connect(self.clear_checked)
        self.tree.itemChanged.connect(self._tree_item_changed)
        self.tree.currentItemChanged.connect(self._tree_selection_changed)
        self.tree.itemDoubleClicked.connect(self._tree_double_clicked)
        self.tree.customContextMenuRequested.connect(self._tree_context_menu)
        self.map_widget.status_changed.connect(self._map_status_changed)
        self.map_widget.track_selected.connect(self._map_track_selected)
        self.map_widget.online_restored.connect(self._online_restored)

    # ---------- Refresh ----------
    def refresh_tree(self) -> None:
        selected_track = self.selected_track_id()
        tracks = self.repository.list_tracks(self.search.text(), self.criteria)
        trips = self.repository.list_trips()
        tracks_by_trip: dict[int, list] = {}
        for track in tracks:
            tracks_by_trip.setdefault(track.trip_id, []).append(track)
        has_filter = bool(
            self.search.text().strip() or self.criteria.date_from or self.criteria.date_to or
            self.criteria.place_ids or self.criteria.companion_ids or self.criteria.activity_ids
        )
        self._loading_tree = True
        self.tree.clear()
        for trip in trips:
            trip_tracks = tracks_by_trip.get(trip.id, [])
            if has_filter and not trip_tracks and self.search.text().strip().lower() not in trip.name.lower():
                continue
            trip_item = QTreeWidgetItem([trip.name, str(sum(track.point_count for track in trip_tracks)) if trip_tracks else ""])
            trip_item.setData(0, KIND_ROLE, "trip"); trip_item.setData(0, ID_ROLE, trip.id); trip_item.setData(0, TRIP_ID_ROLE, trip.id)
            font = trip_item.font(0); font.setBold(True); trip_item.setFont(0, font)
            self.tree.addTopLevelItem(trip_item)
            date_items: dict[str, QTreeWidgetItem] = {}
            for track in trip_tracks:
                group_date = track.assigned_date or track.first_local_date
                date_key = group_date.isoformat() if group_date else ""
                if date_key not in date_items:
                    label = group_date.strftime("%Y/%m/%d") if group_date else "無日期"
                    item = QTreeWidgetItem([label, ""])
                    item.setData(0, KIND_ROLE, "date"); item.setData(0, TRIP_ID_ROLE, trip.id)
                    trip_item.addChild(item); date_items[date_key] = item
                label = track.display_name
                if track.time_data_status == "missing": label += "  [無時間]"
                track_item = QTreeWidgetItem([label, str(track.point_count)])
                track_item.setData(0, KIND_ROLE, "track"); track_item.setData(0, ID_ROLE, track.id); track_item.setData(0, TRIP_ID_ROLE, trip.id)
                track_item.setFlags(track_item.flags() | Qt.ItemFlag.ItemIsUserCheckable)
                track_item.setCheckState(0, Qt.CheckState.Checked if track.is_checked else Qt.CheckState.Unchecked)
                date_items[date_key].addChild(track_item)
                if selected_track == track.id:
                    self.tree.setCurrentItem(track_item)
            trip_item.setExpanded(True)
            for item in date_items.values(): item.setExpanded(True)
        self._loading_tree = False
        count = len(tracks)
        self.filter_status.setText(f"顯示 {count} 筆軌跡" + ("（篩選中）" if has_filter else ""))

    def refresh_map(self, *, fit: bool = False) -> None:
        ids = self.repository.checked_track_ids()
        payload = self.repository.map_payload(
            ids, max_points=self.app_settings.max_map_points, gap_seconds=self.app_settings.gap_display_seconds
        )
        self.map_widget.set_tracks(
            payload,
            gap_seconds=self.app_settings.gap_display_seconds,
            show_points=self.points_action.isChecked(),
            show_endpoints=self.endpoints_action.isChecked(),
            show_segment_connectors=self.app_settings.show_segment_connectors,
            line_width=self.app_settings.line_width,
            point_size=self.app_settings.point_size,
            fit=fit,
        )

    def _display_button(self, tooltip: str, shapes: str, checked: bool) -> QToolButton:
        button = QToolButton(self)
        svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#173b59" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">{shapes}</svg>'
        pixmap = QPixmap(); pixmap.loadFromData(svg.encode(), "SVG")
        button.setIcon(QIcon(pixmap)); button.setIconSize(QSize(24, 24))
        button.setFixedSize(36, 36); button.setCheckable(True); button.setChecked(checked)
        button.setToolTip(tooltip); button.setAccessibleName(tooltip)
        button.setStyleSheet("QToolButton {background:#eef0f2;border:1px solid #c1c8ce;border-radius:5px;padding:4px;} QToolButton:checked {background:#99cff7;border:2px solid #1877b9;} QToolButton:hover {border-color:#1877b9;}")
        return button

    def _update_menu_actions(self) -> None:
        has_trip = self.selected_trip_id() is not None
        has_track = self.selected_track_id() is not None
        self.rename_trip_action.setEnabled(has_trip); self.delete_trip_action.setEnabled(has_trip)
        self.rename_track_action.setEnabled(has_track); self.delete_track_action.setEnabled(has_track)
        self.merge_action.setEnabled(len(self.repository.checked_track_ids()) >= 2)

    def rename_selected_track(self) -> None:
        track_id = self.selected_track_id()
        if track_id is not None: self.rename_track(track_id)

    def delete_selected_track(self) -> None:
        track_id = self.selected_track_id()
        if track_id is not None: self.delete_track(track_id)

    # ---------- Selection ----------
    def selected_track_id(self) -> int | None:
        item = self.tree.currentItem()
        return int(item.data(0, ID_ROLE)) if item and item.data(0, KIND_ROLE) == "track" else None

    def selected_trip_id(self) -> int | None:
        item = self.tree.currentItem()
        if not item:
            return None
        value = item.data(0, TRIP_ID_ROLE)
        return int(value) if value is not None else None

    def _tree_item_changed(self, item: QTreeWidgetItem, column: int) -> None:
        if self._loading_tree or column != 0 or item.data(0, KIND_ROLE) != "track": return
        self.repository.set_checked(int(item.data(0, ID_ROLE)), item.checkState(0) == Qt.CheckState.Checked)
        self.refresh_map(fit=item.checkState(0) == Qt.CheckState.Checked)

    def _tree_selection_changed(self, current: QTreeWidgetItem | None, previous: QTreeWidgetItem | None) -> None:
        track_id = int(current.data(0, ID_ROLE)) if current and current.data(0, KIND_ROLE) == "track" else None
        self.map_widget.select_track(track_id)

    def _tree_double_clicked(self, item: QTreeWidgetItem, column: int) -> None:
        kind = item.data(0, KIND_ROLE)
        if kind == "trip": self.edit_trip(int(item.data(0, ID_ROLE)))
        elif kind == "track": self.edit_track(int(item.data(0, ID_ROLE)))

    # ---------- Trips ----------
    def add_trip(self) -> None:
        dialog = TripEditorDialog(self.repository, parent=self)
        if dialog.exec(): self.refresh_tree()

    def edit_trip(self, trip_id: int) -> None:
        dialog = TripEditorDialog(self.repository, trip_id, self)
        if dialog.exec(): self.refresh_tree(); self.refresh_map()

    def rename_selected_trip(self) -> None:
        trip_id = self.selected_trip_id()
        if trip_id is None: return
        row = self.repository.get_trip(trip_id)
        if not row: return
        name, ok = QInputDialog.getText(self, "重新命名旅程", "旅程名稱", text=str(row["name"]))
        if ok: self.repository.rename_trip(trip_id, name); self.refresh_tree()

    def delete_selected_trip(self) -> None:
        trip_id = self.selected_trip_id()
        if trip_id is None: return
        try:
            self.repository.delete_trip(trip_id); self.refresh_tree()
        except Exception as exc: QMessageBox.warning(self, "無法刪除旅程", str(exc))

    # ---------- Import ----------
    def import_riti(self) -> None:
        files, _ = QFileDialog.getOpenFileNames(self, "匯入 RITI TXT", "", "RITI TXT (*.txt);;所有檔案 (*)")
        if not files:
            return
        trip_id = self.selected_trip_id() or self.repository.get_unclassified_trip_id()
        paths = [Path(file) for file in files]
        self._start_task(
            "匯入 RITI TXT",
            ", ".join(path.name for path in paths),
            lambda token, progress: self.import_service.import_riti(paths, trip_id, token, progress),
            self._show_import_report,
        )

    def import_gpx(self) -> None:
        files, _ = QFileDialog.getOpenFileNames(self, "匯入 GPX", "", "GPX (*.gpx);;所有檔案 (*)")
        if not files:
            return
        trip_id = self.selected_trip_id() or self.repository.get_unclassified_trip_id()
        paths = [Path(file) for file in files]

        def completed(report) -> None:
            self._show_import_report(report)
            for track_id in report.imported_track_ids:
                row = self.repository.get_track(track_id)
                if row and row["time_data_status"] == "missing":
                    TrackEditorDialog(self.repository, track_id, self).exec()
            self.refresh_tree()
            self.refresh_map()

        self._start_task(
            "匯入 GPX",
            ", ".join(path.name for path in paths),
            lambda token, progress: self.import_service.import_gpx(paths, trip_id, token, progress),
            completed,
        )

    def _show_import_report(self, report) -> None:
        self.refresh_tree(); self.refresh_map(fit=True)
        lines = [f"已匯入軌跡：{len(report.imported_track_ids)}"]
        for item in report.source_reports:
            if "valid_points" in item:
                lines.append(
                    f"{Path(item['path']).name}：每日軌跡 {item.get('track_count', 1)}、"
                    f"有效 {item['valid_points']}、略過 {item['skipped_rows']}、Segment {item['segment_count']}"
                )
            else:
                lines.append(f"{Path(item['path']).name}：軌跡 {item['track_count']}、點數 {item['point_count']}")
        for path, reason in report.skipped_files: lines.append(f"未匯入 {path.name}：{reason}")
        QMessageBox.information(self, "匯入完成", "\n".join(lines))

    # ---------- Track operations ----------
    def edit_track(self, track_id: int) -> None:
        dialog = TrackEditorDialog(self.repository, track_id, self)
        if dialog.exec(): self.refresh_tree(); self.refresh_map()

    def rename_track(self, track_id: int) -> None:
        row = self.repository.get_track(track_id)
        if not row: return
        name, ok = QInputDialog.getText(self, "重新命名軌跡", "軌跡名稱", text=str(row["display_name"]))
        if ok: self.repository.rename_track(track_id, name); self.refresh_tree(); self.refresh_map()

    def delete_track(self, track_id: int) -> None:
        self.repository.delete_track(track_id)
        self.refresh_tree(); self.refresh_map()

    def clear_checked(self) -> None:
        self.repository.clear_checked(); self.refresh_tree(); self.refresh_map()

    def merge_checked(self) -> None:
        ids = self.repository.checked_track_ids()
        if len(ids) < 2:
            QMessageBox.warning(self, "無法合併", "請至少勾選兩筆軌跡。")
            return
        name, ok = QInputDialog.getText(self, "合併軌跡", "新軌跡名稱", text="合併軌跡")
        if not ok: return
        try:
            new_id = self.repository.merge_tracks(ids, name)
            self.refresh_tree(); self.refresh_map(fit=True); self._select_track_in_tree(new_id)
            QMessageBox.information(self, "合併完成", "軌跡合併完成。")
        except Exception as exc: QMessageBox.warning(self, "無法合併", str(exc))

    # ---------- Export ----------
    def export_checked(self, extension: str) -> None:
        ids = self.repository.checked_track_ids()
        if not ids:
            QMessageBox.warning(self, "沒有勾選", "請先勾選要匯出的軌跡。")
            return
        default = f"TravelMemory_export.{extension}"
        target, _ = QFileDialog.getSaveFileName(self, "匯出", default, f"{extension.upper()} (*.{extension})")
        if target: self._export(ids, extension, Path(target))

    def export_single(self, track_id: int, extension: str) -> None:
        row = self.repository.get_track(track_id)
        if not row: return
        target, _ = QFileDialog.getSaveFileName(self, "匯出軌跡", f"{row['display_name']}.{extension}", f"{extension.upper()} (*.{extension})")
        if target: self._export([track_id], extension, Path(target))

    def _export(self, ids: list[int], extension: str, target: Path) -> None:
        function = self.export_service.export_gpx if extension == "gpx" else self.export_service.export_kml
        self._start_task(
            f"匯出 {extension.upper()}",
            target.name,
            lambda token, progress: function(ids, target, token, progress),
            lambda path: QMessageBox.information(self, "匯出完成", f"已輸出：\n{path}"),
        )

    # ---------- Background tasks ----------
    def _start_task(self, title: str, subject: str, function, on_success) -> None:
        if self._active_task is not None:
            QMessageBox.warning(self, "作業進行中", "請先等待目前作業完成或取消。")
            return
        progress_dialog = QProgressDialog(title, "取消", 0, 0, self)
        progress_dialog.setWindowTitle(title)
        progress_dialog.setWindowModality(Qt.WindowModality.WindowModal)
        progress_dialog.setMinimumDuration(0)
        progress_dialog.setAutoClose(False)
        progress_dialog.setAutoReset(False)
        progress_dialog.setMinimumWidth(480)
        progress_dialog.setLabelText(f"正在準備：{subject}" if subject else f"正在準備：{title}")
        thread = QThread(self)
        worker = TaskWorker(title, subject, function, self.operation_journal)
        worker.moveToThread(thread)
        handle = TaskHandle(thread, worker)
        self._active_task = handle
        self._active_progress = progress_dialog

        def update(current: int, total: int, message: str) -> None:
            progress_dialog.setLabelText(message or title)
            if total > 0:
                progress_dialog.setRange(0, total)
                progress_dialog.setValue(min(current, total))
            else:
                progress_dialog.setRange(0, 0)

        def failed(traceback_text: str) -> None:
            self.logger.error("Background task failed: %s", traceback_text)
            last_line = traceback_text.strip().splitlines()[-1] if traceback_text.strip() else "未知錯誤"
            QMessageBox.critical(self, f"{title}失敗", last_line)

        def succeeded(result) -> None:
            progress_dialog.close()
            on_success(result)

        def finished() -> None:
            progress_dialog.close()
            thread.quit()
            self._active_task = None
            self._active_progress = None

        progress_dialog.canceled.connect(handle.cancel)
        thread.started.connect(worker.run)
        worker.progress.connect(update)
        worker.succeeded.connect(succeeded)
        worker.failed.connect(failed)
        worker.cancelled.connect(lambda: self.statusBar().showMessage(f"{title}已取消", 5000))
        worker.finished.connect(finished)
        worker.finished.connect(worker.deleteLater)
        thread.finished.connect(thread.deleteLater)
        progress_dialog.show()
        progress_dialog.raise_()
        QTimer.singleShot(0, thread.start)

    # ---------- Filter / Settings ----------
    def open_filter(self) -> None:
        dialog = FilterDialog(self.repository, self.criteria, self)
        if dialog.exec(): self.criteria = dialog.criteria; self.refresh_tree()

    def clear_filter(self) -> None:
        self.criteria = FilterCriteria(); self.refresh_tree()

    def open_settings(self, tab_index: int = 0) -> None:
        old_provider = self.app_settings.map_provider
        dialog = SettingsDialog(self.app_settings, self.repository, self.backup_service, self, tab_index=tab_index)
        if dialog.exec():
            self.settings_service.save(self.app_settings)
            self.points_action.blockSignals(True); self.endpoints_action.blockSignals(True)
            self.points_action.setChecked(self.app_settings.show_points)
            self.endpoints_action.setChecked(self.app_settings.show_endpoints)
            self.points_action.blockSignals(False); self.endpoints_action.blockSignals(False)
            if old_provider != self.app_settings.map_provider:
                self.map_widget.set_provider(self.app_settings.map_provider)
            self.refresh_tree(); self.refresh_map()

    def _map_option_changed(self) -> None:
        self.app_settings.show_points = self.points_action.isChecked()
        self.app_settings.show_endpoints = self.endpoints_action.isChecked()
        self.settings_service.save(self.app_settings)
        self.refresh_map()

    # ---------- Context / Map ----------
    def _tree_context_menu(self, position) -> None:
        item = self.tree.itemAt(position)
        if not item: return
        self.tree.setCurrentItem(item)
        kind = item.data(0, KIND_ROLE)
        menu = QMenu(self)
        if kind == "trip":
            trip_id = int(item.data(0, ID_ROLE))
            menu.addAction("編輯旅程資料", lambda: self.edit_trip(trip_id))
            menu.addAction("重新命名旅程", self.rename_selected_trip)
            menu.addAction("刪除旅程", self.delete_selected_trip)
        elif kind == "track":
            track_id = int(item.data(0, ID_ROLE))
            menu.addAction("定位到軌跡", lambda: self.locate_track(track_id))
            menu.addSeparator()
            menu.addAction("編輯資訊", lambda: self.edit_track(track_id))
            menu.addAction("重新命名", lambda: self.rename_track(track_id))
            export = menu.addMenu("匯出")
            export.addAction("GPX", lambda: self.export_single(track_id, "gpx"))
            export.addAction("KML", lambda: self.export_single(track_id, "kml"))
            move = menu.addMenu("移動到旅程")
            for trip in self.repository.list_trips():
                move.addAction(trip.name, lambda checked=False, tid=trip.id: self._move_track(track_id, tid))
            menu.addSeparator()
            menu.addAction("刪除", lambda: self.delete_track(track_id))
        menu.exec(self.tree.viewport().mapToGlobal(position))

    def locate_track(self, track_id: int) -> None:
        bounds = self.repository.track_bounds(track_id)
        if bounds is None:
            QMessageBox.information(self, "定位到軌跡", "此軌跡沒有有效定位點。")
            return
        self.repository.set_checked(track_id, True)
        self.refresh_tree(); self._select_track_in_tree(track_id)
        self.refresh_map()
        self.map_widget.select_track(track_id)
        self.map_widget.fit_track(track_id, bounds)

    def _move_track(self, track_id: int, trip_id: int) -> None:
        self.repository.move_track(track_id, trip_id); self.refresh_tree(); self.refresh_map()

    def _map_status_changed(self, state: str, provider: str) -> None:
        self.map_status.setText(f"地圖連線：{provider}")

    def _map_track_selected(self, track_id: int) -> None:
        self._select_track_in_tree(track_id)

    def _select_track_in_tree(self, track_id: int) -> None:
        iterator = []
        for i in range(self.tree.topLevelItemCount()): iterator.append(self.tree.topLevelItem(i))
        while iterator:
            item = iterator.pop()
            if item.data(0, KIND_ROLE) == "track" and int(item.data(0, ID_ROLE)) == track_id:
                self.tree.setCurrentItem(item); self.tree.scrollToItem(item); return
            iterator.extend(item.child(i) for i in range(item.childCount()))

    def _online_restored(self) -> None:
        if self.app_settings.map_provider == "fallback": return
        if QMessageBox.question(self, "網路已恢復", "是否切回線上地圖？") == QMessageBox.StandardButton.Yes:
            self.map_widget.set_provider(self.app_settings.map_provider)

    # ---------- Window state ----------
    def _restore_window_state(self) -> None:
        try:
            if self.app_settings.window_geometry:
                self.restoreGeometry(QByteArray.fromBase64(self.app_settings.window_geometry.encode("ascii")))
            if self.app_settings.splitter_state:
                self.splitter.restoreState(QByteArray.fromBase64(self.app_settings.splitter_state.encode("ascii")))
        except Exception:
            pass

    def closeEvent(self, event: QCloseEvent) -> None:
        if self._active_task is not None:
            if not self._closing_after_cancel:
                answer = QMessageBox.question(
                    self, "作業仍在進行", "目前作業尚未完成。是否取消作業並關閉程式？"
                )
                if answer != QMessageBox.StandardButton.Yes:
                    event.ignore()
                    return
                self._closing_after_cancel = True
                self._active_task.cancel()
            event.ignore()
            QTimer.singleShot(250, self._close_after_task_finishes)
            return
        self.app_settings.window_geometry = bytes(self.saveGeometry().toBase64()).decode("ascii")
        self.app_settings.splitter_state = bytes(self.splitter.saveState().toBase64()).decode("ascii")
        self.settings_service.save(self.app_settings)
        try:
            self.map_widget.shutdown()
        except Exception:
            self.logger.exception("Map shutdown failed")
        super().closeEvent(event)

    def _close_after_task_finishes(self) -> None:
        if self._active_task is None:
            self.close()
        else:
            QTimer.singleShot(250, self._close_after_task_finishes)
