"""Run the local controller feedback editor: python tools/combat/gamepad_feedback_tuner.py"""
import argparse
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import tempfile
import threading
import webbrowser

import export_gamepad_feedback as catalog

PAGE = Path(__file__).with_suffix(".html")


class ConflictError(ValueError):
    pass


class ProfileStore:
    def __init__(self, source=catalog.SOURCE, target=catalog.TARGET):
        self.source, self.target = Path(source), Path(target)
        self.lock = threading.Lock()

    def read(self):
        with self.lock:
            return self._read()

    def _read(self):
        raw = self.source.read_bytes()
        profile = catalog.validate(json.loads(raw.decode("utf-8-sig")))
        generated = catalog.serialize(profile)
        return {"profile": profile, "revision": hashlib.sha256(raw).hexdigest(),
                "catalogMatches": self.target.exists() and self.target.read_text(encoding="utf-8-sig") == generated}

    def save(self, profile, revision):
        raw = catalog.serialize(profile).encode("utf-8")
        with self.lock:
            if self._read()["revision"] != revision:
                raise ConflictError("다른 곳에서 원본이 변경됐습니다. 편집한 JSON을 내려받고 원본을 다시 불러오세요.")
            old_source = self.source.read_bytes()
            source_temp = self._stage(self.source, raw)
            target_temp = None
            try:
                target_temp = self._stage(self.target, raw)
                # Recheck after staging; an external editor may have saved meanwhile.
                if hashlib.sha256(self.source.read_bytes()).hexdigest() != revision:
                    raise ConflictError("원본이 변경되어 저장을 중단했습니다. 원본을 다시 불러오세요.")
                os.replace(source_temp, self.source)
                try:
                    os.replace(target_temp, self.target)
                except OSError:
                    # Do not roll back over a newer edit made outside this process.
                    if self.source.read_bytes() == raw:
                        rollback = self._stage(self.source, old_source)
                        os.replace(rollback, self.source)
                    raise
            finally:
                for path in (source_temp, target_temp):
                    if path is not None:
                        path.unlink(missing_ok=True)
            return self._read()

    @staticmethod
    def _stage(target, raw):
        target.parent.mkdir(parents=True, exist_ok=True)
        stream = tempfile.NamedTemporaryFile(dir=target.parent, prefix=target.name + ".", suffix=".tmp", delete=False)
        path = Path(stream.name)
        try:
            with stream:
                stream.write(raw)
                stream.flush()
                os.fsync(stream.fileno())
        except BaseException:
            path.unlink(missing_ok=True)
            raise
        return path


def handler_for(store):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, message, *args):
            print(message % args, flush=True)

        def same_host(self):
            return self.headers.get("Host") in (f"127.0.0.1:{self.server.server_port}", f"localhost:{self.server.server_port}")

        def respond(self, status, value, content_type="application/json; charset=utf-8"):
            raw = value.encode("utf-8") if isinstance(value, str) else json.dumps(value, ensure_ascii=False, allow_nan=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(raw)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.end_headers()
            self.wfile.write(raw)

        def do_GET(self):
            if not self.same_host():
                return self.respond(403, {"error": "로컬 주소로 접속하세요."})
            try:
                if self.path == "/":
                    self.respond(200, PAGE.read_text(encoding="utf-8"), "text/html; charset=utf-8")
                elif self.path == "/api/profile":
                    self.respond(200, store.read())
                elif self.path == "/favicon.ico":
                    self.respond(204, "", "image/x-icon")
                else:
                    self.respond(404, {"error": "찾을 수 없는 경로입니다."})
            except (OSError, ValueError) as error:
                self.respond(500, {"error": str(error)})

        def do_POST(self):
            origins = (f"http://127.0.0.1:{self.server.server_port}", f"http://localhost:{self.server.server_port}")
            if not self.same_host() or self.headers.get("Origin") not in origins:
                return self.respond(403, {"error": "같은 로컬 편집기에서만 저장할 수 있습니다."})
            if self.path != "/api/profile":
                return self.respond(404, {"error": "찾을 수 없는 경로입니다."})
            if self.headers.get_content_type() != "application/json":
                return self.respond(415, {"error": "JSON 데이터가 필요합니다."})
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if not 0 < length <= 65536:
                    return self.respond(413, {"error": "프로필 데이터 크기가 올바르지 않습니다."})
                request = json.loads(self.rfile.read(length))
                if not isinstance(request, dict) or set(request) != {"profile", "revision"} or not isinstance(request["revision"], str):
                    raise ValueError("프로필과 원본 버전이 필요합니다.")
                self.respond(200, store.save(request["profile"], request["revision"]))
            except ConflictError as error:
                self.respond(409, {"error": str(error)})
            except (ValueError, TypeError) as error:
                self.respond(400, {"error": str(error)})
            except OSError as error:
                self.respond(500, {"error": "파일 저장에 실패했습니다: " + str(error)})

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8766, help="Local port (default: 8766; 0 selects a free port)")
    parser.add_argument("--no-browser", action="store_true")
    args = parser.parse_args()
    store = ProfileStore()
    store.read()  # Report an invalid source before opening a browser.
    with ThreadingHTTPServer(("127.0.0.1", args.port), handler_for(store)) as server:
        url = f"http://127.0.0.1:{server.server_port}/"
        print("Gamepad feedback tuner: " + url, flush=True)
        print("Ctrl+C closes the editor server.", flush=True)
        if not args.no_browser:
            webbrowser.open(url)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
