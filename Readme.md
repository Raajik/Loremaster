# Loremaster

An ACE (Asheron's Call Emulator) server mod branched from [QuestBonus](https://github.com/aquafir/ACE.BaseMod) by aquafir. Loremaster extends the quest-driven XP system with account-wide tracking, level-scaled completion rewards, weighted repeat-solve loot, milestone broadcasts, and per-player notification preferences.

---

## Features

### Quest Points and XP Multiplier

Every quest you solve earns Quest Points (QP). Your accumulated QP converts into a persistent multiplier applied to all XP and luminance gains:

```
Multiplier = 1 + (QuestPoints x BonusPerQuestPoint / 100)
```

Example with the default `BonusPerQuestPoint` of `3.0`:

| Quest Points | XP Multiplier |
|---|---|
| 0 QP | 1.00x |
| 10 QP | 1.30x |
| 50 QP | 2.50x |
| 100 QP | 4.00x |

Individual quests can be weighted in `Settings.json`. A quest set to `0` is tracked but awards no QP — useful for suppressing timers, stipends, or other low-value quests.

---

### Account-Wide Quest Tracking

When `UseAccountWideQuests` is `true` (default), Quest Points and the XP multiplier are calculated from the combined unique quests solved across all characters on the same account, not just the logged-in character.

- Progress on your main character benefits all your alts automatically.
- Each unique quest name is counted once regardless of how many characters solved it.
- Recalculated on login by scanning all online and offline characters on the account.
- Can be disabled to restore the original per-character behavior.

---

### One-Time Completion Bonus XP

On a quest's first ever solve, the player receives a flat XP grant in addition to the ongoing multiplier. This does not fire on repeat solves.

- The bonus scales with the player's current level: `multiplier x (XP needed for next level)`.
- A global default multiplier is set via `DefaultCompletionBonusXpMultiplier`.
- Per-quest overrides are supported via `CompletionBonusXpOverrides`.
- Set a quest's multiplier to `0` to suppress the bonus for that specific quest.
- Can be disabled entirely with `EnableCompletionBonusXp: false`.

Note: the completion bonus goes through the normal `GrantXP` path, so it is also subject to the ongoing QP multiplier. Higher-progress players earn slightly more from each new quest.

---

### Repeat Solve Bonus Loot

On every repeat solve (second completion onward) of a quest, one item from a weighted loot table is delivered to the player's inventory. If the inventory is full, the item drops at their feet with a notification.

- Loot tables are defined in `RepeatSolveLoot.json` using weighted groups of WCIDs.
- All items within a group are equally likely; the group Weight controls its frequency relative to other groups.
- Per-quest overrides are supported — a specific quest can have its own loot table that replaces the global one entirely.
- Groups with `Weight: 0` are disabled but remain in the file for reference.
- Can be disabled entirely with `EnableRepeatSolveLoot: false`.

Default loot table:

| Group | Weight | Contents |
|---|---|---|
| Currency | 1000 | Trade Note (250,000) |
| Salvage - Common | 800 | Granite, Iron, Mahogany, Steel, Ivory, Leather, Sandstone |
| Foolproof Gems | 100 | All 13 foolproof gem types |

---

### Milestone Broadcasts

When a player's account reaches a configured milestone of unique quests solved, a message is broadcast server-wide and an optional bonus QP reward is granted.

- Milestones are account-wide, not per-character.
- Default thresholds: 25, 50, 100, 250, 500, 750, 1000 — then every 500 up to 10,000.
- Default bonus QP: 5 QP at the first three milestones (25, 50, 100), then 10 QP for all subsequent ones.
- The broadcast message format is configurable.
- Can be disabled entirely with `EnableMilestoneBroadcasts: false`.

Default broadcast format:
```
[Loremaster] {0} has just completed their {1} unique quest and earned {2} bonus quest points!
```

---

### Per-Player Notification Preferences

All verbose notifications are opt-in and toggled per character. Preferences persist across sessions. No server-wide setting controls these — each player configures their own.

| Command | What it toggles |
|---|---|
| `/qb NotifyQuest` | QP gains and losses |
| `/qb NotifyKillXp` | XP multiplier message on kills |
| `/qb NotifyQuestXp` | XP multiplier message on quest completion |
| `/qb NotifyKillLuminance` | Luminance multiplier message on kills |
| `/qb NotifyQuestLuminance` | Luminance multiplier message on quest completion |
| `/qb NotifyAll` | Toggle all of the above on or off at once |

When a notification fires, the message format is:
```
Earned 426,711 XP! (300,501 * 142%)
```

---

## Player Commands

| Command | Description |
|---|---|
| `/qb` | Shows your Quest Points, XP multiplier, account-wide unique quest count, and per-character quest count. |
| `/qb list` | Lists all quests on your character with their completion count and QP value. |
| `/qb help` | Shows all available subcommands. |
| `/qb NotifyAll` | Toggle all personal notifications on or off. |
| `/qb Notify<Flag>` | Toggle a specific notification. See above for the full list. |

---

## Admin Commands

| Command | Description |
|---|---|
| `/qb-inspect <name>` | Shows stored QP, XP multiplier, character quest count, and account-wide unique count for an online player. |
| `/qb-reset <name>` | Recalculates and re-stores QP for a specific online player. Notifies both admin and player. |
| `/qb-resetall` | Recalculates QP for all currently online players. Safe to run after changes to `Settings.json`. |

---

## Settings Reference

All settings live in `Settings.json` in the mod folder. The file is auto-generated with defaults on first run. Hot-reload is supported — run `/qb-resetall` after changing values to apply them to online players immediately.

### Quest Point System

| Setting | Type | Default | Description |
|---|---|---|---|
| `BonusPerQuestPoint` | float | `3.0` | Percentage of XP bonus per QP. Formula: `1 + (QP x BonusPerQuestPoint / 100)`. |
| `DefaultPoints` | float | `1.0` | QP awarded for any quest not listed in `QuestBonuses`. Set to `0` to only reward explicitly listed quests. |
| `QuestBonuses` | dict | see below | Per-quest QP overrides keyed by internal quest name. See `Quests.txt` for all valid names. |

### Account-Wide Tracking

| Setting | Type | Default | Description |
|---|---|---|---|
| `UseAccountWideQuests` | bool | `true` | Count unique quests across all characters on the account. Set to `false` for per-character mode. |

### One-Time Completion Bonus XP

| Setting | Type | Default | Description |
|---|---|---|---|
| `EnableCompletionBonusXp` | bool | `true` | Master toggle for the one-time XP grant on first solve. |
| `DefaultCompletionBonusXpMultiplier` | float | `1.5` | Multiplier applied to the player's XP-to-next-level cost. `1.5` = 150% of the current level-up cost. |
| `CompletionBonusXpOverrides` | dict | see below | Per-quest multiplier overrides. Set to `0.0` to suppress the bonus for a specific quest. |

### Repeat Solve Bonus Loot

| Setting | Type | Default | Description |
|---|---|---|---|
| `EnableRepeatSolveLoot` | bool | `true` | Master toggle for repeat-solve item rewards. |

Loot tables are configured in `RepeatSolveLoot.json`. See that file for documentation on the weight system and per-quest overrides.

### Milestone Broadcasts

| Setting | Type | Default | Description |
|---|---|---|---|
| `EnableMilestoneBroadcasts` | bool | `true` | Master toggle for server-wide milestone messages. |
| `MilestoneThresholds` | list | see below | Account-wide unique quest counts that trigger a broadcast. |
| `MilestoneBonusQP` | dict | see below | Bonus QP granted per milestone. Missing entries award no QP but still broadcast. |
| `MilestoneBroadcastFormat` | string | see below | Format string. `{0}` = character name, `{1}` = ordinal milestone (e.g. "50th"), `{2}` = bonus QP awarded. |

---

## RepeatSolveLoot.json

The loot table file supports a flexible weighted group system. Groups with `Weight: 0` are disabled. The effective drop chance for a single item is:

```
GroupWeight / TotalPoolWeight / ItemsInGroup
```

Per-quest overrides are defined under `QuestOverrides` keyed by internal quest name. When a quest has an override, its groups replace the global groups entirely for that quest.

Suggested weight tiers:

| Tier | Weight |
|---|---|
| Common | 1000 |
| Uncommon | 100 |
| Rare | 10 |
| Ultra-Rare | 1 |

---

## Installation

### Pre-built

1. Download the latest release zip and extract it to `C:\ACE\Mods\Loremaster\`.
2. Start or reload your server. `Settings.json` is auto-generated on first load.
3. Edit `Settings.json` to taste, then run `/qb-resetall` in-game to apply changes to online players.

### Build from source

The `.csproj` pulls `ACEmulator.ACE.Shared` from NuGet automatically. It expects your ACE server binaries at `C:\ACE\Server\`. If your server lives elsewhere, update `ACEPath` in the `.csproj`.

1. Open the solution in Visual Studio or run `dotnet build` from the project folder.
2. The compiled output is written directly to `C:\ACE\Mods\Loremaster\`.
3. Hot-reload is supported via `/mod f Loremaster`.

---

## Differences from QuestBonus

| Feature | QuestBonus | Loremaster |
|---|---|---|
| XP multiplier from Quest Points | Yes | Yes |
| Luminance multiplier from Quest Points | No | Yes |
| Per-quest QP weights | Yes | Yes |
| Account-wide unique quest tracking | No | Yes |
| One-time XP on first solve (level-scaled) | No | Yes |
| Weighted repeat-solve loot | No | Yes |
| Milestone broadcasts with bonus QP | No | Yes |
| Per-player notification toggles | No | Yes |
| Admin inspect / reset commands | No | Yes |

---

## Credits

Built on top of the ACE modding framework by [aquafir](https://github.com/aquafir/ACE.BaseMod). Quest Point system and core architecture derived from QuestBonus.
