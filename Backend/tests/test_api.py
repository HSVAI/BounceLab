import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

from bouncelab_maps import HEIGHT, WIDTH, _default_maps, create_app


def valid_map():
    tiles = [0] * (WIDTH * HEIGHT)
    for x in range(WIDTH):
        tiles[x] = 1
    tiles[WIDTH + 2] = 5
    tiles[WIDTH * 3 + 7] = 3
    return {"name": "Test Map", "author": "Maker", "width": WIDTH, "height": HEIGHT, "tiles": tiles}


class ApiTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.app = create_app(Path(self.temp.name) / "test.db")
        self.client = self.app.test_client()

    def tearDown(self):
        self.temp.cleanup()

    def test_health_and_seed_maps(self):
        self.assertEqual(self.client.get("/health").status_code, 200)
        body = self.client.get("/api/maps").get_json()
        self.assertEqual(len(body["maps"]), 3)

    def test_official_maps_have_balanced_shapes(self):
        first, fork, tower = _default_maps()
        self.assertEqual([first[0], fork[0], tower[0]], ["첫 번째 바운스", "갈림길", "스프링 타워"])
        self.assertEqual(first[2].count(2), 8)
        self.assertEqual(first[2][14 * WIDTH + 6], 3)
        self.assertEqual(fork[2].count(2), 1)
        self.assertEqual(fork[2][WIDTH + 4], 1)
        self.assertEqual(tower[2].count(4), 6)
        self.assertEqual(tower[2][8 * WIDTH + 8], 0)

    def test_admin_can_hide_and_restore_user_map(self):
        with patch.dict("os.environ", {"BOUNCELAB_ADMIN_PASSWORD": "test-password",
                                        "BOUNCELAB_SESSION_SECRET": "test-session-secret",
                                        "BOUNCELAB_COOKIE_SECURE": "0"}):
            app = create_app(Path(self.temp.name) / "admin.db")
        client = app.test_client()
        created = client.post("/api/maps", json=valid_map()).get_json()["id"]
        self.assertEqual(client.get("/admin").status_code, 200)
        self.assertEqual(client.post("/admin", data={"password": "wrong"}).status_code, 401)
        self.assertEqual(client.post("/admin", data={"password": "test-password"}).status_code, 302)
        with client.session_transaction() as state:
            csrf = state["csrf"]
        self.assertEqual(client.post(f"/admin/maps/{created}/hide", data={"csrf": csrf}).status_code, 302)
        self.assertEqual(client.get(f"/api/maps/{created}").status_code, 404)
        self.assertEqual(client.post(f"/admin/maps/{created}/restore", data={"csrf": csrf}).status_code, 302)
        self.assertEqual(client.get(f"/api/maps/{created}").status_code, 200)

    def test_upload_round_trip_and_completion(self):
        created = self.client.post("/api/maps", json=valid_map())
        self.assertEqual(created.status_code, 201)
        map_id = created.get_json()["id"]
        fetched = self.client.get(f"/api/maps/{map_id}")
        self.assertEqual(fetched.get_json()["tiles"], valid_map()["tiles"])
        self.assertEqual(self.client.post(f"/api/maps/{map_id}/complete", json={"seconds": 8.2}).status_code, 200)

    def test_rejects_malformed_maps(self):
        data = valid_map()
        data["tiles"][WIDTH + 2] = 0
        self.assertEqual(self.client.post("/api/maps", json=data).status_code, 400)
        data = valid_map()
        data["name"] = "<script>"
        self.assertEqual(self.client.post("/api/maps", json=data).status_code, 400)

    def test_rate_limit(self):
        for index in range(6):
            data = valid_map()
            data["name"] = f"Map {index}"
            self.assertEqual(self.client.post("/api/maps", json=data).status_code, 201)
        self.assertEqual(self.client.post("/api/maps", json=valid_map()).status_code, 429)


if __name__ == "__main__":
    unittest.main()
