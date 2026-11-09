using System;

namespace ER2FovUnlock
{
    /// <summary>
    /// Read-only access to the game's own setting value: SavableData.Settings.system.fieldOfView(int).
    /// The plugin never writes back to this field, so after deleting the DLL, the FOV settings in the game's save file are completely unaffected.
    /// </summary>
    internal static class GameFov
    {
        private static bool _warned;

        internal static bool TryReadSettingsFov(out float fov)
        {
            fov = 0f;
            try
            {
                var settings = SavableData.Settings;
                if (settings == null)
                    return false;

                var system = settings.system;
                if (system == null)
                    return false;

                fov = system.fieldOfView;
                return fov > 1f && fov < 179f;
            }
            catch (Exception e)
            {
                if (!_warned)
                {
                    _warned = true;
                    FovUnlockPlugin.Logger.LogWarning(
                        "[FOV] Failed to read the game's FOV settings：" + e.Message);
                }
                return false;
            }
        }
    }
}
