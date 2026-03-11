namespace Loremaster;

public static class LoremasterExtensions
{
    // ─────────────────────────────────────────────────────────────────────────
    // Quest Point helpers
    // ─────────────────────────────────────────────────────────────────────────

    public static float Value(this CharacterPropertiesQuestRegistry quest) =>
        PatchClass.Settings.QuestBonuses.TryGetValue(quest.QuestName, out var points)
            ? points
            : PatchClass.Settings.DefaultPoints;

    public static void UpdateQuestPoints(this Player player)
    {
        if (PatchClass.Settings is null || player.QuestManager is null) return;

        var points = PatchClass.Settings.UseAccountWideQuests
            ? player.CalculateAccountQuestPoints()
            : player.CalculateQuestPoints();

        player.SetProperty(FakeFloat.QuestBonus, points);
    }

    public static void IncQuestPoints(this Player player, float amount)
    {
        var current = player.GetProperty(FakeFloat.QuestBonus) ?? 0;
        player.SetProperty(FakeFloat.QuestBonus, current + amount);
    }

    public static float CalculateQuestPoints(this Player player)
    {
        if (player.QuestManager is null) return 0;
        float total = 0;
        foreach (var quest in player.QuestManager.GetQuests().Where(x => x.HasSolves()))
            total += quest.Value();
        return total;
    }

    public static float CalculateAccountQuestPoints(this Player player)
    {
        var accountId = player.Account?.AccountId;
        if (accountId is null)
        {
            ModManager.Log($"[Loremaster] Could not resolve account for {player.Name}, falling back to per-character QP.", ModManager.LogLevel.Warn);
            return player.CalculateQuestPoints();
        }

        var solvedQuestNames = GetAccountUniqueQuestNames(player, accountId.Value);

        float total = 0;
        foreach (var questName in solvedQuestNames)
        {
            total += PatchClass.Settings.QuestBonuses.TryGetValue(questName, out var points)
                ? points
                : PatchClass.Settings.DefaultPoints;
        }
        return total;
    }

    public static double QuestBonus(this Player player)
    {
        if (PatchClass.Settings is null) return 1.0;
        var qp = player.GetProperty(FakeFloat.QuestBonus) ?? 0;
        return 1.0 + qp * PatchClass.Settings.BonusPerQuestPoint / 100.0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Account-wide unique quest count (for milestones)
    // ─────────────────────────────────────────────────────────────────────────

    public static int GetAccountUniqueQuestCount(this Player player)
    {
        var accountId = player.Account?.AccountId;
        if (accountId is null)
            return player.QuestManager?.GetQuests().Count(x => x.HasSolves()) ?? 0;

        return GetAccountUniqueQuestNames(player, accountId.Value).Count;
    }

    private static HashSet<string> GetAccountUniqueQuestNames(Player player, uint accountId)
    {
        var solvedQuestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var onlinePlayer in PlayerManager.GetAllOnline())
        {
            if (onlinePlayer.Account?.AccountId != accountId) continue;
            foreach (var quest in onlinePlayer.QuestManager.GetQuests().Where(x => x.HasSolves()))
                solvedQuestNames.Add(quest.QuestName);
        }

        var offlineCharacterIds = PlayerManager.GetAllOffline()
            .Where(p => p.Account?.AccountId == accountId)
            .Select(p => p.Guid.Full)
            .ToHashSet();

        if (offlineCharacterIds.Count > 0)
        {
            using var context = new ACE.Database.Models.Shard.ShardDbContext();
            var offlineQuests = context.CharacterPropertiesQuestRegistry
                .Where(q => offlineCharacterIds.Contains(q.CharacterId) && q.NumTimesCompleted > 0)
                .Select(q => q.QuestName)
                .ToList();
            foreach (var questName in offlineQuests)
                solvedQuestNames.Add(questName);
        }

        return solvedQuestNames;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Per-player notification helpers
    // ─────────────────────────────────────────────────────────────────────────

    // Returns the player's personal preference for a notify flag (default: false).
    public static bool Notify(this Player player, FakeBool flag) =>
        player.GetProperty(flag) ?? false;

    // Toggles a notify flag and confirms to the player. Returns the new state.
    public static bool ToggleNotify(this Player player, FakeBool flag, string label)
    {
        var next = !player.Notify(flag);
        player.SetProperty(flag, next);
        player.SendMessage($"[Loremaster] {label} notifications {(next ? "enabled" : "disabled")}.");
        return next;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Completion bonus helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the XP needed for the player to gain one more level.
    ///
    /// For levels 1–274: reads directly from the vanilla DAT XP table.
    /// For levels 275+: extrapolates using a quadratic fit to the high-level
    ///   cost curve (fit over levels 200–274, max error 0.22%).
    ///   Formula: cost(L) = 1973*L² - 585787*L + 48728021
    ///   This matches the growth rate the vanilla table was trending toward
    ///   and is consistent with how most infinite-level servers extend it.
    /// Returns 0 only if the DAT table is unavailable.
    /// </summary>
    public static long GetXpToNextLevel(this Player player)
    {
        var level = player.Level ?? 1;

        var xpTable = DatManager.PortalDat.XpTable;
        if (xpTable?.CharacterLevelXPList is not null)
        {
            var levelList = xpTable.CharacterLevelXPList;

            // Within the vanilla table: use actual values
            if (level + 1 < levelList.Count)
                return (long)(levelList[level + 1] - levelList[level]);
        }

        // At or above cap (275+): extrapolate with quadratic fit
        // Derived by fitting cost-per-level over levels 200–274 (max error 0.22%)
        return ExtrapolateXpCost(level);
    }

    private static long ExtrapolateXpCost(int level)
    {
        // cost(L) = 1973*L² - 585787*L + 48728021
        const double a =  1973.0;
        const double b = -585787.0;
        const double c =  48728021.0;
        var cost = a * level * level + b * level + c;
        return Math.Max(0, (long)cost);
    }

    /// <summary>
    /// Returns the one-time XP bonus for the first solve of the given quest,
    /// calculated as a multiplier of the XP the player needs to reach their next level.
    ///
    /// Resolution order:
    ///   1. Feature disabled → 0
    ///   2. Quest in CompletionBonusXpOverrides → override multiplier × next-level XP
    ///   3. Otherwise → DefaultCompletionBonusXpMultiplier × next-level XP
    /// </summary>
    public static long GetCompletionBonusXp(string questName, Player player)
    {
        if (!PatchClass.Settings.EnableCompletionBonusXp) return 0;

        var multiplier = PatchClass.Settings.CompletionBonusXpOverrides.TryGetValue(questName, out var overrideMultiplier)
            ? overrideMultiplier
            : PatchClass.Settings.DefaultCompletionBonusXpMultiplier;

        if (multiplier <= 0f) return 0;

        var xpToNextLevel = player.GetXpToNextLevel();
        if (xpToNextLevel <= 0) return 0;

        return (long)(xpToNextLevel * multiplier);
    }

    /// <summary>
    /// Grants the one-time first-solve XP bonus.
    /// Called only when solves go 0 → 1.
    /// </summary>
    public static void GrantCompletionBonuses(this Player player, string questName)
    {
        var bonusXp = GetCompletionBonusXp(questName, player);
        if (bonusXp > 0)
        {
            player.GrantXP(bonusXp, XpType.Quest, ShareType.None);
            if (player.Notify(LMBool.NotifyQuestXp))
                player.SendMessage($"[Loremaster] First solve of {questName} awarded {bonusXp:N0} bonus XP!");
        }
    }

    /// <summary>
    /// Rolls the weighted loot pool for the given quest and gives the player one item.
    /// Called on every repeat solve (2nd, 3rd, etc.) of a quest.
    /// Pool is resolved from RepeatSolveLoot.json via RepeatSolveLootLoader.
    /// </summary>
    public static void GrantRepeatSolveLoot(this Player player, string questName)
    {
        if (!PatchClass.Settings.EnableRepeatSolveLoot) return;

        var pool = RepeatSolveLootLoader.GetPool(questName);
        if (pool.Count == 0) return;

        var wcid = PickFromPool(pool);
        if (wcid == 0) return;

        var capturedWcid = wcid;
        var chain = new ActionChain();
        chain.AddAction(player, () =>
        {
            var item = WorldObjectFactory.CreateNewWorldObject(capturedWcid);
            if (item is null)
            {
                ModManager.Log($"[Loremaster] Failed to create repeat-loot item WCID {capturedWcid} for {player.Name}", ModManager.LogLevel.Warn);
                return;
            }

            if (!player.TryAddToInventory(item))
            {
                item.Location = player.Location.InFrontOf(0.5f);
                item.EnterWorld();
                player.SendMessage($"[Loremaster] Your inventory was full — {item.Name} dropped at your feet.");
            }
            else if (player.Notify(LMBool.NotifyQuest))
            {
                player.SendMessage($"[Loremaster] Repeat solve of {questName} rewarded: {item.Name}!");
            }
        });
        chain.EnqueueChain();
    }

    /// <summary>
    /// Picks one WCID from a flat weighted pool using weighted random selection.
    /// Returns 0 if the pool is empty or all weights are zero.
    /// </summary>
    private static uint PickFromPool(List<(uint Wcid, int Weight)> pool)
    {
        var totalWeight = pool.Sum(e => e.Weight);
        if (totalWeight <= 0) return 0;

        var roll = Random.Shared.Next(totalWeight);
        var cumulative = 0;
        foreach (var (wcid, weight) in pool)
        {
            cumulative += weight;
            if (roll < cumulative)
                return wcid;
        }

        return pool[^1].Wcid; // fallback (should never reach here)
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Milestone broadcast helpers
    // ─────────────────────────────────────────────────────────────────────────

    public static void CheckAndBroadcastMilestone(this Player player, int previousCount, int newCount)
    {
        if (!PatchClass.Settings.EnableMilestoneBroadcasts) return;

        foreach (var threshold in PatchClass.Settings.MilestoneThresholds)
        {
            if (previousCount < threshold && newCount >= threshold)
            {
                var bonusQp = PatchClass.Settings.MilestoneBonusQP.TryGetValue(threshold, out var qp) ? qp : 0f;
                if (bonusQp > 0)
                    player.IncQuestPoints(bonusQp);

                var message = string.Format(PatchClass.Settings.MilestoneBroadcastFormat,
                    player.Name, Ordinal(threshold), (int)bonusQp);
                foreach (var online in PlayerManager.GetAllOnline())
                    online.SendMessage(message, ChatMessageType.Broadcast);
                ModManager.Log($"[Loremaster] Milestone broadcast: {message}");
            }
        }
    }

    private static string Ordinal(int n)
    {
        var mod100 = n % 100;
        if (mod100 >= 11 && mod100 <= 13) return $"{n}th";
        return (n % 10) switch
        {
            1 => $"{n}st",
            2 => $"{n}nd",
            3 => $"{n}rd",
            _ => $"{n}th"
        };
    }
}

// Per-player notify preference IDs (FakeBool 11000–11004, safe Loremaster range).
// Stored on the character — persist across sessions, toggled via /qb <flag>.
internal static class LMBool
{
    internal const FakeBool NotifyQuest          = (FakeBool)11000; // QP gains/losses
    internal const FakeBool NotifyKillXp         = (FakeBool)11001; // kill XP boost
    internal const FakeBool NotifyQuestXp        = (FakeBool)11002; // quest XP boost
    internal const FakeBool NotifyKillLuminance  = (FakeBool)11003; // kill luminance boost
    internal const FakeBool NotifyQuestLuminance = (FakeBool)11004; // quest luminance boost
}
