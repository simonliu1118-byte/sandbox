// @bun
var __defProp = Object.defineProperty;
var __returnValue = (v) => v;
function __exportSetter(name, newValue) {
  this[name] = __returnValue.bind(null, newValue);
}
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, {
      get: all[name],
      enumerable: true,
      configurable: true,
      set: __exportSetter.bind(all, name)
    });
};
var __esm = (fn, res) => () => (fn && (res = fn(fn = 0)), res);
var __require = import.meta.require;

// runtime/webview-bun/src/ffi.ts
import { dlopen, FFIType, ptr } from "bun:ffi";
function encodeCString(value) {
  return ptr(new TextEncoder().encode(value + "\x00"));
}
function unload() {
  for (const instance of instances)
    instance.destroy();
  lib.close();
}
var instances, lib_file, lib;
var init_ffi = __esm(() => {
  instances = [];
  lib_file = process.env.WEBVIEW_PATH;
  if (!lib_file)
    throw new Error("WEBVIEW_PATH is not configured");
  lib = dlopen(lib_file, {
    webview_create: {
      args: [FFIType.i32, FFIType.ptr],
      returns: FFIType.ptr
    },
    webview_destroy: {
      args: [FFIType.ptr],
      returns: FFIType.void
    },
    webview_run: {
      args: [FFIType.ptr],
      returns: FFIType.void
    },
    webview_terminate: {
      args: [FFIType.ptr],
      returns: FFIType.void
    },
    webview_get_window: {
      args: [FFIType.ptr],
      returns: FFIType.ptr
    },
    webview_set_title: {
      args: [FFIType.ptr, FFIType.ptr],
      returns: FFIType.void
    },
    webview_set_size: {
      args: [FFIType.ptr, FFIType.i32, FFIType.i32, FFIType.i32],
      returns: FFIType.void
    },
    webview_navigate: {
      args: [FFIType.ptr, FFIType.ptr],
      returns: FFIType.void
    },
    webview_set_html: {
      args: [FFIType.ptr, FFIType.ptr],
      returns: FFIType.void
    },
    webview_init: {
      args: [FFIType.ptr, FFIType.ptr],
      returns: FFIType.void
    },
    webview_eval: {
      args: [FFIType.ptr, FFIType.ptr],
      returns: FFIType.void
    },
    webview_bind: {
      args: [FFIType.ptr, FFIType.ptr, FFIType.function, FFIType.ptr],
      returns: FFIType.void
    },
    webview_unbind: {
      args: [FFIType.ptr, FFIType.ptr],
      returns: FFIType.void
    },
    webview_return: {
      args: [FFIType.ptr, FFIType.ptr, FFIType.i32, FFIType.ptr],
      returns: FFIType.void
    }
  });
});

// runtime/webview-bun/src/webview.ts
import { CString, FFIType as FFIType2, JSCallback } from "bun:ffi";

class Webview {
  #handle = null;
  #callbacks = new Map;
  get unsafeHandle() {
    return this.#handle;
  }
  get unsafeWindowHandle() {
    return lib.symbols.webview_get_window(this.#handle);
  }
  set size({ width, height, hint }) {
    lib.symbols.webview_set_size(this.#handle, width, height, hint);
  }
  set title(title) {
    lib.symbols.webview_set_title(this.#handle, encodeCString(title));
  }
  constructor(debugOrHandle = false, size = {
    width: 1024,
    height: 768,
    hint: 0 /* NONE */
  }, window = null) {
    this.#handle = typeof debugOrHandle === "bigint" || typeof debugOrHandle === "number" ? debugOrHandle : lib.symbols.webview_create(Number(debugOrHandle), window);
    if (size !== undefined)
      this.size = size;
    instances.push(this);
  }
  destroy() {
    for (const callback of this.#callbacks.keys())
      this.unbind(callback);
    lib.symbols.webview_terminate(this.#handle);
    lib.symbols.webview_destroy(this.#handle);
    this.#handle = null;
  }
  navigate(url) {
    lib.symbols.webview_navigate(this.#handle, encodeCString(url));
  }
  setHTML(html) {
    lib.symbols.webview_set_html(this.#handle, encodeCString(html));
  }
  run() {
    lib.symbols.webview_run(this.#handle);
    this.destroy();
  }
  bindRaw(name, callback, arg = null) {
    const callbackResource = new JSCallback((seqPtr, reqPtr, arg2) => {
      const seq = seqPtr ? new CString(seqPtr) : "";
      const req = reqPtr ? new CString(reqPtr) : "";
      callback(seq, req, arg2);
    }, {
      args: [FFIType2.pointer, FFIType2.pointer, FFIType2.pointer],
      returns: FFIType2.void
    });
    this.#callbacks.set(name, callbackResource);
    lib.symbols.webview_bind(this.#handle, encodeCString(name), callbackResource.ptr, arg);
  }
  bind(name, callback) {
    this.bindRaw(name, (seq, req) => {
      const args = JSON.parse(req);
      let result;
      let success;
      try {
        result = callback(...args);
        success = true;
      } catch (err) {
        result = err;
        success = false;
      }
      if (result instanceof Promise) {
        result.then((r) => this.return(seq, success ? 0 : 1, JSON.stringify(r)));
      } else {
        this.return(seq, success ? 0 : 1, JSON.stringify(result));
      }
    });
  }
  unbind(name) {
    lib.symbols.webview_unbind(this.#handle, encodeCString(name));
    this.#callbacks.get(name)?.close();
    this.#callbacks.delete(name);
  }
  return(seq, status, result) {
    lib.symbols.webview_return(this.#handle, encodeCString(seq), status, encodeCString(result));
  }
  eval(source) {
    lib.symbols.webview_eval(this.#handle, encodeCString(source));
  }
  init(source) {
    lib.symbols.webview_init(this.#handle, encodeCString(source));
  }
}
var SizeHint;
var init_webview = __esm(() => {
  init_ffi();
  ((SizeHint2) => {
    SizeHint2[SizeHint2["NONE"] = 0] = "NONE";
    SizeHint2[SizeHint2["MIN"] = 1] = "MIN";
    SizeHint2[SizeHint2["MAX"] = 2] = "MAX";
    SizeHint2[SizeHint2["FIXED"] = 3] = "FIXED";
  })(SizeHint ||= {});
});

// runtime/webview-bun/src/index.ts
var exports_src = {};
__export(exports_src, {
  unload: () => unload,
  Webview: () => Webview,
  WEBVIEW_VERSION: () => WEBVIEW_VERSION,
  SizeHint: () => SizeHint
});
var WEBVIEW_VERSION = "0.12.0";
var init_src = __esm(() => {
  init_ffi();
  init_webview();
});

// runtime/ui-worker.ts
self.onmessage = async (event) => {
  const { url, dllPath, launcherPath } = event.data || {};
  try {
    if (!url || !dllPath)
      throw new Error("Windows \u8996\u7A97\u555F\u52D5\u53C3\u6578\u4E0D\u5B8C\u6574");
    process.env.WEBVIEW_PATH = String(dllPath);
    const { Webview: Webview2 } = await Promise.resolve().then(() => (init_src(), exports_src));
    const webview2 = new Webview2(false, {
      width: 960,
      height: 820,
      hint: 0
    });
    webview2.size = { width: 760, height: 600, hint: 1 };
    webview2.title = "Google Drive \u5F71\u7247\u4E0B\u8F09\u5668";
    if (process.platform === "win32" && launcherPath) {
      try {
        const { dlopen: dlopen2, FFIType: FFIType3, ptr: ptr2 } = await import("bun:ffi");
        const shell32 = dlopen2("shell32.dll", {
          ExtractIconExW: {
            args: [
              FFIType3.pointer,
              FFIType3.i32,
              FFIType3.pointer,
              FFIType3.pointer,
              FFIType3.u32
            ],
            returns: FFIType3.u32
          }
        });
        const user32 = dlopen2("user32.dll", {
          SendMessageW: {
            args: [FFIType3.pointer, FFIType3.u32, FFIType3.u64, FFIType3.u64],
            returns: FFIType3.u64
          }
        });
        const path = Buffer.from(`${launcherPath}\x00`, "utf16le");
        const large = Buffer.alloc(8);
        const small = Buffer.alloc(8);
        shell32.symbols.ExtractIconExW(ptr2(path), 0, ptr2(large), ptr2(small), 1);
        const hwnd = webview2.unsafeWindowHandle;
        const largeIcon = large.readBigUInt64LE();
        const smallIcon = small.readBigUInt64LE();
        if (largeIcon)
          user32.symbols.SendMessageW(hwnd, 128, 1n, largeIcon);
        if (smallIcon)
          user32.symbols.SendMessageW(hwnd, 128, 0n, smallIcon);
      } catch {}
    }
    webview2.navigate(String(url));
    postMessage({ type: "ready" });
    webview2.run();
    postMessage({ type: "closed" });
  } catch (error) {
    postMessage({
      type: "error",
      message: error instanceof Error ? `${error.message}
${error.stack || ""}` : String(error)
    });
  }
};
