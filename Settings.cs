namespace Loremaster;

public class Settings
{
    // ─────────────────────────────────────────────────────────────────────────
    // QUEST POINT SYSTEM (inherited from QuestBonus)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// XP bonus granted per Quest Point, expressed as a percentage.
    /// Formula: multiplier = 1 + (QuestPoints * BonusPerQuestPoint / 100)
    /// Example at default (0.5): 100 QP → 1 + (100 * 0.5 / 100) = 1.5x XP multiplier
    ///                           200 QP → 2.0x, 50 QP → 1.25x
    /// </summary>
    public float BonusPerQuestPoint { get; set; } = 0.5f;

    /// <summary>
    /// Default Quest Point value awarded for completing any quest not listed in QuestBonuses.
    /// Set to 0 to only reward quests explicitly listed below.
    /// </summary>
    public float DefaultPoints { get; set; } = 1;

    /// <summary>
    /// Notify the player when their Quest Point total changes (quest complete/removed).
    /// </summary>
    public bool NotifyQuest { get; set; } = true;

    /// <summary>
    /// Notify the player when XP is boosted by their quest bonus multiplier.
    /// Verbose — recommended off in production.
    /// </summary>
    public bool NotifyExp { get; set; } = false;

    /// <summary>
    /// Per-quest Quest Point overrides. Use the quest's internal name (case-sensitive).
    /// A value of 0 means the quest is tracked but awards no QP.
    /// Quests not listed here use DefaultPoints.
    /// See Quests.txt for a full list of valid quest names.
    /// </summary>
    public Dictionary<string, float> QuestBonuses { get; set; } = new()
    {
        ["PathwardenComplete"]        = 10,
        ["PathwardenFound1111"]       = 5,
        ["StipendsCollectedInAMonth"] = 0,
        ["StipendTimer_08"]           = 0,
        ["StipendTimer_Monthly"]      = 0,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // ACCOUNT-WIDE QUEST TRACKING
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When true, Quest Points and the XP multiplier are calculated from the
    /// total unique quests solved across ALL characters on the same account,
    /// not just the logged-in character. Alts benefit from your main's progress.
    /// Computed by scanning all characters on login (slight extra load).
    /// </summary>
    public bool UseAccountWideQuests { get; set; } = true;

    // ─────────────────────────────────────────────────────────────────────────
    // ONE-TIME COMPLETION BONUS XP
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enable a one-time XP grant when a player solves a quest for the first time.
    /// The XP amount is calculated as a multiplier of the XP needed to reach the
    /// player's next level at the time of completion.
    /// This is separate from (and stacks with) the ongoing XP multiplier.
    /// </summary>
    public bool EnableCompletionBonusXp { get; set; } = true;

    /// <summary>
    /// Default multiplier applied to (XP needed for next level) for the one-time
    /// first-solve bonus. 1.5 = 150% of the player's current level-up cost.
    /// Set to 0.0 to only reward quests listed explicitly in CompletionBonusXpOverrides.
    /// Example: a level 100 player needs ~5M XP to level up → 1.5 × 5M = 7.5M bonus XP.
    /// </summary>
    public float DefaultCompletionBonusXpMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Per-quest multiplier overrides for the one-time completion bonus.
    /// Use the quest's internal name (case-sensitive).
    /// Value is a multiplier of (XP needed for next level), same as DefaultCompletionBonusXpMultiplier.
    /// Set a value to 0.0 to suppress the bonus for that specific quest without
    /// disabling the feature globally.
    /// Examples: 3.0 = 300% of level-up cost, 0.5 = 50%, 0.0 = no bonus.
    /// See Quests.txt for a full list of valid quest names.
    /// </summary>
    public Dictionary<string, float> CompletionBonusXpOverrides { get; set; } = new()
    {
        ["PathwardenComplete"]        = 10.0f,
        ["PathwardenFound1111"]       = 3.0f,
        ["StipendsCollectedInAMonth"] = 0.0f,
        ["StipendTimer_08"]           = 0.0f,
        ["StipendTimer_Monthly"]      = 0.0f,
    };

    // ─────────────────────────────────────────────────────────────────────────
    // REPEAT SOLVE BONUS LOOT
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enable weighted loot drops on every repeat solve of a quest (2nd, 3rd, etc.).
    /// First solves do NOT trigger this — use the first-solve XP bonus above for those.
    /// Each trigger rolls exactly once against the weighted loot table and awards one item.
    ///
    /// Loot tables, groups, weights, and per-quest overrides are configured in
    /// RepeatSolveLoot.json in the same mod folder as this settings file.
    /// </summary>
    public bool EnableRepeatSolveLoot { get; set; } = true;

    // ─────────────────────────────────────────────────────────────────────────
    // MILESTONE BROADCASTS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enable server-wide broadcast messages when a player's account reaches
    /// a milestone number of unique quests solved.
    /// </summary>
    public bool EnableMilestoneBroadcasts { get; set; } = true;

    /// <summary>
    /// The unique quest solve counts that trigger a server-wide broadcast.
    /// These are per-account (across all characters). Editable — add or remove values freely.
    /// </summary>
    public List<int> MilestoneThresholds { get; set; } = new()
    {
        100, 500, 1000, 2500, 5000, 10000
    };

    /// <summary>
    /// Format string for the milestone broadcast message.
    /// {0} = the character's current name
    /// {1} = the milestone number reached
    /// Example output: "[Server] Thorin just reached 1000 unique quests on their account!"
    /// </summary>
    public string MilestoneBroadcastFormat { get; set; } =
        "[Server] {0} just reached {1} unique quests solved on their account!";
}
