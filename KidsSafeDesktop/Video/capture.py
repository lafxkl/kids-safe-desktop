#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""用 CDP 驱动无头 Chrome 逐帧渲染 promo.html，输出 JPEG 序列。"""
import base64, json, os, subprocess, sys, time, urllib.request
import websocket

ROOT = os.path.dirname(os.path.abspath(__file__))
PAGE = "file:///" + os.path.join(ROOT, "promo.html").replace("\\", "/")
PORT = 9333
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
FPS = 30


def jget(url):
    return json.loads(urllib.request.urlopen(url, timeout=10).read().decode())


def start_chrome():
    prof = os.path.join(ROOT, "_chromeprofile")
    args = [CHROME, "--headless=new", f"--remote-debugging-port={PORT}",
            f"--user-data-dir={prof}", "--window-size=1920,1080",
            "--force-device-scale-factor=1", "--hide-scrollbars",
            "--allow-file-access-from-files", "--disable-extensions",
            "--disable-background-timer-throttling", "--no-first-run",
            "--force-color-profile=srgb", "--remote-allow-origins=*", "about:blank"]
    p = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    for _ in range(80):
        try:
            jget(f"http://127.0.0.1:{PORT}/json/version")
            return p
        except Exception:
            time.sleep(0.25)
    raise RuntimeError("chrome 启动超时")


class CDP:
    def __init__(self, ws_url):
        self.ws = websocket.create_connection(ws_url, timeout=60, max_size=64 * 1024 * 1024)
        self.i = 0

    def send(self, method, **params):
        self.i += 1
        mid = self.i
        self.ws.send(json.dumps({"id": mid, "method": method, "params": params}))
        while True:
            msg = json.loads(self.ws.recv())
            if msg.get("id") == mid:
                if "error" in msg:
                    raise RuntimeError(f"{method}: {msg['error']}")
                return msg.get("result", {})

    def eval(self, expr, await_promise=False):
        r = self.send("Runtime.evaluate", expression=expr, returnByValue=True,
                      awaitPromise=await_promise)
        return r.get("result", {}).get("value")


def main():
    total = float(sys.argv[1]) if len(sys.argv) > 1 else 46.4
    arg2 = sys.argv[2] if len(sys.argv) > 2 else "-"
    only = [float(x) for x in arg2.split(",")] if arg2 not in ("-", "", "all") else None
    mode = sys.argv[3] if len(sys.argv) > 3 and sys.argv[3] else "site"
    outdir = sys.argv[4] if len(sys.argv) > 4 and sys.argv[4] else "frames"
    FRAMES = os.path.join(ROOT, outdir)
    page = PAGE + ("?mode=" + mode if mode != "site" else "")
    os.makedirs(FRAMES, exist_ok=True)
    print("模式", mode, "→", page)

    proc = start_chrome()
    try:
        tabs = [t for t in jget(f"http://127.0.0.1:{PORT}/json/list") if t["type"] == "page"]
        cdp = CDP(tabs[0]["webSocketDebuggerUrl"])
        cdp.send("Page.enable")
        cdp.send("Runtime.enable")
        cdp.send("Emulation.setDeviceMetricsOverride", width=1920, height=1080,
                 deviceScaleFactor=1, mobile=False)
        cdp.send("Page.navigate", url=page)
        t0 = time.time()
        while time.time() - t0 < 30:
            if cdp.eval("window.READY===true"):
                break
            time.sleep(0.2)
        else:
            print("警告：图片可能未全部加载，继续")

        if only is not None:
            for t in only:
                cdp.eval(f"window.render({t})")
                shot = cdp.send("Page.captureScreenshot", format="jpeg", quality=92,
                                fromSurface=True, captureBeyondViewport=False)
                fn = os.path.join(FRAMES, f"test_{t:06.2f}.jpg")
                open(fn, "wb").write(base64.b64decode(shot["data"]))
                print("wrote", fn)
            return

        n = int(round(total * FPS))
        t_start = time.time()
        for i in range(n):
            t = i / FPS
            cdp.eval(f"window.render({t})")
            shot = cdp.send("Page.captureScreenshot", format="jpeg", quality=92,
                            fromSurface=True, captureBeyondViewport=False)
            open(os.path.join(FRAMES, f"f{i:05d}.jpg"), "wb").write(
                base64.b64decode(shot["data"]))
            if i % 60 == 0:
                el = time.time() - t_start
                eta = el / (i + 1) * (n - i - 1)
                print(f"{i}/{n}  {el:.0f}s  ETA {eta:.0f}s", flush=True)
        print("done", n, f"{time.time()-t_start:.0f}s")
    finally:
        proc.terminate()


if __name__ == "__main__":
    main()
