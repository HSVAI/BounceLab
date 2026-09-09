"""Deterministic completion check for curated Bounce Lab maps.

This mirrors ``BounceLabGame.Simulate`` closely enough to regression-test the
intended route at common frame rates.  It is deliberately not used to approve
community uploads: only a real clear in MAP MAKER unlocks that button.
"""

from math import floor

from . import HEIGHT, WIDTH


RADIUS = 0.33


def _tile(tiles, x, y):
    if x < 0 or x >= WIDTH or y >= HEIGHT:
        return 1
    if y < 0:
        return 0
    return tiles[y * WIDTH + x]


def _solid(tile):
    return tile in (1, 4)


def _move_towards(value, target, delta):
    if value < target:
        return min(value + delta, target)
    return max(value - delta, target)


def _frame(tiles, state, direction, dt):
    x, y, velocity_x, velocity_y = state
    velocity_x = _move_towards(velocity_x, direction * 4.7, 16 * dt)
    velocity_y -= 19 * dt

    next_x = x + velocity_x * dt
    if velocity_x > 0:
        wall_x = floor(next_x + RADIUS)
        if (_solid(_tile(tiles, wall_x, floor(y - RADIUS + 0.04))) or
                _solid(_tile(tiles, wall_x, floor(y + RADIUS - 0.04)))):
            next_x = wall_x - RADIUS
            velocity_x = 0
    elif velocity_x < 0:
        wall_x = floor(next_x - RADIUS)
        if (_solid(_tile(tiles, wall_x, floor(y - RADIUS + 0.04))) or
                _solid(_tile(tiles, wall_x, floor(y + RADIUS - 0.04)))):
            next_x = wall_x + 1 + RADIUS
            velocity_x = 0
    x = next_x

    next_y = y + velocity_y * dt
    landed_y = None
    if velocity_y <= 0:
        floor_y = floor(next_y - RADIUS)
        left_x = floor(x - RADIUS + 0.05)
        right_x = floor(x + RADIUS - 0.05)
        left_tile = _tile(tiles, left_x, floor_y)
        right_tile = _tile(tiles, right_x, floor_y)
        if _solid(left_tile) or _solid(right_tile):
            top = floor_y + 1
            if y - RADIUS >= top - 0.3:
                next_y = top + RADIUS
                velocity_y = 13.2 if 4 in (left_tile, right_tile) else 9.6
                landed_y = floor_y
    else:
        ceiling_y = floor(next_y + RADIUS)
        if (_solid(_tile(tiles, floor(x - RADIUS + 0.05), ceiling_y)) or
                _solid(_tile(tiles, floor(x + RADIUS - 0.05), ceiling_y))):
            next_y = ceiling_y - RADIUS
            velocity_y = 0
    y = next_y

    for tile_y in range(floor(y - RADIUS), floor(y + RADIUS) + 1):
        for tile_x in range(floor(x - RADIUS), floor(x + RADIUS) + 1):
            tile = _tile(tiles, tile_x, tile_y)
            if tile == 2:
                return None, "spike", landed_y
            if tile == 3:
                return None, "goal", landed_y
    if y < -0.5:
        return None, "fall", landed_y
    return (x, y, velocity_x, velocity_y), None, landed_y


def complete_route(tiles, route, dt=1 / 60, max_seconds=30):
    """Return elapsed seconds for a successful route, or raise AssertionError."""
    spawn = tiles.index(5)
    state = (spawn % WIDTH + 0.5, spawn // WIDTH + 0.5, 0.0, 8.8)
    route_index = 0

    for frame in range(int(max_seconds / dt)):
        platform_y, first_x, last_x, approach = route[min(route_index, len(route) - 1)]
        x, y, velocity_x, _ = state
        above_ledge = y - RADIUS >= platform_y + 1 - 0.03
        if above_ledge:
            target_x = (first_x + last_x + 1) / 2
            lead = 0.14
        else:
            target_x = first_x - RADIUS - 0.08 if approach == "L" else last_x + 1 + RADIUS + 0.08
            lead = 0.09
        error = target_x - (x + velocity_x * lead)
        direction = 1 if error > 0.07 else -1 if error < -0.07 else 0

        state, result, landed_y = _frame(tiles, state, direction, dt)
        if result == "goal":
            return (frame + 1) * dt
        if result:
            raise AssertionError(f"route hit {result} at step {route_index}")
        if landed_y == platform_y:
            route_index += 1

    raise AssertionError(f"route timed out after {max_seconds}s at step {route_index}")
