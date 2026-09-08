using System;

namespace BounceLab
{
    [Serializable]
    public sealed class MapData
    {
        public string id = "";
        public string name = "UNTITLED";
        public string author = "MAKER";
        public int width = MapRules.Width;
        public int height = MapRules.Height;
        public int[] tiles = new int[MapRules.Width * MapRules.Height];
        public int plays;
        public int completions;
        public long createdAt;

        public MapData Clone()
        {
            var copy = (MapData)MemberwiseClone();
            copy.tiles = (int[])tiles.Clone();
            return copy;
        }
    }

    [Serializable] public sealed class MapListResponse { public MapData[] maps; }
    [Serializable] public sealed class ApiConfig { public string baseUrl; }
    [Serializable] public sealed class UploadResponse { public bool ok; public string id; public string error; }

    public static class MapRules
    {
        public const int Width = 10;
        public const int Height = 16;
        public const int Empty = 0;
        public const int Block = 1;
        public const int Spike = 2;
        public const int Goal = 3;
        public const int Spring = 4;
        public const int Spawn = 5;

        public static int Index(int x, int y) { return y * Width + x; }
        public static bool Inside(int x, int y) { return x >= 0 && x < Width && y >= 0 && y < Height; }
        public static int Tile(MapData map, int x, int y)
        {
            if (!Inside(x, y)) return Block;
            return map.tiles[Index(x, y)];
        }
        public static bool IsSolid(int tile) { return tile == Block || tile == Spring; }

        public static string Validate(MapData map)
        {
            if (map == null || map.width != Width || map.height != Height || map.tiles == null || map.tiles.Length != Width * Height)
                return "MAP MUST BE 10 x 16";
            int starts = 0, goals = 0, solids = 0;
            for (int i = 0; i < map.tiles.Length; i++)
            {
                int tile = map.tiles[i];
                if (tile < Empty || tile > Spawn) return "UNKNOWN TILE";
                if (tile == Spawn) starts++;
                if (tile == Goal) goals++;
                if (IsSolid(tile)) solids++;
            }
            if (starts != 1) return "PLACE EXACTLY ONE START";
            if (goals != 1) return "PLACE EXACTLY ONE GOAL";
            if (solids < Width) return "ADD MORE PLATFORMS";
            return "";
        }

        public static MapData Blank()
        {
            var map = new MapData { name = "MY FIRST MAP", author = "MAKER" };
            for (int x = 0; x < Width; x++) map.tiles[Index(x, 0)] = Block;
            for (int y = 0; y < Height; y++)
            {
                map.tiles[Index(0, y)] = Block;
                map.tiles[Index(Width - 1, y)] = Block;
            }
            map.tiles[Index(2, 1)] = Spawn;
            map.tiles[Index(7, 3)] = Goal;
            for (int x = 5; x <= 8; x++) map.tiles[Index(x, 2)] = Block;
            return map;
        }

        public static MapData Training()
        {
            var map = Blank();
            map.id = "training";
            map.name = "BOUNCE SCHOOL";
            map.author = "BOUNCE LAB";
            Array.Clear(map.tiles, 0, map.tiles.Length);
            for (int x = 0; x < Width; x++) map.tiles[Index(x, 0)] = Block;
            for (int y = 0; y < Height; y++)
            {
                map.tiles[Index(0, y)] = Block;
                map.tiles[Index(Width - 1, y)] = Block;
            }
            map.tiles[Index(2, 1)] = Spawn;
            map.tiles[Index(4, 1)] = Spike;
            map.tiles[Index(5, 1)] = Spike;
            Platform(map, 1, 4, 2);
            Platform(map, 5, 8, 4);
            Platform(map, 2, 6, 6);
            Platform(map, 6, 8, 8);
            Platform(map, 2, 5, 10);
            Platform(map, 5, 8, 12);
            map.tiles[Index(7, 13)] = Goal;
            map.tiles[Index(8, 8)] = Spring;
            return map;
        }

        private static void Platform(MapData map, int from, int to, int y)
        {
            for (int x = from; x <= to; x++) map.tiles[Index(x, y)] = Block;
        }
    }
}
