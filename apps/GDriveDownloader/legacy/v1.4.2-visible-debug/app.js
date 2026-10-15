// @bun
import { createServer as z0 } from "net";
import {
  existsSync as c,
  readFileSync as K0,
  renameSync as Z0,
  rmSync as X,
  writeFileSync as B0,
} from "fs";
import { homedir as u } from "os";
import { basename as G0, dirname as i, join as M } from "path";
import { existsSync as y } from "fs";
import { join as Y } from "path";
import { existsSync as j, mkdirSync as d } from "fs";
import { join as x } from "path";
var o = new Set([139, 140, 141, 171, 172, 249, 250, 251]),
  l = new Set([
    133, 134, 135, 136, 137, 160, 212, 242, 243, 244, 247, 248, 264, 266, 271,
    272, 278, 298, 299, 302, 303, 308, 313, 315,
  ]),
  v = new Set([17, 18, 22, 37, 38, 59, 78]);
var k0 = new Map([
    [17, 144], [18, 360], [22, 720], [37, 1080], [38, 3072], [59, 480], [78, 480],
    [160, 144], [133, 240], [134, 360], [135, 480], [136, 720], [137, 1080],
    [264, 1440], [266, 2160], [298, 720], [299, 1080],
    [278, 144], [242, 240], [243, 360], [244, 480], [247, 720], [248, 1080],
    [271, 1440], [272, 2160], [302, 720], [303, 1080], [308, 1440], [313, 2160], [315, 2160],
  ]),
  p0 = new Set(["highest", "1080", "720", "480", "360", "auto"]);
function r0(value) {
  let quality = String(value || "highest").toLowerCase();
  return p0.has(quality) ? quality : "highest";
}
function n0(source) {
  try {
    let storedHeight = Number(
      typeof source === "object" && source ? source.height : 0,
    );
    if (Number.isFinite(storedHeight) && storedHeight > 0)
      return storedHeight;
    let url = new URL(a0(source)),
      explicit = Number(
        url.searchParams.get("height") ||
          String(url.searchParams.get("quality_label") || "").match(/\d+/)?.[0],
      );
    if (Number.isFinite(explicit) && explicit > 0) return explicit;
    return k0.get(Number(url.searchParams.get("itag"))) || 0;
  } catch {
    return 0;
  }
}
function q(f) {
  let _ = f.trim(),
    $ = [
      /\/file\/d\/([a-zA-Z0-9_-]{10,})/,
      /[?&]id=([a-zA-Z0-9_-]{10,})/,
      /\/d\/([a-zA-Z0-9_-]{10,})/,
    ];
  for (let z of $) {
    let Z = _.match(z);
    if (Z) return Z[1];
  }
  if (/^[a-zA-Z0-9_-]{20,}$/.test(_)) return _;
  return null;
}
function I(f) {
  return `https://drive.google.com/file/d/${f}/view`;
}
function L(f) {
  let _ = new URL(f);
  return (
    _.searchParams.delete("ump"),
    _.searchParams.delete("range"),
    _.toString()
  );
}
function a0(f) {
  return typeof f === "string" ? f : String(f?.url || "");
}
function s0(f) {
  return typeof f === "object" && f && f.candidateId
    ? String(f.candidateId)
    : a0(f);
}
function c0(f) {
  return typeof f === "object" && f ? { ...(f.headers || {}) } : {};
}
function d0(f) {
  let _ = {},
    $ = new Set([
      "accept",
      "accept-language",
      "authorization",
      "cookie",
      "origin",
      "referer",
      "user-agent",
      "x-client-data",
    ]);
  for (let [z, Z] of Object.entries(f || {})) {
    let K = z.toLowerCase();
    if (
      K.startsWith(":") ||
      K === "range" ||
      K === "host" ||
      K === "content-length"
    )
      continue;
    if ($.has(K) || K.startsWith("sec-ch-") || K.startsWith("sec-fetch-"))
      _[z] = String(Z);
  }
  return _;
}
function e0(...f) {
  let _ = new Map();
  for (let $ of f.flat()) {
    let z = a0($);
    if (!z) continue;
    let sourceKey = s0($),
      Z = _.get(sourceKey);
    if (Z) Z.headers = { ...Z.headers, ...c0($) };
    else
      _.set(
        sourceKey,
        typeof $ === "string" ? { url: z, headers: {} } : $,
      );
  }
  return [..._.values()];
}
var P0 = `(() => {
  const meta = document.querySelector('meta[property="og:title"]')
    || document.querySelector('meta[name="title"]')
    || document.querySelector('meta[itemprop="name"]');
  const allElements = [];
  const roots = [document];
  for (let rootIndex = 0; rootIndex < roots.length; rootIndex += 1) {
    const root = roots[rootIndex];
    for (const node of root.querySelectorAll('*')) {
      allElements.push(node);
      if (node.shadowRoot) roots.push(node.shadowRoot);
    }
  }
  const videos = allElements.filter((node) => node.matches('video,audio'));
  for (const media of videos) {
    media.muted = true;
    media.volume = 0;
    media.autoplay = true;
    media.play().catch(() => {});
  }
  const buttons = allElements.filter((node) => node.matches('button,[role="button"]'));
  const play = buttons.find((node) => {
    const label = (node.getAttribute('aria-label') || node.getAttribute('data-tooltip') || node.textContent || '').trim();
    const rect = node.getBoundingClientRect();
    return /(\\u64AD\\u653E|play\\b)/i.test(label)
      && !/(\\u66AB\\u505C|pause\\b)/i.test(label)
      && rect.width > 0 && rect.height > 0;
  });
  for (const media of videos) {
    media.muted = true;
    media.volume = 0;
    media.play().catch(() => {});
  }
  const rect = play && play.getBoundingClientRect();
  const pointOf = (node) => {
    const box = node && node.getBoundingClientRect();
    return box && box.width > 0 && box.height > 0
      ? { x: box.left + box.width / 2, y: box.top + box.height / 2 }
      : null;
  };
  const preview = allElements
    .filter((node) => node.matches('video,img,canvas'))
    .filter((node) => {
      const box = node.getBoundingClientRect();
      return box.width >= 320 && box.height >= 180 && box.bottom > 140 && box.top < innerHeight;
    })
    .sort((a, b) => {
      const aBox = a.getBoundingClientRect();
      const bBox = b.getBoundingClientRect();
      return bBox.width * bBox.height - aBox.width * aBox.height;
    })[0] || null;
  const mediaSurface = videos[0] || preview;
  const mediaRect = mediaSurface && mediaSurface.getBoundingClientRect();
  const playPoint = pointOf(play) || pointOf(preview);
  const playMethod = play ? 'labeled-control' : preview ? 'preview-center' : '';
  return {
    title: (meta && meta.content) || document.title || '',
    bodyText: (document.body && document.body.innerText || '').slice(0, 4000),
    mediaCount: videos.length,
    playingCount: videos.filter((media) => !media.paused && !media.ended).length,
    hasPlayButton: Boolean(play),
    playPoint,
    playMethod,
    resourceUrls: performance.getEntriesByType('resource')
      .map((entry) => String(entry.name || ''))
      .filter((url) => url.includes('videoplayback'))
  };
})()`;
class O0 {
  items = [];
  closed = !1;
  error = null;
  add(source) {
    let url = a0(source),
      sourceKey = s0(source);
    if (!url || this.items.some((item) => s0(item) === sourceKey)) return !1;
    this.items.push(source);
    return !0;
  }
  close(error = null) {
    this.closed = !0;
    this.error = error;
  }
  async get(index, signal) {
    while (!this.items[index]) {
      if (this.error) throw this.error;
      if (this.closed) return null;
      await T(200, signal);
    }
    return this.items[index];
  }
  get size() {
    return this.items.length;
  }
}
function R(f) {
  try {
    let storedType =
        typeof f === "object" && f
          ? String(f.mediaType || "").toLowerCase()
          : "",
      storedMime =
        typeof f === "object" && f
          ? String(f.mimeType || "").toLowerCase()
          : "",
      responseContentType = "";
    if (typeof f === "object" && f)
      for (let [name, value] of Object.entries({
        ...(f.responseHeaders || {}),
        ...(f.headers || {}),
      }))
        if (name.toLowerCase() === "content-type")
          responseContentType = String(value || "").toLowerCase();
    if (["audio", "video", "combined"].includes(storedType))
      return storedType;
    let _ = new URL(a0(f)),
      $ = (
        storedMime ||
        responseContentType ||
        _.searchParams.get("mime") ||
        ""
      ).toLowerCase();
    if ($.startsWith("audio/")) return "audio";
    if ($.startsWith("video/")) {
      let Z = Number(_.searchParams.get("itag"));
      return v.has(Z) ? "combined" : "video";
    }
    let z = Number(_.searchParams.get("itag"));
    if (o.has(z)) return "audio";
    if (l.has(z)) return "video";
    if (v.has(z)) return "combined";
  } catch {
    return "unknown";
  }
  return "unknown";
}
function V(f) {
  try {
    let storedLength = Number(
      typeof f === "object" && f ? f.contentLength : 0,
    );
    if (Number.isFinite(storedLength) && storedLength > 0)
      return storedLength;
    if (typeof f === "object" && f)
      for (let [name, value] of Object.entries(f.responseHeaders || {}))
        if (name.toLowerCase() === "content-length") {
          let headerLength = Number(value);
          if (Number.isFinite(headerLength) && headerLength > 0)
            return headerLength;
        }
    let _ = Number(new URL(a0(f)).searchParams.get("clen"));
    return Number.isFinite(_) && _ > 0 ? _ : 0;
  } catch {
    return 0;
  }
}
function W(f, _) {
  let $ = new Map();
  for (let K of f) {
    if (R(K) !== _) continue;
    let B = "unknown";
    try {
      B = new URL(a0(K)).searchParams.get("itag") || "unknown";
    } catch {}
    let G = $.get(B) || [];
    if (!G.some((E) => s0(E) === s0(K))) G.push(K);
    $.set(B, G);
  }
  if (!$.size) return [];
  let z = [],
    Z = -1;
  for (let K of $.values()) {
    let B = Math.max(...K.map(V), 0);
    if (B > Z) ((z = K), (Z = B));
  }
  return z;
}
function m(f, _) {
  let $ = f
    .replace(/\s+-\s+Google Drive\s*$/i, "")
    .replace(/[<>:"/\\|?*\u0000-\u001f]/g, "_")
    .replace(/[. ]+$/g, "")
    .trim();
  if (!$ || /^(sign in|google drive)$/i.test($)) $ = _;
  return (
    ($ = $.replace(/\.(mp4|m4a|mov|mkv|webm|avi)$/i, "")),
    $.slice(0, 180) || _
  );
}
function Q(f) {
  if (!j(f)) d(f, { recursive: !0 });
}
function g(f, _) {
  Q(f);
  let $ = x(f, `${_}.mp4`),
    z = 1;
  while (j($)) (($ = x(f, `${_} (${z}).mp4`)), (z += 1));
  return $;
}
function T(f, _) {
  return new Promise(($, z) => {
    if (_?.aborted) {
      z(new DOMException("Aborted", "AbortError"));
      return;
    }
    let Z = setTimeout($, f);
    _?.addEventListener(
      "abort",
      () => {
        (clearTimeout(Z), z(new DOMException("Aborted", "AbortError")));
      },
      { once: !0 },
    );
  });
}
function w(f) {
  return f instanceof DOMException
    ? f.name === "AbortError"
    : f instanceof Error && /abort/i.test(f.message);
}
function isBrowserControlFailure(f) {
  let message = f instanceof Error ? f.message : String(f || "");
  return /\u700F\u89BD\u5668\u63A7\u5236\u9023\u7DDA\u5C1A\u672A\u5C31\u7DD2|Failed to open a new tab|Browser has closed|browser.*disconnected/i.test(
    message,
  );
}
class P {
  socket = null;
  nextId = 1;
  pending = new Map();
  sessionHandlers = new Map();
  allHandlers = new Set();
  async connect(f) {
    await new Promise((_, $) => {
      let z = new WebSocket(f),
        Z = setTimeout(
          () => $(Error("\u9023\u7DDA\u700F\u89BD\u5668\u903E\u6642")),
          1e4,
        );
      (z.addEventListener("open", () => {
        (clearTimeout(Z), (this.socket = z), _());
      }),
        z.addEventListener("error", () => {
          (clearTimeout(Z),
            $(
              Error(
                "\u7121\u6CD5\u9023\u7DDA\u700F\u89BD\u5668\u63A7\u5236\u4ECB\u9762",
              ),
            ));
        }),
        z.addEventListener("message", (K) => this.onMessage(String(K.data))),
        z.addEventListener("close", () => this.onClose()));
    });
  }
  send(f, _ = {}, $) {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN)
      return Promise.reject(
        Error(
          "\u700F\u89BD\u5668\u63A7\u5236\u9023\u7DDA\u5C1A\u672A\u5C31\u7DD2",
        ),
      );
    let z = this.nextId++,
      Z = { id: z, method: f, params: _ };
    if ($) Z.sessionId = $;
    return new Promise((K, B) => {
      (this.pending.set(z, { resolve: K, reject: B }),
        this.socket.send(JSON.stringify(Z)));
    });
  }
  onSession(f, _) {
    let $ = this.sessionHandlers.get(f) || new Set();
    return ($.add(_), this.sessionHandlers.set(f, $), () => $.delete(_));
  }
  onAll(f) {
    this.allHandlers.add(f);
    return () => this.allHandlers.delete(f);
  }
  close() {
    this.socket?.close();
  }
  onMessage(f) {
    let _;
    try {
      _ = JSON.parse(f);
    } catch {
      return;
    }
    for (let handler of this.allHandlers) handler(_);
    if (typeof _.id === "number") {
      let $ = this.pending.get(_.id);
      if (!$) return;
      if ((this.pending.delete(_.id), _.error))
        $.reject(Error(_.error.message || "CDP\u547D\u4EE4\u5931\u6557"));
      else $.resolve(_.result);
      return;
    }
    if (_.sessionId)
      for (let $ of this.sessionHandlers.get(_.sessionId) || []) $(_);
  }
  onClose() {
    for (let f of this.pending.values())
      f.reject(
        Error("\u700F\u89BD\u5668\u63A7\u5236\u9023\u7DDA\u5DF2\u95DC\u9589"),
      );
    (this.pending.clear(), (this.socket = null));
  }
}
class b {
  connection;
  sessionId;
  targetId;
  constructor(f, _, $) {
    this.connection = f;
    this.sessionId = _;
    this.targetId = $;
  }
  send(f, _ = {}) {
    return this.connection.send(f, _, this.sessionId);
  }
  sendTo(f, _ = {}, sessionId) {
    return this.connection.send(f, _, sessionId);
  }
  on(f) {
    return this.connection.onSession(this.sessionId, f);
  }
  onAll(f) {
    return this.connection.onAll(f);
  }
}
class DirectPageSession {
  connection;
  targetId;
  constructor(connection, targetId) {
    this.connection = connection;
    this.targetId = targetId;
  }
  send(method, params = {}) {
    return this.connection.send(method, params);
  }
  sendTo(method, params = {}, sessionId) {
    let childSessionId =
      sessionId && !String(sessionId).startsWith("direct:")
        ? sessionId
        : void 0;
    return this.connection.send(method, params, childSessionId);
  }
  onAll(handler) {
    return this.connection.onAll(handler);
  }
  close() {
    this.connection.close();
  }
}
class F {
  debugPort;
  profileDirectory;
  uiUrl;
  logger;
  connection = new P();
  browserProcess = null;
  loginTargetId = null;
  connected = !1;
  browserName = "\u700F\u89BD\u5668";
  constructor(f, _, $, z) {
    this.debugPort = f;
    this.profileDirectory = _;
    this.uiUrl = $;
    this.logger = z;
  }
  async launch() {
    let f = this.findBrowserExecutable();
    if (!f)
      throw Error("\u627E\u4E0D\u5230 Microsoft Edge \u6216 Google Chrome");
    this.browserName = /chrome\.exe$/i.test(f) ? "Chrome" : "Edge";
    this.profileDirectory = M(this.profileDirectory, this.browserName);
    Q(this.profileDirectory);
    let _ = [
      f,
      `--remote-debugging-port=${this.debugPort}`,
      "--remote-debugging-address=127.0.0.1",
      `--user-data-dir=${this.profileDirectory}`,
      "--no-first-run",
      "--no-default-browser-check",
      "--disable-features=msEdgeFirstRunExperience",
      "--autoplay-policy=no-user-gesture-required",
      "--disable-background-timer-throttling",
      "--disable-backgrounding-occluded-windows",
      "--disable-renderer-backgrounding",
      "--mute-audio",
      "--window-position=120,80",
      "--window-size=1100,820",
      "about:blank",
    ];
    ((this.browserProcess = Bun.spawn(_, {
      stdin: "ignore",
      stdout: "ignore",
      stderr: "ignore",
      windowsHide: !0,
    })),
      this.logger.info(
        `\u5DF2\u555F\u52D5\u53D7\u63A7 ${this.browserName}\uFF08\u767B\u5165\u3001\u4F86\u6E90\u64F7\u53D6\u8207\u4E0B\u8F09\u6388\u6B0A\u5171\u7528\u540C\u4E00\u8A2D\u5B9A\u6A94\uFF09\uFF1A${f}`,
      ));
    let $;
    for (let z = 0; z < 80; z += 1)
      try {
        let K = await (
          await fetch(`http://127.0.0.1:${this.debugPort}/json/version`)
        ).json();
        (await this.connection.connect(K.webSocketDebuggerUrl),
          (this.connected = !0));
        return;
      } catch (Z) {
        (($ = Z), await T(250));
      }
    throw Error(
      `\u700F\u89BD\u5668\u555F\u52D5\u903E\u6642\uFF1A${$ instanceof Error ? $.message : "\u672A\u77E5\u932F\u8AA4"}`,
    );
  }
  async createSession(f = "about:blank", _ = !1, skipPageAutoAttach = !1) {
    this.assertConnected();
    let $ = await this.connection.send("Target.createTarget", {
        url: f,
        newWindow: _,
        background: !_,
      }),
      z = await this.connection.send("Target.attachToTarget", {
        targetId: $.targetId,
        flatten: !0,
      });
    let session = new b(this.connection, z.sessionId, $.targetId);
    if (!skipPageAutoAttach)
      await session.send("Target.setAutoAttach", {
        autoAttach: !0,
        waitForDebuggerOnStart: !0,
        flatten: !0,
      });
    if (!_) {
      await this.connection.send("Target.activateTarget", {
        targetId: $.targetId,
      });
    }
    return session;
  }
  async createDirectPage(visible = !0) {
    this.assertConnected();
    let targetId = null;
    try {
      let target = await this.connection.send("Target.createTarget", {
        url: "about:blank",
        background: !visible,
      });
      targetId = target.targetId;
      if (visible)
        await this.connection.send("Target.activateTarget", { targetId });
      if (visible) try {
        let windowInfo = await this.connection.send(
          "Browser.getWindowForTarget",
          { targetId },
        );
        await this.connection.send("Browser.setWindowBounds", {
          windowId: windowInfo.windowId,
          bounds: {
            left: 120,
            top: 80,
            width: 1100,
            height: 820,
            windowState: "normal",
          },
        });
        await this.connection.send("Target.activateTarget", { targetId });
        this.logger.info(
          `Drive \u9664\u932F\u5206\u9801\u5DF2\u986F\u793A\u5728\u756B\u9762\u4E0A\uFF1Atarget=${targetId}`,
        );
      } catch (error) {
        this.logger.error("\u7121\u6CD5\u5C07 Drive \u9664\u932f\u5206\u9801\u79FB\u5230\u756B\u9762\u4E0A", error);
      }
      let webSocketDebuggerUrl = await this.resolvePageWebSocket(targetId);
      if (!webSocketDebuggerUrl)
        throw Error("\u627E\u4E0D\u5230 Drive \u5206\u9801\u7684 DevTools \u9023\u7DDA");
      let pageConnection = this.createPageConnection();
      await pageConnection.connect(webSocketDebuggerUrl);
      this.logger.info(
        `${visible ? "\u5DF2\u76F4\u63A5\u9023\u7DDA Drive \u5206\u9801 DevTools" : "\u5DF2\u5EFA\u7ACB Chrome \u539F\u751F\u4E0B\u8F09\u5206\u9801"}\uFF1Atarget=${targetId}`,
      );
      return new DirectPageSession(pageConnection, targetId);
    } catch (error) {
      if (targetId) await this.closeTarget(targetId);
      throw error;
    }
  }
  createPageConnection() {
    return new P();
  }
  async resolvePageWebSocket(targetId) {
    let webSocketDebuggerUrl = "";
    for (let attempt = 0; attempt < 40; attempt += 1) {
      let targets = await (
        await fetch(`http://127.0.0.1:${this.debugPort}/json/list`)
      ).json();
      let page = (targets || []).find(
        (item) => item.id === targetId || item.targetId === targetId,
      );
      if (page?.webSocketDebuggerUrl) {
        webSocketDebuggerUrl = page.webSocketDebuggerUrl;
        break;
      }
      await T(50);
    }
    return webSocketDebuggerUrl;
  }
  async closeTarget(f) {
    if (!this.connected) return;
    try {
      await this.connection.send("Target.closeTarget", { targetId: f });
    } catch {}
  }
  async activateTarget(f) {
    if (!this.connected || !f) return;
    try {
      await this.connection.send("Target.activateTarget", { targetId: f });
      let windowInfo = await this.connection.send("Browser.getWindowForTarget", {
        targetId: f,
      });
      await this.connection.send("Browser.setWindowBounds", {
        windowId: windowInfo.windowId,
        bounds: { windowState: "normal" },
      });
      await this.connection.send("Target.activateTarget", { targetId: f });
    } catch (error) {
      this.logger.error("\u7121\u6CD5\u986F\u793A Drive \u64AD\u653E\u5206\u9801", error);
    }
  }
  async openLoginWindow() {
    if (this.loginTargetId) await this.closeTarget(this.loginTargetId);
    let _ = `https://accounts.google.com/AccountChooser?continue=${encodeURIComponent("https://drive.google.com/drive/my-drive")}`,
      $ = await this.createSession(_, !0);
    ((this.loginTargetId = $.targetId),
      await this.connection.send("Target.activateTarget", {
        targetId: $.targetId,
      }));
    try {
      let windowInfo = await this.connection.send(
        "Browser.getWindowForTarget",
        {
          targetId: $.targetId,
        },
      );
      await this.connection.send("Browser.setWindowBounds", {
        windowId: windowInfo.windowId,
        bounds: {
          left: 120,
          top: 80,
          width: 1100,
          height: 820,
          windowState: "normal",
        },
      });
    } catch (error) {
      this.logger.error("\u7121\u6CD5\u5C07 Google \u767B\u5165\u8996\u7A97\u79FB\u5230\u756B\u9762\u4E0A", error);
    }
  }
  async loginWindowStatus() {
    if (!this.loginTargetId || !this.connected) return "closed";
    try {
      let _ = (
        (await this.connection.send("Target.getTargets")).targetInfos || []
      ).find(($) => $.targetId === this.loginTargetId);
      if (!_) return ((this.loginTargetId = null), "closed");
      return String(_.url || "").startsWith("https://drive.google.com/")
        ? "complete"
        : "open";
    } catch {
      return "open";
    }
  }
  async closeLoginWindow() {
    if (!this.loginTargetId) return;
    (await this.closeTarget(this.loginTargetId), (this.loginTargetId = null));
  }
  async isGoogleLoggedIn() {
    if (!this.connected) return !1;
    try {
      return (
        (await this.connection.send("Storage.getCookies")).cookies || []
      ).some((_) => {
        let $ = String(_.domain || ""),
          z = String(_.name || "");
        return (
          $.endsWith("google.com") &&
          ["SID", "SAPISID", "__Secure-1PSID"].includes(z)
        );
      });
    } catch {
      return !1;
    }
  }
  async openResourceStream(source, signal, timeoutMs = 60000) {
    this.assertConnected();
    let url = a0(source);
    if (!url)
      throw Error("\u64AD\u653E\u4F86\u6E90\u7DB2\u5740\u7121\u6548");
    let page = await this.createDirectPage(!1),
      handle = "",
      closed = !1,
      unsubscribe = () => {},
      abortHandler = null,
      cleanup = async () => {
        if (closed) return;
        closed = !0;
        unsubscribe();
        if (abortHandler) signal?.removeEventListener("abort", abortHandler);
        if (handle)
          try {
            await page.send("IO.close", { handle });
          } catch {}
        try {
          await page.send("Fetch.disable");
        } catch {}
        await this.closeTarget(page.targetId);
        page.close();
      };
    try {
      await page.send("Network.enable");
      let capturedHeaders = d0(source?.headers || {}),
        extraHeaders = {};
      for (let [name, value] of Object.entries(capturedHeaders))
        if (
          [
            "accept",
            "accept-language",
            "authorization",
            "origin",
            "referer",
            "x-client-data",
          ].includes(name.toLowerCase())
        )
          extraHeaders[name] = value;
      if (!Object.keys(extraHeaders).some((name) => name.toLowerCase() === "referer"))
        extraHeaders.Referer = "https://drive.google.com/";
      await page.send("Network.setExtraHTTPHeaders", {
        headers: extraHeaders,
      });
      await page.send("Fetch.enable", {
        patterns: [
          { urlPattern: "*videoplayback*", requestStage: "Response" },
        ],
      });
      let response = await new Promise((resolve, reject) => {
        let settled = !1,
          responseTimeoutMs = Math.max(5000, Number(timeoutMs || 60000)),
          timer = setTimeout(() => {
            if (settled) return;
            settled = !0;
            reject(
              Error(
                `Chrome \u539F\u751F\u4E0B\u8F09 ${Math.round(responseTimeoutMs / 1000)} \u79D2\u5167\u672A\u6536\u5230\u56DE\u61C9\u6A19\u982D`,
              ),
            );
          }, responseTimeoutMs),
          finish = (callback, value) => {
            if (settled) return;
            settled = !0;
            clearTimeout(timer);
            callback(value);
          };
        abortHandler = () => {
          if (!settled)
            finish(reject, new DOMException("Aborted", "AbortError"));
          else void cleanup();
        };
        signal?.addEventListener("abort", abortHandler, { once: !0 });
        unsubscribe = page.onAll((event) => {
          if (event.method !== "Fetch.requestPaused") return;
          let params = event.params || {},
            status = Number(params.responseStatusCode || 0);
          if (!String(params.request?.url || "").includes("videoplayback"))
            return;
          void (async () => {
            if (status >= 300 && status < 400) {
              await page.send("Fetch.continueRequest", {
                requestId: params.requestId,
              });
              return;
            }
            if (status >= 400) {
              finish(
                reject,
                Error(`HTTP ${status} Chrome \u539F\u751F\u4E0B\u8F09\u88AB\u62D2\u7D55`),
              );
              return;
            }
            let stream = await page.send("Fetch.takeResponseBodyAsStream", {
              requestId: params.requestId,
            });
            handle = stream.stream || "";
            if (!handle)
              throw Error("Chrome \u6C92\u6709\u56DE\u50B3\u53EF\u8B80\u53D6\u7684\u5A92\u9AD4\u4E32\u6D41");
            let headers = {};
            for (let header of params.responseHeaders || [])
              headers[header.name] = header.value;
            finish(resolve, { headers });
          })().catch((error) => finish(reject, error));
        });
        void page.send("Page.navigate", { url }).catch((error) =>
          finish(reject, error),
        );
      });
      this.logger.info(
        "\u5DF2\u7531 Chrome \u539F\u751F\u7DB2\u8DEF\u9023\u7DDA\u53D6\u5F97 videoplayback \u56DE\u61C9\u4E32\u6D41",
      );
      return {
        headers: response.headers || {},
        async read(size = 1048576) {
          if (closed) return { data: new Uint8Array(), eof: !0 };
          let chunk = await page.send("IO.read", { handle, size }),
            data = chunk.base64Encoded
              ? Buffer.from(chunk.data || "", "base64")
              : Buffer.from(chunk.data || "", "utf8");
          return { data, eof: Boolean(chunk.eof) };
        },
        close: cleanup,
      };
    } catch (error) {
      await cleanup();
      throw error;
    }
  }
  async shutdown() {
    if (this.connected)
      try {
        await this.connection.send("Browser.close");
      } catch {}
    (this.connection.close(), (this.connected = !1));
  }
  assertConnected() {
    if (!this.connected)
      throw Error("\u700F\u89BD\u5668\u5C1A\u672A\u5C31\u7DD2");
  }
  findBrowserExecutable() {
    if (process.env.CYDRIVE_BROWSER_PATH && y(process.env.CYDRIVE_BROWSER_PATH))
      return process.env.CYDRIVE_BROWSER_PATH;
    let f = process.env.LOCALAPPDATA || "",
      _ = process.env.ProgramFiles || "C:\\Program Files",
      $ = process.env["ProgramFiles(x86)"] || "C:\\Program Files (x86)";
    return (
      [
        Y(f, "Google", "Chrome", "Application", "chrome.exe"),
        Y(_, "Google", "Chrome", "Application", "chrome.exe"),
        Y($, "Google", "Chrome", "Application", "chrome.exe"),
        Y(f, "Microsoft", "Edge", "Application", "msedge.exe"),
        Y(_, "Microsoft", "Edge", "Application", "msedge.exe"),
        Y($, "Microsoft", "Edge", "Application", "msedge.exe"),
      ].find(y) || null
    );
  }
}
import { renameSync as k, rmSync as h } from "fs";
import { basename as r, dirname as t } from "path";
class p {
  source;
  url;
  headers;
  browser;
  path;
  parentSignal;
  onProgress;
  logger;
  controller = new AbortController();
  done;
  downloaded = 0;
  total = 0;
  speed = 0;
  samples = [];
  finalResult = null;
  firstByte;
  firstByteSeen = !1;
  resolveFirstByte;
  firstByteTimeoutMs = 60000;
  firstByteResult = null;
  constructor(f, _, $, z, Z, K = null, firstByteTimeoutSeconds = 60) {
    this.source = f;
    this.url = a0(f);
    this.headers = c0(f);
    this.browser = K;
    this.path = _;
    this.parentSignal = $;
    this.onProgress = z;
    this.logger = Z;
    this.firstByteTimeoutMs = Math.max(
      5000,
      Number(firstByteTimeoutSeconds || 60) * 1000,
    );
    ((this.total = V(f)),
      (this.firstByte = new Promise((resolve) => {
        this.resolveFirstByte = resolve;
      })),
      $.addEventListener("abort", () => this.controller.abort(), { once: !0 }),
      (this.done = this.run().then(
        () => (this.finalResult = { state: "completed" }),
        (K) => {
          if (w(K) || this.controller.signal.aborted)
            return (this.finalResult = { state: "aborted" });
          let B = K instanceof Error ? K : Error(String(K));
          return (
            this.logger.error(
              `\u4E0B\u8F09\u4F86\u6E90\u5931\u6557 ${r(this.path)}`,
              B,
            ),
            (this.finalResult = { state: "failed", error: B })
          );
        },
      )));
  }
  waitForFirstByte() {
    if (!this.firstByteResult)
      this.firstByteResult = Promise.race([
        this.firstByte.then(() => ({ state: "started" })),
        this.done,
        T(this.firstByteTimeoutMs, this.parentSignal).then(() => ({ state: "stalled" })),
      ]);
    return this.firstByteResult;
  }
  async observe(f) {
    let firstResult = await this.waitForFirstByte();
    if (firstResult.state !== "started") return firstResult.state;
    let _ = f.observationSeconds * 1000,
      $ = await Promise.race([
        this.done,
        T(_, this.parentSignal).then(() => null),
      ]);
    if ($) return $.state;
    return this.averageSpeed(f.observationSeconds * 1000) <
      f.slowThresholdKBs * 1024
      ? "slow"
      : "fast";
  }
  markFirstByte() {
    if (this.firstByteSeen) return;
    this.firstByteSeen = !0;
    this.resolveFirstByte?.();
  }
  abortAndDelete() {
    this.controller.abort();
    try {
      h(this.path, { force: !0 });
    } catch {}
  }
  result() {
    return this.finalResult;
  }
  averageSpeed(f) {
    let _ = Date.now(),
      $ = this.samples.filter((B) => B.time >= _ - f);
    if ($.length < 2) return this.speed;
    let z = $[0],
      Z = $[$.length - 1],
      K = Math.max((Z.time - z.time) / 1000, 0.001);
    return (Z.bytes - z.bytes) / K;
  }
  async run() {
    if (this.browser) {
      this.logger.info?.(
        `\u4E0B\u8F09 ${r(this.path)} \u4F7F\u7528 Chrome \u539F\u751F HTTP/2\uFF0FTLS \u9023\u7DDA\u4E32\u6D41\u50B3\u8F38`,
      );
      return await this.runInBrowser();
    }
    return await this.runWithFetch();
  }
  async runInBrowser() {
    Q(t(this.path));
    this.logger.info?.(
      `\u4E0B\u8F09 ${r(this.path)} \u6539\u7531\u53D7\u63A7 ${this.browser.browserName || "Chrome"} \u539F\u751F\u7DB2\u8DEF\u5DE5\u4F5C\u968E\u6BB5\u4E32\u6D41\u50B3\u8F38`,
    );
    let source = this.source;
    for (let redirectIndex = 0; redirectIndex < 6; redirectIndex += 1) {
      if (this.controller.signal.aborted)
        throw new DOMException("Aborted", "AbortError");
        let resource = await this.browser.openResourceStream(
          source,
          this.controller.signal,
          this.firstByteTimeoutMs,
        ),
        headers = resource.headers || {},
        header = (name) => {
          let key = Object.keys(headers).find(
            (candidate) => candidate.toLowerCase() === name.toLowerCase(),
          );
          return key ? String(headers[key]) : "";
        },
        contentType = header("content-type").toLowerCase(),
        contentLength = Number(header("content-length") || 0),
        expectedType = R(source),
        responseType = contentType.startsWith("audio/")
          ? "audio"
          : contentType.startsWith("video/")
            ? "video"
            : "unknown",
        isText =
          contentType.startsWith("text/") ||
          contentType.includes("json") ||
          (contentLength > 0 &&
            contentLength < 16384 &&
            !contentType.includes("audio") &&
            !contentType.includes("video"));
      this.logger.info?.(
        `\u4F86\u6E90\u9A57\u8B49\uFF1A\u8ACB\u6C42=${expectedType} itag=${new URL(a0(source)).searchParams.get("itag") || "?"} \u756B\u8CEA=${n0(source) || "?"}p \u56DE\u61C9=${contentType || "unknown"}`,
      );
      if (
        responseType !== "unknown" &&
        ((expectedType === "audio" && responseType !== "audio") ||
          (["video", "combined"].includes(expectedType) &&
            responseType !== "video"))
      ) {
        await resource.close();
        throw Error("\u4F86\u6E90\u56DE\u61C9\u985E\u578B\u8207 videoplayback \u5C6C\u6027\u4E0D\u4E00\u81F4");
      }
      if (isText) {
        try {
          let chunks = [],
            size = 0;
          while (size < 4194304) {
            let chunk = await resource.read(262144);
            if (chunk.data.length)
              (chunks.push(chunk.data), (size += chunk.data.length));
            if (chunk.eof) break;
          }
          let nextUrl = this.extractNextUrl(Buffer.concat(chunks).toString());
          if (!nextUrl)
            throw Error(
              "\u4F3A\u670D\u5668\u56DE\u50B3\u6587\u5B57\uFF0C\u4F46\u627E\u4E0D\u5230\u4E0B\u4E00\u500B\u4E0B\u8F09\u7DB2\u5740",
            );
          source = {
            ...source,
            url: nextUrl.includes("videoplayback") ? L(nextUrl) : nextUrl,
          };
          continue;
        } finally {
          await resource.close();
        }
      }
      this.total = contentLength || V(source) || this.total;
      let writer = Bun.file(this.path).writer({ highWaterMark: 1048576 }),
        lastUpdate = 0,
        startedAt = Date.now();
      try {
        while (!0) {
          let chunk = await resource.read(1048576);
          if (this.controller.signal.aborted)
            throw new DOMException("Aborted", "AbortError");
          if (chunk.data.length) {
            this.markFirstByte();
            writer.write(chunk.data);
            this.downloaded += chunk.data.length;
            let now = Date.now();
            this.samples.push({ time: now, bytes: this.downloaded });
            while (
              this.samples.length > 2 &&
              this.samples[0].time < now - 60000
            )
              this.samples.shift();
            this.speed = this.averageSpeed(Math.min(10000, now - startedAt));
            if (now - lastUpdate >= 200)
              ((lastUpdate = now), this.onProgress(this));
          }
          if (chunk.eof) break;
        }
      } finally {
        writer.end();
        await resource.close();
      }
      if (this.downloaded <= 0)
        throw Error("\u4F86\u6E90\u9023\u7DDA\u5DF2\u7D50\u675F\uFF0C\u4F46\u6C92\u6709\u6536\u5230\u4EFB\u4F55\u5A92\u9AD4\u8CC7\u6599");
      if (this.total > 0 && this.downloaded < this.total * 0.98)
        throw Error(
          `\u6A94\u6848\u4E0D\u5B8C\u6574\uFF1A${this.downloaded}/${this.total}`,
        );
      this.onProgress(this);
      return;
    }
    throw Error("\u4E0B\u8F09\u7DB2\u5740\u8F49\u63DB\u6B21\u6578\u904E\u591A");
  }
  async runWithFetch() {
    Q(t(this.path));
    let f = this.url;
    for (let _ = 0; _ < 6; _ += 1) {
      let requestHeaders = d0(this.headers),
        headerNames = new Set(
          Object.keys(requestHeaders).map((header) => header.toLowerCase()),
        );
      if (!headerNames.has("accept")) requestHeaders.Accept = "*/*";
      if (!headerNames.has("referer"))
        requestHeaders.Referer = "https://drive.google.com/";
      let $ = await fetch(f, {
        signal: this.controller.signal,
        redirect: "follow",
        headers: requestHeaders,
      });
      if (!$.ok) throw Error(`HTTP ${$.status} ${$.statusText}`);
      let z = ($.headers.get("content-type") || "").toLowerCase(),
        Z = Number($.headers.get("content-length") || 0);
      if (
        z.startsWith("text/") ||
        z.includes("json") ||
        (Z > 0 && Z < 16384 && !z.includes("audio") && !z.includes("video"))
      ) {
        let N = await $.text(),
          J = this.extractNextUrl(N);
        if (!J)
          throw Error(
            "\u4F3A\u670D\u5668\u56DE\u50B3\u6587\u5B57\uFF0C\u4F46\u627E\u4E0D\u5230\u4E0B\u4E00\u500B\u4E0B\u8F09\u7DB2\u5740",
          );
        f = J.includes("videoplayback") ? L(J) : J;
        continue;
      }
      this.total = Z || this.total;
      let B = Bun.file(this.path).writer({ highWaterMark: 1048576 }),
        G = $.body?.getReader();
      if (!G) throw Error("\u4E0B\u8F09\u56DE\u61C9\u6C92\u6709\u5167\u5BB9");
      let E = 0,
        O = Date.now();
      try {
        while (!0) {
          let { done: N, value: J } = await G.read();
          if (N) break;
          if (this.controller.signal.aborted)
            throw new DOMException("Aborted", "AbortError");
          (this.markFirstByte(), B.write(J), (this.downloaded += J.byteLength));
          let A = Date.now();
          this.samples.push({ time: A, bytes: this.downloaded });
          while (this.samples.length > 2 && this.samples[0].time < A - 60000)
            this.samples.shift();
          if (
            ((this.speed = this.averageSpeed(Math.min(1e4, A - O))),
            A - E >= 200)
          )
            ((E = A), this.onProgress(this));
        }
      } finally {
        B.end();
      }
      if (this.downloaded <= 0)
        throw Error("\u4F86\u6E90\u9023\u7DDA\u5DF2\u7D50\u675F\uFF0C\u4F46\u6C92\u6709\u6536\u5230\u4EFB\u4F55\u5A92\u9AD4\u8CC7\u6599");
      if (this.total > 0 && this.downloaded < this.total * 0.98)
        throw Error(
          `\u6A94\u6848\u4E0D\u5B8C\u6574\uFF1A${this.downloaded}/${this.total}`,
        );
      this.onProgress(this);
      return;
    }
    throw Error("\u4E0B\u8F09\u7DB2\u5740\u8F49\u63DB\u6B21\u6578\u904E\u591A");
  }
  extractNextUrl(f) {
    let $ = f
      .replace(/&amp;/g, "&")
      .replace(/\\u0026/g, "&")
      .match(/https?:\/\/[^\s"'<>]+/);
    if (!$) return null;
    try {
      return new URL($[0]).toString();
    } catch {
      return null;
    }
  }
}
class C {
  settings;
  logger;
  notify;
  browser;
  constructor(f, _, $, z = null) {
    this.settings = f;
    this.logger = _;
    this.notify = $;
    this.browser = z;
  }
  async _downloadDynamicLegacy(f, _, pool, z, onSatisfied) {
    let track = f[_],
      index = 0,
      nextCandidateIndex = 1,
      baseline = null,
      attemptedCount = 0,
      lastFailure = "";
    track.state = "downloading";
    track.note = "\u7B49\u5F85\u7B2C\u4E00\u500B\u64AD\u653E\u4F86\u6E90";
    this.notify();
    while (!0) {
      if (!baseline) {
        let source = await pool.get(index, f.abortController.signal);
        if (!source) {
          let sourceLabel = _ === "audio" ? "\u97F3\u8A0A" : "\u8996\u8A0A";
          if (attemptedCount > 0)
            throw Error(
              `\u5DF2\u9010\u7B46\u5617\u8A66 ${attemptedCount} \u500B${sourceLabel}\u5019\u9078\uFF0C\u4F46\u5168\u90E8\u4E0B\u8F09\u5931\u6557${lastFailure ? `\uFF1A${lastFailure}` : ""}`,
            );
          throw Error(`\u6536\u96C6\u7D50\u675F\u4ECD\u672A\u6536\u5230${sourceLabel}\u5019\u9078\u4F86\u6E90`);
        }
        track.sourceCount = pool.size;
        track.note = `\u5DF2\u627E\u5230\u4F86\u6E90 ${index + 1}\uFF0C\u7ACB\u5373\u4E0B\u8F09\u4E26\u6E2C\u901F\uFF1B\u672A\u5B8C\u6210\u5224\u5B9A\u524D\u4E0D\u6703\u958B\u555F\u5F8C\u7E8C\u4F86\u6E90`;
        this.notify();
        baseline = this.startAttempt(f, _, source, index, z);
        attemptedCount += 1;
        nextCandidateIndex = index + 1;
        track.sourceCount = pool.size;
        let firstResult = await baseline.waitForFirstByte(),
          status =
            firstResult.state === "started"
              ? await baseline.observe(this.settings)
              : firstResult.state;
        if (status === "completed") {
          onSatisfied();
          return this.acceptAttempt(baseline, z, track);
        }
        if (status === "aborted")
          throw new DOMException("Aborted", "AbortError");
        if (status === "failed" || status === "stalled") {
          lastFailure =
            status === "stalled"
              ? `${this.settings.firstByteTimeoutSeconds} \u79D2\u5167\u672A\u6536\u5230\u7B2C\u4E00\u500B\u4F4D\u5143`
              : baseline.result()?.error?.message || "\u4E0B\u8F09\u5931\u6557";
          if (status === "stalled") {
            track.note = `\u4F86\u6E90 ${index + 1} \u9023\u7DDA ${this.settings.firstByteTimeoutSeconds} \u79D2\u4ECD\u70BA 0 B\uFF0C\u53D6\u6D88\u4E26\u5617\u8A66\u4E0B\u4E00\u500B`;
            this.notify();
          }
          baseline.abortAndDelete();
          baseline = null;
          index += 1;
          nextCandidateIndex = index + 1;
          continue;
        }
        if (status === "fast") {
          track.note = `\u4F86\u6E90 ${index + 1}/${track.sourceCount} \u901F\u5EA6\u6B63\u5E38\uFF0C\u505C\u6B62\u6536\u96C6\u9019\u985E\u4F86\u6E90`;
          this.notify();
          onSatisfied();
          let result = await baseline.done;
          if (result.state === "completed")
            return this.acceptAttempt(baseline, z, track);
          if (result.state === "aborted")
            throw new DOMException("Aborted", "AbortError");
          baseline.abortAndDelete();
          baseline = null;
          index = nextCandidateIndex;
          continue;
        }
        track.note = `\u4F86\u6E90 ${index + 1}/${track.sourceCount} \u4F4E\u65BC ${this.settings.slowThresholdKBs} KB/s\uFF0C\u4FDD\u7559\u4F5C\u70BA\u4FDD\u5E95\u4E26\u7B49\u5F85\u65B0\u4F86\u6E90`;
        this.notify();
        nextCandidateIndex = Math.max(nextCandidateIndex, index + 1);
      }
      let nextIndex = nextCandidateIndex,
        next = await Promise.race([
          pool
            .get(nextIndex, f.abortController.signal)
            .then((source) => ({ kind: "source", source })),
          baseline.done.then((result) => ({ kind: "baseline", result })),
        ]);
      if (next.kind === "baseline") {
        if (next.result.state === "completed") {
          onSatisfied();
          return this.acceptAttempt(baseline, z, track);
        }
        if (next.result.state === "aborted")
          throw new DOMException("Aborted", "AbortError");
        baseline.abortAndDelete();
        baseline = null;
        index = nextIndex;
        nextCandidateIndex = index + 1;
        continue;
      }
      if (!next.source) {
        track.note = `\u4F86\u6E90\u6536\u96C6\u5DF2\u7D50\u675F\uFF0C\u4FDD\u7559\u6162\u901F\u4F86\u6E90 ${index + 1}/${track.sourceCount} \u4E0B\u8F09\u81F3\u5B8C\u6210`;
        this.notify();
        let result = await baseline.done;
        if (result.state === "completed")
          return this.acceptAttempt(baseline, z, track);
        if (result.state === "aborted")
          throw new DOMException("Aborted", "AbortError");
        throw result.error || Error("\u4FDD\u5E95\u4F86\u6E90\u4E0B\u8F09\u5931\u6557");
      }
      track.sourceCount = pool.size;
      let probe = this.startAttempt(f, _, next.source, nextIndex, z),
        probeStatus = await probe.observe(this.settings);
      if (baseline.result()?.state === "completed") {
        probe.abortAndDelete();
        onSatisfied();
        return this.acceptAttempt(baseline, z, track);
      }
      if (probeStatus === "completed") {
        baseline.abortAndDelete();
        onSatisfied();
        return this.acceptAttempt(probe, z, track);
      }
      if (probeStatus === "fast") {
        track.note = `\u65B0\u4F86\u6E90 ${nextIndex + 1}/${track.sourceCount} \u901F\u5EA6\u6B63\u5E38\uFF0C\u53D6\u6D88\u4FDD\u5E95\u4E26\u505C\u6B62\u6536\u96C6\u9019\u985E\u4F86\u6E90`;
        this.notify();
        baseline.abortAndDelete();
        onSatisfied();
        let result = await probe.done;
        if (result.state === "completed")
          return this.acceptAttempt(probe, z, track);
        if (result.state === "aborted")
          throw new DOMException("Aborted", "AbortError");
        probe.abortAndDelete();
        baseline = null;
        index = nextIndex + 1;
        nextCandidateIndex = index + 1;
        continue;
      }
      probe.abortAndDelete();
      track.note = `\u65B0\u4F86\u6E90 ${nextIndex + 1}/${track.sourceCount}${probeStatus === "slow" ? "\u4ECD\u70BA\u6162\u901F" : "\u5931\u6557"}\uFF0C\u5DF2\u53D6\u6D88\uFF1B\u4FDD\u5E95\u4F86\u6E90 ${index + 1} \u7E7C\u7E8C`;
      this.notify();
      nextCandidateIndex = nextIndex + 1;
    }
  }
  async downloadDynamic(f, _, pool, z, onSatisfied, policy = {}) {
    let track = f[_], cursor = 0, baseline = null, baselineStatus = "", pending = null, attemptedCount = 0, lastFailure = "";
    track.state = "downloading";
    track.note = "\u7B49\u5F85\u7B2C\u4E00\u500B\u53EF\u4E0B\u8F09\u7684\u64AD\u653E\u4F86\u6E90";
    this.notify();
    while (!0) {
      if (!baseline) {
        let item = pending || { source: await pool.get(cursor, f.abortController.signal), index: cursor };
        pending = null;
        if (!item.source) {
          let label = _ === "audio" ? "\u97F3\u8A0A" : "\u8996\u8A0A";
          throw Error(attemptedCount ? `\u5DF2\u9010\u7B46\u5617\u8A66 ${attemptedCount} \u500B${label}\u4F86\u6E90\uFF0C\u4F46\u90FD\u7121\u6CD5\u5B8C\u6210${lastFailure ? `\uFF1A${lastFailure}` : ""}` : `\u672A\u6536\u5230${label}\u5019\u9078\u4F86\u6E90`);
        }
        cursor = Math.max(cursor, item.index + 1);
        track.sourceCount = pool.size;
        baseline = this.startAttempt(f, _, item.source, item.index, z);
        attemptedCount += 1;
        let first = await baseline.waitForFirstByte();
        baselineStatus = first.state === "started" ? await baseline.observe(this.settings) : first.state;
        if (baselineStatus === "completed") {
          this.setSourceStatus(f, _, baseline.source, "completed", baseline);
          onSatisfied("completed", baseline.source);
          return this.acceptAttempt(baseline, z, track, f, _);
        }
        if (baselineStatus === "aborted") throw new DOMException("Aborted", "AbortError");
        if (["failed", "stalled"].includes(baselineStatus)) {
          let failureError = baseline.result()?.error;
          lastFailure = baselineStatus === "stalled" ? `${this.settings.firstByteTimeoutSeconds} \u79D2\u5167\u672A\u6536\u5230\u7B2C\u4E00\u500B\u4F4D\u5143` : failureError?.message || "\u4E0B\u8F09\u5931\u6557";
          this.setSourceStatus(f, _, baseline.source, "failed", baseline);
          baseline.abortAndDelete();
          baseline = null;
          if (baselineStatus === "failed" && isBrowserControlFailure(failureError)) {
            track.note = "Chrome \u63A7\u5236\u9023\u7DDA\u5DF2\u5931\u6548\uFF0C\u505C\u6B62\u6E2C\u8A66\uFF1B\u5F8C\u7E8C\u4F86\u6E90\u4FDD\u6301\u672A\u4F7F\u7528";
            this.notify();
            throw failureError;
          }
          track.note = "\u7576\u524D\u4F86\u6E90\u7121\u6CD5\u50B3\u8F38\uFF0C\u7E7C\u7E8C\u7B49\u5F85\u4E0B\u4E00\u7B46\u540C\u756B\u8CEA\u4F86\u6E90";
          this.notify();
          continue;
        }
        if (baselineStatus === "slow") {
          this.setSourceStatus(f, _, baseline.source, "slow", baseline);
          track.note = `\u4F86\u6E90 ${item.index + 1} \u4F4E\u65BC ${this.settings.slowThresholdKBs} KB/s\uFF0C\u4FDD\u7559\u4E0B\u8F09\u4E26\u9010\u7B46\u6E2C\u8A66\u65B0\u4F86\u6E90`;
        } else {
          this.setSourceStatus(f, _, baseline.source, "fast", baseline);
          track.note = `\u4F86\u6E90 ${item.index + 1} \u901F\u5EA6\u6B63\u5E38`;
          if (!policy.keepSearchingAfterFast?.(baseline.source)) {
            onSatisfied("fast", baseline.source);
            let result = await baseline.done;
            if (result.state === "completed") return this.acceptAttempt(baseline, z, track, f, _);
            if (result.state === "aborted") throw new DOMException("Aborted", "AbortError");
            this.setSourceStatus(f, _, baseline.source, "failed", baseline);
            baseline.abortAndDelete();
            baseline = null;
            continue;
          }
          track.note += "\uFF0C\u4F46\u4ECD\u7E7C\u7E8C\u7B49\u5F85\u66F4\u9AD8\u756B\u8CEA";
        }
        this.notify();
      }
      let next = await Promise.race([
        pool.get(cursor, f.abortController.signal).then((source) => ({ kind: "source", source, index: cursor })),
        baseline.done.then((result) => ({ kind: "baseline", result })),
      ]);
      if (next.kind === "baseline") {
        if (next.result.state === "completed") {
          this.setSourceStatus(f, _, baseline.source, "completed", baseline);
          onSatisfied("completed", baseline.source);
          return this.acceptAttempt(baseline, z, track, f, _);
        }
        if (next.result.state === "aborted") throw new DOMException("Aborted", "AbortError");
        if (isBrowserControlFailure(next.result.error)) {
          this.setSourceStatus(f, _, baseline.source, "failed", baseline);
          baseline.abortAndDelete();
          track.note = "Chrome \u63A7\u5236\u9023\u7DDA\u5DF2\u5931\u6548\uFF0C\u505C\u6B62\u6E2C\u8A66\uFF1B\u5F8C\u7E8C\u4F86\u6E90\u4FDD\u6301\u672A\u4F7F\u7528";
          this.notify();
          throw next.result.error;
        }
        this.setSourceStatus(f, _, baseline.source, "failed", baseline);
        baseline.abortAndDelete(); baseline = null; continue;
      }
      if (!next.source) {
        let result = await baseline.done;
        if (result.state === "completed") return this.acceptAttempt(baseline, z, track, f, _);
        if (result.state === "aborted") throw new DOMException("Aborted", "AbortError");
        throw result.error || Error("\u4FDD\u7559\u4F86\u6E90\u4E0B\u8F09\u5931\u6557");
      }
      cursor = next.index + 1;
      track.sourceCount = pool.size;
      if (policy.shouldReplaceBaseline?.(next.source, baseline.source)) {
        this.setSourceStatus(f, _, baseline.source, "cancelled", baseline);
        baseline.abortAndDelete();
        track.note = `\u767C\u73FE ${n0(next.source)}p\uFF0C\u5DF2\u53D6\u6D88\u8F03\u4F4E\u756B\u8CEA\u4E26\u7ACB\u5373\u5207\u63DB`;
        pending = { source: next.source, index: next.index };
        baseline = null;
        this.notify();
        continue;
      }
      if (baselineStatus === "fast") continue;
      let probe = this.startAttempt(f, _, next.source, next.index, z), probeStatus = await probe.observe(this.settings);
      if (probeStatus === "failed" && isBrowserControlFailure(probe.result()?.error)) {
        this.setSourceStatus(f, _, probe.source, "failed", probe);
        probe.abortAndDelete();
        track.note = "Chrome \u63A7\u5236\u9023\u7DDA\u5DF2\u5931\u6548\uFF0C\u505C\u6B62\u6E2C\u8A66\uFF1B\u5F8C\u7E8C\u4F86\u6E90\u4FDD\u6301\u672A\u4F7F\u7528";
        this.notify();
        throw probe.result().error;
      }
      if (baseline.result()?.state === "completed") {
        this.setSourceStatus(f, _, probe.source, "cancelled", probe); probe.abortAndDelete();
        return this.acceptAttempt(baseline, z, track, f, _);
      }
      if (probeStatus === "completed") {
        this.setSourceStatus(f, _, baseline.source, "cancelled", baseline); baseline.abortAndDelete();
        this.setSourceStatus(f, _, probe.source, "completed", probe); onSatisfied("completed", probe.source);
        return this.acceptAttempt(probe, z, track, f, _);
      }
      if (probeStatus === "fast") {
        this.setSourceStatus(f, _, baseline.source, "cancelled", baseline); baseline.abortAndDelete();
        this.setSourceStatus(f, _, probe.source, "fast", probe); baseline = probe; baselineStatus = "fast";
        if (!policy.keepSearchingAfterFast?.(probe.source)) {
          onSatisfied("fast", probe.source);
          let result = await probe.done;
          if (result.state === "completed") return this.acceptAttempt(probe, z, track, f, _);
          if (result.state === "aborted") throw new DOMException("Aborted", "AbortError");
          this.setSourceStatus(f, _, probe.source, "failed", probe); probe.abortAndDelete(); baseline = null;
        }
        continue;
      }
      this.setSourceStatus(f, _, probe.source, ["failed", "stalled"].includes(probeStatus) ? "failed" : "cancelled", probe);
      probe.abortAndDelete();
      track.note = `\u65B0\u4F86\u6E90 ${next.index + 1}${probeStatus === "slow" ? "\u4ECD\u70BA\u6162\u901F" : "\u7121\u6CD5\u50B3\u8F38"}\uFF0C\u5DF2\u53D6\u6D88\uFF1B\u4FDD\u5E95\u4F86\u6E90\u7E7C\u7E8C`;
      this.notify();
    }
  }
  async download(f, _, $, z) {
    if (!$.length)
      throw Error(
        `\u627E\u4E0D\u5230${_ === "audio" ? "\u97F3\u8A0A" : "\u8996\u8A0A"}\u4F86\u6E90`,
      );
    let Z = f[_];
    ((Z.state = "downloading"),
      (Z.sourceCount = $.length),
      (Z.note = "\u958B\u59CB\u4E0B\u8F09"),
      this.notify());
    let K = 0,
      B = this.startAttempt(f, _, $[K], K, z);
    while (!0) {
      let G = await B.observe(this.settings);
      if (G === "completed") return this.acceptAttempt(B, z, Z);
      if (G === "aborted") throw new DOMException("Aborted", "AbortError");
      if (G === "failed" || G === "stalled") {
        if ((B.abortAndDelete(), (K += 1), K >= $.length))
          throw (
            B.result()?.error ||
            Error("\u6240\u6709\u4E0B\u8F09\u4F86\u6E90\u5747\u5931\u6557")
          );
        B = this.startAttempt(f, _, $[K], K, z);
        continue;
      }
      if (G === "fast") {
        ((Z.note = `\u4F86\u6E90${K + 1}\u901F\u5EA6\u6B63\u5E38\uFF0C\u7E7C\u7E8C\u4E0B\u8F09`),
          this.notify());
        let E = await B.done;
        if (E.state === "completed") return this.acceptAttempt(B, z, Z);
        if (E.state === "aborted")
          throw new DOMException("Aborted", "AbortError");
        if ((B.abortAndDelete(), (K += 1), K >= $.length))
          throw E.error || Error("\u4E0B\u8F09\u5931\u6557");
        B = this.startAttempt(f, _, $[K], K, z);
        continue;
      }
      ((Z.note = `\u4F86\u6E90${K + 1}\u4F4E\u65BC ${this.settings.slowThresholdKBs} KB/s\uFF0C\u4FDD\u7559\u4F5C\u70BA\u4FDD\u5E95`),
        this.notify());
      for (let E = K + 1; E < $.length; E += 1) {
        let O = this.startAttempt(f, _, $[E], E, z),
          N = await Promise.race([
            O.observe(this.settings).then((J) => ({ who: "probe", value: J })),
            B.done.then((J) => ({ who: "baseline", value: J.state })),
          ]);
        if (N.who === "baseline") {
          if ((O.abortAndDelete(), N.value === "completed"))
            return this.acceptAttempt(B, z, Z);
          if (N.value === "aborted")
            throw new DOMException("Aborted", "AbortError");
          (B.abortAndDelete(), (B = O), (K = E));
          break;
        }
        if (N.value === "completed")
          return (B.abortAndDelete(), this.acceptAttempt(O, z, Z));
        if (N.value === "fast") {
          ((Z.note = `\u4F86\u6E90${E + 1}\u901F\u5EA6\u6B63\u5E38\uFF0C\u53D6\u6D88\u4FDD\u5E95\u4F86\u6E90${K + 1}`),
            this.notify(),
            B.abortAndDelete());
          let J = await O.done;
          if (J.state === "completed") return this.acceptAttempt(O, z, Z);
          if (J.state === "aborted")
            throw new DOMException("Aborted", "AbortError");
          if ((O.abortAndDelete(), (K = E + 1), K >= $.length))
            throw (
              J.error ||
              Error("\u5FEB\u901F\u4F86\u6E90\u4E0B\u8F09\u5931\u6557")
            );
          B = this.startAttempt(f, _, $[K], K, z);
          break;
        }
        (O.abortAndDelete(),
          (Z.note = `\u4F86\u6E90${E + 1}${N.value === "slow" ? "\u4ECD\u70BA\u6162\u901F" : "\u5931\u6557"}\uFF0C\u5DF2\u53D6\u6D88\uFF1B\u4FDD\u5E95\u4F86\u6E90${K + 1}\u7E7C\u7E8C`),
          this.notify());
      }
      if (B.result()?.state === "completed") return this.acceptAttempt(B, z, Z);
      if (K >= $.length - 1 || Z.note.includes("\u4FDD\u5E95\u4F86\u6E90")) {
        ((Z.note = `\u5F8C\u7E8C\u4F86\u6E90\u7686\u672A\u6539\u5584\uFF0C\u4FDD\u7559\u4F86\u6E90${K + 1}\u4E0B\u8F09\u81F3\u5B8C\u6210`),
          this.notify());
        let E = await B.done;
        if (E.state === "completed") return this.acceptAttempt(B, z, Z);
        if (E.state === "aborted")
          throw new DOMException("Aborted", "AbortError");
        throw (
          E.error || Error("\u4FDD\u5E95\u4F86\u6E90\u4E0B\u8F09\u5931\u6557")
        );
      }
    }
  }
  startAttempt(f, _, $, z, Z) {
    let K = `${Z}.source-${z + 1}.part`,
      B = f[_];
    return (
      (B.sourceIndex = z + 1),
      (B.note = `\u6B63\u5728\u5617\u8A66\u4F86\u6E90 ${z + 1}/${B.sourceCount}`),
      this.setSourceStatus(f, _, $, "downloading"),
      this.notify(),
      new p(
        $,
        K,
        f.abortController.signal,
        (G) => {
          ((B.downloaded = G.downloaded),
            (B.total = G.total),
            (B.speed = G.speed),
            (B.percent =
              G.total > 0 ? Math.min(100, (G.downloaded / G.total) * 100) : 0),
            this.updateSourceProgress(f, _, $, G),
            (f.overallPercent = Math.round(
              (f.audio.percent + f.video.percent) / 2,
            )),
            this.notify());
        },
        this.logger,
        this.browser,
        this.settings.firstByteTimeoutSeconds,
      )
    );
  }
  sourceEntry(f, _, source) {
    let list = _ === "audio" ? f.audioSources : f.videoSources,
      sourceKey = s0(source);
    return (list || []).find((entry) => entry.candidateId === sourceKey);
  }
  setSourceStatus(f, _, source, status, attempt = null) {
    let entry = this.sourceEntry(f, _, source);
    if (!entry) return;
    entry.status = status;
    if (attempt) {
      entry.speed = Number(attempt.speed || 0);
      entry.downloaded = Number(attempt.downloaded || 0);
    }
    this.notify();
  }
  updateSourceProgress(f, _, source, attempt) {
    let entry = this.sourceEntry(f, _, source);
    if (!entry) return;
    entry.speed = Number(attempt.speed || 0);
    entry.downloaded = Number(attempt.downloaded || 0);
    this.notify();
  }
  acceptAttempt(f, _, $, task = null, type = "") {
    try {
      h(_, { force: !0 });
    } catch {}
    if (task && type) this.setSourceStatus(task, type, f.source, "completed", f);
    return (
      k(f.path, _),
      ($.state = "completed"),
      ($.percent = 100),
      ($.speed = 0),
      ($.note = `\u4F86\u6E90 ${$.sourceIndex}/${$.sourceCount} \u4E0B\u8F09\u5B8C\u6210`),
      this.notify(),
      _
    );
  }
}
import {
  appendFileSync as s,
  existsSync as a,
  renameSync as e,
  rmSync as f0,
  statSync as $0,
} from "fs";
import { dirname as _0 } from "path";
class U {
  path;
  constructor(f) {
    this.path = f;
    Q(_0(f));
  }
  info(f) {
    this.write("INFO", f);
  }
  error(f, _) {
    let $ =
      _ instanceof Error
        ? `${_.message}
${_.stack || ""}`
        : String(_ || "");
    this.write("ERROR", $ ? `${f}: ${$}` : f);
  }
  bug(f) {
    this.write("BUG", f);
    try {
      s(
        M(_0(this.path), "BUG.log"),
        `${new Date().toISOString()} [BUG] ${f}\n`,
        "utf8",
      );
    } catch {}
  }
  write(f, _) {
    try {
      if (a(this.path) && $0(this.path).size > 2097152)
        (f0(`${this.path}.old`, { force: !0 }),
          e(this.path, `${this.path}.old`));
      s(
        this.path,
        `${new Date().toISOString()} [${f}] ${_}
`,
        "utf8",
      );
    } catch {}
  }
}
function S() {
  return {
    state: "waiting",
    percent: 0,
    downloaded: 0,
    total: 0,
    speed: 0,
    sourceIndex: 0,
    sourceCount: 0,
    note: "\u7B49\u5F85\u4E2D",
  };
}
var D = "1.4.2-debug",
  H = { high: 0, normal: 1, low: 2 };
async function E0() {
  return new Promise((f, _) => {
    let $ = z0();
    ($.once("error", _),
      $.listen(0, "127.0.0.1", () => {
        let z = $.address(),
          Z = typeof z === "object" && z ? z.port : 0;
        $.close((K) => (K ? _(K) : f(Z)));
      }));
  });
}
class n {
  appRoot = process.cwd();
  localData = process.env.LOCALAPPDATA || M(u(), ".cy-drive-downloader");
  dataRoot = M(this.localData, "CYDriveDownloader");
  tempRoot = M(this.dataRoot, "temp");
  settingsPath = M(this.dataRoot, "settings.json");
  logger = new U(M(i(this.appRoot), "log", "app.log"));
  tasks = new Map();
  running = new Set();
  qualityDecisionWaiters = new Map();
  settings;
  browser = null;
  server = null;
  uiWorker = null;
  uiProcess = null;
  nativeUiFallbackStarted = !1;
  loggedIn = !1;
  loginPending = !1;
  shuttingDown = !1;
  stateRevision = 1;
  authPollTimer = null;
  constructor() {
    (Q(this.dataRoot),
      X(this.tempRoot, { recursive: !0, force: !0 }),
      Q(this.tempRoot),
      (this.settings = this.loadSettings()));
  }
  async start() {
    if (
      ((this.server = Bun.serve({
        hostname: "127.0.0.1",
        port: Number(process.env.CYDRIVE_TEST_PORT || 0),
        fetch: (z) => this.handleRequest(z),
        error: (z) => {
          return (
            this.logger.error("HTTP\u4F3A\u670D\u5668\u932F\u8AA4", z),
            this.json({ error: "\u7A0B\u5F0F\u5167\u90E8\u932F\u8AA4" }, 500)
          );
        },
      })),
      process.env.CYDRIVE_TEST_MODE === "1")
    ) {
      console.log(`CYDRIVE_TEST_PORT=${this.server.port}`);
      return;
    }
    let _ = await E0(),
      $ = `http://127.0.0.1:${this.server.port}/`;
    (this.openNativeWindow($),
      (this.browser = new F(
        _,
        M(this.dataRoot, "BrowserProfiles"),
        $,
        this.logger,
      )),
      await this.browser.launch(),
      (this.loggedIn = await this.browser.isGoogleLoggedIn()),
      this.touch(),
      (this.authPollTimer = setInterval(
        () => void this.pollAuthentication(),
        2000,
      )),
      this.logger.info(`\u7A0B\u5F0F\u555F\u52D5 v${D}`));
  }
  openNativeWindow(url) {
    let workerPath = M(this.appRoot, "ui-worker.js"),
      dllPath = M(this.appRoot, "libwebview.dll");
    if (!c(workerPath) || !c(dllPath))
      throw Error("\u627E\u4E0D\u5230\u5167\u5EFA\u7684 Windows \u8996\u7A97\u5143\u4EF6");
    let worker = new Worker(workerPath);
    this.uiWorker = worker;
    worker.onmessage = (event) => {
      let data = event.data || {};
      if (data.type === "ready") {
        this.logger.info("\u539F\u751F Windows \u4E3B\u8996\u7A97\u5DF2\u958B\u555F");
      } else if (data.type === "closed") {
        this.logger.info("\u539F\u751F Windows \u4E3B\u8996\u7A97\u5DF2\u95DC\u9589");
        void this.shutdown();
      } else if (data.type === "error") {
        this.logger.error("\u539F\u751F Windows \u4E3B\u8996\u7A97\u555F\u52D5\u5931\u6557", data.message);
        this.openLegacyNativeWindow(url);
      }
    };
    worker.onerror = (error) => {
      this.logger.error("\u539F\u751F Windows \u4E3B\u8996\u7A97\u57F7\u884C\u932F\u8AA4", error);
      this.openLegacyNativeWindow(url);
    };
    worker.postMessage({
      url,
      dllPath,
      launcherPath: M(i(this.appRoot), "GoogleDrive\u5F71\u7247\u4E0B\u8F09\u5668.exe"),
    });
  }
  openLegacyNativeWindow(url) {
    if (this.nativeUiFallbackStarted || this.shuttingDown) return;
    this.nativeUiFallbackStarted = !0;
    this.uiWorker?.terminate();
    let legacyRoot = M(this.appRoot, "legacy-webview"),
      executable = M(legacyRoot, "webview.exe");
    if (!c(executable)) {
      this.logger.error("\u627E\u4E0D\u5230\u5099\u7528 Windows \u8996\u7A97\u5143\u4EF6");
      void this.shutdown(1);
      return;
    }
    this.logger.info("\u6B63\u5728\u6539\u7528\u5099\u7528 Windows WebView2 \u4E3B\u8996\u7A97");
    let child = Bun.spawn(
      [
        executable,
        "--url",
        url,
        "--title",
        "Google Drive \u5F71\u7247\u4E0B\u8F09\u5668",
        "--width",
        "960",
        "--height",
        "820",
      ],
      {
        cwd: legacyRoot,
        stdin: "ignore",
        stdout: "ignore",
        stderr: "pipe",
        windowsHide: !0,
      },
    );
    this.uiProcess = child;
    void (async () => {
      let errorText = await new Response(child.stderr).text();
      let exitCode = await child.exited;
      if (errorText.trim())
        this.logger.error("\u5099\u7528 Windows \u4E3B\u8996\u7A97\u8F38\u51FA", errorText.trim());
      this.logger.info(`\u5099\u7528 Windows \u4E3B\u8996\u7A97\u5DF2\u95DC\u9589\uFF0C\u7D50\u675F\u4EE3\u78BC ${exitCode}`);
      if (!this.shuttingDown) await this.shutdown(exitCode === 0 ? 0 : 1);
    })();
  }
  loadSettings() {
    let _ = {
      outputDirectory: M(
        process.env.USERPROFILE || u(),
        "Downloads",
        "GoogleDriveVideos",
      ),
      concurrency: 2,
      slowThresholdKBs: 50,
      firstByteTimeoutSeconds: 60,
      observationSeconds: 60,
      qualityConfirmSeconds: 60,
      settingsVersion: 7,
    };
    try {
      if (!c(this.settingsPath)) return _;
      let $ = JSON.parse(K0(this.settingsPath, "utf8"));
      delete $.playerWarmupSeconds;
      delete $.warmupSeconds;
      delete $.captureSeconds;
      if (Number($.settingsVersion || 0) < 7) {
        if ([20, 30].includes(Number($.observationSeconds)))
          $.observationSeconds = 60;
        if (!$.firstByteTimeoutSeconds || Number($.firstByteTimeoutSeconds) === 30)
          $.firstByteTimeoutSeconds = 60;
      }
      $.settingsVersion = 7;
      return this.validateSettings({ ..._, ...$ });
    } catch ($) {
      return (
        this.logger.error(
          "\u8B80\u53D6\u8A2D\u5B9A\u5931\u6557\uFF0C\u6539\u7528\u9810\u8A2D\u503C",
          $,
        ),
        _
      );
    }
  }
  validateSettings(f) {
    let _ = String(f.outputDirectory || "").trim();
    if (!_)
      throw Error("\u8ACB\u6307\u5B9A\u9810\u8A2D\u5B58\u6A94\u4F4D\u7F6E");
    return {
      outputDirectory: _,
      concurrency: this.clampNumber(f.concurrency, 1, 8, 2),
      slowThresholdKBs: this.clampNumber(f.slowThresholdKBs, 1, 1e4, 50),
      firstByteTimeoutSeconds: this.clampNumber(f.firstByteTimeoutSeconds, 5, 120, 60),
      observationSeconds: this.clampNumber(f.observationSeconds, 3, 300, 60),
      qualityConfirmSeconds: this.clampNumber(f.qualityConfirmSeconds, 10, 300, 60),
      settingsVersion: 7,
    };
  }
  clampNumber(f, _, $, z) {
    let Z = Number(f);
    if (!Number.isFinite(Z)) return z;
    return Math.min($, Math.max(_, Math.round(Z)));
  }
  saveSettings() {
    (Q(i(this.settingsPath)),
      B0(this.settingsPath, JSON.stringify(this.settings, null, 2), "utf8"));
  }
  async handleRequest(f) {
    let _ = new URL(f.url);
    try {
      if (f.method === "GET" && _.pathname === "/")
        return new Response(Bun.file(M(this.appRoot, "ui.html")), {
          headers: {
            "content-type": "text/html; charset=utf-8",
            "cache-control": "no-store",
          },
        });
      if (f.method === "GET" && _.pathname === "/ui.js")
        return new Response(Bun.file(M(this.appRoot, "ui.js")), {
          headers: {
            "content-type": "text/javascript; charset=utf-8",
            "cache-control": "no-store",
          },
        });
      if (f.method === "GET" && _.pathname === "/style.css")
        return new Response(Bun.file(M(this.appRoot, "style.css")), {
          headers: {
            "content-type": "text/css; charset=utf-8",
            "cache-control": "no-store",
          },
        });
      if (f.method === "GET" && _.pathname === "/api/state")
        return this.json(this.publicState());
      if (f.method === "GET" && _.pathname === "/api/health")
        return this.json({ ok: !0, version: D });
      if (f.method === "POST" && _.pathname === "/api/tasks") {
        let z = await f.json();
        return this.json(
          this.addTask(String(z.url || ""), r0(z.quality)),
          201,
        );
      }
      if (f.method === "POST" && _.pathname === "/api/cancel-all")
        return (this.cancelAll(), this.json({ ok: !0 }));
      if (f.method === "POST" && _.pathname === "/api/login")
        return (await this.beginLogin(), this.json({ ok: !0 }));
      if (f.method === "POST" && _.pathname === "/api/browse") {
        let z = await this.browseForFolder();
        return this.json({ path: z });
      }
      if (f.method === "POST" && _.pathname === "/api/ui-closed")
        return (
          setTimeout(() => void this.shutdown(), 250),
          this.json({ ok: !0 })
        );
      if (f.method === "PUT" && _.pathname === "/api/settings") {
        let z = await f.json();
        return (
          (this.settings = this.validateSettings(z)),
          Q(this.settings.outputDirectory),
          this.saveSettings(),
          this.touch(),
          this.pump(),
          this.json({ ok: !0, settings: this.settings })
        );
      }
      let $ = _.pathname.match(/^\/api\/tasks\/([^/]+)$/);
      if ($ && f.method === "DELETE")
        return (
          this.cancelTask(decodeURIComponent($[1])),
          this.json({ ok: !0 })
        );
      if ($ && f.method === "PATCH") {
        let z = await f.json();
        return (
          this.setPriority(
            decodeURIComponent($[1]),
            String(z.priority || "normal"),
          ),
          this.json({ ok: !0 })
        );
      }
      let qualityDecisionMatch = _.pathname.match(
        /^\/api\/tasks\/([^/]+)\/quality-decision$/,
      );
      if (qualityDecisionMatch && f.method === "POST") {
        let z = await f.json();
        await this.resolveQualityDecision(
          decodeURIComponent(qualityDecisionMatch[1]),
          String(z.action || ""),
        );
        return this.json({ ok: !0 });
      }
      return this.json(
        { error: "\u627E\u4E0D\u5230\u6307\u5B9A\u529F\u80FD" },
        404,
      );
    } catch ($) {
      let z = $ instanceof Error ? $.message : String($);
      return (
        this.logger.error(`${f.method} ${_.pathname}`, $),
        this.json({ error: z }, 400)
      );
    }
  }
  addTask(f, quality = "highest") {
    let _ = q(f);
    if (!_)
      throw Error(
        "\u8ACB\u8F38\u5165\u6709\u6548\u7684 Google Drive \u5F71\u7247\u7DB2\u5740",
      );
    if (
      [...this.tasks.values()].find(
        (K) => K.fileId === _ && !["completed", "failed"].includes(K.state),
      )
    )
      throw Error(
        "\u9019\u90E8\u5F71\u7247\u5DF2\u5728\u4EFB\u52D9\u6E05\u55AE\u4E2D",
      );
    let z = crypto.randomUUID(),
      Z = {
        id: z,
        url: I(_),
        fileId: _,
        title: "\u6B63\u5728\u53D6\u5F97\u5F71\u7247\u6A19\u984C",
        quality: r0(quality),
        resolvedQuality: "",
        qualityNote: "",
        videoSources: [],
        audioSources: [],
        priority: "normal",
        state: "queued",
        createdAt: Date.now(),
        message:
          "\u5DF2\u52A0\u5165\u4EFB\u52D9\uFF0C\u6E96\u5099\u81EA\u52D5\u958B\u59CB",
        overallPercent: 0,
        outputPath: "",
        audio: S(),
        video: S(),
        authRefreshAttempts: 0,
        cancelRequested: !1,
        abortController: new AbortController(),
      };
    return (
      this.tasks.set(z, Z),
      this.touch(),
      this.pump(),
      { id: z, state: Z.state }
    );
  }
  setPriority(f, _) {
    if (!(_ in H)) throw Error("\u7121\u6548\u7684\u512A\u5148\u5EA6");
    let $ = this.tasks.get(f);
    if (!$) throw Error("\u627E\u4E0D\u5230\u4EFB\u52D9");
    (($.priority = _), this.touch(), this.pump());
  }
  async requestQualityDecision(f, mismatch, targetId) {
    if (this.qualityDecisionWaiters.has(f.id))
      throw Error("\u756B\u8CEA\u78BA\u8A8D\u6B63\u5728\u7B49\u5F85\u4F7F\u7528\u8005\u9078\u64C7");
    f.state = "quality_wait";
    f.video.state = "waiting";
    f.video.note = "\u7B49\u5F85\u78BA\u8A8D\u662F\u5426\u4F7F\u7528\u8F03\u4F4E\u756B\u8CEA";
    f.message = "\u7A0B\u5F0F\u672A\u53D6\u5F97\u6307\u5B9A\u756B\u8CEA\uFF1B\u97F3\u8A0A\u4E0B\u8F09\u8207\u756B\u8CEA\u78BA\u8A8D\u5206\u958B\u8655\u7406";
    f.qualityPrompt = {
      ...mismatch,
      targetId: targetId || "",
    };
    this.touch();
    return await new Promise((resolve, reject) => {
      let abortHandler = () => {
        this.qualityDecisionWaiters.delete(f.id);
        reject(new DOMException("Aborted", "AbortError"));
      };
      f.abortController.signal.addEventListener("abort", abortHandler, {
        once: !0,
      });
      this.qualityDecisionWaiters.set(f.id, {
        resolve: (action) => {
          f.abortController.signal.removeEventListener("abort", abortHandler);
          resolve(action);
        },
      });
    });
  }
  async resolveQualityDecision(f, action) {
    let task = this.tasks.get(f);
    if (!task || !task.qualityPrompt)
      throw Error("\u9019\u7B46\u4EFB\u52D9\u76EE\u524D\u6C92\u6709\u7B49\u5F85\u756B\u8CEA\u78BA\u8A8D");
    if (action === "open-player") {
      await this.browser?.activateTarget(task.qualityPrompt.targetId);
      return;
    }
    let fallbackMatch = action.match(/^fallback:(\d+)$/),
      fallbackHeight = Number(fallbackMatch?.[1] || 0);
    if (!["bug", "cancel"].includes(action) && !fallbackMatch)
      throw Error("\u7121\u6548\u7684\u756B\u8CEA\u78BA\u8A8D\u9078\u64C7");
    if (fallbackMatch && !(task.qualityPrompt.detectedHeights || []).map(Number).includes(fallbackHeight))
      throw Error("\u9078\u64C7\u7684\u756B\u8CEA\u4E0D\u5728\u5DF2\u5075\u6E2C\u4F86\u6E90\u4E2D");
    let waiter = this.qualityDecisionWaiters.get(f);
    if (!waiter)
      throw Error("\u756B\u8CEA\u78BA\u8A8D\u5DF2\u7D50\u675F");
    this.qualityDecisionWaiters.delete(f);
    delete task.qualityPrompt;
    this.logger.info(
      `\u4F7F\u7528\u8005\u756B\u8CEA\u78BA\u8A8D\u9078\u64C7\uFF1A${action}\uFF0C\u4EFB\u52D9=${task.fileId}`,
    );
    waiter.resolve(action);
    this.touch();
  }
  cancelTask(f) {
    let _ = this.tasks.get(f);
    if (!_) return;
    if (this.running.has(f)) {
      _.cancelRequested = !0;
      _.state = "cancelling";
      _.message = "\u6B63\u5728\u5B89\u5168\u505C\u6B62\u4F86\u6E90\u64F7\u53D6\u8207\u4E0B\u8F09";
      _.audio.speed = 0;
      _.video.speed = 0;
      _.abortController.abort();
      this.logger.info(`\u5DF2\u8981\u6C42\u53D6\u6D88\u57F7\u884C\u4E2D\u7684\u4EFB\u52D9 ${_.fileId}`);
      this.touch();
      return;
    }
    (_.abortController.abort(),
      this.tasks.delete(f),
      this.cleanupTaskTemp(f),
      this.logger.info(`\u5DF2\u53D6\u6D88\u7B49\u5F85\u4E2D\u7684\u4EFB\u52D9 ${_.fileId}`),
      this.touch(),
      this.pump());
  }
  cancelAll() {
    for (let f of [...this.tasks.values()]) {
      if (f.state === "completed") continue;
      if (this.running.has(f.id)) {
        f.cancelRequested = !0;
        f.state = "cancelling";
        f.message = "\u6B63\u5728\u5B89\u5168\u505C\u6B62\u4F86\u6E90\u64F7\u53D6\u8207\u4E0B\u8F09";
        f.audio.speed = 0;
        f.video.speed = 0;
        f.abortController.abort();
        continue;
      }
      (f.abortController.abort(),
        this.tasks.delete(f.id),
        this.cleanupTaskTemp(f.id));
    }
    (this.logger.info("\u5DF2\u8981\u6C42\u53D6\u6D88\u5168\u90E8\u672A\u5B8C\u6210\u4EFB\u52D9"), this.touch(), this.pump());
  }
  pump() {
    if (this.shuttingDown) return;
    let f = this.settings.concurrency - this.running.size;
    if (f <= 0) return;
    let _ = [...this.tasks.values()]
      .filter(($) => $.state === "queued" && !this.running.has($.id))
      .sort(
        ($, z) => H[$.priority] - H[z.priority] || $.createdAt - z.createdAt,
      );
    for (let $ of _.slice(0, f)) {
      this.running.add($.id);
      void this.runTask($);
    }
  }
  async runTask(f) {
    try {
      await this.processTask(f);
    } catch ($) {
      if (!w($) && !f.abortController.signal.aborted) {
        f.state = "failed";
        f.message = $ instanceof Error ? $.message : String($);
        this.logger.error(`\u4EFB\u52D9\u767C\u751F\u672A\u9810\u671F\u932F\u8AA4 ${f.fileId}`, $);
      }
    } finally {
      this.running.delete(f.id);
      if (f.cancelRequested) {
        this.tasks.delete(f.id);
        this.cleanupTaskTemp(f.id);
        this.logger.info(`\u4EFB\u52D9\u53D6\u6D88\u6536\u5C3E\u5B8C\u6210 ${f.fileId}`);
      }
      this.touch();
      this.pump();
    }
  }
  async processTask(f) {
    let _ = M(this.tempRoot, f.id),
      captureTargets = [],
      captureSessions = [];
    Q(_);
    try {
      f.state = "downloading";
      f.message =
        "\u6B63\u5728\u975C\u97F3\u64AD\u653E\uFF0C\u4E26\u884C\u6536\u96C6\u8207\u6E2C\u8A66\u97F3\u8A0A\u3001\u8996\u8A0A\u4F86\u6E90";
      f.audio.state = "downloading";
      f.video.state = "downloading";
      this.touch();
      let audioPool = new O0(),
        videoPool = new O0(),
        stopped = { audio: !1, video: !1 },
        captured = null,
        z = M(_, "audio.m4a"),
        Z = M(_, "video.mp4"),
        K = new C(
          this.settings,
          this.logger,
          () => this.touch(),
          this.browser,
        ),
        markSatisfied = (type, reason = "fast", source = null) => {
          if (
            type === "video" &&
            f.quality === "highest" &&
            reason === "fast" &&
            n0(source) < 1080
          ) {
            this.logger.info(
              `\u8996\u8A0A ${n0(source)}p \u901F\u5EA6\u6B63\u5E38\uFF0C\u4ECD\u4FDD\u7559\u76E3\u807D\u4EE5\u7B49\u5F85\u66F4\u9AD8\u756B\u8CEA`,
            );
            return;
          }
          if (stopped[type]) return;
          stopped[type] = !0;
          (type === "audio" ? audioPool : videoPool).close();
          this.logger.info(
            `${type === "audio" ? "\u97F3\u8A0A" : "\u8996\u8A0A"}\u5DF2\u627E\u5230\u6B63\u5E38\u901F\u5EA6\u4F86\u6E90\uFF0C\u505C\u6B62\u6536\u96C6\u9019\u985E\u4F86\u6E90`,
          );
          this.touch();
        },
        captureOptions = {
          shouldCollect: (type) =>
            type === "audio"
              ? !stopped.audio
              : type === "video"
                ? !stopped.video
                : !(stopped.audio && stopped.video),
          shouldStopAll: () => stopped.audio && stopped.video,
          onSource: (source, type) => {
            if (type === "audio" && !stopped.audio && audioPool.add(source))
              f.audio.sourceCount = audioPool.size;
            if (type === "video" && !stopped.video) {
              let selectedHeight = Number(f.resolvedQuality || 0),
                sourceHeight = n0(source);
              if (
                (!selectedHeight ||
                  !sourceHeight ||
                  sourceHeight === selectedHeight) &&
                videoPool.add(source)
              )
                f.video.sourceCount = videoPool.size;
            }
            this.touch();
          },
        },
        capturePromise = this.captureSources(f, captureOptions),
        audioDownload = K.downloadDynamic(
          f,
          "audio",
          audioPool,
          z,
          (reason, source) => markSatisfied("audio", reason, source),
        ),
        videoDownload = K.downloadDynamic(
          f,
          "video",
          videoPool,
          Z,
          (reason, source) => markSatisfied("video", reason, source),
          {
            keepSearchingAfterFast: (source) =>
              f.quality === "highest" && n0(source) < 1080,
            shouldReplaceBaseline: (nextSource, currentSource) =>
              f.quality === "highest" && n0(nextSource) > n0(currentSource),
          },
        );
      // Bun may report a rejection as unhandled before capturePromise finishes.
      // Attach handlers immediately; the actual results are still collected below.
      void audioDownload.catch(() => {});
      void videoDownload.catch(() => {});
      try {
        captured = await capturePromise;
        if (captured.targetId) captureTargets.push(captured.targetId);
        if (captured.pageSession) captureSessions.push(captured.pageSession);
        this.logger.info(
          `\u4F86\u6E90\u6536\u96C6\u7D50\u675F\uFF1ADrive \u5206\u9801\u770B\u5230 ${captured.rawRequestCount || 0} \u7B46 videoplayback\u3001\u97F3\u8A0A\u5019\u9078 ${captured.audio.length}\u3001\u8996\u8A0A\u5019\u9078 ${captured.video.length}\u3001\u5408\u4F75\u5019\u9078 ${captured.combined.length}`,
        );
        audioPool.close();
        if (captured.qualityMismatch) {
          let action = await this.requestQualityDecision(
            f,
            captured.qualityMismatch,
            captured.targetId,
          );
          if (action === "bug") {
            let mismatch = captured.qualityMismatch,
              detected = mismatch.detectedHeights.length
                ? mismatch.detectedHeights.map((height) => `${height}p`).join(", ")
                : "\u672A\u77E5";
            f.state = "failed";
            f.bugReported = !0;
            f.message = `\u5DF2\u78BA\u8A8D\u5F71\u7247\u6709${mismatch.requestedLabel}\uFF0C\u4F46\u7A0B\u5F0F\u53EA\u8FA8\u8B58\u5230 ${detected}\uFF1B\u5DF2\u6A19\u8A18\u70BA\u756B\u8CEA\u8FA8\u8B58 BUG`;
            f.video.note = "\u8996\u8A0A\u756B\u8CEA\u8FA8\u8B58 BUG\uFF0C\u672A\u64C5\u81EA\u964D\u7D1A\u4E0B\u8F09";
            let bugError = Error(f.message);
            videoPool.close(bugError);
            f.abortController.abort();
            await Promise.allSettled([audioDownload, videoDownload]);
            this.logger.bug(
              `BUG \u56DE\u5831 ${f.fileId}\uFF1A\u8981\u6C42=${mismatch.requestedLabel}\u3001\u5DF2\u8FA8\u8B58=${detected}\u3001\u9078\u55AE=${mismatch.menuHeights.join(",") || "unknown"}`,
            );
            this.touch();
            return;
          }
          if (action === "cancel") {
            f.cancelRequested = !0;
            f.state = "cancelling";
            f.message = "\u5DF2\u53D6\u6D88\u756B\u8CEA\u964D\u7D1A\u4E0B\u8F09\uFF0C\u6B63\u5728\u5B89\u5168\u505C\u6B62\u4EFB\u52D9";
            f.abortController.abort();
            videoPool.close();
            await Promise.allSettled([audioDownload, videoDownload]);
            this.touch();
            return;
          }
          let fallbackHeight = Number(
              captured.qualityMismatch.fallbackHeight || 0,
            ),
            fallbackSources = captured.allVideo.filter(
              (source) => n0(source) === fallbackHeight,
            );
          f.resolvedQuality = String(fallbackHeight);
          f.qualityNote = `${fallbackHeight}p\uFF08\u4F7F\u7528\u8005\u540C\u610F\u964D\u7D1A\uFF09`;
          f.state = "downloading";
          f.message = `\u5DF2\u540C\u610F\u6539\u7528 ${fallbackHeight}p\uFF0C\u97F3\u8A0A\u8207\u8996\u8A0A\u7E7C\u7E8C\u5404\u81EA\u4E0B\u8F09`;
          f.video.state = "downloading";
          f.video.note = `\u5DF2\u540C\u610F\u6539\u7528 ${fallbackHeight}p\uFF0C\u958B\u59CB\u8996\u8A0A\u4E0B\u8F09`;
          for (let source of fallbackSources) videoPool.add(source);
          f.video.sourceCount = videoPool.size;
          this.logger.info(
            `\u4F7F\u7528\u8005\u540C\u610F\u756B\u8CEA\u964D\u7D1A\uFF1A${fallbackHeight}p\uFF0C\u5019\u9078=${videoPool.size}`,
          );
          this.touch();
        }
        videoPool.close();
      } catch (captureError) {
        audioPool.close(captureError);
        videoPool.close(captureError);
        await Promise.allSettled([audioDownload, videoDownload]);
        throw captureError;
      }
      let trackResults = await Promise.allSettled([
        audioDownload,
        videoDownload,
      ]);
      f.title = m(
        captured.title,
        `GoogleDrive-${f.fileId.slice(0, 10)}`,
      );
      if (
        trackResults[0].status !== "fulfilled" ||
        trackResults[1].status !== "fulfilled"
      ) {
        if (captured.combined.length) {
          X(z, { force: !0 });
          X(Z, { force: !0 });
          f.audio = S();
          f.video = S();
          f.audio.state = "completed";
          f.audio.percent = 100;
          f.audio.note = "\u6539\u7528\u5DF2\u5305\u542B\u97F3\u8A0A\u7684\u5F71\u7247\u4F86\u6E90";
          f.message = "\u5206\u96E2\u97F3\u8A0A\u6216\u8996\u8A0A\u4F86\u6E90\u7121\u6CD5\u5B8C\u6210\uFF0C\u6539\u4E0B\u8F09\u5408\u4F75 MP4";
          let combinedPath = M(_, "combined.mp4");
          await K.download(f, "video", captured.combined, combinedPath);
          let outputPath = g(this.settings.outputDirectory, f.title);
          await Bun.write(outputPath, Bun.file(combinedPath));
          X(combinedPath, { force: !0 });
          f.outputPath = outputPath;
          f.state = "completed";
          f.message = `\u5DF2\u5B8C\u6210\uFF1A${G0(outputPath)}`;
          f.overallPercent = 100;
          f.audio.percent = 100;
          f.video.percent = 100;
          this.touch();
          this.cleanupTaskTemp(f.id);
          return;
        }
        if (
          (!this.loggedIn && captured.requiresLogin) ||
          (captured.accessDenied && !captured.pagePlayable)
        ) {
          f.audio.state = "waiting";
          f.video.state = "waiting";
          f.state = "auth_wait";
          f.message = this.loggedIn
            ? "\u76EE\u524D\u767B\u5165\u7684 Google \u5E33\u865F\u6C92\u6709\u9019\u90E8\u5F71\u7247\u7684\u89C0\u770B\u6B0A\u9650"
            : "\u9019\u90E8\u5F71\u7247\u9700\u8981\u89C0\u770B\u6B0A\u9650\uFF1B\u4EFB\u52D9\u6703\u7B49\u5F85\u767B\u5165 Google";
          this.touch();
          return;
        }
        let failed = trackResults.find((result) => result.status === "rejected");
        throw failed?.reason || Error("\u97F3\u8A0A\u6216\u8996\u8A0A\u4F86\u6E90\u4E0B\u8F09\u5931\u6557");
      }
      f.state = "merging";
      f.message =
        "\u97F3\u8A0A\u8207\u8996\u8A0A\u4E0B\u8F09\u5B8C\u6210\uFF0C\u6B63\u5728\u5408\u4F75 MP4";
      f.overallPercent = 96;
      this.touch();
      let B = g(this.settings.outputDirectory, f.title);
      (await this.mergeMedia(f, Z, z, B),
        (f.outputPath = B),
        (f.state = "completed"),
        (f.message = `\u5DF2\u5B8C\u6210\uFF1A${G0(B)}`),
        (f.overallPercent = 100),
        (f.audio.percent = 100),
        (f.video.percent = 100),
        this.logger.info(`\u4EFB\u52D9\u5B8C\u6210\uFF1A${B}`),
        this.touch(),
        this.cleanupTaskTemp(f.id));
    } catch ($) {
      if (w($) || f.abortController.signal.aborted) return;
      let errorMessage = $ instanceof Error ? $.message : String($);
      if (/HTTP\s+403/i.test(errorMessage)) {
        f.abortController.abort();
        await T(250).catch(() => {});
        this.cleanupTaskTemp(f.id);
        f.audio = S();
        f.video = S();
        f.overallPercent = 0;
        f.abortController = new AbortController();
        if ((f.authRefreshAttempts || 0) < 1) {
          f.authRefreshAttempts = (f.authRefreshAttempts || 0) + 1;
          f.state = "capturing";
          f.message =
            "\u4E0B\u8F09\u4F86\u6E90\u56DE\u8986 403\uFF0C\u6B63\u5728\u91CD\u65B0\u64F7\u53D6\u540C\u4E00\u700F\u89BD\u5668\u7684\u6388\u6B0A\u4F86\u6E90";
          this.logger.info(
            `\u4EFB\u52D9 ${f.fileId} \u9047\u5230 403\uFF0C\u81EA\u52D5\u91CD\u65B0\u64F7\u53D6\u4F86\u6E90\u4E00\u6B21`,
          );
          this.touch();
          return await this.processTask(f);
        }
        f.state = "auth_wait";
        f.message = `\u4E0B\u8F09\u4F86\u6E90\u56DE\u8986 403\uFF1B\u8ACB\u6309\u300C\u767B\u5165 Google\uFF08${this.browser?.browserName || "\u700F\u89BD\u5668"}\uFF09\u300D\uFF0C\u5728\u7A0B\u5F0F\u6307\u5B9A\u7684\u540C\u4E00\u500B\u700F\u89BD\u5668\u4E2D\u91CD\u65B0\u767B\u5165`;
        this.logger.error(`\u4EFB\u52D9\u6388\u6B0A\u4F86\u6E90\u5931\u6548 ${f.fileId}`, $);
        this.touch();
        return;
      }
      ((f.state = "failed"),
        (f.message = errorMessage),
        (f.audio.speed = 0),
        (f.video.speed = 0),
        this.logger.error(`\u4EFB\u52D9\u5931\u6557 ${f.fileId}`, $),
        this.touch());
    } finally {
      let keepDebugPage = ["failed", "auth_wait"].includes(f.state);
      if (keepDebugPage && captureTargets.length) {
        f.message = `${f.message}\uFF1BChrome \u9664\u932F\u5206\u9801\u5DF2\u4FDD\u7559`;
        this.logger.info(
          `\u4EFB\u52D9 ${f.fileId} \u672A\u5B8C\u6210\uFF0C\u4FDD\u7559 Chrome \u9664\u932F\u5206\u9801\uFF1A${captureTargets.join(",")}`,
        );
        this.touch();
      }
      if (this.browser && !keepDebugPage)
        await Promise.allSettled([
          ...captureTargets.map((targetId) =>
            this.browser.closeTarget(targetId),
          ),
          ...captureSessions.map(async (session) => session.close()),
        ]);
    }
  }
  async captureSources(f, options = {}) {
    if (!this.browser)
      throw Error("\u700F\u89BD\u5668\u5C1A\u672A\u555F\u52D5");
    let _ = await this.browser.createDirectPage(),
      pageSessionKey = `direct:${_.targetId}`,
      $ = [],
      sourceRequests = new Map(),
      requiresLogin = !1,
      accessDenied = !1,
      pagePlayable = !1,
      retainTarget = !1,
      sessions = new Set([pageSessionKey]),
      sessionTypes = new Map([[pageSessionKey, "page"]]),
      executionContexts = new Map(),
      playbackConfirmedSessions = new Set(),
      playbackStatusBySession = new Map(),
      playAttemptCounts = new Map(),
      frameIds = new Map(),
      mainFrameId = "",
      deliveredSources = new Set(),
      hydratingSources = new Set(),
      sourceHydrations = new Set(),
      requestedQuality = r0(f.quality),
      resolvedVideoQuality = requestedQuality === "auto" ? "auto" : null,
      networkRequestCount = 0,
      rawVideoplaybackCount = 0,
      qualityDecisionCompleted = !1,
      seenRawRequests = new Set(),
      seenAcceptedRequests = new Set();
    f.debugTargetId = _.targetId;
    f.videoSources = [];
    f.audioSources = [];
    this.touch();
    let requestKey = (sessionId, requestId) =>
        `${sessionId || "root"}:${requestId || ""}`,
      deliverSource = async (source, sourceType) => {
        let url = a0(source),
          sourceKey = s0(source);
        if (!['audio', 'video', 'combined'].includes(sourceType)) return;
        if (
          !url ||
          deliveredSources.has(sourceKey) ||
          hydratingSources.has(sourceKey)
        )
          return;
        if (
          (sourceType === "video" || sourceType === "combined") &&
          requestedQuality !== "auto"
        ) {
          if (!resolvedVideoQuality) return;
          if (n0(source) !== Number(resolvedVideoQuality)) return;
        }
        hydratingSources.add(sourceKey);
        try {
          if (
            !Object.keys(source.headers || {}).some(
              (header) => header.toLowerCase() === "cookie",
            )
          ) {
            let cookieResult = await _.sendTo(
                "Network.getCookies",
                { urls: [source.url] },
                source.sessionId || pageSessionKey,
              ),
              cookieHeader = (cookieResult.cookies || [])
                .map((cookie) => `${cookie.name}=${cookie.value}`)
                .join("; ");
            if (cookieHeader) source.headers.Cookie = cookieHeader;
          }
        } catch (error) {
          this.logger.error(
            "\u4E0B\u8F09\u524D\u540C\u6B65\u64AD\u653E\u4F86\u6E90 Cookie \u5931\u6557",
            error,
          );
        } finally {
          hydratingSources.delete(sourceKey);
          if (!deliveredSources.has(sourceKey)) {
            deliveredSources.add(sourceKey);
            options.onSource?.(source, sourceType);
          }
        }
      },
      queueSourceDelivery = (source, sourceType) => {
        let hydration = deliverSource(source, sourceType);
        sourceHydrations.add(hydration);
        void hydration.finally(() => sourceHydrations.delete(hydration));
      },
      resolveVideoQuality = (height, reason) => {
        let numericHeight = Number(height || 0);
        if (!numericHeight || resolvedVideoQuality === numericHeight) return;
        resolvedVideoQuality = numericHeight;
        let currentTaskHeight = Number(f.resolvedQuality || 0),
          shouldUpdateTaskQuality =
            !currentTaskHeight ||
            (requestedQuality === "highest"
              ? numericHeight >= currentTaskHeight
              : numericHeight === Number(requestedQuality) ||
                currentTaskHeight !== Number(requestedQuality));
        if (shouldUpdateTaskQuality) {
          f.resolvedQuality = String(numericHeight);
          f.qualityNote = `${numericHeight}p\uFF08${reason}\uFF09`;
        }
        this.logger.info(
          `\u8996\u8A0A\u756B\u8CEA\u5DF2\u78BA\u5B9A\uFF1A${numericHeight}p\uFF0C\u65B9\u5F0F=${reason}`,
        );
        for (let existing of $)
          queueSourceDelivery(existing, R(existing));
        this.touch();
      },
      recordSourceDiagnostic = (source, sourceType) => {
        if (
          source.diagnosticLogged ||
          !["audio", "video", "combined"].includes(sourceType)
        )
          return;
        source.diagnosticLogged = !0;
        let url = new URL(a0(source)),
          itag = url.searchParams.get("itag") || "?",
          mime =
            String(source.mimeType || "") ||
            url.searchParams.get("mime") ||
            "unknown";
        if (sourceType === "audio") {
          let entry = {
            index: f.audioSources.length + 1,
            candidateId: s0(source),
            itag,
            mime,
            size: V(source),
            status: "unused",
            speed: 0,
          };
          f.audioSources.push(entry);
          this.logger.info(
            `\u97F3\u8A0A\u4F86\u6E90\u8FA8\u8B58 #${entry.index}\uFF1Aitag=${itag}\u3001MIME=${mime}`,
          );
        } else {
          let height = n0(source),
            entry = {
              index: f.videoSources.length + 1,
              candidateId: s0(source),
              itag,
              height,
              mime,
              size: V(source),
              combined: sourceType === "combined",
              status: "unused",
              speed: 0,
            };
          f.videoSources.push(entry);
          this.logger.info(
            `\u8996\u8A0A\u4F86\u6E90\u8FA8\u8B58 #${entry.index}\uFF1A\u756B\u8CEA=${height ? `${height}p` : "\u7121\u6CD5\u8FA8\u8B58"}\u3001itag=${itag}\u3001MIME=${mime}\u3001\u985E\u578B=${sourceType}`,
          );
        }
        this.touch();
      },
      buildQualityMismatch = () => {
        if (requestedQuality === "auto" || resolvedVideoQuality) return null;
        let heights = [
          ...new Set(
            $.filter((source) => ["video", "combined"].includes(R(source)))
              .map(n0)
              .filter(Boolean),
          ),
        ].sort((a, b) => b - a);
        let menuHeights = [],
          requestedHeight =
            requestedQuality === "highest" ? 720 : Number(requestedQuality),
          fallbackHeight = heights[0] || 0;
        return {
          requestedQuality,
          requestedHeight,
          requestedLabel:
            requestedQuality === "highest"
              ? "\u6700\u9AD8\u53EF\u7528"
              : `${requestedHeight}p`,
          detectedHeights: heights,
          menuHeights,
          fallbackHeight,
          menuConfirmsHigher: !1,
          clickedHeight: 0,
        };
      },
      recordSource = (rawUrl, context = {}) => {
        if (!String(rawUrl || "").includes("videoplayback")) return null;
        let normalized = L(String(rawUrl)),
          key = requestKey(context.sessionId, context.requestId),
          networkChannel = String(context.channel || "").startsWith("network");
        if (networkChannel && !seenRawRequests.has(key)) {
          seenRawRequests.add(key);
          rawVideoplaybackCount += 1;
        }
        if (networkChannel && !seenAcceptedRequests.has(key)) {
          seenAcceptedRequests.add(key);
          networkRequestCount += 1;
        }
        let source = context.requestId
          ? sourceRequests.get(key)
          : $.find((item) => item.url === normalized);
        let previousType = source ? R(source) : "unknown",
          observedAt = Date.now(),
          contextType = R({
            url: normalized,
            mimeType: context.mimeType,
            responseHeaders: context.responseHeaders || {},
          });
        if (!source) {
          source = {
            url: normalized,
            candidateId: context.requestId ? key : `resource:${normalized}`,
            firstSeenAt: observedAt,
            headers: {
              ...d0(context.headers || {}),
            },
            responseHeaders: { ...(context.responseHeaders || {}) },
            mimeType: String(context.mimeType || ""),
            mediaType:
              contextType === "unknown" ? "" : contextType,
            contentLength: Number(context.contentLength || 0),
            sessionId: context.sessionId || pageSessionKey,
            frameId: context.frameId,
            pageConnection: _.connection,
            targetType:
              context.targetType ||
              sessionTypes.get(context.sessionId) ||
              "page",
          };
          $.push(source);
          f.message = `\u5DF2\u622A\u7372 ${networkRequestCount} \u7B46 videoplayback \u8ACB\u6C42\uFF0C\u53EF\u5617\u8A66\u4F86\u6E90 ${$.length} \u500B`;
          this.touch();
        } else {
          source.headers = {
            ...source.headers,
            ...d0(context.headers || {}),
          };
          source.responseHeaders = {
            ...(source.responseHeaders || {}),
            ...(context.responseHeaders || {}),
          };
          source.mimeType ||= String(context.mimeType || "");
          source.contentLength ||=
            Number(context.contentLength || 0);
          if (contextType !== "unknown") source.mediaType = contextType;
          source.sessionId ||= context.sessionId || pageSessionKey;
          source.frameId ||= context.frameId;
          source.pageConnection ||= _.connection;
          source.targetType ||=
            context.targetType ||
            sessionTypes.get(context.sessionId) ||
            "page";
        }
        if (context.frameId && context.sessionId)
          frameIds.set(context.sessionId, context.frameId);
        if (context.requestId) {
          sourceRequests.set(key, source);
        }
        let sourceType = R(source);
        let sourceHeight = n0(source);
        if (
          resolvedVideoQuality &&
          resolvedVideoQuality !== "auto" &&
          ["video", "combined"].includes(sourceType) &&
          sourceHeight !== Number(resolvedVideoQuality)
        ) {
          if (
            requestedQuality === "highest" &&
            sourceHeight > Number(resolvedVideoQuality)
          )
            resolveVideoQuality(
              sourceHeight,
              "\u76E3\u807D\u671F\u9593\u767C\u73FE\u66F4\u9AD8\u756B\u8CEA\uFF0C\u5F37\u5236\u5347\u7D1A",
            );
          else return source;
        }
        recordSourceDiagnostic(source, sourceType);
        if (
          requestedQuality !== "auto" &&
          sourceHeight > 0 &&
          ["video", "combined"].includes(sourceType)
        ) {
          if (requestedQuality === "highest" && sourceHeight >= 1080)
            resolveVideoQuality(
              sourceHeight,
              "\u756B\u8CEA\u78BA\u8A8D\u6642\u9593\u5167\u5DF2\u53D6\u5F97 1080p \u6216\u4EE5\u4E0A\u4F86\u6E90",
            );
          else if (
            requestedQuality !== "highest" &&
            sourceHeight === Number(requestedQuality)
          )
            resolveVideoQuality(
              sourceHeight,
              "videoplayback \u756B\u8CEA\u7B26\u5408\u4F7F\u7528\u8005\u6307\u5B9A\u503C",
            );
        }
        if (!source.loggedInitial) {
          source.loggedInitial = !0;
          this.logger.info(
            `\u5075\u6E2C\u5230\u64AD\u653E\u4F86\u6E90\uFF1A\u901A\u9053=${context.channel || "network"} \u985E\u578B=${sourceType} itag=${new URL(normalized).searchParams.get("itag") || "?"} mime=${new URL(normalized).searchParams.get("mime") || context.mimeType || "?"} clen=${V(source) || "?"}`,
          );
        } else if (previousType === "unknown" && sourceType !== "unknown")
          this.logger.info(
            `videoplayback \u5DF2\u4F9D Chrome \u56DE\u61C9\u91CD\u65B0\u5206\u985E\uFF1A${sourceType}\uFF0Cmime=${source.mimeType || "?"}\uFF0Cheight=${sourceHeight || "?"}p`,
          );
        if (sourceType !== "unknown" && options.shouldCollect?.(sourceType) === !1)
          return source;
        queueSourceDelivery(source, sourceType);
        return source;
      },
      resolveFrameId = async (sessionId) => {
        if (frameIds.has(sessionId)) return frameIds.get(sessionId);
        try {
          let tree = await _.sendTo("Page.getFrameTree", {}, sessionId),
            frameId = tree?.frameTree?.frame?.id;
          if (frameId) frameIds.set(sessionId, frameId);
          return frameId;
        } catch {
          return null;
        }
      },
      enableRelatedTarget = async (sessionId, targetInfo = {}) => {
        if (!sessionId || sessions.has(sessionId)) return;
        let targetType = String(targetInfo.type || "unknown");
        sessions.add(sessionId);
        sessionTypes.set(sessionId, targetType);
        try {
          await _.sendTo(
            "Network.enable",
            { maxTotalBufferSize: 1e8 },
            sessionId,
          );
          this.logger.info(
            `Drive \u76F8\u95DC\u76EE\u6A19 Network.enable \u5DF2\u5B8C\u6210\uFF1Atype=${targetType} target=${targetInfo.targetId || "?"}`,
          );
        } catch (error) {
          this.logger.error(
            `Drive \u76F8\u95DC\u76EE\u6A19 Network.enable \u5931\u6557\uFF1Atype=${targetType}`,
            error,
          );
        }
        try {
          await _.sendTo("Runtime.enable", {}, sessionId);
          this.logger.info(
            `Drive \u76F8\u95DC\u76EE\u6A19 Runtime.enable \u5DF2\u5B8C\u6210\uFF1Atype=${targetType} target=${targetInfo.targetId || "?"}`,
          );
        } catch (error) {
          this.logger.error(
            `Drive \u76F8\u95DC\u76EE\u6A19 Runtime.enable \u5931\u6557\uFF1Atype=${targetType}`,
            error,
          );
        }
        try {
          await _.sendTo(
            "Target.setAutoAttach",
            {
              autoAttach: !0,
              waitForDebuggerOnStart: !1,
              flatten: !0,
            },
            sessionId,
          );
        } catch {}
      },
      ensurePlayback = async (actor) => {
        let {
            sessionId,
            contextId = null,
            frameId = null,
            actorKey = sessionId,
            canUseInput = !0,
          } = actor,
          evaluateParams = (expression) => ({
            expression,
            returnByValue: !0,
            userGesture: !0,
            ...(contextId ? { contextId } : {}),
          });
        let result = await _.sendTo(
            "Runtime.evaluate",
            evaluateParams(P0),
            sessionId,
          ),
          page = result?.result?.value || {};
        if (
          page.playPoint &&
          !page.playingCount &&
          canUseInput &&
          (page.hasPlayButton || !playAttemptCounts.has(actorKey))
        ) {
          let attempt = (playAttemptCounts.get(actorKey) || 0) + 1;
          playAttemptCounts.set(actorKey, attempt);
          this.logger.info(
            `\u64AD\u653E\u9375\u9EDE\u64CA\u5617\u8A66 ${attempt}\uFF1A\u65B9\u5F0F=${page.playMethod || "unknown"} x=${Math.round(page.playPoint.x)} y=${Math.round(page.playPoint.y)}`,
          );
          await _.sendTo(
            "Input.dispatchMouseEvent",
            {
              type: "mouseMoved",
              x: page.playPoint.x,
              y: page.playPoint.y,
            },
            sessionId,
          );
          await _.sendTo(
            "Input.dispatchMouseEvent",
            {
              type: "mousePressed",
              x: page.playPoint.x,
              y: page.playPoint.y,
              button: "left",
              clickCount: 1,
            },
            sessionId,
          );
          await T(80, f.abortController.signal);
          await _.sendTo(
            "Input.dispatchMouseEvent",
            {
              type: "mouseReleased",
              x: page.playPoint.x,
              y: page.playPoint.y,
              button: "left",
              clickCount: 1,
            },
            sessionId,
          );
          await T(1000, f.abortController.signal);
          let confirmation = await _.sendTo(
            "Runtime.evaluate",
            evaluateParams(P0),
            sessionId,
          );
          page = confirmation?.result?.value || page;
        }
        let playbackStatus = `media=${page.mediaCount || 0} playing=${page.playingCount || 0} button=${page.hasPlayButton ? 1 : 0}`;
        if (playbackStatusBySession.get(actorKey) !== playbackStatus) {
          playbackStatusBySession.set(actorKey, playbackStatus);
          this.logger.info(
            `\u64AD\u653E\u5668\u72C0\u614B\uFF1Atype=${sessionTypes.get(sessionId) || "page"} frame=${frameId || "?"} ${playbackStatus}`,
          );
        }
        if (page.playingCount && !playbackConfirmedSessions.has(actorKey)) {
          playbackConfirmedSessions.add(actorKey);
          this.logger.info(
            `\u5DF2\u5728\u64AD\u653E\u5668\u5DE5\u4F5C\u968E\u6BB5\u555F\u52D5\u5F71\u7247\uFF1Amedia=${page.mediaCount || 0} playing=${page.playingCount || 0}`,
          );
        }
        return page;
      },
      Z = _.onAll((event) => {
        if (event.method === "Target.attachedToTarget") {
          let childSessionId = event.params?.sessionId,
            targetInfo = event.params?.targetInfo || {};
          void enableRelatedTarget(childSessionId, targetInfo);
          return;
        }
        if (event.method === "Target.detachedFromTarget") {
          let childSessionId = event.params?.sessionId;
          if (childSessionId) {
            sessions.delete(childSessionId);
            sessionTypes.delete(childSessionId);
            for (let [key, context] of executionContexts)
              if (context.sessionId === childSessionId)
                executionContexts.delete(key);
          }
          return;
        }
        if (event.method === "Runtime.executionContextCreated") {
          let context = event.params?.context || {},
            auxData = context.auxData || {},
            contextId = Number(context.id || 0),
            eventSessionId = event.sessionId || pageSessionKey;
          if (contextId && auxData.isDefault !== !1) {
            let key = `${eventSessionId}:${contextId}`;
            executionContexts.set(key, {
              actorKey: key,
              sessionId: eventSessionId,
              contextId,
              frameId: String(auxData.frameId || ""),
              origin: String(context.origin || ""),
              canUseInput:
                eventSessionId !== pageSessionKey || !auxData.frameId,
            });
            this.logger.info(
              `\u767C\u73FE\u53EF\u64CD\u4F5C\u7684\u9801\u9762\u57F7\u884C\u74B0\u5883\uFF1Atype=${sessionTypes.get(eventSessionId) || "page"} frame=${auxData.frameId || "?"} origin=${context.origin || "?"}`,
            );
          }
          return;
        }
        if (event.method === "Runtime.executionContextDestroyed") {
          let eventSessionId = event.sessionId || pageSessionKey,
            contextId = Number(event.params?.executionContextId || 0);
          executionContexts.delete(`${eventSessionId}:${contextId}`);
          return;
        }
        if (event.method === "Runtime.executionContextsCleared") {
          let eventSessionId = event.sessionId || pageSessionKey;
          for (let [key, context] of executionContexts)
            if (context.sessionId === eventSessionId)
              executionContexts.delete(key);
          return;
        }
        if (
          ![
            "Network.requestWillBeSent",
            "Network.responseReceived",
          ].includes(event.method)
        )
          return;
        let request = event.params?.request || {},
          response = event.params?.response || {},
          url = String(request.url || response.url || ""),
          eventSessionId = event.sessionId || pageSessionKey;
        if (!url.includes("videoplayback")) return;
        try {
          recordSource(url, {
            channel:
              event.method === "Network.requestWillBeSent"
                ? "network-request"
                : "network-response",
            headers:
              event.method === "Network.requestWillBeSent"
                ? request.headers || {}
                : {},
            responseHeaders:
              event.method === "Network.responseReceived"
                ? response.headers || {}
                : {},
            mimeType:
              event.method === "Network.responseReceived"
                ? response.mimeType || ""
                : "",
            contentLength:
              event.method === "Network.responseReceived"
                ? Number(response.encodedDataLength || 0)
                : 0,
            sessionId: eventSessionId,
            frameId: event.params?.frameId,
            requestId: event.params?.requestId,
            targetId: _.targetId,
            targetType: sessionTypes.get(eventSessionId) || "page",
          });
        } catch (error) {
          this.logger.error("\u8655\u7406 videoplayback \u7DB2\u8DEF\u4E8B\u4EF6\u5931\u6557", error);
        }
      });
    try {
      await _.send("Network.enable", { maxTotalBufferSize: 1e8 });
      this.logger.info(
        `Drive \u5206\u9801 Network.enable \u5DF2\u5B8C\u6210\uFF0C\u6E96\u5099\u5C0E\u822A\uFF1Atarget=${_.targetId}`,
      );
      await _.send("Runtime.enable");
      this.logger.info(
        `Drive \u5206\u9801 Runtime.enable \u5DF2\u5B8C\u6210\uFF1Atarget=${_.targetId}`,
      );
      await _.send("Target.setAutoAttach", {
        autoAttach: !0,
        waitForDebuggerOnStart: !1,
        flatten: !0,
      });
      this.logger.info(
        "Drive \u5206\u9801\u76F8\u95DC iframe\uFF0Fworker \u76E3\u807D\u5DF2\u555F\u7528",
      );
      await _.send("Page.enable");
      let navigation = await _.send("Page.navigate", { url: f.url });
      mainFrameId = String(navigation?.frameId || "");
      this.logger.info(
        `Drive \u5206\u9801\u5DF2\u5C0E\u822A\uFF1Aframe=${navigation?.frameId || "?"} loader=${navigation?.loaderId || "?"} error=${navigation?.errorText || "none"}`,
      );
      let K = `GoogleDrive-${f.fileId.slice(0, 10)}`,
        B = Date.now(),
        G = B + this.settings.qualityConfirmSeconds * 1000,
        buildResult = async () => {
          await Promise.allSettled([...sourceHydrations]);
          let detectedVideoHeights = [
            ...new Set(
              $.filter((source) =>
                ["video", "combined"].includes(R(source)),
              )
                .map(n0)
                .filter(Boolean),
            ),
          ].sort((left, right) => right - left);
          await Promise.allSettled([...sourceHydrations]);
          if (
            requestedQuality !== "auto" &&
            resolvedVideoQuality &&
            !$.some(
              (source) =>
                ["video", "combined"].includes(R(source)) &&
                n0(source) === Number(resolvedVideoQuality),
            )
          ) {
            this.logger.info(
              `\u64AD\u653E\u5668\u5DF2\u9078\u64C7 ${resolvedVideoQuality}p\uFF0C\u4F46\u6536\u96C6\u671F\u9650\u5167\u672A\u6536\u5230\u5C0D\u61C9\u4F86\u6E90\uFF0C\u6539\u7528\u5BE6\u969B\u5075\u6E2C\u5230\u7684\u6700\u63A5\u8FD1\u756B\u8CEA`,
            );
            resolvedVideoQuality = null;
            f.resolvedQuality = "";
            f.qualityNote = "";
          }
          let qualityMismatch = buildQualityMismatch();
          for (let source of $) {
            if (
              Object.keys(source.headers).some(
                (header) => header.toLowerCase() === "cookie",
              )
            )
              continue;
            try {
              let cookieResult = await _.sendTo(
                  "Network.getCookies",
                  { urls: [source.url] },
                  source.sessionId || pageSessionKey,
                ),
                cookieHeader = (cookieResult.cookies || [])
                  .map((cookie) => `${cookie.name}=${cookie.value}`)
                  .join("; ");
              if (cookieHeader) source.headers.Cookie = cookieHeader;
            } catch (error) {
              this.logger.error("\u8B80\u53D6\u64AD\u653E\u4F86\u6E90\u7684\u767B\u5165 Cookie \u5931\u6557", error);
            }
          }
          let safeSources = $.map((source) => source),
            qualitySources =
              requestedQuality === "auto"
                ? safeSources
                : safeSources.filter((source) => {
                    let type = R(source);
                    return (
                      !["video", "combined"].includes(type) ||
                      n0(source) === Number(resolvedVideoQuality)
                    );
                  });
          let cookieSources = safeSources.filter((source) =>
              Object.keys(source.headers).some(
                (header) => header.toLowerCase() === "cookie",
              ),
            ).length,
            userAgentSources = safeSources.filter((source) =>
              Object.keys(source.headers).some(
                (header) => header.toLowerCase() === "user-agent",
              ),
            ).length;
          this.logger.info(
            `\u64AD\u653E\u4F86\u6E90\u6388\u6B0A\u540C\u6B65\uFF1A\u4F86\u6E90 ${safeSources.length}\u3001Cookie ${cookieSources}\u3001User-Agent ${userAgentSources}\u3001\u700F\u89BD\u5668 ${this.browser?.browserName || "?"}`,
          );
          retainTarget = !0;
          return {
            title: K,
            audio: W(qualitySources, "audio"),
            video: W(qualitySources, "video"),
            combined: W(qualitySources, "combined"),
            allVideo: W(safeSources, "video"),
            allCombined: W(safeSources, "combined"),
            qualityMismatch,
            sourceCount: safeSources.length,
            requestCount: networkRequestCount,
            rawRequestCount: rawVideoplaybackCount,
            requiresLogin,
            accessDenied,
            pagePlayable: pagePlayable || safeSources.length > 0,
            playbackWithoutSources: pagePlayable && safeSources.length === 0,
            targetId: _.targetId,
            pageSession: _,
          };
        };
      while (!0) {
        if (f.abortController.signal.aborted)
          throw new DOMException("Aborted", "AbortError");
        if (options.shouldStopAll?.()) {
          f.message =
            "\u97F3\u8A0A\u8207\u8996\u8A0A\u90FD\u5DF2\u627E\u5230\u6B63\u5E38\u901F\u5EA6\u4F86\u6E90\uFF0C\u505C\u6B62\u6536\u96C6";
          this.touch();
          break;
        }
        let contextSessions = new Set(
            [...executionContexts.values()].map((actor) => actor.sessionId),
          ),
          fallbackActors = [...sessions]
            .filter(
              (sessionId) =>
                !contextSessions.has(sessionId) &&
                ["page", "iframe"].includes(
                  sessionTypes.get(sessionId) || "page",
                ),
            )
            .map((sessionId) => ({
              actorKey: sessionId,
              sessionId,
              contextId: null,
              frameId: frameIds.get(sessionId) || "",
              canUseInput: !0,
            })),
          interactiveActors = [
            ...executionContexts.values(),
            ...fallbackActors,
          ]
            .filter((actor) =>
              ["page", "iframe"].includes(
                sessionTypes.get(actor.sessionId) || "page",
              ),
            )
            .map((actor) => ({
              ...actor,
              canUseInput:
                !actor.contextId ||
                actor.sessionId !== pageSessionKey ||
                !actor.frameId ||
                actor.frameId === mainFrameId,
            })),
          playbackResults = await Promise.allSettled(
          interactiveActors.map(async (actor) => ({
            sessionId: actor.sessionId,
            actorKey: actor.actorKey,
            page: await ensurePlayback(actor),
            frameId:
              actor.frameId || (await resolveFrameId(actor.sessionId)),
          })),
        );
        let playbackActive = !1;
        for (let playbackResult of playbackResults) {
          if (playbackResult.status !== "fulfilled") continue;
          let { page, sessionId, frameId } = playbackResult.value;
          playbackActive ||= Boolean(page?.playingCount);
          if (
            sessionId === pageSessionKey &&
            page?.title &&
            !/^(Google Drive|Sign in)/i.test(page.title)
          ) {
            K = String(page.title);
            let visibleTitle = m(K, `GoogleDrive-${f.fileId.slice(0, 10)}`);
            if (visibleTitle && f.title !== visibleTitle) {
              f.title = visibleTitle;
              this.touch();
            }
          }
          for (let resourceUrl of page?.resourceUrls || []) {
            try {
              recordSource(resourceUrl, {
                channel: "performance",
                sessionId,
                frameId,
                targetType: sessionTypes.get(sessionId) || "page",
              });
            } catch {}
          }
          let bodyText = String(page?.bodyText || "");
          requiresLogin ||= /sign in|\u767B\u5165 Google|\u767B\u5165\u5E33\u6236/i.test(bodyText);
          accessDenied ||=
            /need access|request access|access denied|\u9700\u8981\u5B58\u53D6\u6B0A|\u8981\u6C42\u5B58\u53D6\u6B0A|\u6C92\u6709\u6B0A\u9650/i.test(
              bodyText,
            );
          pagePlayable ||= Boolean(
            page?.mediaCount || page?.hasPlayButton || page?.playingCount,
          );
        }
        let audioCount = $.filter((N) => R(N) === "audio").length,
          videoCount = $.filter((N) => R(N) === "video").length,
          combinedCount = $.filter((N) => R(N) === "combined").length,
          elapsed = Date.now() - B,
          remainingSeconds = Math.max(0, Math.ceil((G - Date.now()) / 1000));
        if (
          requestedQuality !== "auto" &&
          !resolvedVideoQuality &&
          Date.now() >= G &&
          !qualityDecisionCompleted
        ) {
          let detectedHeights = [
            ...new Set(
              $.filter((source) => ["video", "combined"].includes(R(source)))
                .map(n0)
                .filter(Boolean),
            ),
          ].sort((left, right) => right - left);
          if (requestedQuality === "highest" && detectedHeights[0] >= 720) {
            resolveVideoQuality(
              detectedHeights[0],
              `\u756B\u8CEA\u78BA\u8A8D ${this.settings.qualityConfirmSeconds} \u79D2\u7D50\u675F\u6642\u7684\u6700\u9AD8\u4F86\u6E90`,
            );
            qualityDecisionCompleted = !0;
          } else {
            let mismatch = buildQualityMismatch(),
              action = await this.requestQualityDecision(f, mismatch, _.targetId);
            if (action === "bug") {
              let detected = mismatch.detectedHeights.length
                ? mismatch.detectedHeights.map((height) => `${height}p`).join(", ")
                : "\u672A\u77E5";
              f.bugReported = !0;
              f.message = `\u5DF2\u78BA\u8A8D\u5F71\u7247\u6709${mismatch.requestedLabel}\uFF0C\u4F46\u7A0B\u5F0F\u53EA\u8FA8\u8B58\u5230 ${detected}\uFF1B\u5DF2\u6A19\u8A18\u70BA\u756B\u8CEA\u8FA8\u8B58 BUG`;
              f.video.note = "\u8996\u8A0A\u756B\u8CEA\u8FA8\u8B58 BUG\uFF0C\u672A\u64C5\u81EA\u964D\u7D1A\u4E0B\u8F09";
              this.logger.bug(`BUG \u56DE\u5831 ${f.fileId}\uFF1A\u8981\u6C42=${mismatch.requestedLabel}\u3001\u5DF2\u8FA8\u8B58=${detected}`);
              throw Error(f.message);
            }
            if (action === "cancel") {
              f.cancelRequested = !0;
              f.abortController.abort();
              throw new DOMException("Aborted", "AbortError");
            }
            let selectedHeight = Number(action.split(":")[1] || 0);
            requestedQuality = String(selectedHeight);
            resolveVideoQuality(selectedHeight, "\u4F7F\u7528\u8005\u78BA\u8A8D\u6539\u7528\u5DF2\u5075\u6E2C\u756B\u8CEA");
            f.state = "downloading";
            f.video.state = "downloading";
            qualityDecisionCompleted = !0;
          }
        }
        let collecting = [
          options.shouldCollect?.("video") === !1 ? null : "\u8996\u8A0A",
          options.shouldCollect?.("audio") === !1 ? null : "\u97F3\u8A0A",
        ].filter(Boolean);
        let qualityStatus =
          requestedQuality === "auto"
            ? "\u756B\u8CEA\uFF1A\u64AD\u653E\u5668\u81EA\u52D5"
            : resolvedVideoQuality
              ? `\u5DF2\u9396\u5B9A\u756B\u8CEA\uFF1A${resolvedVideoQuality}p\uFF0C\u540C\u756B\u8CEA\u9AD8\u901F\u4F86\u6E90\u4E0D\u9650\u6642\u5C0B\u627E`
              : `\u64AD\u653E\u5668 Auto\uFF0C\u756B\u8CEA\u78BA\u8A8D\u9084\u6709 ${remainingSeconds} \u79D2`;
        f.message = `${playbackActive ? "\u5F71\u7247\u6B63\u5728\u975C\u97F3\u64AD\u653E" : "\u6B63\u5728\u5C0B\u627E\u4E26\u555F\u52D5\u64AD\u653E\u5668"}\uFF0C${qualityStatus}\uFF0CDrive \u5206\u9801\u770B\u5230 ${rawVideoplaybackCount} \u7B46 videoplayback\uFF0C\u6574\u7406\u70BA ${$.length} \u500B\u5019\u9078\uFF08\u8996\u8A0A ${videoCount}\u3001\u97F3\u8A0A ${audioCount}\u3001\u5408\u4F75 ${combinedCount}\uFF09\uFF0C${collecting.length ? `\u7E7C\u7E8C\u6536\u96C6${collecting.join("\u3001")}` : "\u4E0B\u8F09\u4F86\u6E90\u5DF2\u78BA\u5B9A"}`;
        this.touch();
        if (
          !$.length &&
          elapsed > 5000 &&
          (requiresLogin || accessDenied)
        )
          break;
        await T(1000, f.abortController.signal);
      }
      await Promise.allSettled(
        [...sessions].map((sessionId) =>
          _.sendTo(
            "Runtime.evaluate",
            {
              expression:
                "document.querySelectorAll('video,audio').forEach((media) => { media.muted = true; media.volume = 0; media.pause(); });",
            },
            sessionId,
          ),
        ),
      );
      return await buildResult();
    } finally {
      Z();
      if (!retainTarget && f.abortController.signal.aborted) {
        await this.browser.closeTarget(_.targetId);
        _.close();
      } else if (!retainTarget) {
        this.logger.info(
          `\u4F86\u6E90\u64F7\u53D6\u7570\u5E38\uFF0C\u4FDD\u7559 Chrome \u9664\u932F\u5206\u9801\uFF1Atarget=${_.targetId}`,
        );
      }
    }
  }
  async mergeMedia(f, _, $, z) {
    let Z = process.env.CYDRIVE_FFMPEG_PATH || M(this.appRoot, "ffmpeg.exe");
    if (!c(Z))
      throw Error(
        "\u627E\u4E0D\u5230\u5167\u5EFA\u7684\u5F71\u97F3\u5408\u4F75\u5143\u4EF6",
      );
    Q(i(z));
    let K = `${z}.partial.mp4`;
    X(K, { force: !0 });
    let B = [
        Z,
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-i",
        _,
        "-i",
        $,
        "-map",
        "0:v:0",
        "-map",
        "1:a:0",
        "-c",
        "copy",
        "-shortest",
        K,
      ],
      G = await this.runFfmpeg(B, f.abortController.signal);
    if (G.code !== 0) {
      (this.logger.info(
        "\u76F4\u63A5\u5C01\u88DD\u5931\u6557\uFF0C\u6539\u5C07\u97F3\u8A0A\u8F49\u70BA AAC",
      ),
        X(K, { force: !0 }));
      let E = [
        Z,
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-i",
        _,
        "-i",
        $,
        "-map",
        "0:v:0",
        "-map",
        "1:a:0",
        "-c:v",
        "copy",
        "-c:a",
        "aac",
        "-b:a",
        "192k",
        "-shortest",
        K,
      ];
      G = await this.runFfmpeg(E, f.abortController.signal);
    }
    if (G.code !== 0)
      throw Error(
        `\u5F71\u97F3\u5408\u4F75\u5931\u6557\uFF1A${G.stderr.slice(-500)}`,
      );
    (X(z, { force: !0 }), Z0(K, z));
  }
  async runFfmpeg(f, _) {
    let $ = Bun.spawn(f, {
        stdin: "ignore",
        stdout: "ignore",
        stderr: "pipe",
        windowsHide: !0,
      }),
      z = () => $.kill();
    _.addEventListener("abort", z, { once: !0 });
    try {
      let Z = new Response($.stderr).text(),
        K = await $.exited,
        B = await Z;
      if (_.aborted) throw new DOMException("Aborted", "AbortError");
      return { code: K, stderr: B };
    } finally {
      _.removeEventListener("abort", z);
    }
  }
  async beginLogin() {
    if (!this.browser)
      throw Error("\u700F\u89BD\u5668\u5C1A\u672A\u555F\u52D5");
    ((this.loginPending = !0),
      await this.browser.openLoginWindow(),
      this.touch());
  }
  async pollAuthentication() {
    if (!this.browser || this.shuttingDown) return;
    let f = await this.browser.isGoogleLoggedIn(),
      _ = this.loginPending,
      $ = _ ? await this.browser.loginWindowStatus() : "closed",
      z = _ && f && $ === "complete",
      Z = _ && $ === "closed";
    if (Z) this.loginPending = !1;
    let K = f !== this.loggedIn;
    if (!K && !z && !Z) return;
    if (((this.loggedIn = f), z))
      ((this.loginPending = !1), await this.browser.closeLoginWindow());
    if (f && (K || z)) {
      for (let B of this.tasks.values()) {
        if (B.state !== "auth_wait") continue;
        ((B.state = "queued"),
          (B.message =
            "\u767B\u5165\u5B8C\u6210\uFF0C\u91CD\u65B0\u52A0\u5165\u4E0B\u8F09\u6392\u7A0B"),
          (B.authRefreshAttempts = 0),
          (B.abortController = new AbortController()));
      }
      this.pump();
    }
    this.touch();
  }
  async browseForFolder() {
    if (process.platform !== "win32") return this.settings.outputDirectory;
    let f = [
        "Add-Type -AssemblyName System.Windows.Forms",
        "$dialog = New-Object System.Windows.Forms.FolderBrowserDialog",
        `$dialog.SelectedPath = '${this.settings.outputDirectory.replace(/'/g, "''")}'`,
        "$dialog.Description = '\u9078\u64C7\u5F71\u7247\u9810\u8A2D\u5B58\u6A94\u4F4D\u7F6E'",
        "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::OutputEncoding = [Text.Encoding]::UTF8; Write-Output $dialog.SelectedPath }",
      ].join("; "),
      _ = Bun.spawn(["powershell.exe", "-NoProfile", "-STA", "-Command", f], {
        stdin: "ignore",
        stdout: "pipe",
        stderr: "ignore",
        windowsHide: !0,
      }),
      $ = (await new Response(_.stdout).text()).trim();
    return (await _.exited, $ || this.settings.outputDirectory);
  }
  publicState() {
    let f = [...this.tasks.values()]
        .sort(($, z) => {
          let Z = $.state === "completed" ? 1 : 0,
            K = z.state === "completed" ? 1 : 0;
          if (Z !== K) return Z - K;
          if (!Z && !K && $.state === "queued" && z.state === "queued")
            return H[$.priority] - H[z.priority] || $.createdAt - z.createdAt;
          return $.createdAt - z.createdAt;
        })
        .map(({ abortController: $, ...z }) => z),
      _ = f.filter(($) =>
        ["capturing", "downloading", "merging", "cancelling", "quality_wait"].includes($.state),
      ).length;
    return {
      version: D,
      revision: this.stateRevision,
      browserName: this.browser?.browserName || "\u700F\u89BD\u5668",
      loggedIn: this.loggedIn,
      loginPending: this.loginPending,
      settings: this.settings,
      active: _,
      tasks: f,
    };
  }
  cleanupTaskTemp(f) {
    try {
      X(M(this.tempRoot, f), { recursive: !0, force: !0 });
    } catch {}
  }
  touch() {
    this.stateRevision += 1;
  }
  json(f, _ = 200) {
    return Response.json(f, {
      status: _,
      headers: { "cache-control": "no-store" },
    });
  }
  async shutdown(exitCode = 0) {
    if (this.shuttingDown) return;
    if (((this.shuttingDown = !0), this.authPollTimer))
      clearInterval(this.authPollTimer);
    for (let f of this.tasks.values())
      if (f.state !== "completed") f.abortController.abort();
    (this.logger.info(
      "\u7A0B\u5F0F\u95DC\u9589\uFF1B\u672A\u5B8C\u6210\u4EFB\u52D9\u4E0D\u4FDD\u7559\u7E8C\u50B3",
    ),
      await T(200).catch(() => {
        return;
      }),
      await this.browser?.shutdown(),
      this.server?.stop(!0),
      this.uiWorker?.terminate(),
      this.uiProcess?.kill(),
      process.exit(exitCode));
  }
}
export {
  n as DriveDownloaderApp,
  F as BrowserController,
  P as CDPConnection,
  b as BrowserSession,
  p as DownloadAttempt,
  R as classifyPlaybackUrl,
  L as normalizePlaybackUrl,
  W as selectPlaybackSources,
  d0 as sanitizePlaybackHeaders,
  e0 as mergePlaybackSources,
  n0 as playbackSourceHeight,
  r0 as normalizeQuality,
  O0 as DynamicSourcePool,
  C as SourceDownloader,
  P0 as playbackActivationExpression,
};
if (import.meta.main)
  new n().start().catch((_) => {
    let $ = _ instanceof Error ? _.message : String(_);
    (console.error($), process.exit(1));
  });
