# Bounce Lab Maps API

Anonymous community-map storage for the Bounce Lab game. It is a separate service from PlaylistServer.

- Fixed 10 × 16 maps, one spawn and one goal.
- SQLite WAL storage and 10 seeded official maps verified against the client physics at 50, 60 and 75 FPS.
- Request-size, schema, name and per-IP upload-rate limits.
- Public map list, map fetch and completion counters.
- CORS enabled for the native game and GitHub Pages tooling.
- Password-protected `/admin` console for reversible map hiding and restoring.

Set `BOUNCELAB_ADMIN_PASSWORD` and `BOUNCELAB_SESSION_SECRET` in the service environment.
The stable <https://hsvai.github.io/BounceLab/admin/> page resolves the current tunnel and opens the console.
