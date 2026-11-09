using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace ER2FovUnlock
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Easy Red 2.exe")]
    public sealed class FovUnlockPlugin : BasePlugin
    {
        internal const string PluginGuid = "com.wenyi.er2.fovunlock";
        internal const string PluginName = "ER2 FOV Unlock";
        internal const string PluginVersion = "1.0.0";

        /// <summary>Public-facing config file name for the Nexus release.</summary>
        internal const string ConfigFileName = "ER2_FOVUnlock.cfg";

        internal static FovUnlockPlugin Instance { get; private set; }
        internal static ManualLogSource Logger { get; private set; }
        internal FovConfig FovConfig { get; private set; }

        private ConfigFile _configFile;

        public override void Load()
        {
            Instance = this;
            Logger = base.Log;

            // BasePlugin.Config is read-only and tied to the GUID filename,
            // so use our own ConfigFile with the release-friendly name.
            _configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, ConfigFileName), false);
            FovConfig = new FovConfig(_configFile);

            // Use BepInEx's official AddComponent<T>(): it attaches the component to the
            // persistent BepInEx_Manager and registers the IL2CPP type for us.
            var component = AddComponent<FovBehaviour>();
            if (component == null)
                Logger.LogError("[FOV] Failed to attach component; plugin disabled.");
        }

        /// <summary>Let the running plugin pick up external edits to the cfg file.</summary>
        internal void ReloadConfigFile()
        {
            _configFile.Reload();
        }
    }
}
