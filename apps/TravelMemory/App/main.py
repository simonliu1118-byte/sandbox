from __future__ import annotations

import argparse
import ctypes
import logging
import os
import sys
import traceback
from pathlib import Path

# The Windows launcher intentionally uses Python isolated mode (-I).
# In isolated mode Python does not prepend the script directory to sys.path,
# so explicitly add the bundled App directory before importing travel_memory.
APP_DIR = Path(__file__).resolve().parent
if str(APP_DIR) not in sys.path:
    sys.path.insert(0, str(APP_DIR))

from travel_memory import APP_NAME, COPYRIGHT, __version__
from travel_memory.database import Database
from travel_memory.logging_config import configure_logging, write_crash_report
from travel_memory.paths import AppPaths, bundled_sample_paths
from travel_memory.repository import Repository
from travel_memory.services.backup_service import BackupService
from travel_memory.services.diagnostics import run_self_test, run_webengine_smoke, write_release_check
from travel_memory.services.export_service import ExportService
from travel_memory.services.import_service import ImportService
from travel_memory.services.riti_parser import validate_reference_files
from travel_memory.services.single_instance import SingleInstance
from travel_memory.services.stability import recover_after_unclean_shutdown, verify_runtime_environment
from travel_memory.services.timezone_service import TimeZoneService
from travel_memory.settings import SettingsService
from travel_memory.operations import OperationJournal


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="旅行記憶（Travel Memory）")
    parser.add_argument("--self-test", action="store_true", help="執行非互動安裝檢查")
    parser.add_argument("--validate-riti", action="store_true", help="驗證內附 260530/260602 RITI 樣本")
    parser.add_argument("--version", action="version", version=f"{APP_NAME} {__version__}")
    parser.add_argument("--write-version-file", type=Path, help="將版本寫入指定檔案")
    parser.add_argument("--release-check", type=Path, help="執行封裝後檢查並寫入指定檔案")
    parser.add_argument("--webengine-smoke-test", type=Path, help="執行 QtWebEngine 地圖測試並寫入指定檔案")
    return parser.parse_args()


def resolve_database_path(paths: AppPaths, configured: str) -> Path:
    if not configured.strip():
        return paths.database
    candidate = Path(configured).expanduser()
    return candidate if candidate.is_absolute() else (paths.root / candidate).resolve()


def _launcher_token() -> str:
    return os.environ.get("TRAVEL_MEMORY_LAUNCH_TOKEN", "").strip()


def _launcher_path(paths: AppPaths, suffix: str) -> Path | None:
    token = _launcher_token()
    if not token:
        return None
    safe = "".join(ch for ch in token if ch.isalnum() or ch in "-_")[:96]
    return paths.runtime / f"launch_{safe}.{suffix}"


def _write_launcher_status(paths: AppPaths, suffix: str, text: str) -> None:
    target = _launcher_path(paths, suffix)
    if target is None:
        return
    target.parent.mkdir(parents=True, exist_ok=True)
    temp = target.with_suffix(target.suffix + ".tmp")
    temp.write_text(text, encoding="utf-8")
    temp.replace(target)


def _show_emergency_error(text: str) -> None:
    if os.name != "nt":
        return
    try:
        ctypes.windll.user32.MessageBoxW(None, text, "旅行記憶啟動失敗", 0x10)
    except Exception:
        pass


def _force_window_visible(window, app, logger: logging.Logger) -> None:
    from PySide6.QtCore import QPoint
    from PySide6.QtGui import QGuiApplication

    window.showNormal()
    app.processEvents()

    frame = window.frameGeometry()
    screens = QGuiApplication.screens()
    visible = any(screen.availableGeometry().intersects(frame) for screen in screens)
    if not visible:
        screen = QGuiApplication.primaryScreen()
        if screen is not None:
            area = screen.availableGeometry()
            frame.moveCenter(area.center())
            window.move(frame.topLeft())

    window.show()
    window.raise_()
    window.activateWindow()
    app.processEvents()

    if os.name == "nt":
        try:
            hwnd = int(window.winId())
            user32 = ctypes.windll.user32
            SW_RESTORE = 9
            SWP_NOMOVE = 0x0002
            SWP_NOSIZE = 0x0001
            SWP_SHOWWINDOW = 0x0040
            HWND_TOPMOST = -1
            HWND_NOTOPMOST = -2
            user32.ShowWindow(hwnd, SW_RESTORE)
            user32.SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW)
            user32.SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW)
            user32.SetForegroundWindow(hwnd)
            user32.BringWindowToTop(hwnd)
        except Exception:
            logger.exception("Unable to force main window to foreground")


def main() -> int:
    args = parse_args()
    paths = AppPaths.discover()
    paths.ensure()
    logger = configure_logging(paths)
    logger.info("Startup entered. version=%s pid=%s root=%s", __version__, os.getpid(), paths.root)

    try:
        verify_runtime_environment(paths)
        settings_service = SettingsService(paths)
        settings = settings_service.load()
        resolved_database = resolve_database_path(paths, settings.database_path)
        database = Database(resolved_database, paths, logger)
        database.initialize()
        map_cache_directory = database.path.parent / "MapCache"
        map_cache_directory.mkdir(parents=True, exist_ok=True)
        journal = OperationJournal(paths.runtime / "current_operation.json")
        recovery_report = recover_after_unclean_shutdown(paths, database, journal)
        if recovery_report:
            logger.warning("Recovered after unclean shutdown: %s", recovery_report)

        if args.write_version_file:
            args.write_version_file.parent.mkdir(parents=True, exist_ok=True)
            args.write_version_file.write_text(f"{APP_NAME} {__version__}\n", encoding="utf-8")
            return 0

        if args.release_check:
            return 0 if write_release_check(paths, database, args.release_check) else 1

        if args.webengine_smoke_test:
            return 0 if run_webengine_smoke(paths, args.webengine_smoke_test) else 1

        if args.validate_riti:
            sample_paths = bundled_sample_paths()
            reports = validate_reference_files(sample_paths)
            for name, report in reports.items():
                print(name, report)
            return 0 if all(bool(item["passed"]) for item in reports.values()) else 1

        if args.self_test:
            passed, output = run_self_test(paths, database)
            print(output.read_text(encoding="utf-8"))
            return 0 if passed else 1

        instance = SingleInstance(paths.runtime / "TravelMemory.lock")
        if not instance.acquire():
            message = "旅行記憶已在執行中，或前次異常結束留下鎖定。"
            logger.error(message)
            _write_launcher_status(paths, "error", message)
            return 2

        try:
            from PySide6.QtCore import QTimer
            from PySide6.QtGui import QIcon
            from PySide6.QtWidgets import QApplication, QMessageBox
            from travel_memory.ui.main_window import MainWindow

            app = QApplication(sys.argv)
            app.setApplicationName(APP_NAME)
            app.setApplicationVersion(__version__)
            app.setOrganizationName("C.C.Liu")
            app.setQuitOnLastWindowClosed(True)
            icon = paths.resources / "Icons" / "TravelMemory.ico"
            if icon.exists():
                app.setWindowIcon(QIcon(str(icon)))

            def exception_hook(exc_type, exc, tb) -> None:
                logger.critical("Unhandled exception", exc_info=(exc_type, exc, tb))
                report = write_crash_report(paths, exc_type, exc, tb)
                _write_launcher_status(paths, "error", f"未處理錯誤：{exc}\n診斷檔：{report}")
                QMessageBox.critical(None, "旅行記憶發生錯誤", f"程式發生未處理錯誤，已建立診斷檔：\n{report}")

            sys.excepthook = exception_hook
            timezone_service = TimeZoneService(logger)
            repository = Repository(database, timezone_service)
            import_service = ImportService(repository, timezone_service, logger)
            export_service = ExportService(database)
            backup_service = BackupService(database, paths, settings, logger)
            window = MainWindow(
                repository, import_service, export_service, backup_service, settings, settings_service, journal,
                paths.map_resources, map_cache_directory, logger,
            )
            _force_window_visible(window, app, logger)

            if not window.isVisible():
                raise RuntimeError("主視窗建立後仍未進入可見狀態")

            hwnd = int(window.winId()) if os.name == "nt" else 0
            _write_launcher_status(
                paths,
                "ready",
                f"pid={os.getpid()}\nhwnd={hwnd}\nversion={__version__}\ntitle={window.windowTitle()}\n",
            )
            logger.info("Application window shown. version=%s pid=%s hwnd=%s root=%s", __version__, os.getpid(), hwnd, paths.root)

            # Windows sometimes rejects the first foreground request while the launcher
            # still owns focus. Repeat briefly after the event loop starts.
            for delay in (100, 350, 900):
                QTimer.singleShot(delay, lambda w=window, a=app, log=logger: _force_window_visible(w, a, log))

            def startup_backup() -> None:
                try:
                    backup_service.automatic_backup()
                except Exception:
                    logger.exception("Startup automatic backup failed")

            QTimer.singleShot(1200, startup_backup)
            result = app.exec()
            logger.info("Application exited. code=%s", result)
            return int(result)
        except ImportError as exc:
            logger.exception("PySide6 startup failed")
            _write_launcher_status(paths, "error", f"缺少執行元件：{exc}")
            return 3
        finally:
            instance.release()
    except BaseException as exc:
        logger.critical("Startup failed before main window", exc_info=True)
        detail = "".join(traceback.format_exception(type(exc), exc, exc.__traceback__))
        _write_launcher_status(paths, "error", detail)
        return 99


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except SystemExit:
        raise
    except BaseException as exc:
        text = "".join(traceback.format_exception(type(exc), exc, exc.__traceback__))
        try:
            paths = AppPaths.discover()
            paths.ensure()
            _write_launcher_status(paths, "error", text)
            (paths.logs / "StartupEmergency.log").write_text(text, encoding="utf-8")
        except Exception:
            pass
        _show_emergency_error(f"旅行記憶無法啟動：\n\n{exc}\n\n詳細記錄請查看 Logs 資料夾。")
        raise
