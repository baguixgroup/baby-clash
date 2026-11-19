#!/usr/bin/env python3
"""Serve the Baby Clash Web build on this Pi and open Chromium."""

import argparse
import functools
import http.server
import pathlib
import shutil
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=18765, help="Choose a localhost port (default: 18765)")
    parser.add_argument("--serve-only", action="store_true", help="Start the server without opening Chromium")
    args = parser.parse_args()

    root = pathlib.Path(__file__).resolve().parent
    if not (root / "index.html").is_file():
        parser.error("Place this launcher next to the extracted game's index.html.")

    browser = shutil.which("chromium-browser") or shutil.which("chromium")
    if not args.serve_only and not browser:
        parser.error("Chromium was not found. Use --serve-only to open the URL in your browser.")

    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(root))
    with http.server.ThreadingHTTPServer(("127.0.0.1", args.port), handler) as server:
        url = f"http://127.0.0.1:{server.server_port}/"
        print(f"Baby Clash is ready at {url}. Keep this terminal open while playing.", flush=True)
        if not args.serve_only:
            subprocess.Popen([browser, url])
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
