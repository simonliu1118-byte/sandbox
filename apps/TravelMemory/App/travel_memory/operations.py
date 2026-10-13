from __future__ import annotations

import json
import threading
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable


class OperationCancelled(RuntimeError):
    """Raised when a cooperative long-running operation is cancelled."""


@dataclass(slots=True)
class ProgressUpdate:
    current: int
    total: int
    message: str = ""


ProgressCallback = Callable[[ProgressUpdate], None]


class CancellationToken:
    def __init__(self) -> None:
        self._event = threading.Event()

    def cancel(self) -> None:
        self._event.set()

    @property
    def is_cancelled(self) -> bool:
        return self._event.is_set()

    def raise_if_cancelled(self) -> None:
        if self.is_cancelled:
            raise OperationCancelled("作業已取消")


class OperationJournal:
    """Small crash-recovery journal that never stores track or coordinate data."""

    def __init__(self, path: Path) -> None:
        self.path = Path(path)
        self._lock = threading.Lock()

    def begin(self, operation: str, subject: str = "") -> str:
        operation_id = f"{datetime.now(timezone.utc):%Y%m%dT%H%M%S%fZ}-{threading.get_ident()}"
        payload = {
            "operation_id": operation_id,
            "operation": operation,
            "subject": subject,
            "started_at_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "state": "running",
        }
        with self._lock:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            temporary = self.path.with_suffix(self.path.suffix + ".tmp")
            temporary.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
            temporary.replace(self.path)
        return operation_id

    def finish(self, operation_id: str, state: str = "completed") -> None:
        with self._lock:
            if not self.path.exists():
                return
            try:
                payload = json.loads(self.path.read_text(encoding="utf-8"))
            except Exception:
                self.path.unlink(missing_ok=True)
                return
            if payload.get("operation_id") != operation_id:
                return
            payload["state"] = state
            payload["finished_at_utc"] = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
            history = self.path.with_name(f"operation_{operation_id}_{state}.json")
            history.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
            self.path.unlink(missing_ok=True)

    def unfinished(self) -> dict[str, object] | None:
        if not self.path.exists():
            return None
        try:
            payload = json.loads(self.path.read_text(encoding="utf-8"))
            return payload if payload.get("state") == "running" else None
        except Exception:
            return {"state": "unreadable", "path": str(self.path)}
