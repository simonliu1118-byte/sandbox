from __future__ import annotations

import traceback
from collections.abc import Callable
from typing import Any

from PySide6.QtCore import QObject, QThread, Signal, Slot

from ..operations import CancellationToken, OperationCancelled, OperationJournal, ProgressUpdate


class TaskWorker(QObject):
    progress = Signal(int, int, str)
    succeeded = Signal(object)
    failed = Signal(str)
    cancelled = Signal()
    finished = Signal()

    def __init__(
        self,
        operation_name: str,
        subject: str,
        function: Callable[[CancellationToken, Callable[[ProgressUpdate], None]], Any],
        journal: OperationJournal,
    ) -> None:
        super().__init__()
        self.operation_name = operation_name
        self.subject = subject
        self.function = function
        self.journal = journal
        self.token = CancellationToken()

    def report(self, update: ProgressUpdate) -> None:
        self.progress.emit(int(update.current), int(update.total), update.message)

    @Slot()
    def run(self) -> None:
        operation_id = self.journal.begin(self.operation_name, self.subject)
        state = "failed"
        try:
            result = self.function(self.token, self.report)
            self.token.raise_if_cancelled()
            state = "completed"
            self.succeeded.emit(result)
        except OperationCancelled:
            state = "cancelled"
            self.cancelled.emit()
        except Exception:
            self.failed.emit(traceback.format_exc())
        finally:
            self.journal.finish(operation_id, state)
            self.finished.emit()

    @Slot()
    def request_cancel(self) -> None:
        self.token.cancel()


class TaskHandle(QObject):
    def __init__(self, thread: QThread, worker: TaskWorker) -> None:
        super().__init__()
        self.thread = thread
        self.worker = worker

    def cancel(self) -> None:
        self.worker.request_cancel()
