namespace VampireCrawlersFarmBot
{
    // Phase 1 stub.
    internal sealed class ChestPolicy
    {
        internal bool ShouldCashOut() => BotConfig.Instance?.PreferCashOut.Value ?? true;

        internal void OnCashOutSuccess(int goldGained) =>
            BotLogger.Info($"Chest cashed out: +{goldGained} gold.");

        internal void OnCashOutFailed() =>
            BotLogger.Warn("Cash-out failed — will retry or skip chest.");
    }
}
