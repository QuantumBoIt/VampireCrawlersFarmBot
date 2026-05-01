using BepInEx.Logging;

namespace VampireCrawlersFarmBot
{
    internal static class BotLogger
    {
        private static ManualLogSource _log;

        internal static void Init(ManualLogSource source) => _log = source;

        internal static void Essential(string msg) => _log?.LogInfo($"[FarmBot] {msg}");
        internal static void Info(string msg)
        {
            if (ShouldLog(BotLogLevel.Info))
                _log?.LogInfo($"[FarmBot] {msg}");
        }

        internal static void Debug(string msg)
        {
            if (ShouldLog(BotLogLevel.Debug))
                _log?.LogDebug($"[FarmBot] {msg}");
        }

        internal static void Warn(string msg) => _log?.LogWarning($"[FarmBot] {msg}");
        internal static void Error(string msg) => _log?.LogError($"[FarmBot] {msg}");
        internal static void Error(string msg, System.Exception ex) => _log?.LogError($"[FarmBot] {msg}\n{ex}");

        private static bool ShouldLog(BotLogLevel requested)
        {
            var cfg = BotConfig.Instance;
            if (cfg == null) return true;
            if (!cfg.VerboseLogging.Value) return false;

            var configured = ParseLevel(cfg.LogLevel.Value);
            return requested >= configured;
        }

        private static BotLogLevel ParseLevel(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return BotLogLevel.Info;
            return value.Trim().ToLowerInvariant() switch
            {
                "debug" => BotLogLevel.Debug,
                "info" => BotLogLevel.Info,
                "warn" => BotLogLevel.Warn,
                "warning" => BotLogLevel.Warn,
                "error" => BotLogLevel.Error,
                _ => BotLogLevel.Info
            };
        }
    }

    internal enum BotLogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }
}
