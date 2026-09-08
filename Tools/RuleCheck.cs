using System;
using BounceLab;

public static class RuleCheck
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Main()
    {
        var draft = MapRules.Blank();
        Check(MapRules.Validate(draft) == "", "Blank map");
        Check(MapRules.Validate(MapRules.Training()) == "", "Training map");
        Check(draft.tiles.Length == 160, "Map size");
        var invalid = draft.Clone();
        invalid.tiles[MapRules.Index(2, 1)] = MapRules.Empty;
        Check(MapRules.Validate(invalid) == "PLACE EXACTLY ONE START", "Start validation");
        Console.WriteLine("PASS: Bounce Lab map rules");
    }
}
