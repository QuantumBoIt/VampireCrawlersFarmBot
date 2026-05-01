using System.Collections.Generic;

namespace VampireCrawlersFarmBot
{
    internal enum RewardChoice { Skip }

    internal sealed class RewardOption
    {
        internal string Name;
        internal bool IsGem;
        internal bool IsSkip;
    }

    // Phase 1 stub: always returns Skip until UI inspection is implemented.
    internal sealed class RewardPolicy
    {
        private static readonly string[] GemKeywords = { "gem", "宝石" };
        private static readonly string[] SkipKeywords = { "skip", "跳过" };

        internal RewardOption ChooseReward(List<RewardOption> options)
        {
            BotLogger.Debug($"STUB: ChooseReward called with {options?.Count ?? 0} options — returning Skip");

            if (options == null || options.Count == 0)
                return new RewardOption { Name = "Skip (no options)", IsSkip = true };

            // Safe non-gem pick
            foreach (var opt in options)
                if (!opt.IsGem && !opt.IsSkip) return opt;

            // Fallback: explicit skip
            foreach (var opt in options)
                if (opt.IsSkip) return opt;

            return new RewardOption { Name = "Skip (fallback)", IsSkip = true };
        }

        internal static bool LooksLikeGem(string name)
        {
            var lower = name.ToLowerInvariant();
            foreach (var kw in GemKeywords)
                if (lower.Contains(kw)) return true;
            return false;
        }
    }
}
