using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace VampireCrawlersFarmBot
{
    internal sealed class GameObserver
    {
        internal sealed class LevelUpOptionInfo
        {
            internal Button Button;
            internal string Label;
            internal bool IsGem;
            internal bool IsSafeCard;
            internal string Reason;
        }

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
        private const string PathStageSelectionPanel =
            "LevelSelect/LevelSelectUI/_LevelInfoContainer/UI_SelectionPanel_MapLocations";
        private const string PathStageInfoPanel =
            "LevelSelect/LevelSelectUI/_LevelInfoContainer/UI_SelectionPanel_MapLocations/BumphContianer/UI_MapLocations_StageInfoPanel";
        private const string PathTownLocationInfoPanel =
            "TownViewController/Canvas/CanvasShaker/UI_InfoPanel_TownLocations";
        private const string PathRightSwipeBtn =
            "LevelSelect/LevelSelectUI/Stages/MapLocationInfo (Dairy Plant)/RightSwipe/_button";
        private const string PathLeftSwipeBtn =
            "LevelSelect/LevelSelectUI/Stages/MapLocationInfo (Dairy Plant)/LeftSwipe/_button";
        private const string PathYesBtn =
            "MessageBoxManager/Canvas/Message - Border/Buttons/YesButton/_YesButton";
        private const string PathNoBtn =
            "MessageBoxManager/Canvas/Message - Border/Buttons/NoButton/_NoButton";
        private const string PathPauseExitGameBtn =
            "PauseMenuModal/Canvas/GameOptions PauseMenu/PauseFunctionalButtons/_ExitGameButton";
        private const string PathPauseAbortRunBtn =
            "PauseMenuModal/Canvas/GameOptions PauseMenu/PauseFunctionalButtons/_AbortRunButton";
        private const string PathPauseBtn =
            "CardGame/Player/EdgeCanvas/PauseButton";
        private const string PathResultsQuitBtn =
            "CardGame/ResultsSummaryModal/Canvas/QuitButton/_QuitButton";
        private const string PathAchievementQuitBtn =
            "CardGame/AchievementSummaryModal/Canvas/QuitButton/_QuitButton";
        private const string PathEndGameBtn =
            "CardGame/Player/Canvas/EndGameButtons/EndGameButton/_EndGameButton";

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

        internal bool IsTownInfoPanelShowingWorldMap()
        {
            var root = GameObject.Find(PathTownLocationInfoPanel);
            if (root == null || !root.activeInHierarchy) return false;

            var text = ReadTextBlob(root).ToLowerInvariant();
            bool looksWorldMap =
                text.Contains("worldmap") ||
                text.Contains("world map") ||
                text.Contains("world") && text.Contains("map") ||
                text.Contains("\u4e16\u754c") && text.Contains("\u5730\u56fe");

            if (!looksWorldMap)
                BotLogger.Info($"TownInfoPanel: not WorldMap yet, text='{TrimForLog(text)}'");
            return looksWorldMap;
        }

        internal Button GetDairyPlantButton() => FindButton(PathDairyPlantBtn);

        internal bool IsDairyPlantPanelVisible()
        {
            return IsStageSelectionPanelVisible();
        }

        internal bool IsStageSelectionPanelVisible()
        {
            return FindActiveObject(PathStageSelectionPanel) != null ||
                   FindActiveObjectByPathHints("levelselectui", "ui_selectionpanel_maplocations") != null ||
                   FindActiveObject(PathDairyPlantPanel) != null;
        }

        internal bool IsSelectedStage(string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName)) return true;

            var go = FindActiveObject(PathStageInfoPanel) ??
                     FindActiveObjectByPathHints("levelselectui", "ui_maplocations_stageinfopanel") ??
                     FindActiveObject(PathDairyPlantPanel);

            if (go == null)
            {
                BotLogger.Info($"StageSelect: no active stage info panel while looking for '{stageName}'");
                return false;
            }

            var text = ReadTextBlob(go);
            var normalizedText = NormalizeForMatch(text);
            var normalizedTarget = NormalizeForMatch(stageName);
            bool matched = normalizedText.Contains(normalizedTarget);
            BotLogger.Info(
                matched
                    ? $"StageSelect: selected stage matches '{stageName}'"
                    : $"StageSelect: selected stage is not '{stageName}', panel='{TrimForLog(text)}'");
            return matched;
        }

        internal Button GetStageButton(string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName)) return null;

            var target = NormalizeForMatch(stageName);
            foreach (var btn in UnityEngine.Object.FindObjectsOfType<Button>(true))
            {
                if (btn == null || !btn.interactable || !btn.gameObject.activeInHierarchy) continue;

                var path = BuildPath(btn.transform);
                var lowerPath = path.ToLowerInvariant();
                if (!lowerPath.Contains("levelselectui")) continue;
                if (!lowerPath.Contains("ui_maplocations_sublevelinfo") &&
                    !lowerPath.Contains("stageinfopanel") &&
                    !lowerPath.Contains("startdungeonbutton"))
                    continue;

                var text = ReadTextAround(btn.transform);
                var normalized = NormalizeForMatch(path + " " + text);
                if (!normalized.Contains(target)) continue;

                BotLogger.Info($"StageSelect: found stage button '{stageName}' at {path}, text='{TrimForLog(text)}'");
                return btn;
            }

            BotLogger.Info($"StageSelect: active stage button '{stageName}' not found");
            return null;
        }

        internal Button GetStartDungeonButton() =>
            FindActiveButton(PathStartDungeonBtn) ??
            FindActiveButtonByPathHints("levelselectui", "startdungeonbutton") ??
            FindActiveButtonByPathHints("ui_selectionpanel_maplocations", "startdungeonbutton");

        internal Button GetRightSwipeButton() =>
            FindActiveButton(PathRightSwipeBtn) ??
            FindActiveButtonByPathHints("levelselectui", "arrowrightbutton") ??
            FindActiveButtonByPathHints("levelselectui", "rightswipe");

        internal Button GetLeftSwipeButton() =>
            FindActiveButton(PathLeftSwipeBtn) ??
            FindActiveButtonByPathHints("levelselectui", "arrowleftbutton") ??
            FindActiveButtonByPathHints("levelselectui", "leftswipe");

        internal Button GetYesButton()          => FindButton(PathYesBtn);
        internal Button GetNoButton()           => FindButton(PathNoBtn);
        internal GameObject GetYesButtonObject() => FindActiveObject(PathYesBtn) ?? FindActiveObjectByPathHints("messageboxmanager", "yesbutton");
        internal Button GetPauseButton() => FindActiveButton(PathPauseBtn) ?? FindActiveButtonByPathHints("edgecanvas", "pausebutton");
        internal GameObject GetPauseButtonObject() => FindActiveObject(PathPauseBtn) ?? FindActiveObjectByPathHints("edgecanvas", "pausebutton");
        internal Button GetPauseExitGameButton() =>
            FindActiveButton(PathPauseAbortRunBtn) ??
            FindActiveButtonByPathHints("pause", "abortrun") ??
            FindActiveButton(PathPauseExitGameBtn) ??
            FindActiveButtonByPathHints("pause", "exitgame");
        internal bool IsPauseMenuVisible() => GetPauseExitGameButton() != null;

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

        internal Button GetSafeLevelUpCardButton()
        {
            var options = GetLevelUpOptions();
            foreach (var option in options)
            {
                BotLogger.Info($"LevelUpOption: {option.Label} safe={option.IsSafeCard} gem={option.IsGem} reason={option.Reason}");
                if (option.IsSafeCard && option.Button != null && option.Button.interactable)
                    return option.Button;
            }
            return null;
        }

        internal List<LevelUpOptionInfo> GetLevelUpOptions()
        {
            var result = new List<LevelUpOptionInfo>();
            var container = GameObject.Find(PathChooseCardContainer);
            if (container == null) return result;

            for (int i = 0; i < container.transform.childCount; i++)
            {
                var child = container.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;

                var option = AnalyzeLevelUpOption(child);
                option.Label = $"{i}:{BuildPath(child)}";
                result.Add(option);
            }
            return result;
        }

        private static LevelUpOptionInfo AnalyzeLevelUpOption(Transform root)
        {
            var option = new LevelUpOptionInfo
            {
                Button = root.GetComponentInChildren<Button>(),
                IsGem = false,
                IsSafeCard = false,
                Reason = "unknown"
            };

            bool hasCardView = false;
            bool hasNormalCardName = false;
            var evidence = new StringBuilder();
            AnalyzeLevelUpTree(root, ref hasCardView, ref hasNormalCardName, option, evidence, 0);

            if (option.IsGem)
            {
                option.IsSafeCard = false;
                option.Reason = evidence.Length > 0 ? evidence.ToString() : "gem evidence";
            }
            else if (hasCardView || hasNormalCardName)
            {
                option.IsSafeCard = true;
                option.Reason = evidence.Length > 0 ? evidence.ToString() : "card visual/model without gem evidence";
            }
            else
            {
                option.Reason = evidence.Length > 0 ? evidence.ToString() : "no card model evidence";
            }

            return option;
        }

        private static void AnalyzeLevelUpTree(
            Transform t,
            ref bool hasCardView,
            ref bool hasNormalCardName,
            LevelUpOptionInfo option,
            StringBuilder evidence,
            int depth)
        {
            if (t == null || depth > 12) return;

            var go = t.gameObject;
            var lowerName = go.name.ToLowerInvariant();
            if (lowerName.Contains("animatedgemview") || lowerName.Contains("gemview") ||
                lowerName.Contains("gemcard") || lowerName == "card_pickup")
            {
                option.IsGem = true;
                AppendEvidence(evidence, $"name={go.name}");
            }

            if (go.name.StartsWith("Card_", StringComparison.Ordinal) && lowerName != "card_pickup" &&
                !lowerName.Contains("rewardchest"))
            {
                hasNormalCardName = true;
                AppendEvidence(evidence, $"cardGO={go.name}");
            }

            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var typeName = SafeTypeName(c);
                var lowerType = typeName.ToLowerInvariant();
                if (lowerType == "cardview")
                {
                    hasCardView = true;
                    AppendEvidence(evidence, $"component={typeName}");
                    AnalyzeCardViewModel(c, option, evidence);
                }
                else if (lowerType.Contains("animatedgemview") || lowerType.Contains("gemreward") ||
                         lowerType.Contains("gemcard") || lowerType.Contains("jewel") ||
                         lowerType.Contains("socket"))
                {
                    option.IsGem = true;
                    AppendEvidence(evidence, $"component={typeName}");
                }
            }

            for (int i = 0; i < t.childCount; i++)
                AnalyzeLevelUpTree(t.GetChild(i), ref hasCardView, ref hasNormalCardName, option, evidence, depth + 1);
        }

        private static void AnalyzeCardViewModel(Component cardView, LevelUpOptionInfo option, StringBuilder evidence)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            object model = TryGetMember(cardView, "CardModel", flags) ?? TryGetMember(cardView, "_cardModel", flags);
            if (model == null) return;

            AppendEvidence(evidence, $"model={SafeTypeName(model)}");
            AnalyzeModelObject(model, option, evidence, 0);

            object config = TryGetMember(model, "CardConfig", flags) ??
                            TryGetMember(model, "BaseCardConfig", flags) ??
                            TryGetMember(model, "_cardConfig", flags);
            if (config != null)
            {
                AppendEvidence(evidence, $"config={SafeTypeName(config)}");
                AnalyzeModelObject(config, option, evidence, 0);
            }
        }

        private static void AnalyzeModelObject(object obj, LevelUpOptionInfo option, StringBuilder evidence, int depth)
        {
            if (obj == null || depth > 1) return;

            var typeName = SafeTypeName(obj);
            if (LooksLikeGemText(typeName))
            {
                option.IsGem = true;
                AppendEvidence(evidence, $"gemType={typeName}");
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var name in new[] { "Name", "DisplayName", "Id", "ID", "Key", "LocalizationKey", "_name", "_id" })
            {
                var value = TryGetMember(obj, name, flags);
                if (value == null) continue;
                var text = value.ToString();
                if (string.IsNullOrEmpty(text)) continue;
                AppendEvidence(evidence, $"{name}={text}");
                if (LooksLikeGemText(text)) option.IsGem = true;
            }
        }

        private static object TryGetMember(object obj, string name, BindingFlags flags)
        {
            if (obj == null) return null;
            var type = obj.GetType();
            for (var t = type; t != null; t = t.BaseType)
            {
                try
                {
                    var prop = t.GetProperty(name, flags);
                    if (prop != null && prop.GetIndexParameters().Length == 0)
                        return prop.GetValue(obj);
                }
                catch { }
                try
                {
                    var field = t.GetField(name, flags);
                    if (field != null) return field.GetValue(obj);
                }
                catch { }
            }
            return null;
        }

        private static bool LooksLikeGemText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var lower = text.ToLowerInvariant();
            return lower.Contains("gem") || lower.Contains("jewel") || lower.Contains("socket") ||
                   lower.Contains("pickup") || lower.Contains("\u5b9d\u77f3");
        }

        private static string SafeTypeName(object obj)
        {
            try
            {
                if (obj is Component c)
                    return c.GetIl2CppType()?.Name ?? c.GetType().Name;
                return obj.GetType().Name;
            }
            catch { return "(unknown)"; }
        }

        private static void AppendEvidence(StringBuilder sb, string value)
        {
            if (sb.Length > 240) return;
            if (sb.Length > 0) sb.Append("; ");
            sb.Append(value);
        }

        private static string BuildPath(Transform t)
        {
            if (t == null) return "(null)";
            if (t.parent == null) return t.name;
            return BuildPath(t.parent) + "/" + t.name;
        }

        // ── Dungeon: chest interaction menu (WorldSpace MenuCanvas on chest) ──

        internal Button GetChestDoneButton() => FindChestMenuButton("DoneButton", "done", "cash", "cashout", "coin");
        internal Button GetChestOpenButton() => FindChestMenuButton("OpenButton", "open");

        private static Button FindChestMenuButton(string childName, params string[] looseHints)
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

            foreach (var btn in UnityEngine.Object.FindObjectsOfType<Button>(true))
            {
                if (btn == null || !btn.interactable || !btn.gameObject.activeInHierarchy) continue;
                var path = BuildPath(btn.transform);
                var lower = path.ToLowerInvariant();
                if (!LooksLikeChestButtonContext(lower)) continue;

                if (btn.gameObject.name == childName || lower.Contains(childName.ToLowerInvariant()))
                    return btn;

                foreach (var hint in looseHints)
                    if (lower.Contains(hint))
                        return btn;
            }
            return null;
        }

        private static bool LooksLikeChestButtonContext(string lowerPath)
            => lowerPath.Contains("chest") ||
               lowerPath.Contains("treasure") ||
               lowerPath.Contains("reward") ||
               lowerPath.Contains("coins") ||
               lowerPath.Contains("cash");

        // ── Dungeon: exit to village ─────────────────────────────────────────

        private const string PathExitToVillageBtn =
            "CardGame/EndOfDemoModal/Canvas/Container/ExitToVillageButton/_ExitToVillageButton";

        internal Button GetExitToVillageButton() => FindActiveButton(PathExitToVillageBtn);
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
            => GameObject.Find("DungeonPlayer")?.transform ?? GameObject.Find("CardGame/Player")?.transform;

        // ── Dungeon: post-run screens ──

        internal bool IsGameOverVisible()
        {
            var results = GameObject.Find("CardGame/ResultsSummaryModal/Canvas");
            if (results != null && results.activeInHierarchy) return true;

            var endGame = GameObject.Find(PathEndGameBtn);
            if (endGame != null && endGame.activeInHierarchy) return true;

            var oldScreen = GameObject.Find("CardGame/Player/Canvas/RenderTextureWindow/BackgroundTextureCanvas/GameOverScreen");
            return oldScreen != null && oldScreen.activeInHierarchy;
        }

        internal bool IsBattleStatsVisible()
        {
            var go = GameObject.Find("CardGame/AchievementSummaryModal/Canvas");
            return go != null && go.activeInHierarchy;
        }

        internal Button GetCloseGameOverButton()
            => FindActiveButton(PathResultsQuitBtn) ??
               FindActiveButtonByPathHints("resultssummarymodal", "quitbutton");

        internal Button GetEndGameButton()
            => FindActiveButton(PathEndGameBtn) ??
               FindActiveButtonByPathHints("endgamebuttons", "endgamebutton");

        internal Button GetCloseBattleStatsButton()
            => FindActiveButton(PathAchievementQuitBtn) ?? FindActiveButtonByPathHints("achievementsummarymodal", "quitbutton");

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

        private static Button FindActiveButton(string path)
        {
            var go = GameObject.Find(path);
            if (go == null || !go.activeInHierarchy) return null;
            return go.GetComponent<Button>();
        }

        private static GameObject FindActiveObject(string path)
        {
            var go = GameObject.Find(path);
            return go != null && go.activeInHierarchy ? go : null;
        }

        private static string ReadTextBlob(GameObject root)
        {
            var sb = new StringBuilder();
            try
            {
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    try
                    {
                        if (text != null && !string.IsNullOrEmpty(text.text))
                            sb.Append(text.text).Append(' ');
                    }
                    catch { }
                }

                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    try
                    {
                        if (text != null && !string.IsNullOrEmpty(text.text))
                            sb.Append(text.text).Append(' ');
                    }
                    catch { }
                }

                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    try
                    {
                        if (c == null) continue;
                        var t = c.GetType();
                        foreach (var name in new[] { "text", "Text", "m_text", "Value", "value" })
                        {
                            var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (prop != null && prop.PropertyType == typeof(string))
                            {
                                var value = prop.GetValue(c) as string;
                                if (!string.IsNullOrEmpty(value)) sb.Append(value).Append(' ');
                            }

                            var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (field != null && field.FieldType == typeof(string))
                            {
                                var value = field.GetValue(c) as string;
                                if (!string.IsNullOrEmpty(value)) sb.Append(value).Append(' ');
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return sb.ToString();
        }

        private static string ReadTextAround(Transform t)
        {
            var sb = new StringBuilder();
            for (int depth = 0; t != null && depth < 2; depth++, t = t.parent)
                sb.Append(ReadTextBlob(t.gameObject)).Append(' ');
            return sb.ToString();
        }

        private static string TrimForLog(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            value = value.Replace("\r", " ").Replace("\n", " ");
            return value.Length <= 160 ? value : value.Substring(0, 160);
        }

        private static string NormalizeForMatch(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace(" ", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .ToLowerInvariant();
        }

        private static Button FindActiveButtonByPathHints(params string[] hints)
        {
            foreach (var btn in UnityEngine.Object.FindObjectsOfType<Button>(true))
            {
                if (btn == null || !btn.interactable || !btn.gameObject.activeInHierarchy) continue;
                var lower = BuildPath(btn.transform).ToLowerInvariant();
                bool all = true;
                foreach (var hint in hints)
                {
                    if (!lower.Contains(hint))
                    {
                        all = false;
                        break;
                    }
                }
                if (all) return btn;
            }
            return null;
        }

        private static GameObject FindActiveObjectByPathHints(params string[] hints)
        {
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>(true))
            {
                if (go == null || !go.activeInHierarchy) continue;
                var lower = BuildPath(go.transform).ToLowerInvariant();
                bool all = true;
                foreach (var hint in hints)
                {
                    if (!lower.Contains(hint))
                    {
                        all = false;
                        break;
                    }
                }
                if (all) return go;
            }
            return null;
        }
    }
}
