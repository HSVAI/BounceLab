import hashlib
import json
import os
import re
import secrets
import sqlite3
import time
from pathlib import Path

from flask import Flask, jsonify, request


WIDTH = 10
HEIGHT = 16
MAX_BODY = 24 * 1024
NAME_RE = re.compile(r"^[^<>\x00-\x1f]{1,28}$")


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
              [(x, 1) for x in range(4, 8)] + [(x, 3) for x in range(2, 5)] +
              [(x, 5) for x in range(5, 8)] + [(x, 7) for x in range(2, 6)] +
              [(x, 9) for x in range(5, 9)] + [(x, 11) for x in range(3, 7)],
              [(8, 1), (1, 3), (8, 5), (1, 7)], (2, 1), (5, 12), [(4, 7)]),
        level("갈림길", "SYSTEM",
              [(x, 1) for x in range(1, 5)] +
              [(x, 3) for x in range(1, 4)] + [(x, 3) for x in range(6, 9)] +
              [(x, 5) for x in range(3, 7)] +
              [(x, 7) for x in range(1, 4)] + [(x, 7) for x in range(6, 9)] +
              [(x, 9) for x in range(3, 7)] + [(x, 11) for x in range(5, 8)],
              [(4, 1), (3, 5), (8, 7), (6, 9)], (7, 1), (6, 12), [(7, 7)]),
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
    Path(app.config["DATABASE"]).parent.mkdir(parents=True, exist_ok=True)
    _initialize(app.config["DATABASE"])

    @app.after_request
    def cors(response):
        response.headers["Access-Control-Allow-Origin"] = "*"
        response.headers["Access-Control-Allow-Headers"] = "Content-Type"
        response.headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS"
        response.headers["Cache-Control"] = "no-store"
        response.headers["X-Content-Type-Options"] = "nosniff"
        return response

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
