from __future__ import annotations

from datetime import date
from pathlib import Path
from zoneinfo import available_timezones

from PySide6.QtCore import QDate, Qt, Signal
from PySide6.QtWidgets import (
    QAbstractItemView,
    QCheckBox,
    QComboBox,
    QDateEdit,
    QDialog,
    QDialogButtonBox,
    QFileDialog,
    QFormLayout,
    QGroupBox,
    QHBoxLayout,
    QHeaderView,
    QInputDialog,
    QLabel,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QMessageBox,
    QPushButton,
    QSpinBox,
    QTabWidget,
    QTableWidget,
    QTableWidgetItem,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from ..models import FilterCriteria
from ..repository import Repository
from ..services.backup_service import BackupService
from ..settings import AppSettings

ID_ROLE = Qt.ItemDataRole.UserRole


class OptionalDateEdit(QWidget):
    changed = Signal()

    def __init__(self, label: str = "啟用", parent=None) -> None:
        super().__init__(parent)
        layout = QHBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        self.enabled_box = QCheckBox(label)
        self.date_edit = QDateEdit(QDate.currentDate())
        self.date_edit.setCalendarPopup(True)
        self.date_edit.setDisplayFormat("yyyy/MM/dd")
        self.date_edit.setEnabled(False)
        self.enabled_box.toggled.connect(self.date_edit.setEnabled)
        self.enabled_box.toggled.connect(self.changed)
        self.date_edit.dateChanged.connect(self.changed)
        layout.addWidget(self.enabled_box)
        layout.addWidget(self.date_edit, 1)

    def value(self) -> date | None:
        if not self.enabled_box.isChecked():
            return None
        qdate = self.date_edit.date()
        return date(qdate.year(), qdate.month(), qdate.day())

    def set_value(self, value: date | None) -> None:
        self.enabled_box.setChecked(value is not None)
        if value:
            self.date_edit.setDate(QDate(value.year, value.month, value.day))


class CheckList(QWidget):
    def __init__(self, title: str, parent=None) -> None:
        super().__init__(parent)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.addWidget(QLabel(title))
        self.search = QLineEdit()
        self.search.setPlaceholderText("搜尋")
        self.list = QListWidget()
        self.list.setSelectionMode(QAbstractItemView.SelectionMode.NoSelection)
        self.search.textChanged.connect(self._apply_filter)
        layout.addWidget(self.search)
        layout.addWidget(self.list, 1)

    def set_items(self, items: list[tuple[int | None, str, bool]], selected: set[int] | None = None) -> None:
        selected = selected or set()
        self.list.clear()
        for record_id, text, selectable in items:
            item = QListWidgetItem(text)
            item.setData(ID_ROLE, record_id)
            if selectable and record_id is not None:
                item.setFlags(item.flags() | Qt.ItemFlag.ItemIsUserCheckable)
                item.setCheckState(Qt.CheckState.Checked if record_id in selected else Qt.CheckState.Unchecked)
            else:
                item.setFlags(item.flags() & ~Qt.ItemFlag.ItemIsEnabled)
                font = item.font()
                font.setBold(True)
                item.setFont(font)
            self.list.addItem(item)

    def selected_ids(self) -> list[int]:
        values: list[int] = []
        for index in range(self.list.count()):
            item = self.list.item(index)
            record_id = item.data(ID_ROLE)
            if record_id is not None and item.checkState() == Qt.CheckState.Checked:
                values.append(int(record_id))
        return values

    def _apply_filter(self, text: str) -> None:
        token = text.strip().lower()
        for index in range(self.list.count()):
            item = self.list.item(index)
            if item.data(ID_ROLE) is None:
                item.setHidden(False)
            else:
                item.setHidden(bool(token and token not in item.text().lower()))


def reference_items(repository: Repository) -> tuple[list[tuple[int | None, str, bool]], list[tuple[int | None, str, bool]], list[tuple[int | None, str, bool]]]:
    place_items = [(int(row["id"]), str(row["full_name"]), True) for row in repository.list_places(limit=5000)]
    companion_items: list[tuple[int | None, str, bool]] = []
    current_group = None
    for row in repository.list_companions():
        group = str(row["group_name"])
        if group != current_group:
            companion_items.append((None, group, False))
            current_group = group
        companion_items.append((int(row["id"]), f"　{row['name']}", True))
    activity_items = [(int(row["id"]), str(row["name"]), True) for row in repository.list_activities()]
    return place_items, companion_items, activity_items


class TripEditorDialog(QDialog):
    def __init__(self, repository: Repository, trip_id: int | None = None, parent=None) -> None:
        super().__init__(parent)
        self.repository = repository
        self.trip_id = trip_id
        self.setWindowTitle("新增旅程" if trip_id is None else "編輯旅程資料")
        self.resize(720, 640)
        root = QVBoxLayout(self)
        form = QFormLayout()
        self.name = QLineEdit()
        self.start = OptionalDateEdit()
        self.end = OptionalDateEdit()
        self.notes = QTextEdit()
        self.notes.setMinimumHeight(90)
        form.addRow("名稱", self.name)
        form.addRow("開始日期", self.start)
        form.addRow("結束日期", self.end)
        form.addRow("備註", self.notes)
        root.addLayout(form)
        tabs = QTabWidget()
        places, companions, activities = reference_items(repository)
        self.places = CheckList("地點（國家／行政區／城市皆可選）")
        self.companions = CheckList("旅伴（群組不可選）")
        self.activities = CheckList("活動")
        tabs.addTab(self.places, "地點")
        tabs.addTab(self.companions, "旅伴")
        tabs.addTab(self.activities, "活動")
        root.addWidget(tabs, 1)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self._accept)
        buttons.rejected.connect(self.reject)
        root.addWidget(buttons)

        selected = {"places": [], "companions": [], "activities": []}
        if trip_id is not None:
            row = repository.get_trip(trip_id)
            if row:
                self.name.setText(str(row["name"]))
                self.start.set_value(repository._parse_date(row["start_date"]))
                self.end.set_value(repository._parse_date(row["end_date"]))
                self.notes.setPlainText(str(row["notes"]))
            selected = repository.trip_relation_ids(trip_id)
        self.places.set_items(places, set(selected["places"]))
        self.companions.set_items(companions, set(selected["companions"]))
        self.activities.set_items(activities, set(selected["activities"]))

    def _accept(self) -> None:
        if self.start.value() and self.end.value() and self.start.value() > self.end.value():
            QMessageBox.warning(self, "日期錯誤", "開始日期不可晚於結束日期。")
            return
        if self.trip_id is None:
            self.trip_id = self.repository.create_trip(
                self.name.text(), self.start.value(), self.end.value(), self.notes.toPlainText(),
                self.places.selected_ids(), self.companions.selected_ids(), self.activities.selected_ids(),
            )
        else:
            self.repository.update_trip(
                self.trip_id, self.name.text(), self.start.value(), self.end.value(), self.notes.toPlainText(),
                self.places.selected_ids(), self.companions.selected_ids(), self.activities.selected_ids(),
            )
        self.accept()


class TrackEditorDialog(QDialog):
    def __init__(self, repository: Repository, track_id: int, parent=None) -> None:
        super().__init__(parent)
        self.repository = repository
        self.track_id = track_id
        row = repository.get_track(track_id)
        if not row:
            raise ValueError("軌跡不存在")
        self.row = row
        self.setWindowTitle("編輯軌跡資訊")
        self.resize(720, 680)
        root = QVBoxLayout(self)
        form = QFormLayout()
        self.name = QLineEdit(str(row["display_name"]))
        self.assigned_date = OptionalDateEdit()
        self.assigned_date.set_value(repository._parse_date(row["assigned_date"]))
        self.time_status = QLabel("有原始時間" if row["time_data_status"] != "missing" else "無原始時間")
        self.timezone_mode = QComboBox()
        self.timezone_mode.addItem("自動依每個定位點判定", "auto")
        self.timezone_mode.addItem("固定使用指定時區", "fixed")
        self.timezone_mode.setCurrentIndex(1 if row["timezone_mode"] == "fixed" else 0)
        self.fixed_timezone = QComboBox()
        self.fixed_timezone.setEditable(True)
        self.fixed_timezone.addItems(sorted(available_timezones()))
        if row["fixed_timezone_id"]:
            self.fixed_timezone.setCurrentText(str(row["fixed_timezone_id"]))
        else:
            self.fixed_timezone.setCurrentText("Asia/Tokyo")
        self.fixed_timezone.setEnabled(self.timezone_mode.currentData() == "fixed")
        self.timezone_mode.currentIndexChanged.connect(
            lambda: self.fixed_timezone.setEnabled(self.timezone_mode.currentData() == "fixed")
        )
        self.notes = QTextEdit(str(row["notes"]))
        self.notes.setMinimumHeight(90)
        form.addRow("軌跡名稱", self.name)
        form.addRow("日期", self.assigned_date)
        form.addRow("原始時間", self.time_status)
        form.addRow("時區模式", self.timezone_mode)
        form.addRow("固定 IANA 時區", self.fixed_timezone)
        form.addRow("備註", self.notes)
        root.addLayout(form)
        tabs = QTabWidget()
        places, companions, activities = reference_items(repository)
        relations = repository.track_relation_ids(track_id)
        self.places = CheckList("地點")
        self.companions = CheckList("旅伴")
        self.activities = CheckList("活動")
        self.places.set_items(places, set(relations["places"]))
        self.companions.set_items(companions, set(relations["companions"]))
        self.activities.set_items(activities, set(relations["activities"]))
        tabs.addTab(self.places, "地點")
        tabs.addTab(self.companions, "旅伴")
        tabs.addTab(self.activities, "活動")
        root.addWidget(tabs, 1)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self._accept)
        buttons.rejected.connect(self.reject)
        root.addWidget(buttons)

    def _accept(self) -> None:
        if self.row["time_data_status"] == "missing" and self.assigned_date.value() is None:
            QMessageBox.warning(self, "需要日期", "無原始時間的軌跡必須由使用者指定日期。")
            return
        mode = str(self.timezone_mode.currentData())
        fixed = self.fixed_timezone.currentText().strip() if mode == "fixed" else None
        if mode == "fixed" and fixed not in available_timezones():
            QMessageBox.warning(self, "時區錯誤", "請輸入有效的 IANA 時區，例如 Asia/Tokyo。")
            return
        self.repository.update_track(
            self.track_id, self.name.text(), self.assigned_date.value(), mode, fixed,
            self.notes.toPlainText(), self.places.selected_ids(), self.companions.selected_ids(),
            self.activities.selected_ids(),
        )
        self.accept()


class FilterDialog(QDialog):
    def __init__(self, repository: Repository, criteria: FilterCriteria, parent=None) -> None:
        super().__init__(parent)
        self.repository = repository
        self.criteria = criteria
        self.setWindowTitle("篩選")
        self.resize(720, 650)
        root = QVBoxLayout(self)
        form = QFormLayout()
        self.date_from = OptionalDateEdit()
        self.date_to = OptionalDateEdit()
        self.date_from.set_value(criteria.date_from)
        self.date_to.set_value(criteria.date_to)
        self.operator = QComboBox()
        self.operator.addItem("不同種類全部符合（AND）", "AND")
        self.operator.addItem("不同種類任一符合（OR）", "OR")
        self.operator.setCurrentIndex(1 if criteria.category_operator == "OR" else 0)
        form.addRow("日期由", self.date_from)
        form.addRow("日期至", self.date_to)
        form.addRow("種類關係", self.operator)
        root.addLayout(form)
        tabs = QTabWidget()
        places, companions, activities = reference_items(repository)
        self.places = CheckList("地點")
        self.companions = CheckList("旅伴")
        self.activities = CheckList("活動")
        self.places.set_items(places, set(criteria.place_ids))
        self.companions.set_items(companions, set(criteria.companion_ids))
        self.activities.set_items(activities, set(criteria.activity_ids))
        self.place_match = QComboBox(); self.place_match.addItem("符合任一", "ANY"); self.place_match.addItem("符合全部", "ALL")
        self.companion_match = QComboBox(); self.companion_match.addItem("符合任一", "ANY"); self.companion_match.addItem("符合全部", "ALL")
        self.activity_match = QComboBox(); self.activity_match.addItem("符合任一", "ANY"); self.activity_match.addItem("符合全部", "ALL")
        self.place_match.setCurrentIndex(1 if criteria.place_match == "ALL" else 0)
        self.companion_match.setCurrentIndex(1 if criteria.companion_match == "ALL" else 0)
        self.activity_match.setCurrentIndex(1 if criteria.activity_match == "ALL" else 0)
        tabs.addTab(self._with_match(self.places, self.place_match), "地點")
        tabs.addTab(self._with_match(self.companions, self.companion_match), "旅伴")
        tabs.addTab(self._with_match(self.activities, self.activity_match), "活動")
        root.addWidget(tabs, 1)
        clear = QPushButton("清除篩選設定")
        clear.clicked.connect(self._clear)
        root.addWidget(clear)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Ok | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self._accept)
        buttons.rejected.connect(self.reject)
        root.addWidget(buttons)

    @staticmethod
    def _with_match(widget: QWidget, combo: QComboBox) -> QWidget:
        container = QWidget()
        layout = QVBoxLayout(container)
        row = QHBoxLayout(); row.addWidget(QLabel("同一種類：")); row.addWidget(combo); row.addStretch(1)
        layout.addLayout(row); layout.addWidget(widget, 1)
        return container

    def _clear(self) -> None:
        self.criteria = FilterCriteria()
        self.accept()

    def _accept(self) -> None:
        if self.date_from.value() and self.date_to.value() and self.date_from.value() > self.date_to.value():
            QMessageBox.warning(self, "日期錯誤", "起始日期不可晚於結束日期。")
            return
        self.criteria = FilterCriteria(
            date_from=self.date_from.value(), date_to=self.date_to.value(),
            place_ids=self.places.selected_ids(), companion_ids=self.companions.selected_ids(),
            activity_ids=self.activities.selected_ids(), category_operator=str(self.operator.currentData()),
            place_match=str(self.place_match.currentData()), companion_match=str(self.companion_match.currentData()),
            activity_match=str(self.activity_match.currentData()),
        )
        self.accept()


class CompanionEditDialog(QDialog):
    def __init__(self, repository: Repository, row=None, parent=None) -> None:
        super().__init__(parent)
        self.repository = repository
        self.row = row
        self.setWindowTitle("旅伴")
        layout = QFormLayout(self)
        self.name = QLineEdit(str(row["name"]) if row else "")
        self.group = QComboBox()
        for group in repository.list_companion_groups():
            self.group.addItem(str(group["name"]), int(group["id"]))
        if row:
            idx = self.group.findData(int(row["group_id"])); self.group.setCurrentIndex(max(0, idx))
        self.notes = QTextEdit(str(row["notes"]) if row else "")
        self.sort = QSpinBox(); self.sort.setRange(-100000, 100000); self.sort.setValue(int(row["sort_order"]) if row else 0)
        layout.addRow("名稱", self.name); layout.addRow("群組", self.group); layout.addRow("備註", self.notes); layout.addRow("排序", self.sort)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self.accept); buttons.rejected.connect(self.reject); layout.addRow(buttons)


class ReferenceManager(QWidget):
    changed = Signal()

    def __init__(self, repository: Repository, kind: str, parent=None) -> None:
        super().__init__(parent)
        self.repository = repository
        self.kind = kind
        layout = QVBoxLayout(self)
        self.table = QTableWidget()
        self.table.setSelectionBehavior(QAbstractItemView.SelectionBehavior.SelectRows)
        self.table.setSelectionMode(QAbstractItemView.SelectionMode.SingleSelection)
        self.table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        layout.addWidget(self.table, 1)
        buttons = QHBoxLayout()
        self.add_button = QPushButton("新增")
        self.edit_button = QPushButton("編輯")
        self.delete_button = QPushButton("刪除")
        buttons.addWidget(self.add_button); buttons.addWidget(self.edit_button); buttons.addWidget(self.delete_button); buttons.addStretch(1)
        layout.addLayout(buttons)
        self.add_button.clicked.connect(self.add_record)
        self.edit_button.clicked.connect(self.edit_record)
        self.delete_button.clicked.connect(self.delete_record)
        self.reload()

    def reload(self) -> None:
        if self.kind == "groups":
            rows = self.repository.list_companion_groups(); headers = ["群組", "排序", "系統"]
            values = lambda r: [r["name"], r["sort_order"], "是" if r["is_system"] else ""]
        elif self.kind == "companions":
            rows = self.repository.list_companions(); headers = ["旅伴", "群組", "備註", "排序"]
            values = lambda r: [r["name"], r["group_name"], r["notes"], r["sort_order"]]
        else:
            rows = self.repository.list_activities(); headers = ["活動", "備註", "排序"]
            values = lambda r: [r["name"], r["notes"], r["sort_order"]]
        self.rows = list(rows)
        self.table.setColumnCount(len(headers)); self.table.setHorizontalHeaderLabels(headers); self.table.setRowCount(len(self.rows))
        for y, row in enumerate(self.rows):
            for x, value in enumerate(values(row)):
                item = QTableWidgetItem(str(value)); item.setData(ID_ROLE, int(row["id"])); self.table.setItem(y, x, item)

    def current_row(self):
        index = self.table.currentRow()
        return self.rows[index] if 0 <= index < len(self.rows) else None

    def add_record(self) -> None:
        if self.kind == "groups":
            name, ok = QInputDialog.getText(self, "新增群組", "群組名稱")
            if ok and name.strip(): self.repository.save_companion_group(None, name, len(self.rows))
        elif self.kind == "companions":
            dialog = CompanionEditDialog(self.repository, parent=self)
            if dialog.exec() == QDialog.DialogCode.Accepted and dialog.name.text().strip():
                self.repository.save_companion(None, int(dialog.group.currentData()), dialog.name.text(), dialog.notes.toPlainText(), dialog.sort.value())
        else:
            name, ok = QInputDialog.getText(self, "新增活動", "活動名稱")
            if ok and name.strip(): self.repository.save_activity(None, name, "", len(self.rows))
        self.reload(); self.changed.emit()

    def edit_record(self) -> None:
        row = self.current_row()
        if not row: return
        if self.kind == "groups":
            if bool(row["is_system"]): return
            name, ok = QInputDialog.getText(self, "編輯群組", "群組名稱", text=str(row["name"]))
            if ok and name.strip(): self.repository.save_companion_group(int(row["id"]), name, int(row["sort_order"]))
        elif self.kind == "companions":
            dialog = CompanionEditDialog(self.repository, row, self)
            if dialog.exec() == QDialog.DialogCode.Accepted and dialog.name.text().strip():
                self.repository.save_companion(int(row["id"]), int(dialog.group.currentData()), dialog.name.text(), dialog.notes.toPlainText(), dialog.sort.value())
        else:
            name, ok = QInputDialog.getText(self, "編輯活動", "活動名稱", text=str(row["name"]))
            if ok and name.strip(): self.repository.save_activity(int(row["id"]), name, str(row["notes"]), int(row["sort_order"]))
        self.reload(); self.changed.emit()

    def delete_record(self) -> None:
        row = self.current_row()
        if not row: return
        if QMessageBox.question(self, "確認刪除", "確定刪除選取項目？") != QMessageBox.StandardButton.Yes: return
        table = {"groups": "companion_groups", "companions": "companions", "activities": "activities"}[self.kind]
        try:
            self.repository.delete_reference(table, int(row["id"]))
        except Exception as exc:
            QMessageBox.warning(self, "無法刪除", str(exc))
        self.reload(); self.changed.emit()


class SettingsDialog(QDialog):
    def __init__(self, settings: AppSettings, repository: Repository, backup_service: BackupService, parent=None, *, tab_index: int = 0) -> None:
        super().__init__(parent)
        self.settings = settings
        self.repository = repository
        self.backup_service = backup_service
        self.setWindowTitle("設定")
        self.resize(760, 650)
        root = QVBoxLayout(self)
        tabs = QTabWidget()
        tabs.addTab(self._general_tab(), "一般")
        tabs.addTab(self._map_tab(), "軌跡／地圖")
        tabs.addTab(self._backup_tab(), "資料庫備份還原")
        refs = QTabWidget(); refs.addTab(ReferenceManager(repository, "groups"), "群組"); refs.addTab(ReferenceManager(repository, "companions"), "旅伴")
        tabs.addTab(refs, "旅伴設定")
        tabs.addTab(ReferenceManager(repository, "activities"), "活動設定")
        tabs.setCurrentIndex(max(0, min(tab_index, tabs.count() - 1)))
        self.tabs = tabs
        root.addWidget(tabs, 1)
        buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel)
        buttons.accepted.connect(self._save); buttons.rejected.connect(self.reject); root.addWidget(buttons)

    def _general_tab(self) -> QWidget:
        page = QWidget(); form = QFormLayout(page)
        self.database_path = QLineEdit(self.settings.database_path); browse = QPushButton("瀏覽…")
        row = QHBoxLayout(); row.addWidget(self.database_path, 1); row.addWidget(browse)
        browse.clicked.connect(lambda: self._choose_file(self.database_path, "SQLite (*.db)", save=True))
        form.addRow("資料庫位置", row)
        form.addRow(QLabel("變更資料庫位置將於下次啟動生效；快取會維持在程式資料夾。"))
        return page

    def _map_tab(self) -> QWidget:
        page = QWidget(); form = QFormLayout(page)
        self.provider = QComboBox(); self.provider.addItem("自動：OpenFreeMap → OSM → 本機", "auto"); self.provider.addItem("OpenFreeMap", "openfreemap"); self.provider.addItem("OpenStreetMap", "osm"); self.provider.addItem("本機簡易地圖", "fallback")
        idx = self.provider.findData(self.settings.map_provider); self.provider.setCurrentIndex(max(0, idx))
        self.show_points = QCheckBox("顯示定位點"); self.show_points.setChecked(self.settings.show_points)
        self.show_endpoints = QCheckBox("顯示起迄點標記"); self.show_endpoints.setChecked(self.settings.show_endpoints)
        self.show_connectors = QCheckBox("不同 Segment 以虛線連接"); self.show_connectors.setChecked(self.settings.show_segment_connectors)
        self.line_width = QSpinBox(); self.line_width.setRange(1, 8); self.line_width.setValue(round(self.settings.line_width))
        self.point_size = QSpinBox(); self.point_size.setRange(1, 10); self.point_size.setValue(round(self.settings.point_size))
        form.addRow("底圖", self.provider); form.addRow(self.show_points); form.addRow(self.show_endpoints); form.addRow(self.show_connectors); form.addRow("軌跡線寬", self.line_width); form.addRow("定位點大小", self.point_size)
        return page

    def _backup_tab(self) -> QWidget:
        page = QWidget(); layout = QVBoxLayout(page); form = QFormLayout()
        self.backup_dir = QLineEdit(self.settings.backup_directory); backup_browse = QPushButton("瀏覽…")
        row1 = QHBoxLayout(); row1.addWidget(self.backup_dir, 1); row1.addWidget(backup_browse); backup_browse.clicked.connect(lambda: self._choose_directory(self.backup_dir))
        self.drive_dir = QLineEdit(self.settings.google_drive_sync_directory); drive_browse = QPushButton("瀏覽…")
        row2 = QHBoxLayout(); row2.addWidget(self.drive_dir, 1); row2.addWidget(drive_browse); drive_browse.clicked.connect(lambda: self._choose_directory(self.drive_dir))
        self.keep = QSpinBox(); self.keep.setRange(1, 1000); self.keep.setValue(self.settings.automatic_backup_keep)
        form.addRow("本機備份資料夾", row1); form.addRow("Google Drive 同步位置", row2); form.addRow("自動備份保留", self.keep)
        layout.addLayout(form)
        buttons = QHBoxLayout(); backup_now = QPushButton("立即手動備份"); restore = QPushButton("從備份還原")
        buttons.addWidget(backup_now); buttons.addWidget(restore); buttons.addStretch(1); layout.addLayout(buttons); layout.addStretch(1)
        backup_now.clicked.connect(self._backup_now); restore.clicked.connect(self._restore)
        return page

    def _choose_file(self, edit: QLineEdit, pattern: str, save: bool = False) -> None:
        result = QFileDialog.getSaveFileName(self, "選擇檔案", edit.text(), pattern)[0] if save else QFileDialog.getOpenFileName(self, "選擇檔案", edit.text(), pattern)[0]
        if result: edit.setText(result)

    def _choose_directory(self, edit: QLineEdit) -> None:
        result = QFileDialog.getExistingDirectory(self, "選擇資料夾", edit.text())
        if result: edit.setText(result)

    def _backup_now(self) -> None:
        target, _ = QFileDialog.getSaveFileName(self, "手動備份", f"TM_manual_{QDate.currentDate().toString('yyyyMMdd')}.db", "SQLite (*.db)")
        if not target: return
        try:
            self.backup_service.manual_backup(Path(target)); QMessageBox.information(self, "完成", "手動備份完成。")
        except Exception as exc: QMessageBox.critical(self, "備份失敗", str(exc))

    def _restore(self) -> None:
        source, _ = QFileDialog.getOpenFileName(self, "選擇備份", "", "SQLite (*.db)")
        if not source: return
        if QMessageBox.warning(self, "確認還原", "還原後必須重新啟動程式。確定繼續？", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No) != QMessageBox.StandardButton.Yes: return
        try:
            before = self.backup_service.restore(Path(source)); QMessageBox.information(self, "還原完成", f"已還原。還原前備份：\n{before}\n請關閉並重新啟動程式。")
        except Exception as exc: QMessageBox.critical(self, "還原失敗", str(exc))

    def _save(self) -> None:
        new_database_path = self.database_path.text().strip()
        old_database_path = self.settings.database_path.strip()
        if new_database_path and Path(new_database_path).expanduser() != Path(old_database_path).expanduser():
            target = Path(new_database_path).expanduser()
            if target.exists():
                answer = QMessageBox.question(
                    self, "資料庫已存在",
                    "目標位置已有資料庫。是否以目前資料庫的完整備份覆蓋？",
                    QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                )
                if answer != QMessageBox.StandardButton.Yes:
                    return
            try:
                self.backup_service.database.backup_to(target)
            except Exception as exc:
                QMessageBox.critical(self, "搬移資料庫失敗", str(exc))
                return
        self.settings.database_path = new_database_path
        self.settings.backup_directory = self.backup_dir.text().strip()
        self.settings.google_drive_sync_directory = self.drive_dir.text().strip()
        self.settings.automatic_backup_keep = self.keep.value()
        self.settings.map_provider = str(self.provider.currentData())
        self.settings.show_points = self.show_points.isChecked()
        self.settings.show_endpoints = self.show_endpoints.isChecked()
        self.settings.show_segment_connectors = self.show_connectors.isChecked()
        self.settings.line_width = float(self.line_width.value())
        self.settings.point_size = float(self.point_size.value())
        self.accept()
