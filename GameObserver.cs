using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

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

        // ── Dungeon: level-up card modal ─────────────────────────────────────

        // Path to the card container; each child is a LevelUpCardChoice (N).
        private const string PathChooseCardContainer =
            "ChooseCardModal(Clone)/Container/Canvas/ChooseACard/CardsContainer";

        internal bool IsChooseCardModalVisible()
        {
            var go = GameObject.Find(PathChooseCardContainer);
            return go != null && go.activeInHierarchy;
        }

        internal Button GetFirstLevelUpCardButton()
        {
            var container = GameObject.Find(PathChooseCardContainer);
            if (container == null) return null;
            for (int i = 0; i < container.transform.childCount; i++)
            {
                var child = container.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var btn = child.GetComponentInChildren<Button>();
                if (btn != null && btn.interactable && btn.gameObject.activeInHierarchy)
                    return btn;
            }
            return null;
        }

        // ── Dungeon: chest interaction menu (WorldSpace MenuCanvas on chest) ──

        internal Button GetChestDoneButton() => FindChestMenuButton("DoneButton");
        internal Button GetChestOpenButton() => FindChestMenuButton("OpenButton");

        private static Button FindChestMenuButton(string childName)
        {
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                if (canvas.name != "MenuCanvas") continue;
                if (!canvas.gameObject.activeInHierarchy) continue;
                var t = canvas.transform.Find(childName);
                if (t == null) continue;
                var btn = t.GetComponent<Button>();
                if (btn != null) return btn;
            }
            return null;
        }

        // ── Dungeon: exit to village ─────────────────────────────────────────

        private const string PathExitToVillageBtn =
            "CardGame/EndOfDemoModal/Canvas/Container/ExitToVillageButton/_ExitToVillageButton";

        internal Button GetExitToVillageButton() => FindButton(PathExitToVillageBtn);
        internal bool IsExitMenuVisible()        => GetExitToVillageButton() != null;

        // ── Dungeon: nuke ────────────────────────────────────────────────────

        private const string PathNukeRoot =
            "CardGame/Player/Canvas/ShakeContainer/DungeonMovement/3DDungeonMovement/Holder/BombaInfernale";
        private const string PathNukeBtn = PathNukeRoot + "/button";

        internal GameObject GetNukeButtonObject()
        {
            var go = GameObject.Find(PathNukeBtn);
            return go != null && go.activeInHierarchy ? go : null;
        }

        internal bool IsNukeVisible()
        {
            var root = GameObject.Find(PathNukeRoot);
            return root != null && root.activeInHierarchy;
        }

        internal List<GameObject> GetNukeClickTargets()
        {
            var targets = new List<GameObject>();
            var confirmedButton = GameObject.Find(PathNukeBtn);
            AddIfActive(targets, confirmedButton);
            if (targets.Count > 0) return targets;

            var root = GameObject.Find(PathNukeRoot);
            if (root == null || !root.activeInHierarchy) return targets;
            AddNukeTargetsRecursive(root.transform, targets);
            AddIfActive(targets, root);
            return targets;
        }

        private static void AddNukeTargetsRecursive(Transform t, List<GameObject> targets)
        {
            if (t == null) return;
            var go = t.gameObject;
            if (go.activeInHierarchy && LooksClickableForNuke(go))
                AddIfActive(targets, go);

            for (int i = 0; i < t.childCount; i++)
                AddNukeTargetsRecursive(t.GetChild(i), targets);
        }

        private static bool LooksClickableForNuke(GameObject go)
        {
            var lower = go.name.ToLowerInvariant();
            if (lower == "button" || lower.Contains("button"))
                return true;

            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var typeName = c.GetIl2CppType()?.Name ?? c.GetType().Name;
                var lowerType = typeName.ToLowerInvariant();
                if (lowerType == "nukebutton" || lowerType == "button" || lowerType == "inputbuttonglyphhandler")
                    return true;
            }
            return false;
        }

        private static void AddIfActive(List<GameObject> targets, GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return;
            if (!targets.Contains(go)) targets.Add(go);
        }

        // ── Dungeon: player ──────────────────────────────────────────────────

        internal Transform GetPlayerTransform()
            => GameObject.Find("CardGame/Player")?.transform;

        // ── Dungeon: post-run screens (paths TBD — need in-dungeon F9 dump) ──

        internal bool IsGameOverVisible()         => false; // TODO
        internal bool IsBattleStatsVisible()      => false; // TODO
        internal Button GetCloseGameOverButton()  => null;  // TODO
        internal Button GetCloseBattleStatsButton() => null; // TODO

        // ── Minimap ──────────────────────────────────────────────────────────────

        // DungeonMinimap is unique in the dungeon scene.
        internal Transform GetMinimapTransform() => GameObject.Find("DungeonMinimap")?.transform;

        // Returns the PlayerIcon's local position within DungeonMinimap.
        // Shifts by ~one cell each time the player moves to an adjacent room.
        // Returns Vector2.zero when minimap or icon is not found.
        internal Vector2 GetMinimapPlayerPos()
        {
            var minimap = GetMinimapTransform();
            if (minimap == null) return Vector2.zero;
            var icon = FindDescendant(minimap, "PlayerIcon");
            if (icon == null) return Vector2.zero;
            var lp = icon.localPosition;
            return new Vector2(lp.x, lp.y);
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindDescendant(child, name);
                if (found != null) return found;
            }
            return null;
        }

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
