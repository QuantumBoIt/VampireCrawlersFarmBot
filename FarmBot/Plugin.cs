using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace VampireCrawlersFarmBot
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public override void Load()
        {
            BotLogger.Init(Log);
            _ = new BotConfig(Config);
            BotLogger.Essential($"FarmBot loaded. Version: {PluginInfo.PLUGIN_VERSION}");
            AddComponent<FarmBotRunner>();
        }
    }
}
