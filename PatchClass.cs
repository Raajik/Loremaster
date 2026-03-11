namespace Loremaster;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    // Start() runs on every mod load — cold boot AND hot-reload.
    // OnWorldOpen() is a one-shot ACE event fired at server startup; if the mod is
    // loaded after the world is already up (hot-reload), OnWorldOpen() never fires,
    // leaving Settings null.  Assigning here covers both cases.
    public override void Start()
    {
        base.Start();
        Settings = SettingsContainer.Settings ?? new Settings();
    }

    public override async Task OnWorldOpen()
    {
        // SettingsContainer may have reloaded since Start(); refresh the reference.
        Settings = SettingsContainer.Settings ?? new Settings();

        // Load repeat-solve loot tables from RepeatSolveLoot.json
        // Assembly location is e.g. C:\ACE\Mods\Loremaster\Loremaster.dll
        var modFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        RepeatSolveLootLoader.Load(modFolder);

        // Recalculate QP for all online players on reload/start
        UpdateIngamePlayers();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Player commands
    // ─────────────────────────────────────────────────────────────────────────

    [CommandHandler("qp", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
        "Shows your current Quest Points and XP multiplier.")]
    public static void HandleQuestPoints(Session session, params string[] parameters)
    {
        var player = session.Player;
        if (player is null) return;
        var count  = player.QuestManager?.GetQuests().Count(x => x.HasSolves()) ?? 0;
        var qp     = player.GetProperty(FakeFloat.QuestBonus) ?? 0;
        var bonus  = player.QuestBonus();

        if (Settings.UseAccountWideQuests)
        {
            var accountCount = player.GetAccountUniqueQuestCount();
            player.SendMessage(
                $"Account-wide: {accountCount} unique quests solved.\n" +
                $"This character: {count} quests.\n" +
                $"Quest Points: {qp} QP → {bonus:P2} XP multiplier.");
        }
        else
        {
            player.SendMessage($"{count} quests solved for {qp} QP → {bonus:P2} XP multiplier.");
        }
    }

    [CommandHandler("qb", AccessLevel.Player, CommandHandlerFlag.RequiresWorld,
        "Lists all your quests with their Quest Point values and updates your bonus.")]
    public static void HandleQuests(Session session, params string[] parameters)
    {
        var player = session.Player;
        if (player is null) return;
        var quests = player.QuestManager?.GetQuests() ?? new List<CharacterPropertiesQuestRegistry>();

        var sb = new StringBuilder();
        sb.AppendLine($"Quest Name / Completions / QP Value");
        sb.AppendLine(new string('-', 50));

        foreach (var quest in quests)
        {
            if (!Settings.QuestBonuses.TryGetValue(quest.QuestName, out var points))
                points = Settings.DefaultPoints;
            sb.AppendLine($"{quest.QuestName,-35} x{quest.NumTimesCompleted,-4} {points} QP");
        }

        player.UpdateQuestPoints();
        var qp    = player.GetProperty(FakeFloat.QuestBonus) ?? 0;
        var bonus = player.QuestBonus();
        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"Total: {qp} QP → {bonus:P2} XP multiplier");

        player.SendMessage(sb.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Admin commands
    // ─────────────────────────────────────────────────────────────────────────

    [CommandHandler("qb-inspect", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld,
        "Inspect quest bonus data for a named player.",
        "Usage: /qb-inspect <playerName>")]
    public static void HandleAdminInspect(Session session, params string[] parameters)
    {
        if (parameters.Length < 1)
        {
            session.Player.SendMessage("Usage: /qb-inspect <playerName>");
            return;
        }

        var targetName = parameters[0];
        var target     = PlayerManager.GetOnlinePlayer(targetName);

        if (target is null)
        {
            session.Player.SendMessage($"Player '{targetName}' is not online.");
            return;
        }

        var qp          = target.GetProperty(FakeFloat.QuestBonus) ?? 0;
        var bonus       = target.QuestBonus();
        var charCount   = target.QuestManager.GetQuests().Count(x => x.HasSolves());
        var accountCount = target.GetAccountUniqueQuestCount();

        session.Player.SendMessage(
            $"[Loremaster Inspect] {target.Name}\n" +
            $"  Stored QP       : {qp}\n" +
            $"  XP Multiplier   : {bonus:P2}\n" +
            $"  Char quests     : {charCount}\n" +
            $"  Account unique  : {accountCount}\n" +
            $"  Account-wide    : {Settings.UseAccountWideQuests}");
    }

    [CommandHandler("qb-reset", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld,
        "Recalculates and resets quest bonus for a named player.",
        "Usage: /qb-reset <playerName>")]
    public static void HandleAdminReset(Session session, params string[] parameters)
    {
        if (parameters.Length < 1)
        {
            session.Player.SendMessage("Usage: /qb-reset <playerName>");
            return;
        }

        var targetName = parameters[0];
        var target     = PlayerManager.GetOnlinePlayer(targetName);

        if (target is null)
        {
            session.Player.SendMessage($"Player '{targetName}' is not online.");
            return;
        }

        target.UpdateQuestPoints();
        var qp    = target.GetProperty(FakeFloat.QuestBonus) ?? 0;
        var bonus = target.QuestBonus();

        session.Player.SendMessage($"[Loremaster] Reset {target.Name}: {qp} QP → {bonus:P2} XP multiplier.");
        target.SendMessage($"[Loremaster] Your quest bonus has been recalculated by an admin: {qp} QP → {bonus:P2}.");
    }

    [CommandHandler("qb-resetall", AccessLevel.Admin, CommandHandlerFlag.None,
        "Recalculates quest bonus for all online players. Safe to run after settings changes.")]
    public static void HandleAdminResetAll(Session session, params string[] parameters)
    {
        UpdateIngamePlayers();
        var count = PlayerManager.GetAllOnline().Count();
        var msg   = $"[Loremaster] Recalculated quest bonuses for {count} online player(s).";
        session?.Player?.SendMessage(msg);
        ModManager.Log(msg);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Shared utility
    // ─────────────────────────────────────────────────────────────────────────

    public static void UpdateIngamePlayers()
    {
        foreach (var player in PlayerManager.GetAllOnline())
            player.UpdateQuestPoints();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Login hook — recalculate on enter world
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
    public static void PostPlayerEnterWorld(ref Player __instance)
    {
        // Defer QP calculation by one tick — Account and QuestManager may not
        // be fully attached yet at the point this postfix fires.
        // Copy to local first — ref parameters cannot be captured in lambdas.
        var player = __instance;
        var chain = new ActionChain();
        chain.AddAction(player, () => player.UpdateQuestPoints());
        chain.EnqueueChain();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Quest completion / removal tracking
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Decrement), new Type[] { typeof(string), typeof(int) })]
    public static void PreDecrement(string quest, int amount, ref QuestManager __instance)
    {
        var questName = QuestManager.GetQuestName(quest);
        if (questName is null) return;

        var qst = __instance.GetQuest(questName);
        if (qst is null) return;

        if (qst.NumTimesCompleted == 1 && __instance.Creature is Player player)
        {
            player.IncQuestPoints(-qst.Value());
            if (Settings.NotifyQuest)
                player.SendMessage($"Removed {qst.Value()} QP on removing {questName}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Erase), new Type[] { typeof(string) })]
    public static void PreErase(string questFormat, ref QuestManager __instance)
    {
        var questName = QuestManager.GetQuestName(questFormat);
        if (questName is null) return;

        var qst = __instance.GetQuest(questName);
        if (qst is null) return;

        if (qst.NumTimesCompleted == 1 && __instance.Creature is Player player)
        {
            player.IncQuestPoints(-qst.Value());
            if (Settings.NotifyQuest)
                player.SendMessage($"Removed {qst.Value()} QP on removing {questName}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update / SetQuestCompletions — Prefix captures solve count, Postfix reacts
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Update), new Type[] { typeof(string) })]
    public static void PreUpdate(string questFormat, ref QuestManager __instance, ref int __state)
    {
        __state = __instance.GetCurrentSolves(questFormat);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.SetQuestCompletions), new Type[] { typeof(string), typeof(int) })]
    public static void PreSetQuestCompletions(string questFormat, int questCompletions, ref QuestManager __instance, ref int __state)
    {
        __state = __instance.GetCurrentSolves(questFormat);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Update), new Type[] { typeof(string) })]
    public static void PostUpdate(string questFormat, ref QuestManager __instance, ref int __state)
    {
        CheckQuestEligibilityChange(questFormat, __instance, __state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.SetQuestCompletions), new Type[] { typeof(string), typeof(int) })]
    public static void PostSetQuestCompletions(string questFormat, int questCompletions, ref QuestManager __instance, ref int __state)
    {
        CheckQuestEligibilityChange(questFormat, __instance, __state);
    }

    private static void CheckQuestEligibilityChange(string questFormat, QuestManager instance, int previousSolves)
    {
        var solves = instance.GetCurrentSolves(questFormat);
        if (instance.Creature is not Player player) return;

        // ── First ever solve (0 → any) ───────────────────────────────────────
        if (previousSolves == 0 && solves != 0)
        {
            var quest = instance.GetQuest(QuestManager.GetQuestName(questFormat));

            // Quest Point increment
            player.IncQuestPoints(quest.Value());
            if (Settings.NotifyQuest)
                player.SendMessage($"Added {quest.Value()} QP from {questFormat}");

            var questName = QuestManager.GetQuestName(questFormat) ?? questFormat;

            // One-time XP + loot bonuses
            player.GrantCompletionBonuses(questName);

            // Milestone check — get count before and after this solve
            var prevAccountCount = player.GetAccountUniqueQuestCount() - 1; // just added one
            var newAccountCount  = prevAccountCount + 1;
            player.CheckAndBroadcastMilestone(prevAccountCount, newAccountCount);
        }

        // ── Repeat solve (previously solved, solve count increased) ─────────
        if (previousSolves > 0 && solves > previousSolves)
        {
            var questName = QuestManager.GetQuestName(questFormat) ?? questFormat;
            player.GrantRepeatSolveLoot(questName);
        }

        // ── Quest removed (any → 0) ──────────────────────────────────────────
        if (previousSolves != 0 && solves == 0)
        {
            var quest = instance.GetQuest(QuestManager.GetQuestName(questFormat));
            player.IncQuestPoints(-quest.Value());
            if (Settings.NotifyQuest)
                player.SendMessage($"Subtracted {quest.Value()} QP from {questFormat}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // XP multiplier — applies the ongoing QuestBonus to all XP gains
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.GrantXP), new Type[] { typeof(long), typeof(XpType), typeof(ShareType) })]
    public static void PreGrantXP(ref long amount, XpType xpType, ShareType shareType, ref Player __instance)
    {
        if (Settings.NotifyExp)
            __instance.SendMessage($"Boosting {amount:N0} XP by {__instance.QuestBonus():P2} → {(long)(amount * __instance.QuestBonus()):N0}");

        amount = (long)(amount * __instance.QuestBonus());
    }
}
