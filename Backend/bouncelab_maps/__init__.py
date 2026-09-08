import hashlib
import hmac
import json
import os
import re
import secrets
import sqlite3
import time
from datetime import datetime
from pathlib import Path

from flask import Flask, jsonify, redirect, render_template_string, request, session, url_for


WIDTH = 10
HEIGHT = 16
MAX_BODY = 24 * 1024
NAME_RE = re.compile(r"^[^<>\x00-\x1f]{1,28}$")

ADMIN_LOGIN = """<!doctype html><html lang="ko"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Bounce Lab Admin</title>
<style>body{margin:0;background:#07101c;color:#eef5fb;font:16px system-ui}main{max-width:420px;margin:12vh auto;padding:28px}h1{font-size:34px}p{color:#98aabd}input,button{width:100%;padding:15px;margin-top:12px;border:0;border-radius:8px;font:inherit}input{background:#111d2d;color:white}button{background:#39ffc4;color:#05251c;font-weight:800}.error{color:#ff667e}</style>
<main><h1>BOUNCE LAB ADMIN</h1><p>관리자 비밀번호를 입력하세요.</p>{% if error %}<p class="error">{{ error }}</p>{% endif %}
<form method="post"><input name="password" type="password" autocomplete="current-password" required autofocus><button>로그인</button></form></main></html>"""

ADMIN_CONSOLE = """<!doctype html><html lang="ko"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Bounce Lab Admin</title>
<style>body{margin:0;background:#07101c;color:#eef5fb;font:15px system-ui}main{max-width:920px;margin:auto;padding:30px 18px}header{display:flex;align-items:center;justify-content:space-between}h1{font-size:30px}table{width:100%;border-collapse:collapse;background:#0d1828}th,td{padding:12px 10px;border-bottom:1px solid #22344b;text-align:left}th{color:#8398af;font-size:12px}button{border:0;border-radius:6px;padding:9px 13px;font-weight:750;background:#ff546d;color:white}.restore{background:#39ffc4;color:#05251c}.muted{color:#8295ab}.hidden{opacity:.58}@media(max-width:700px){.wide{display:none}th,td{padding:10px 6px;font-size:12px}}</style>
<main><header><h1>MAP ADMIN</h1><form method="post" action="{{ url_for('admin_logout') }}"><input type="hidden" name="csrf" value="{{ csrf }}"><button>로그아웃</button></form></header>
<p class="muted">삭제 대신 숨김 처리하므로 언제든 복구할 수 있습니다.</p><table><thead><tr><th>맵</th><th>제작자</th><th class="wide">업로드</th><th>기록</th><th>상태</th><th></th></tr></thead><tbody>
{% for map in maps %}<tr class="{% if map.hidden %}hidden{% endif %}"><td>{{ map.name }}<br><span class="muted">{{ map.id }}</span></td><td>{{ map.author }}</td><td class="wide">{{ map.uploaded }}</td><td>{{ map.plays }}회 / {{ map.completions }}클리어</td><td>{% if map.hidden %}숨김{% else %}공개{% endif %}</td><td>{% if not map.official %}<form method="post" action="{{ url_for('admin_map_action', map_id=map.id, action='restore' if map.hidden else 'hide') }}"><input type="hidden" name="csrf" value="{{ csrf }}"><button class="{% if map.hidden %}restore{% endif %}">{% if map.hidden %}복구{% else %}숨기기{% endif %}</button></form>{% else %}<span class="muted">SYSTEM</span>{% endif %}</td></tr>{% endfor %}
</tbody></table></main></html>"""


def _default_maps():
    def level(name, author, platforms, spikes, spawn, goal, pads=()):
        tiles = [0] * (WIDTH * HEIGHT)
        for x in range(WIDTH):
            tiles[x] = 1
        for y in range(HEIGHT):
            tiles[y * WIDTH] = tiles[y * WIDTH + WIDTH - 1] = 1
        for x, y in platforms:
            tiles[y * WIDTH + x] = 1
        for x, y in spikes:
            tiles[y * WIDTH + x] = 2
        for x, y in pads:
            tiles[y * WIDTH + x] = 4
        tiles[spawn[1] * WIDTH + spawn[0]] = 5
        tiles[goal[1] * WIDTH + goal[0]] = 3
        return name, author, tiles

    return [
        level("첫 번째 바운스", "SYSTEM",
              [(x, 1) for x in range(4, 7)] + [(x, 3) for x in range(2, 5)] +
              [(x, 5) for x in range(5, 8)] + [(x, 7) for x in range(2, 5)] +
              [(x, 9) for x in range(5, 8)] + [(x, 11) for x in range(2, 5)] +
              [(x, 13) for x in range(5, 8)],
              [(1, 1), (7, 1), (1, 3), (8, 5), (1, 7), (8, 9), (1, 11), (8, 13)],
              (2, 1), (6, 14)),
        level("갈림길", "SYSTEM",
              [(x, 1) for x in range(1, 5)] +
              [(x, 3) for x in range(1, 4)] + [(x, 3) for x in range(6, 9)] +
              [(x, 5) for x in range(3, 7)] +
              [(x, 7) for x in range(1, 4)] + [(x, 7) for x in range(6, 9)] +
              [(x, 9) for x in range(3, 7)] + [(x, 11) for x in range(5, 8)],
              [(8, 1)], (7, 1), (6, 12), [(7, 7)]),
        level("스프링 타워", "SYSTEM",
              [(x, 1) for x in range(5, 9)] + [(x, 4) for x in range(2, 7)] +
              [(x, 7) for x in range(6, 9)] + [(x, 10) for x in range(2, 7)],
              [(1, 1)], (2, 1), (5, 11),
              [(5, 1), (6, 1), (5, 4), (6, 4), (6, 7), (7, 7)]),
    ]


def create_app(database_path=None):
    app = Flask(__name__)
    app.config["MAX_CONTENT_LENGTH"] = MAX_BODY
    app.config["DATABASE"] = str(database_path or os.getenv(
        "BOUNCELAB_MAP_DB", "/home/ghtnql/BounceLab/Backend/data/maps.db"))
    app.config["ADMIN_PASSWORD"] = os.getenv("BOUNCELAB_ADMIN_PASSWORD", "")
    app.secret_key = os.getenv("BOUNCELAB_SESSION_SECRET") or secrets.token_hex(32)
    app.config.update(SESSION_COOKIE_HTTPONLY=True, SESSION_COOKIE_SAMESITE="Strict",
                      SESSION_COOKIE_SECURE=os.getenv("BOUNCELAB_COOKIE_SECURE", "1") != "0")
    Path(app.config["DATABASE"]).parent.mkdir(parents=True, exist_ok=True)
    _initialize(app.config["DATABASE"])

    @app.after_request
    def cors(response):
        response.headers["Access-Control-Allow-Origin"] = "*"
        response.headers["Access-Control-Allow-Headers"] = "Content-Type"
        response.headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS"
        response.headers["Cache-Control"] = "no-store"
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["X-Frame-Options"] = "DENY"
        return response

    @app.route("/admin", methods=["GET", "POST"])
    def admin_console():
        password = app.config["ADMIN_PASSWORD"]
        if not password:
            return "Admin console is not configured", 503
        error = ""
        if request.method == "POST" and not session.get("admin"):
            supplied = request.form.get("password", "")
            if hmac.compare_digest(supplied, password):
                session.clear()
                session["admin"] = True
                session["csrf"] = secrets.token_urlsafe(24)
                return redirect(url_for("admin_console"))
            error = "비밀번호가 맞지 않습니다."
        if not session.get("admin"):
            return render_template_string(ADMIN_LOGIN, error=error), 401 if error else 200
        with _connect(app.config["DATABASE"]) as db:
            rows = db.execute("SELECT * FROM maps ORDER BY created_at DESC").fetchall()
        maps = []
        for row in rows:
            item = dict(row)
            item["uploaded"] = datetime.fromtimestamp(row["created_at"]).astimezone().strftime("%Y-%m-%d %H:%M:%S")
            item["official"] = row["id"].startswith("official-")
            maps.append(item)
        return render_template_string(ADMIN_CONSOLE, maps=maps, csrf=session["csrf"])

    @app.post("/admin/maps/<map_id>/<action>")
    def admin_map_action(map_id, action):
        if not session.get("admin"):
            return "Forbidden", 403
        if not hmac.compare_digest(request.form.get("csrf", ""), session.get("csrf", "")):
            return "Invalid CSRF token", 403
        if action not in ("hide", "restore"):
            return "Unknown action", 400
        hidden = 1 if action == "hide" else 0
        with _connect(app.config["DATABASE"]) as db:
            db.execute("UPDATE maps SET hidden = ? WHERE id = ? AND id NOT LIKE 'official-%'", (hidden, map_id))
        return redirect(url_for("admin_console"))

    @app.post("/admin/logout")
    def admin_logout():
        if not hmac.compare_digest(request.form.get("csrf", ""), session.get("csrf", "")):
            return "Invalid CSRF token", 403
        session.clear()
        return redirect(url_for("admin_console"))

    @app.route("/api/<path:_>", methods=["OPTIONS"])
    def options(_):
        return ("", 204)

    @app.get("/health")
    def health():
        with _connect(app.config["DATABASE"]) as db:
            count = db.execute("SELECT count(*) FROM maps WHERE hidden = 0").fetchone()[0]
        return jsonify(ok=True, maps=count, version=1)

    @app.get("/api/maps")
    def list_maps():
        limit = max(1, min(20, request.args.get("limit", 10, type=int)))
        with _connect(app.config["DATABASE"]) as db:
            rows = db.execute(
                "SELECT * FROM maps WHERE hidden = 0 ORDER BY created_at DESC LIMIT ?", (limit,)).fetchall()
        return jsonify(maps=[_public_map(row) for row in rows])

    @app.get("/api/maps/<map_id>")
    def get_map(map_id):
        with _connect(app.config["DATABASE"]) as db:
            row = db.execute("SELECT * FROM maps WHERE id = ? AND hidden = 0", (map_id,)).fetchone()
            if row is None:
                return jsonify(error="map_not_found"), 404
            db.execute("UPDATE maps SET plays = plays + 1 WHERE id = ?", (map_id,))
        result = _public_map(row)
        result["plays"] += 1
        return jsonify(result)

    @app.post("/api/maps")
    def upload_map():
        payload = request.get_json(silent=True)
        if not isinstance(payload, dict):
            return jsonify(error="invalid_json"), 400
        error, clean = _validate(payload)
        if error:
            return jsonify(error=error), 400
        client = _client_hash(request)
        now = int(time.time())
        with _connect(app.config["DATABASE"]) as db:
            recent_hour = db.execute(
                "SELECT count(*) FROM uploads WHERE client_hash = ? AND created_at > ?", (client, now - 3600)).fetchone()[0]
            recent_day = db.execute(
                "SELECT count(*) FROM uploads WHERE client_hash = ? AND created_at > ?", (client, now - 86400)).fetchone()[0]
            if recent_hour >= 6 or recent_day >= 20:
                return jsonify(error="rate_limited"), 429
            map_id = secrets.token_urlsafe(7).replace("-", "a").replace("_", "b")
            db.execute(
                "INSERT INTO maps(id,name,author,width,height,tiles,created_at) VALUES(?,?,?,?,?,?,?)",
                (map_id, clean["name"], clean["author"], WIDTH, HEIGHT,
                 json.dumps(clean["tiles"], separators=(",", ":")), now))
            db.execute("INSERT INTO uploads(client_hash,created_at) VALUES(?,?)", (client, now))
        return jsonify(id=map_id, ok=True), 201

    @app.post("/api/maps/<map_id>/complete")
    def complete_map(map_id):
        payload = request.get_json(silent=True) or {}
        try:
            seconds = float(payload.get("seconds", 0))
        except (TypeError, ValueError):
            seconds = 0
        if seconds < 0.5 or seconds > 3600:
            return jsonify(error="invalid_time"), 400
        with _connect(app.config["DATABASE"]) as db:
            found = db.execute("SELECT 1 FROM maps WHERE id = ? AND hidden = 0", (map_id,)).fetchone()
            if not found:
                return jsonify(error="map_not_found"), 404
            db.execute("UPDATE maps SET completions = completions + 1 WHERE id = ?", (map_id,))
        return jsonify(ok=True)

    return app


def _connect(path):
    db = sqlite3.connect(path, timeout=10)
    db.row_factory = sqlite3.Row
    db.execute("PRAGMA journal_mode=WAL")
    return db


def _initialize(path):
    with _connect(path) as db:
        db.executescript("""
            CREATE TABLE IF NOT EXISTS maps (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, author TEXT NOT NULL,
                width INTEGER NOT NULL, height INTEGER NOT NULL, tiles TEXT NOT NULL,
                created_at INTEGER NOT NULL, plays INTEGER NOT NULL DEFAULT 0,
                completions INTEGER NOT NULL DEFAULT 0, hidden INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS uploads (client_hash TEXT NOT NULL, created_at INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_uploads_client_time ON uploads(client_hash, created_at);
        """)
        for index, (name, author, tiles) in enumerate(_default_maps(), 1):
            db.execute(
                """INSERT INTO maps(id,name,author,width,height,tiles,created_at) VALUES(?,?,?,?,?,?,?)
                   ON CONFLICT(id) DO UPDATE SET name=excluded.name, author=excluded.author,
                     width=excluded.width, height=excluded.height, tiles=excluded.tiles""",
                (f"official-{index}", name, author, WIDTH, HEIGHT,
                 json.dumps(tiles, separators=(",", ":")), int(time.time()) - (4-index)))


def _validate(payload):
    name = str(payload.get("name", "")).strip()
    author = str(payload.get("author", "")).strip()
    tiles = payload.get("tiles")
    if not NAME_RE.fullmatch(name):
        return "invalid_name", None
    if not NAME_RE.fullmatch(author):
        return "invalid_author", None
    if payload.get("width") != WIDTH or payload.get("height") != HEIGHT:
        return "invalid_dimensions", None
    if not isinstance(tiles, list) or len(tiles) != WIDTH * HEIGHT:
        return "invalid_tiles", None
    if any(type(tile) is not int or tile < 0 or tile > 5 for tile in tiles):
        return "invalid_tile", None
    if tiles.count(5) != 1 or tiles.count(3) != 1:
        return "need_one_spawn_and_goal", None
    if sum(tile in (1, 4) for tile in tiles) < WIDTH:
        return "not_enough_platforms", None
    return None, {"name": name, "author": author, "tiles": tiles}


def _public_map(row):
    return {
        "id": row["id"], "name": row["name"], "author": row["author"],
        "width": row["width"], "height": row["height"], "tiles": json.loads(row["tiles"]),
        "plays": row["plays"], "completions": row["completions"], "createdAt": row["created_at"],
    }


def _client_hash(req):
    forwarded = req.headers.get("CF-Connecting-IP") or req.headers.get("X-Forwarded-For", "")
    address = forwarded.split(",")[0].strip() or req.remote_addr or "unknown"
    salt = os.getenv("BOUNCELAB_HASH_SALT", "bouncelab-local-rate-limit")
    return hashlib.sha256((salt + address).encode()).hexdigest()
