using BepInEx.Logging;

namespace VampireCrawlersFarmBot
{
    internal static class BotLogger
    {
        private static ManualLogSource _log;

        internal static void Init(ManualLogSource source) => _log = source;

        internal static void Info(string msg) => _log?.LogInfo($"[FarmBot] {msg}");
        internal static void Debug(string msg) => _log?.LogDebug($"[FarmBot] {msg}");
        internal static void Warn(string msg) => _log?.LogWarning($"[FarmBot] {msg}");
        internal static void Error(string msg) => _log?.LogError($"[FarmBot] {msg}");
        internal static void Error(string msg, System.Exception ex) => _log?.LogError($"[FarmBot] {msg}\n{ex}");
    }
}
