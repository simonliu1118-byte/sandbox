from __future__ import annotations

import json
from pathlib import Path

from PySide6.QtCore import QObject, QTimer, QUrl, Signal, Slot
from PySide6.QtWebChannel import QWebChannel
from PySide6.QtWebEngineCore import QWebEngineProfile, QWebEngineSettings
from PySide6.QtWebEngineWidgets import QWebEngineView


class MapBridge(QObject):
    status_changed = Signal(str, str)
    track_selected = Signal(int)
    online_restored = Signal()

    @Slot(str, str)
    def mapStatus(self, state: str, provider: str) -> None:  # noqa: N802
        self.status_changed.emit(state, provider)

    @Slot(int)
    def trackSelected(self, track_id: int) -> None:  # noqa: N802
        self.track_selected.emit(track_id)

    @Slot()
    def onlineRestored(self) -> None:  # noqa: N802
        self.online_restored.emit()


class MapWidget(QWebEngineView):
    status_changed = Signal(str, str)
    track_selected = Signal(int)
    online_restored = Signal()

    def __init__(self, map_directory: Path, cache_directory: Path, parent=None) -> None:
        super().__init__(parent)
        self._loaded = False
        self._shutdown = False
        self._pending_by_kind: dict[str, str] = {}
        self._health_failures = 0
        self._map_url = QUrl.fromLocalFile(str(Path(map_directory) / "index.html"))
        self.bridge = MapBridge()
        self.bridge.status_changed.connect(self.status_changed)
        self.bridge.track_selected.connect(self.track_selected)
        self.bridge.online_restored.connect(self.online_restored)
        self.channel = QWebChannel(self.page())
        self.channel.registerObject("bridge", self.bridge)
        self.page().setWebChannel(self.channel)
        profile = self.page().profile()
        Path(cache_directory).mkdir(parents=True, exist_ok=True)
        profile.setHttpCacheType(QWebEngineProfile.HttpCacheType.DiskHttpCache)
        profile.setCachePath(str(Path(cache_directory) / "HttpCache"))
        profile.setPersistentStoragePath(str(Path(cache_directory) / "WebStorage"))
        profile.setHttpCacheMaximumSize(512 * 1024 * 1024)
        settings = self.settings()
        settings.setAttribute(QWebEngineSettings.WebAttribute.LocalContentCanAccessRemoteUrls, True)
        settings.setAttribute(QWebEngineSettings.WebAttribute.JavascriptEnabled, True)
        self.loadFinished.connect(self._on_loaded)
        self._health_timer = QTimer(self)
        self._health_timer.setInterval(15000)
        self._health_timer.timeout.connect(self._check_health)
        self._health_timer.start()
        self.setUrl(self._map_url)

    def _on_loaded(self, ok: bool) -> None:
        if self._shutdown:
            return
        self._loaded = ok
        if ok:
            for script in self._pending_by_kind.values():
                self.page().runJavaScript(script)
            self._pending_by_kind.clear()

    def _run(self, kind: str, script: str) -> None:
        if self._shutdown:
            return
        if self._loaded:
            self.page().runJavaScript(script)
        else:
            self._pending_by_kind[kind] = script

    def set_tracks(self, tracks: list[dict], *, gap_seconds: int = 120, show_points: bool = True,
                   show_endpoints: bool = True, show_segment_connectors: bool = True,
                   line_width: float = 2.4, point_size: float = 3.0, fit: bool = False) -> None:
        payload = {
            "tracks": tracks,
            "gapSeconds": gap_seconds,
            "showPoints": show_points,
            "showEndpoints": show_endpoints,
            "showSegmentConnectors": show_segment_connectors,
            "lineWidth": line_width,
            "pointSize": point_size,
            "fit": fit,
        }
        encoded = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
        self._run("tracks", f"window.tmSetTracks({json.dumps(encoded)});")

    def set_provider(self, provider: str) -> None:
        self._run("provider", f"window.tmSetProvider({json.dumps(provider)});")

    def set_options(self, options: dict) -> None:
        encoded = json.dumps(options, ensure_ascii=False, separators=(",", ":"))
        self._run("options", f"window.tmSetOptions({json.dumps(encoded)});")

    def select_track(self, track_id: int | None) -> None:
        value = "null" if track_id is None else str(int(track_id))
        self._run("selection", f"window.tmSelectTrack({value});")

    def fit_track(self, track_id: int, bounds: list[float] | None = None) -> None:
        self._run("viewport", f"window.tmFitTrack({int(track_id)}, {json.dumps(bounds)});")

    def _check_health(self) -> None:
        if not self._loaded or self._shutdown:
            return

        def received(value) -> None:
            if self._shutdown:
                return
            if isinstance(value, dict) and value.get("ready"):
                self._health_failures = 0
                return
            self._health_failures += 1
            if self._health_failures >= 3:
                self._health_failures = 0
                self._loaded = False
                self.setUrl(self._map_url)

        self.page().runJavaScript("window.tmHealth ? window.tmHealth() : null", received)

    def shutdown(self) -> None:
        if self._shutdown:
            return
        self._shutdown = True
        self._health_timer.stop()
        self._pending_by_kind.clear()
        if self._loaded:
            self.page().runJavaScript("window.tmShutdown ? window.tmShutdown() : true")
        self.stop()
        try:
            self.channel.deregisterObject(self.bridge)
        except Exception:
            pass
        self.page().setWebChannel(None)
        self._loaded = False
