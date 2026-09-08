import os

from waitress import serve

from bouncelab_maps import create_app


if __name__ == "__main__":
    serve(create_app(), host="127.0.0.1", port=int(os.getenv("BOUNCELAB_MAP_PORT", "8790")), threads=4)
