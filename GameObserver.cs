using UnityEngine.SceneManagement;

namespace VampireCrawlersFarmBot
{
    internal sealed class GameObserver
    {
        internal string GetCurrentScene() => SceneManager.GetActiveScene().name;

        internal bool IsInVillage()
        {
            var name = GetCurrentScene().ToLowerInvariant();
            return name.Contains("village") || name.Contains("town") || name.Contains("hub") || name.Contains("main");
        }

        internal bool IsInDungeon()
        {
            var name = GetCurrentScene().ToLowerInvariant();
            return name.Contains("dungeon") || name.Contains("stage") || name.Contains("level") ||
                   name.Contains("run") || name.Contains("dairy") || name.Contains("curdling");
        }

        internal void DumpSceneInfo()
        {
            BotLogger.Info($"Active scene: \"{GetCurrentScene()}\" (in village={IsInVillage()}, in dungeon={IsInDungeon()})");
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                BotLogger.Info($"  Loaded scene[{i}]: \"{s.name}\" (buildIndex={s.buildIndex}, loaded={s.isLoaded})");
            }
        }
    }
}
