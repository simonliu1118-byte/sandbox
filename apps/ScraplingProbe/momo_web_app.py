"""Local-only interactive momo keyword search prototype.

Run: python apps/ScraplingProbe/momo_web_app.py
Open: http://127.0.0.1:8765

The server binds loopback only. It never stores credentials or starts polling.
"""
import argparse
import time
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import parse_qs, urlparse

from keyword_search import keyword_value
from momo_search_data import fetch_keyword
from momo_ui import render_page

CACHE_SECONDS = 600


class SearchHandler(BaseHTTPRequestHandler):
    cache = {}

    def do_GET(self):
        target = urlparse(self.path)
        if target.path not in ("/", "/search"):
            self.send_error(404, "Not Found")
            return
        query = parse_qs(target.query).get("keyword", [""])[0]
        report, message = None, ""
        if query.strip():
            try:
                term = keyword_value(query)
            except ValueError:
                message = "請輸入 2 至 80 個字元的關鍵字"
                term = query[:80]
            else:
                cached = self.cache.get(term)
                now = time.monotonic()
                if cached and now - cached[0] < CACHE_SECONDS:
                    report = cached[1]
                else:
                    report = fetch_keyword(term, 20)
                    self.cache[term] = (now, report)
            query = term
        html = render_page(query, report, message)
        body = html.encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Security-Policy",
                         "default-src 'none'; style-src 'unsafe-inline'; "
                         "form-action 'self'; base-uri 'none'")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format_string, *args):
        # Do not log search keywords to the terminal by default.
        return


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("--port", type=int, default=8765)
    args = cli.parse_args()
    if not 1024 <= args.port <= 65535:
        cli.error("port must be between 1024 and 65535")
    server = HTTPServer(("127.0.0.1", args.port), SearchHandler)
    print(f"momo keyword search prototype: http://127.0.0.1:{args.port}")
    print("Press Ctrl+C to stop. Only this computer can access the prototype.")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
