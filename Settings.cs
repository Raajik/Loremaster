namespace Loremaster;

public class Settings
{
    // ─────────────────────────────────────────────────────────────────────────
    // QUEST POINT SYSTEM (inherited from QuestBonus)
    // ─────────────────────────────────────────────────────────────────────────

    // XP multiplier per Quest Point (as a percentage).
    // Formula: 1 + (QP * BonusPerQuestPoint / 100)
    // Default (0.5): 100 QP → 1.5x, 200 QP → 2.0x, 50 QP → 1.25x
    public float BonusPerQuestPoint { get; set; } = 3.0f;

    // QP awarded for any quest not listed in QuestBonuses.
    // Set to 0 to only reward explicitly listed quests.
    public float DefaultPoints { get; set; } = 1;

    // Per-quest QP overrides. Keys are case-sensitive internal quest names (see Quests.txt).
    // Value 0 = tracked but awards no QP. Unlisted quests use DefaultPoints.
    public Dictionary<string, float> QuestBonuses { get; set; } = new()
    {
        ["PathwardenComplete"]        = 1,
        ["PathwardenFound1111"]       = 1,
        ["StipendsCollectedInAMonth"] = 1,
        ["StipendTimer_08"]           = 1,
        ["StipendTimer_Monthly"]      = 1,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // ACCOUNT-WIDE QUEST TRACKING
    // ─────────────────────────────────────────────────────────────────────────

    // When true, QP and the XP multiplier are calculated from unique quests solved
    // across ALL characters on the account, not just the logged-in character.
    public bool UseAccountWideQuests { get; set; } = true;

    // ─────────────────────────────────────────────────────────────────────────
    // ONE-TIME COMPLETION BONUS XP
    // ─────────────────────────────────────────────────────────────────────────

    // Grant a one-time XP bonus on a quest's first solve. Stacks with the ongoing multiplier.
    // Amount = DefaultCompletionBonusXpMultiplier × (XP needed for player's next level).
    public bool EnableCompletionBonusXp { get; set; } = true;

    // Multiplier applied to (XP needed for next level) for the first-solve bonus.
    // 1.5 = 150% of current level-up cost. Set to 0 to rely solely on CompletionBonusXpOverrides.
    // Example: level 100 needs ~5M XP to level → 1.5 × 5M = 7.5M bonus XP.
    public float DefaultCompletionBonusXpMultiplier { get; set; } = 0.5f;

    // Per-quest multiplier overrides for the first-solve XP bonus (case-sensitive, see Quests.txt).
    // Same unit as DefaultCompletionBonusXpMultiplier. Set to 0.0 to suppress for a specific quest.
    public Dictionary<string, float> CompletionBonusXpOverrides { get; set; } = new()
    {
        ["PathwardenComplete"]        = 1.0f,
        ["PathwardenFound1111"]       = 1.0f,
        ["StipendsCollectedInAMonth"] = 1.0f,
        ["StipendTimer_08"]           = 1.0f,
        ["StipendTimer_Monthly"]      = 1.0f,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // REPEAT SOLVE BONUS LOOT
    // ─────────────────────────────────────────────────────────────────────────

    // Award one weighted-random item on every repeat solve (2nd+). First solves are not affected.
    // Loot tables are configured in RepeatSolveLoot.json in the mod folder.
    public bool EnableRepeatSolveLoot { get; set; } = true;

    // ─────────────────────────────────────────────────────────────────────────
    // MILESTONE BROADCASTS
    // ─────────────────────────────────────────────────────────────────────────

    // Broadcast a server-wide message when an account hits a milestone unique-quest count.
    public bool EnableMilestoneBroadcasts { get; set; } = true;

    // Account-wide unique quest counts that trigger a broadcast. Add or remove freely.
    public List<int> MilestoneThresholds { get; set; } = new()
    {
        25, 50, 100, 250, 500, 750, 1000,
        1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000,
        5500, 6000, 6500, 7000, 7500, 8000, 8500, 9000, 9500, 10000
    };

    // Bonus QP granted to the player when they hit each milestone. Key = threshold.
    // Missing entries award no bonus QP but still trigger the broadcast.
    public Dictionary<int, float> MilestoneBonusQP { get; set; } = new()
    {
        [25]   = 5,  [50]   = 5,  [100]  = 5,
        [250]  = 10, [500]  = 10, [750]  = 10, [1000] = 10,
        [1500] = 10, [2000] = 10, [2500] = 10, [3000] = 10, [3500] = 10,
        [4000] = 10, [4500] = 10, [5000] = 10, [5500] = 10, [6000] = 10,
        [6500] = 10, [7000] = 10, [7500] = 10, [8000] = 10, [8500] = 10,
        [9000] = 10, [9500] = 10, [10000] = 10,
    };

    // Broadcast message format.
    // {0} = character name, {1} = ordinal milestone (e.g. "50th"), {2} = bonus QP awarded.
    public string MilestoneBroadcastFormat { get; set; } =
        "[Loremaster] {0} has just completed their {1} unique quest and earned {2} bonus quest points!";
}
