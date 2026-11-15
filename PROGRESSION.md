# Progression & Wave System Architecture

## 1. Overview & Core Philosophy

The game separates **runtime player progression** from **authored wave difficulty & pacing** through a clean separation of concerns:

- **`PlayerProgression`** is a runtime vessel component managing the player ship's active session state: accumulated XP, level, baseline threshold curves, and progression events.
- **`WaveDefinition`** is a data-driven `ScriptableObject` containing authored pacing, threat budgets, asteroid cluster composition, completion rewards, and wave-specific drop tables.
- **`WaveManager`** serves as the central runtime coordinator bridging the player's progression with the active wave definitions.
- **`PowerupManager`** connects progression to combat rewards by evaluating guaranteed milestone drops defined inside `WaveDefinition`.
- **`PlayerMetaProgression`** manages cross-run persistent upgrades stored in `PlayerPrefs` (Hull, Shields, Thrusters, Weapons).

---

## 2. System Architecture & Data Flow

```text
  ┌─────────────────────────────────────────────────────────┐
  │                    WaveDefinition                       │
  │            (Assets/Scripts/Level/WaveDefinition.cs)     │
  │  ScriptableObject Asset:                                │
  │  - targetXPGoal (Authored override, optional)           │
  │  - rewardXP (XP granted on wave clear)                  │
  │  - threatBudget, duration, spawnInterval                │
  │  - dropTable (Weighted loot table)                      │
  │  - xpThresholdDrops (Guaranteed 100% XP drops)          │
  └───────────────────────────┬─────────────────────────────┘
                              │
               Reads Config   │   Milestone Checks
                              ▼
  ┌──────────────────────────────────────┐     Checks      ┌────────────────────────────────────┐
  │             WaveManager              │ ◄───────────────┤           PowerupManager           │
  │  (Assets/Scripts/Level/              │                 │   (Assets/Scripts/Powerups/        │
  │   WaveManager.cs)                    │                 │    PowerupManager.cs)              │
  │  - Orchestrates wave state machine   │                 │  - Evaluates xpThresholdDrops      │
  │  - Resolves target XP goal           │                 │  - Spawns guaranteed powerups      │
  │  - Evaluates if goal is reached      │                 └─────────────────┬──────────────────┘
  │  - Awards rewardXP to player         │                                   │
  └───────────────────┬──────────────────┘                                   │
                      │                                                      │
         Subscribes / │ Awards XP                                            │ Spawns Drop
         Monitors XP  │                                                      │
                      ▼                                                      ▼
  ┌──────────────────────────────────────┐     Delegates   ┌────────────────────────────────────┐
  │          PlayerProgression           │ ◄───────────────┤             player.cs              │
  │  (Assets/Scripts/Player/             │                 │   (Assets/Scripts/Player/player.cs)│
  │   PlayerProgression.cs)              │                 │  - Ship controller & facades       │
  │  - currentXP, playerLevel, waveLevel │                 │  - Receives XP via AddXP()         │
  │  - levelUps[] thresholds             │                 │  - Collects spawned powerups       │
  │  - waveGoals[] baseline thresholds   │                 └────────────────────────────────────┘
  │  - Events:                           │
  │    * OnXPChanged                     │
  │    * OnPlayerLevelUp                 │
  │    * OnWaveCompleted                 │
  └──────────────────────────────────────┘
```

---

## 3. Class Responsibilities

### A. `PlayerProgression` (Runtime Component)
**Location:** `Assets/Scripts/Player/PlayerProgression.cs`  
**Attached to:** Player ship GameObject (created automatically by `player.Awake()`).

* **State:**
  * `ulong currentXP`: Total lifetime XP gained during the current run.
  * `int playerLevel`: Current player level.
  * `int waveLevel`: Internal progression wave counter.
* **Baseline Threshold Arrays:**
  * `levelUps[]`: XP milestones required to level up the player (`{ 10, 20, 50, 100, 200, ... }`).
  * `weaponUnlocks[]`: Historic XP thresholds for weapon unlocks.
  * `waveGoals[]`: Default baseline XP required to clear each successive wave if no authored override is set (`{ 10, 100, 500, 1000, 2000, ... }`).
* **Core Methods:**
  * `AddXP(int amount, Transform source)`: Accumulates XP, checks level and wave completion, and fires `OnXPChanged`.
  * `GetNextXPGoal()`: Returns the target XP threshold for the current wave (or player level).
  * `ResetProgress()`: Clears session XP and levels upon new game start.
* **Events:**
  * `event Action<ulong, ulong> OnXPChanged`: Broadcasts `(currentXP, nextGoal)`.
  * `event Action<int> OnPlayerLevelUp`: Broadcasts new player level.
  * `event Action<int> OnWaveCompleted`: Broadcasts the completed wave level.

---

### B. `WaveDefinition` (Data Asset)
**Location:** `Assets/Scripts/Level/WaveDefinition.cs`  
**Asset Creation:** `Create > Aster > Wave Definition` (stored under `Assets/Resources/Waves/`).

* **Identity & Pacing:**
  * `int waveNumber`: 1-based wave index.
  * `string waveTitle`: Display name (e.g., `"Wave 1 - Outer Belt"`).
  * `float duration`: Max combat time before automatic wave victory (if timers enabled).
  * `int threatBudget`: Threat point budget to spawn.
  * `float spawnInterval`: Frequency of enemy/asteroid spawning.
* **Progression Integration:**
  * `ulong targetXPGoal`: Custom XP required to clear this wave. **If `0`, `WaveManager` automatically falls back to `PlayerProgression.waveGoals[waveNumber - 1]`.**
  * `int rewardXP`: Bonus XP credited directly to `player.AddXP()` upon clearing the wave.
* **Loot & Milestone Drops:**
  * `float blockDropChance`, `float coreDropChance`: Probabilities for destroyed blocks/cores to drop items.
  * `List<WaveDropEntry> dropTable`: Weighted drop entries for RNG loot.
  * `List<WaveXPDropEntry> xpThresholdDrops`: Guaranteed 100% drops triggered when reaching specific XP milestones.

---

### C. `WaveManager` (Orchestrator)
**Location:** `Assets/Scripts/Level/WaveManager.cs`  
**Role:** Manages the game loop state machine (`Countdown` ➔ `Combat` ➔ `WaveCleared` ➔ `Intermission`).

* Resolves and holds the active wave configuration via `CurrentWaveConfig`.
* Connects to `PlayerProgression` via `ActiveProgression`.
* Evaluates clear conditions through `IsXPGoalReached()`.
* Awards `CurrentWaveConfig.rewardXP` to `player` when the wave ends.

---

## 4. Key Integration Touchpoints

### 1. Goal Resolution: Authored Override vs. Default Baseline
When `WaveManager` checks the target XP needed to clear wave `N`, it executes a fallback resolution order:

```csharp
public virtual ulong GetTargetXPGoal(int waveNumber)
{
    // Priority 1: Authored override on the active WaveDefinition
    if (CurrentWaveConfig != null && CurrentWaveConfig.targetXPGoal > 0)
    {
        return CurrentWaveConfig.targetXPGoal;
    }

    // Priority 2: Baseline curve from PlayerProgression
    PlayerProgression prog = ActiveProgression;
    if (prog != null && prog.waveGoals != null && prog.waveGoals.Length > 0)
    {
        int index = Mathf.Clamp(waveNumber - 1, 0, prog.waveGoals.Length - 1);
        return prog.waveGoals[index];
    }

    // Priority 3: Procedural formula fallback
    return (ulong)(10 * Mathf.Max(1, waveNumber));
}
```

This allows designers to either fine-tune specific wave requirements in `WaveDefinition` assets or leave `targetXPGoal = 0` to let `PlayerProgression` drive the curve globally.

---

### 2. Dual Clear Detection & Event Synchronization
Wave completion can occur through two distinct pathways:
1. **`WaveManager.Tick()` combat loop**: Periodically tests `IsXPGoalReached()` by checking:
   ```csharp
   prog.CurrentXP >= GetTargetXPGoal(currentWaveIndex)
   ```
2. **`PlayerProgression.OnWaveCompleted`**: When the player earns XP that crosses `prog.waveGoals[waveLevel]`, `PlayerProgression` invokes `OnWaveCompleted`. `WaveManager` subscribes to this event (`SubscribeProgression`):
   ```csharp
   private void HandleProgressionWaveCompleted(int completedWaveLevel)
   {
       if (currentState == WaveState.Idle)
       {
           // Supports sandbox/test environments without explicit WaveManager startup
           currentWaveIndex = Mathf.Max(1, completedWaveLevel);
           CurrentWaveConfig = GetWaveDefinition(currentWaveIndex);
           CompleteWaveInternal();
       }
   }
   ```

---

### 3. XP Reward Feedback Loop
Upon victory, `WaveManager.CompleteWaveInternal()` distributes the wave's authored reward:
```csharp
if (CurrentWaveConfig != null && CurrentWaveConfig.rewardXP > 0)
{
    player p = FindPlayer();
    if (p != null)
    {
        p.AddXP(CurrentWaveConfig.rewardXP);
    }
}
```
This feeds bonus XP directly back into `PlayerProgression.AddXP()`, triggering level-up checks and XP progress bar updates on the HUD.

---

### 4. Guaranteed 100% XP Milestone Drops
In addition to RNG block drops, `WaveDefinition` supports guaranteed drops through `WaveXPDropEntry`.

Milestone drops can either:
- Target a specific **Player Level** via an Inspector dropdown (`targetPlayerLevel`), which automatically syncs with the thresholds defined in `PlayerProgression.levelUps` (e.g. `Level 1 (10 XP)`, `Level 2 (20 XP)`), or
- Specify a custom raw XP number (`xpThreshold`).

Whenever an asteroid block or core is destroyed, `PowerupManager.TryDropPowerup` queries:
- `targetPlayer.Progression.CurrentXP` (lifetime run XP) or
- `WaveManager.Instance.WaveEarnedXP` (XP earned specifically in the current wave)

```csharp
ulong xpToCheck = xpDrop.isWaveRelative ? waveEarnedXP : currentXP;
ulong requiredThreshold = xpDrop.xpThreshold;

if (xpDrop.targetPlayerLevel > 0 && targetPlayer.Progression != null)
{
    var thresholds = targetPlayer.Progression.levelUps;
    if (thresholds != null && thresholds.Length > 0)
    {
        int lvlIndex = Mathf.Clamp(xpDrop.targetPlayerLevel - 1, 0, thresholds.Length - 1);
        requiredThreshold = thresholds[lvlIndex];
    }
}

if (xpToCheck >= requiredThreshold)
{
    xpDrop.hasDropped = true;
    SpawnDropEntry(xpDrop.prefab, xpDrop.powerupType, spawnPos);
}
```
When `WaveManager` advances to a new wave, `CurrentWaveConfig.ResetRuntimeDrops()` resets these one-shot flags for the incoming wave.

---

## 5. Meta-Progression vs. Run Progression

| Dimension | `PlayerProgression` | `PlayerMetaProgression` |
| :--- | :--- | :--- |
| **Persistence** | Session-only (resets on death/restart) | Persistent across runs (`PlayerPrefs`) |
| **Tracked Values** | `currentXP`, `playerLevel`, `waveLevel` | Hull Armor level, Shield Capacitor level, Ion Thruster level, Plasma Cannon level |
| **Application** | Drives wave completion and HUD XP bars | Applied to `player` attributes (`health_max_value`, `shield_max_value`, `_thrust_force`, `_bullet_dmg`, `_bullet_force`) in `Start()` and `Respawn()` |
| **Upgrades Spending** | In-game upgrade points allocated via `PlayerWeapons.SpendUpgradePoints()` | Meta levels purchased using banked progression |

---

## 6. Upgrade Interface & Level-Up Handling

`WaveIntermissionPopup` functions as a dual-purpose upgrade panel supporting two trigger states:

1. **Wave Intermission Mode:**
   - Triggered when `WaveManager` enters `WaveState.Intermission`.
   - Displays `"WAVE {N} INTERMISSION"`, wave description, and earned XP.
   - The action button displays `"NEXT WAVE"` and invokes `WaveManager.StartNextWave()`.

2. **Mid-Combat Level-Up Pause Mode:**
   - Triggered when `PlayerProgression.OnPlayerLevelUp` fires during active gameplay.
   - Automatically awards `+1 Upgrade Point` to the player ship.
   - Pauses active combat via `Time.timeScale = 0f` (with DOTween configured to run in unscaled time).
   - Displays `"LEVEL {N} REACHED!"` and changes the action button to `"RESUME"`.
   - Clicking `"RESUME"` (or closing the panel) safely restores `Time.timeScale = 1f`.

