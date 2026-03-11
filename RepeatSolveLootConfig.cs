namespace Loremaster;

// ─────────────────────────────────────────────────────────────────────────────
// Data model — mirrors the structure of RepeatSolveLoot.json
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A named group of equally-likely WCIDs sharing a single drop weight.
/// All groups (global and per-quest) are flattened into one pool at roll time.
/// </summary>
public class LootGroup
{
    public string Name    { get; set; } = "";
    public int    Weight  { get; set; } = 0;
    public List<uint> Wcids { get; set; } = new();
}

/// <summary>
/// The root structure of RepeatSolveLoot.json.
/// </summary>
public class RepeatSolveLootConfig
{
    /// <summary>Global loot groups used for all quests without an override.</summary>
    public List<LootGroup> Groups { get; set; } = new();

    /// <summary>
    /// Per-quest overrides. Key = quest internal name.
    /// When present, the quest's own Groups replace the global Groups entirely.
    /// </summary>
    public Dictionary<string, QuestLootOverride> QuestOverrides { get; set; } = new();
}

/// <summary>Per-quest loot definition used in QuestOverrides.</summary>
public class QuestLootOverride
{
    public List<LootGroup> Groups { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// Loader — reads RepeatSolveLoot.json and exposes the resolved flat pool
// ─────────────────────────────────────────────────────────────────────────────

public static class RepeatSolveLootLoader
{
    private static RepeatSolveLootConfig? _config;

    /// <summary>
    /// Loads (or reloads) RepeatSolveLoot.json from the mod's settings folder.
    /// Call this from PatchClass.OnWorldOpen so hot-reload picks it up.
    /// </summary>
    public static void Load(string modFolderPath)
    {
        var path = Path.Combine(modFolderPath, "RepeatSolveLoot.json");
        if (!File.Exists(path))
        {
            ModManager.Log($"[Loremaster] RepeatSolveLoot.json not found at {path} — repeat loot disabled.", ModManager.LogLevel.Warn);
            _config = null;
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            _config = System.Text.Json.JsonSerializer.Deserialize<RepeatSolveLootConfig>(json,
                new System.Text.Json.JsonSerializerOptions
                {
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true,
                });
            ModManager.Log($"[Loremaster] Loaded RepeatSolveLoot.json — {_config?.Groups.Count ?? 0} global group(s), {_config?.QuestOverrides.Count ?? 0} quest override(s).");
        }
        catch (Exception ex)
        {
            ModManager.Log($"[Loremaster] Failed to parse RepeatSolveLoot.json: {ex.Message}", ModManager.LogLevel.Error);
            _config = null;
        }
    }

    /// <summary>
    /// Resolves the flat weighted pool for the given quest name.
    /// Each entry in the returned list is a (wcid, weight) pair where weight
    /// is the group weight — all items within a group share that weight.
    ///
    /// Resolution order:
    ///   1. Quest has an entry in QuestOverrides → use that quest's Groups
    ///   2. Otherwise → use global Groups
    ///   3. Config not loaded or all groups have Weight 0 → empty list (no drop)
    /// </summary>
    public static List<(uint Wcid, int Weight)> GetPool(string questName)
    {
        if (_config is null) return new();

        var groups = _config.QuestOverrides.TryGetValue(questName, out var overrideEntry)
            ? overrideEntry.Groups
            : _config.Groups;

        var pool = new List<(uint Wcid, int Weight)>();
        foreach (var group in groups)
        {
            if (group.Weight <= 0) continue;
            foreach (var wcid in group.Wcids)
                pool.Add((wcid, group.Weight));
        }
        return pool;
    }
}
