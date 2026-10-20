const elements = {
  version: document.querySelector("#version-label"),
  login: document.querySelector("#login-button"),
  settingsButton: document.querySelector("#settings-button"),
  urlForm: document.querySelector("#url-form"),
  urlInput: document.querySelector("#url-input"),
  qualitySelect: document.querySelector("#quality-select"),
  inputNote: document.querySelector("#input-note"),
  browserSessionNote: document.querySelector("#browser-session-note"),
  list: document.querySelector("#task-list"),
  count: document.querySelector("#queue-count"),
  cancelAll: document.querySelector("#cancel-all-button"),
  summary: document.querySelector("#status-summary"),
  outputSummary: document.querySelector("#output-summary"),
  dialog: document.querySelector("#settings-dialog"),
  settingsForm: document.querySelector("#settings-form"),
  settingsClose: document.querySelector("#settings-close"),
  settingsCancel: document.querySelector("#settings-cancel"),
  outputDirectory: document.querySelector("#output-directory"),
  concurrency: document.querySelector("#concurrency"),
  threshold: document.querySelector("#slow-threshold"),
  firstByteTimeout: document.querySelector("#first-byte-timeout-seconds"),
  observation: document.querySelector("#observation-seconds"),
  qualityConfirm: document.querySelector("#quality-confirm-seconds"),
  browse: document.querySelector("#browse-button"),
  toast: document.querySelector("#toast"),
  qualityDialog: document.querySelector("#quality-dialog"),
  qualityDialogTitle: document.querySelector("#quality-dialog-title"),
  qualityDialogMessage: document.querySelector("#quality-dialog-message"),
  qualityDialogNote: document.querySelector("#quality-dialog-note"),
  qualityConfirmActions: document.querySelector("#quality-confirm-actions"),
  qualityFallbackActions: document.querySelector("#quality-fallback-actions"),
  qualityOpenPlayer: document.querySelector("#quality-open-player"),
  qualityOnlyLower: document.querySelector("#quality-only-lower"),
  qualityCancelDirect: document.querySelector("#quality-cancel-direct"),
  qualityReportBug: document.querySelector("#quality-report-bug"),
  qualityBack: document.querySelector("#quality-back"),
  qualityCancelTask: document.querySelector("#quality-cancel-task"),
  qualityFallbackChoices: document.querySelector("#quality-fallback-choices"),
};

const prioritySequence = ["normal", "high", "low"];
const priorityLabels = { normal: "一般", high: "高", low: "低" };
const qualityLabels = {
  highest: "最高可用",
  "1080": "1080p",
  "720": "720p",
  "480": "480p",
  "360": "360p",
  auto: "播放器自動",
};
const stateLabels = {
  queued: "等待中",
  auth_wait: "等待登入",
  quality_wait: "等待畫質確認",
  capturing: "取得播放來源",
  downloading: "下載中",
  merging: "合併中",
  cancelling: "取消中",
  completed: "已完成",
  failed: "失敗",
};

let appState = null;
let toastTimer = 0;
let polling = false;
let activeQualityTaskId = "";
let qualityDialogStage = "confirm";

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function formatBytes(value) {
  const bytes = Number(value || 0);
  if (!bytes) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  let amount = bytes;
  let index = 0;
  while (amount >= 1024 && index < units.length - 1) {
    amount /= 1024;
    index += 1;
  }
  return `${amount >= 100 || index === 0 ? amount.toFixed(0) : amount.toFixed(1)} ${units[index]}`;
}

function formatSpeed(value) {
  return value > 0 ? `${formatBytes(value)}/s` : "—";
}

function showToast(message) {
  clearTimeout(toastTimer);
  elements.toast.textContent = message;
  elements.toast.classList.add("show");
  toastTimer = setTimeout(() => elements.toast.classList.remove("show"), 2800);
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: { "content-type": "application/json", ...(options.headers || {}) },
  });
  const body = await response.json().catch(() => ({}));
  if (!response.ok)
    throw new Error(body.error || `操作失敗（${response.status}）`);
  return body;
}

function trackMarkup(label, track) {
  const percent = Math.max(0, Math.min(100, Number(track.percent || 0)));
  const amount =
    track.total > 0
      ? `${formatBytes(track.downloaded)} / ${formatBytes(track.total)}`
      : formatBytes(track.downloaded);
  const detail =
    track.state === "completed"
      ? "完成"
      : `${percent.toFixed(0)}%・${formatSpeed(track.speed)}`;
  return `
    <div class="track-row">
      <span class="track-label">${label}</span>
      <progress max="100" value="${percent}" aria-label="${label}進度 ${percent.toFixed(0)}%"></progress>
      <span class="track-detail">${escapeHtml(detail)}　${escapeHtml(amount)}</span>
    </div>`;
}

function sourceDiagnosticsMarkup(label, sources, isVideo = false) {
  if (!Array.isArray(sources) || !sources.length) return "";
  const details = sources
    .map((source) => {
      const identity = isVideo
        ? Number(source.height || 0) > 0
          ? `${source.height}p`
          : "畫質無法辨識"
        : source.mime || "音訊";
      const size = Number(source.size || 0) > 0
        ? `・${formatBytes(source.size)}`
        : "";
      const speed = Number(source.speed || 0) > 0 ? `・${formatSpeed(source.speed)}` : "";
      const status = ["downloading", "fast", "completed", "slow", "cancelled", "failed"].includes(source.status) ? source.status : "unused";
      const statusLabels = { unused: "未使用", downloading: "下載／測速中", fast: "高速", completed: "完成", slow: "慢速保留", cancelled: "已取消", failed: "失敗" };
      return `<span class="source-chip source-${status}" title="${escapeHtml(statusLabels[status])}">#${escapeHtml(source.index)} ${escapeHtml(identity)}・itag ${escapeHtml(source.itag || "?")}${escapeHtml(size + speed)}・${escapeHtml(statusLabels[status])}</span>`;
    })
    .join("");
  return `<div class="source-diagnostics"><span class="source-diagnostics-label">${escapeHtml(label)}：</span>${details}</div>`;
}

function taskMarkup(task) {
  const stateClass = `state-${task.state}`;
  const stateText = task.bugReported
    ? "程式BUG"
    : stateLabels[task.state] || task.state;
  const cancelling = task.state === "cancelling";
  const activePriority = !["completed", "failed"].includes(task.state);
  const operation = cancelling
    ? `<button class="button" disabled>取消中…</button>`
    : activePriority
    ? `<button class="button priority-button priority-${task.priority}" data-action="priority" data-id="${task.id}" data-priority="${task.priority}">優先度：${priorityLabels[task.priority]}</button>
       <button class="button" data-action="cancel" data-id="${task.id}">取消</button>`
    : `<button class="button" data-action="cancel" data-id="${task.id}">移除</button>`;
  const output = task.outputPath
    ? `<div class="file-meta">${escapeHtml(task.outputPath)}</div>`
    : "";
  const quality = qualityLabels[task.quality] || qualityLabels.highest;
  const actualQuality = /^\d+$/.test(String(task.resolvedQuality || ""))
    ? ` → ${task.resolvedQuality}p`
    : "";
  return `
    <tr data-task-id="${task.id}">
      <td>
        <div class="file-name">${escapeHtml(task.title)}</div>
        <span class="state-badge ${stateClass}">${escapeHtml(stateText)}</span>
        <span class="quality-badge">畫質：${escapeHtml(quality + actualQuality)}</span>
        ${output}
      </td>
      <td>
        <div class="track-list">
          ${trackMarkup("視訊", task.video)}
          ${trackMarkup("音訊", task.audio)}
        </div>
        <div class="track-note ${stateClass}">${escapeHtml(task.message)}</div>
        ${sourceDiagnosticsMarkup("視訊來源辨識", task.videoSources, true)}
        ${sourceDiagnosticsMarkup("音訊來源辨識", task.audioSources)}
        ${task.video.note ? `<div class="track-note">視訊：${escapeHtml(task.video.note)}</div>` : ""}
        ${task.audio.note ? `<div class="track-note">音訊：${escapeHtml(task.audio.note)}</div>` : ""}
      </td>
      <td class="total-cell">${Number(task.overallPercent || 0).toFixed(0)}%</td>
      <td><div class="operation-actions">${operation}</div></td>
    </tr>`;
}

function heightList(values) {
  return (values || []).length
    ? [...new Set(values)].sort((a, b) => b - a).map((height) => `${height}p`).join("、")
    : "尚未辨識";
}

function renderQualityDialog(task) {
  const prompt = task.qualityPrompt;
  const detected = heightList(prompt.detectedHeights);
  const menu = heightList(prompt.menuHeights);
  const fallback = Number(prompt.fallbackHeight || 0);
  if (qualityDialogStage === "confirm") {
    elements.qualityDialogTitle.textContent = "確認影片是否有指定畫質";
    elements.qualityDialogMessage.textContent =
      `你要求的畫質：${prompt.requestedLabel}\n` +
      `程式目前辨識到：${detected}\n\n` +
      `請確認 Google Drive 播放器是否提供${prompt.requestedLabel === "最高可用" ? "更高畫質" : prompt.requestedLabel}。`;
    elements.qualityDialogNote.textContent = prompt.menuConfirmsHigher
      ? `程式曾在播放器選單看到：${menu}，但沒有取得對應的視訊來源。`
      : `播放器選單辨識結果：${menu}。音訊下載不受這個確認影響。`;
    elements.qualityOnlyLower.textContent = fallback ? "改選已辨識畫質" : "沒有可選畫質";
    elements.qualityOnlyLower.disabled = !fallback;
    elements.qualityConfirmActions.hidden = false;
    elements.qualityFallbackActions.hidden = true;
  } else {
    elements.qualityDialogTitle.textContent = "選擇要改用的畫質";
    elements.qualityDialogMessage.textContent =
      `程式目前辨識到：${detected}。請選擇其中一種畫質繼續下載。\n` +
      "音訊若已開始下載，將直接沿用，不會重新等待或重新下載。";
    elements.qualityDialogNote.textContent =
      "選定後，只有該畫質的新來源會進入視訊下載及測速流程。";
    const choices = [...new Set(prompt.detectedHeights || [])].filter((height) => Number(height) > 0).sort((a, b) => b - a);
    elements.qualityFallbackChoices.innerHTML = choices.map((height) => `<button type="button" class="button primary-button" data-fallback-height="${height}">改用 ${height}p 下載</button>`).join("");
    elements.qualityConfirmActions.hidden = true;
    elements.qualityFallbackActions.hidden = false;
  }
}

function syncQualityDialog(state) {
  const task = state.tasks.find(
    (item) => item.state === "quality_wait" && item.qualityPrompt,
  );
  if (!task) {
    if (elements.qualityDialog.open) elements.qualityDialog.close();
    activeQualityTaskId = "";
    qualityDialogStage = "confirm";
    return;
  }
  if (activeQualityTaskId !== task.id) {
    activeQualityTaskId = task.id;
    qualityDialogStage = "confirm";
  }
  renderQualityDialog(task);
  if (!elements.qualityDialog.open) elements.qualityDialog.showModal();
}

async function submitQualityDecision(action) {
  if (!activeQualityTaskId) return;
  try {
    await api(
      `/api/tasks/${encodeURIComponent(activeQualityTaskId)}/quality-decision`,
      { method: "POST", body: JSON.stringify({ action }) },
    );
    if (action !== "open-player") {
      elements.qualityDialog.close();
      activeQualityTaskId = "";
      qualityDialogStage = "confirm";
    }
    await poll();
  } catch (error) {
    showToast(error.message);
  }
}

function render(state) {
  appState = state;
  const browserName = state.browserName || "瀏覽器";
  elements.version.textContent = `版本 ${state.version}`;
  elements.login.textContent = state.loginPending
    ? `等待 ${browserName} 登入…`
    : state.loggedIn
      ? `✓ 已登入 Google（${browserName}）`
      : `登入 Google（${browserName}）`;
  elements.login.disabled = state.loginPending;
  elements.login.classList.toggle(
    "login-complete",
    state.loggedIn && !state.loginPending,
  );
  elements.login.title = state.loggedIn
    ? `已登入；點擊可在同一個受控 ${browserName} 中切換 Google 帳號`
    : `在下載來源使用的同一個受控 ${browserName} 中登入 Google`;
  elements.browserSessionNote.textContent = `登入、影片播放來源與下載授權皆共用同一個 ${browserName} 工作階段。`;

  const unfinished = state.tasks.filter((task) => task.state !== "completed");
  const completed = state.tasks.filter((task) => task.state === "completed");
  elements.count.textContent = `未完成 ${unfinished.length}筆・已完成 ${completed.length}筆`;
  elements.cancelAll.disabled = unfinished.length === 0;

  let markup = unfinished.map(taskMarkup).join("");
  if (completed.length) {
    markup += `<tr class="completed-divider"><td colspan="4">已完成</td></tr>`;
    markup += completed.map(taskMarkup).join("");
  }
  if (!state.tasks.length)
    markup = `<tr class="empty-row"><td colspan="4">尚未加入影片網址</td></tr>`;
  elements.list.innerHTML = markup;

  const downloading = state.tasks.filter((task) =>
    ["capturing", "downloading", "merging", "cancelling"].includes(task.state),
  ).length;
  const waiting = state.tasks.filter((task) =>
    ["queued", "auth_wait", "quality_wait"].includes(task.state),
  ).length;
  elements.summary.textContent = `下載中 ${downloading}筆　等待 ${waiting}筆　已完成 ${completed.length}筆`;
  elements.outputSummary.textContent = `儲存位置：${state.settings.outputDirectory}`;
  syncQualityDialog(state);
}

async function poll() {
  if (polling) return;
  polling = true;
  try {
    render(await api("/api/state"));
  } catch (error) {
    elements.version.textContent = "程式連線中斷";
  } finally {
    polling = false;
  }
}

elements.urlForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const url = elements.urlInput.value.trim();
  if (!url) return;
  try {
    const quality = elements.qualitySelect.value;
    localStorage.setItem("cydrive-quality", quality);
    await api("/api/tasks", {
      method: "POST",
      body: JSON.stringify({ url, quality }),
    });
    elements.urlInput.value = "";
    elements.inputNote.textContent = "已加入任務並自動排程。";
    await poll();
  } catch (error) {
    showToast(error.message);
    elements.urlInput.select();
  }
});

const savedQuality = localStorage.getItem("cydrive-quality");
if (savedQuality && qualityLabels[savedQuality])
  elements.qualitySelect.value = savedQuality;
elements.qualitySelect.addEventListener("change", () =>
  localStorage.setItem("cydrive-quality", elements.qualitySelect.value),
);

elements.list.addEventListener("click", async (event) => {
  const button = event.target.closest("button[data-action]");
  if (!button) return;
  const id = button.dataset.id;
  try {
    if (button.dataset.action === "priority") {
      const current = button.dataset.priority;
      const next =
        prioritySequence[
          (prioritySequence.indexOf(current) + 1) % prioritySequence.length
        ];
      await api(`/api/tasks/${encodeURIComponent(id)}`, {
        method: "PATCH",
        body: JSON.stringify({ priority: next }),
      });
    } else {
      await api(`/api/tasks/${encodeURIComponent(id)}`, { method: "DELETE" });
    }
    await poll();
  } catch (error) {
    showToast(error.message);
  }
});

elements.qualityOpenPlayer.addEventListener("click", () =>
  submitQualityDecision("open-player"),
);
elements.qualityCancelDirect.addEventListener("click", () =>
  submitQualityDecision("cancel"),
);
elements.qualityReportBug.addEventListener("click", () =>
  submitQualityDecision("bug"),
);
elements.qualityOnlyLower.addEventListener("click", () => {
  qualityDialogStage = "fallback";
  const task = appState?.tasks.find((item) => item.id === activeQualityTaskId);
  if (task?.qualityPrompt) renderQualityDialog(task);
});
elements.qualityBack.addEventListener("click", () => {
  qualityDialogStage = "confirm";
  const task = appState?.tasks.find((item) => item.id === activeQualityTaskId);
  if (task?.qualityPrompt) renderQualityDialog(task);
});
elements.qualityCancelTask.addEventListener("click", () =>
  submitQualityDecision("cancel"),
);
elements.qualityFallbackChoices.addEventListener("click", (event) => {
  const button = event.target.closest("button[data-fallback-height]");
  if (button) submitQualityDecision(`fallback:${button.dataset.fallbackHeight}`);
});
elements.qualityDialog.addEventListener("cancel", (event) =>
  event.preventDefault(),
);

elements.cancelAll.addEventListener("click", async () => {
  const unfinished =
    appState?.tasks.filter((task) => task.state !== "completed").length || 0;
  if (!unfinished || !confirm(`確定取消全部 ${unfinished} 筆未完成任務？`))
    return;
  try {
    await api("/api/cancel-all", { method: "POST", body: "{}" });
    await poll();
  } catch (error) {
    showToast(error.message);
  }
});

elements.login.addEventListener("click", async () => {
  try {
    await api("/api/login", { method: "POST", body: "{}" });
    showToast(
      appState?.loggedIn
        ? "請在開啟的視窗中選擇或切換 Google 帳號"
        : "請在開啟的視窗中完成 Google 登入",
    );
    await poll();
  } catch (error) {
    showToast(error.message);
  }
});

elements.settingsButton.addEventListener("click", () => {
  const settings = appState.settings;
  elements.outputDirectory.value = settings.outputDirectory;
  elements.concurrency.value = settings.concurrency;
  elements.threshold.value = settings.slowThresholdKBs;
  elements.firstByteTimeout.value = settings.firstByteTimeoutSeconds;
  elements.observation.value = settings.observationSeconds;
  elements.qualityConfirm.value = settings.qualityConfirmSeconds;
  elements.dialog.showModal();
});

elements.settingsClose.addEventListener("click", () => elements.dialog.close());
elements.settingsCancel.addEventListener("click", () =>
  elements.dialog.close(),
);
elements.browse.addEventListener("click", async () => {
  elements.browse.disabled = true;
  try {
    const result = await api("/api/browse", { method: "POST", body: "{}" });
    if (result.path) elements.outputDirectory.value = result.path;
  } catch (error) {
    showToast(error.message);
  } finally {
    elements.browse.disabled = false;
  }
});

elements.settingsForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  try {
    await api("/api/settings", {
      method: "PUT",
      body: JSON.stringify({
        outputDirectory: elements.outputDirectory.value,
        concurrency: Number(elements.concurrency.value),
        slowThresholdKBs: Number(elements.threshold.value),
        firstByteTimeoutSeconds: Number(elements.firstByteTimeout.value),
        observationSeconds: Number(elements.observation.value),
        qualityConfirmSeconds: Number(elements.qualityConfirm.value),
      }),
    });
    elements.dialog.close();
    showToast("設定已儲存");
    await poll();
  } catch (error) {
    showToast(error.message);
  }
});

window.addEventListener("beforeunload", (event) => {
  const active = appState?.tasks.some((task) =>
    ["capturing", "downloading", "merging", "cancelling", "quality_wait"].includes(task.state),
  );
  if (!active) return;
  event.preventDefault();
  event.returnValue = "";
});

window.addEventListener("pagehide", () => {
  navigator.sendBeacon(
    "/api/ui-closed",
    new Blob(["{}"], { type: "application/json" }),
  );
});

setInterval(poll, 700);
poll();
