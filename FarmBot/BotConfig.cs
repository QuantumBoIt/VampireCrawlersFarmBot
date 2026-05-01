using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace VampireCrawlersFarmBot
{
    internal sealed class BotConfig
    {
        internal static BotConfig Instance { get; private set; }

        // General
        internal ConfigEntry<bool> EnabledOnStart { get; }
        internal ConfigEntry<bool> PauseWhenUnfocused { get; }
        internal ConfigEntry<bool> LoopRuns { get; }
        internal ConfigEntry<string> LogLevel { get; }

        // Hotkeys (new Input System Key enum stored as string, parsed on read)
        internal ConfigEntry<string> Toggle { get; }
        internal ConfigEntry<string> DumpScene { get; }
        internal ConfigEntry<string> DumpMap { get; }
        internal ConfigEntry<string> Step { get; }
        internal ConfigEntry<string> EmergencyStop { get; }

        // Stage
        internal ConfigEntry<string> WorldName { get; }
        internal ConfigEntry<string> StageName { get; }

        // Navigation
        internal ConfigEntry<int> MaxPathRetries { get; }
        internal ConfigEntry<int> MaxMoveFailCount { get; }
        internal ConfigEntry<int> ChestSearchRadius { get; }
        internal ConfigEntry<int> ExitSearchRadius { get; }

        // Timing
        internal ConfigEntry<int> UiWaitMs { get; }
        internal ConfigEntry<int> MoveWaitMs { get; }
        internal ConfigEntry<int> TurnWaitMs { get; }
        internal ConfigEntry<int> StateTimeoutSeconds { get; }

        // Movement keys (new Input System Key names, used for dungeon navigation)
        internal ConfigEntry<string> MoveForward { get; }
        internal ConfigEntry<string> MoveBack    { get; }
        internal ConfigEntry<string> TurnLeft    { get; }
        internal ConfigEntry<string> TurnRight   { get; }
        internal ConfigEntry<string> Interact    { get; }
        internal ConfigEntry<string> OpenMenu    { get; }

        // Rewards
        internal ConfigEntry<bool> AvoidGem { get; }
        internal ConfigEntry<bool> SkipIfUncertain { get; }

        // Chest
        internal ConfigEntry<bool> PreferCashOut { get; }
        internal ConfigEntry<int> ExpectedCashOutGold { get; }

        internal BotConfig(ConfigFile cfg)
        {
            EnabledOnStart = cfg.Bind("General", "EnabledOnStart", false, "Start the bot automatically on game launch.");
            PauseWhenUnfocused = cfg.Bind("General", "PauseWhenUnfocused", true, "Pause bot when the game window loses focus.");
            LoopRuns = cfg.Bind("General", "LoopRuns", true, "After a completed run returns to town, start the next run automatically.");
            LogLevel = cfg.Bind("General", "LogLevel", "Debug", "Verbosity: Debug, Info, Warn, Error.");

            Toggle = cfg.Bind("Hotkeys", "Toggle", "F8", "Enable / pause the farm bot (new Input System Key name).");
            DumpScene = cfg.Bind("Hotkeys", "DumpScene", "F9", "Dump scene and UI objects to log.");
            DumpMap = cfg.Bind("Hotkeys", "DumpMap", "F10", "Dump map, player, chest, exit objects to log.");
            Step = cfg.Bind("Hotkeys", "Step", "F11", "Log current state without advancing.");
            EmergencyStop = cfg.Bind("Hotkeys", "EmergencyStop", "F12", "Immediately disable all bot input.");

            WorldName = cfg.Bind("Stage", "WorldName", "乳品厂", "World/area name to select on the world map.");
            StageName = cfg.Bind("Stage", "StageName", "凝乳厂", "Stage name to enter inside the selected world. Examples: 乳品厂, 牛奶厂, 凝乳厂.");

            MaxPathRetries = cfg.Bind("Navigation", "MaxPathRetries", 5, "Re-plan path this many times before giving up.");
            MaxMoveFailCount = cfg.Bind("Navigation", "MaxMoveFailCount", 3, "Consecutive failed moves before marking a direction blocked.");
            ChestSearchRadius = cfg.Bind("Navigation", "ChestSearchRadius", 1, "Grid radius to scan for a chest near the target.");
            ExitSearchRadius = cfg.Bind("Navigation", "ExitSearchRadius", 1, "Grid radius to scan for the exit near the target.");

            UiWaitMs = cfg.Bind("Timing", "UiWaitMs", 500, "Milliseconds to wait after a UI action.");
            MoveWaitMs = cfg.Bind("Timing", "MoveWaitMs", 300, "Milliseconds between movement steps.");
            TurnWaitMs = cfg.Bind("Timing", "TurnWaitMs", 200, "Milliseconds between turn steps.");
            StateTimeoutSeconds = cfg.Bind("Timing", "StateTimeoutSeconds", 20, "Seconds before a stuck state triggers Recovery.");

            MoveForward = cfg.Bind("Movement", "MoveForward", "W",       "Key to move the player forward one tile.");
            MoveBack    = cfg.Bind("Movement", "MoveBack",    "S",       "Key to move backward / used for 180° turns.");
            TurnLeft    = cfg.Bind("Movement", "TurnLeft",    "A",       "Key to turn the player left 90°.");
            TurnRight   = cfg.Bind("Movement", "TurnRight",   "D",       "Key to turn the player right 90°.");
            Interact    = cfg.Bind("Movement", "Interact",    "E",       "Key to interact with chests / exits.");
            OpenMenu    = cfg.Bind("Movement", "OpenMenu",    "Escape",  "Key to open the pause / exit menu.");

            AvoidGem = cfg.Bind("Rewards", "AvoidGem", true, "Do not select gem upgrades.");
            SkipIfUncertain = cfg.Bind("Rewards", "SkipIfUncertain", true, "Skip upgrade if it cannot be classified as safe.");

            PreferCashOut = cfg.Bind("Chest", "PreferCashOut", true, "Choose cash-out over keeping chest items.");
            ExpectedCashOutGold = cfg.Bind("Chest", "ExpectedCashOutGold", 200, "Expected gold from cashing out one chest.");

            Instance = this;
        }

        // Parse a Key from its name string (e.g. "F8" → Key.F8).
        internal static Key ParseKey(string name)
        {
            if (System.Enum.TryParse<Key>(name, true, out var k))
                return k;
            BotLogger.Warn($"Unknown key name '{name}', falling back to None.");
            return Key.None;
        }
    }
}
