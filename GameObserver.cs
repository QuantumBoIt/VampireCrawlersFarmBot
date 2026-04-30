using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VampireCrawlersFarmBot
{
    internal sealed class GameObserver
    {
        // Scene names confirmed from F9 dumps
        private const string SceneVillage = "SC_TownMap";
        private const string SceneDungeon = "SC_Game";

        // UI paths confirmed from F9 dumps
        private const string PathWorldMapBtn =
            "TownViewController/Canvas/CanvasShaker/UI_MenuList (Carousel)/ButtonCarouselContainer/content/WorldMap/_WorldMap";
        private const string PathDairyPlantBtn =
            "LevelSelect/LevelSelectUI/UI_MenuList (Carousel)/ButtonCarouselContainer/content/DairyPlant/_DairyPlant";
        private const string PathDairyPlantPanel =
            "LevelSelect/LevelSelectUI/Stages/MapLocationInfo (Dairy Plant)";
        private const string PathStartDungeonBtn =
            "LevelSelect/LevelSelectUI/Stages/MapLocationInfo (Dairy Plant)/StartDungeonButton/_StartDungeonButton";
        private const string PathRightSwipeBtn =
            "LevelSelect/LevelSelectUI/Stages/MapLocationInfo (Dairy Plant)/RightSwipe/_button";
        private const string PathLeftSwipeBtn =
            "LevelSelect/LevelSelectUI/Stages/MapLocationInfo (Dairy Plant)/LeftSwipe/_button";
        private const string PathYesBtn =
            "MessageBoxManager/Canvas/Message - Border/Buttons/YesButton/_YesButton";
        private const string PathNoBtn =
            "MessageBoxManager/Canvas/Message - Border/Buttons/NoButton/_NoButton";

        // ── Scene ────────────────────────────────────────────────────────────

        internal string GetCurrentScene() => SceneManager.GetActiveScene().name;
        internal bool IsInVillage() => GetCurrentScene() == SceneVillage;
        internal bool IsInDungeon() => GetCurrentScene() == SceneDungeon;

        // ── UI state queries ─────────────────────────────────────────────────

        internal bool IsWorldMapButtonVisible()
        {
            var go = GameObject.Find(PathWorldMapBtn);
            return go != null && go.activeInHierarchy;
        }

        internal Button GetWorldMapButton() => FindButton(PathWorldMapBtn);

        internal bool IsLevelSelectOpen()
        {
            // Root "LevelSelect" GO can be slow to activate after WorldMap click.
            // DairyPlant button existing + active is a more reliable signal.
            return GetDairyPlantButton() != null;
        }

        internal Button GetDairyPlantButton() => FindButton(PathDairyPlantBtn);

        internal bool IsDairyPlantPanelVisible()
        {
            var go = GameObject.Find(PathDairyPlantPanel);
            return go != null && go.activeInHierarchy;
        }

        internal Button GetStartDungeonButton() => FindButton(PathStartDungeonBtn);
        internal Button GetRightSwipeButton()   => FindButton(PathRightSwipeBtn);
        internal Button GetLeftSwipeButton()    => FindButton(PathLeftSwipeBtn);
        internal Button GetYesButton()          => FindButton(PathYesBtn);
        internal Button GetNoButton()           => FindButton(PathNoBtn);

        // ── Dump helpers ─────────────────────────────────────────────────────

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

        // ── Internal helpers ─────────────────────────────────────────────────

        private static Button FindButton(string path)
        {
            var go = GameObject.Find(path);
            if (go == null) return null;
            return go.GetComponent<Button>();
        }
    }
}
