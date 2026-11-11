using BepInEx.Configuration;
using UnityEngine;

namespace ER2FovUnlock
{
    internal enum FovReference
    {
        Vertical,
        Horizontal
    }

    internal enum CameraTarget
    {
        MainCameraOnly,
        AllCameras,
        ByName
    }

    /// <summary>Source used to detect first / third person view.</summary>
    internal enum ViewModeSource
    {
        Auto,
        ForceFirstPerson,
        ForceThirdPerson
    }

    internal sealed class FovConfig
    {
        internal readonly ConfigEntry<bool> Enabled;
        internal readonly ConfigEntry<bool> OnlyInMatch;
        internal readonly ConfigEntry<float> FovFirstPerson;
        internal readonly ConfigEntry<float> FovThirdPerson;
        internal readonly ConfigEntry<float> AimZoomRatio;
        internal readonly ConfigEntry<float> HoldBreathZoomRatio;
        internal readonly ConfigEntry<float> AimZoomStrength;
        internal readonly ConfigEntry<float> ZoomSmoothSpeed;
        internal readonly ConfigEntry<float> HoldBreathSmoothSpeed;
        internal readonly ConfigEntry<float> HoldBreathCurve;
        internal readonly ConfigEntry<bool> EnhancedBreathOnFoot;
        internal readonly ConfigEntry<bool> FlightZoom;
        internal readonly ConfigEntry<ViewModeSource> ViewModeOverride;
        internal readonly ConfigEntry<FovReference> Reference;
        internal readonly ConfigEntry<CameraTarget> Target;
        internal readonly ConfigEntry<string> CameraNames;
        internal readonly ConfigEntry<KeyCode> MenuKey;
        internal readonly ConfigEntry<KeyCode> ToggleKey;
        internal readonly ConfigEntry<KeyCode> FovIncreaseKey;
        internal readonly ConfigEntry<KeyCode> FovDecreaseKey;
        internal readonly ConfigEntry<float> FovHotkeyStep;
        internal readonly ConfigEntry<bool> UnlockCursor;
        internal readonly ConfigEntry<PluginLanguage> LanguageOption;
        internal readonly ConfigEntry<float> VanillaViewRatio;
        internal readonly ConfigEntry<string> ExcludedCameraNames;
        internal readonly ConfigEntry<bool> BlockInputViaPause;
        internal readonly ConfigEntry<bool> UseInventoryGate;
        internal readonly ConfigEntry<bool> DebugWarnings;
        internal readonly ConfigEntry<bool> VerboseDebug;

        internal FovConfig(ConfigFile config)
        {
            Enabled = config.Bind(
                "1.General", "Enabled", true,
                "Master switch. When disabled, the game's native FOV is restored immediately. " +
                "The plugin never writes to the game's saved FOV setting.");

            OnlyInMatch = config.Bind(
                "1.General", "OnlyInMatch", true,
                "Only override FOV during an actual match. The 3D camera on the main menu / loading screens is left untouched.");

            FovFirstPerson = config.Bind(
                "1.General", "FovFirstPerson", 100f,
                new ConfigDescription(
                    "First person field of view in degrees. In-game slider range: 65-130.",
                    new AcceptableValueRange<float>(65f, 130f)));

            FovThirdPerson = config.Bind(
                "1.General", "FovThirdPerson", 100f,
                new ConfigDescription(
                    "Third person field of view in degrees. In-game slider range: 65-130. " +
                    "Independent from first person.",
                    new AcceptableValueRange<float>(65f, 130f)));

            AimZoomRatio = config.Bind(
                "2.Aim", "AimZoomRatio", 0.75f,
                new ConfigDescription(
                    "FOV multiplier while aiming (right mouse). 0.75 means the view is zoomed to 75% of the configured FOV.",
                    new AcceptableValueRange<float>(0.70f, 1.00f)));

            HoldBreathZoomRatio = config.Bind(
                "2.Aim", "HoldBreathZoomRatio", 0.55f,
                new ConfigDescription(
                    "FOV multiplier while holding breath (Shift). Takes priority over AimZoomRatio.",
                    new AcceptableValueRange<float>(0.45f, 0.70f)));

            AimZoomStrength = config.Bind(
                "2.Aim", "AimZoomStrength", 1f,
                new ConfigDescription(
                    "Overall zoom strength: 1 = apply the two multipliers fully, 0 = never zoom (FOV stays fixed).",
                    new AcceptableValueRange<float>(0f, 1f)));

            ZoomSmoothSpeed = config.Bind(
                "2.Aim", "ZoomSmoothSpeed", 2f,
                new ConfigDescription(
                    "Linear transition speed of the zoom multiplier (ratio per second). Lower is softer; 0 = instant. " +
                    "Only affects the zoom multiplier: dragging the FOV slider still applies instantly.",
                    new AcceptableValueRange<float>(0f, 10f)));

            HoldBreathSmoothSpeed = config.Bind(
                "2.Aim", "HoldBreathSmoothSpeed", 0.7f,
                new ConfigDescription(
                    "Transition speed (ratio per second) for Breath-holding Animation Improvement on foot. Lower feels slower and more deliberate. " +
                    "Vehicles and mounted weapons use ZoomSmoothSpeed instead.",
                    new AcceptableValueRange<float>(0.1f, 2f)));

            HoldBreathCurve = config.Bind(
                "2.Aim", "HoldBreathCurve", 4f,
                new ConfigDescription(
                    "Exponential ease-in strength for Breath-holding Animation Improvement. Higher means 'slow at first, then a sudden snap to full zoom'. " +
                    "0.5 is close to linear. Vehicles and mounted weapons are not affected.",
                    new AcceptableValueRange<float>(0.5f, 10f)));

            EnhancedBreathOnFoot = config.Bind(
                "2.Aim", "EnhancedBreathOnFoot", true,
                "Enable Breath-holding Animation Improvement (slower, exponential ease-in) for on-foot infantry only. " +
                "When off, infantry uses the same basic breath zoom as vehicles and mounted weapons. " +
                "Can also be toggled directly in the in-game menu.");

            FlightZoom = config.Bind(
                "2.Aim", "FlightZoom", true,
                "Enable hold-breath zoom in third-person air vehicles without aiming first. " +
                "When disabled, the breath key does nothing in a third-person aircraft. " +
                "First-person aircraft are unaffected (aiming is still required).");

            ViewModeOverride = config.Bind(
                "3.Advanced", "ViewModeOverride", ViewModeSource.Auto,
                "Auto = detect first / third person from the game. Force one of the other values if detection is wrong.");

            Reference = config.Bind(
                "3.Advanced", "Reference", FovReference.Vertical,
                "Vertical = Unity's native vertical FOV. Horizontal = interpret the value as horizontal FOV and convert using the aspect ratio.");

            Target = config.Bind(
                "3.Advanced", "Target", CameraTarget.AllCameras,
                "MainCameraOnly = only Camera.main. AllCameras = every camera (recommended). ByName = only cameras matching CameraNames.");

            CameraNames = config.Bind(
                "3.Advanced", "CameraNames", "*",
                "Used when Target=ByName. Comma-separated camera names, * wildcard supported. " +
                "Enable DebugWarnings to see the actual camera names.");

            MenuKey = config.Bind(
                "4.Menu", "MenuKey", KeyCode.F8,
                "Key that opens / closes the in-game FOV menu. Can be rebound from the menu.");

            ToggleKey = config.Bind(
                "4.Menu", "ToggleKey", KeyCode.F7,
                "Key that enables / disables the FOV override. Set to None to unbind and use only the menu toggle.");

            FovIncreaseKey = config.Bind(
                "4.Menu", "FovIncreaseKey", KeyCode.KeypadPlus,
                "Key that increases the FOV of the current view mode. Set to None to unbind.");

            FovDecreaseKey = config.Bind(
                "4.Menu", "FovDecreaseKey", KeyCode.KeypadMinus,
                "Key that decreases the FOV of the current view mode. Set to None to unbind.");

            FovHotkeyStep = config.Bind(
                "4.Menu", "FovHotkeyStep", 1f,
                new ConfigDescription(
                    "Degrees changed per FOV hotkey press (hold to repeat).",
                    new AcceptableValueRange<float>(1f, 10f)));

            UnlockCursor = config.Bind(
                "4.Menu", "UnlockCursor", true,
                "Release and show the mouse cursor while the menu is open; restore on close.");

            LanguageOption = config.Bind(
                "4.Menu", "Language", PluginLanguage.Auto,
                "Menu language. Auto = follow the game language (Simplified Chinese, Traditional Chinese, Japanese; " +
                "all other languages fall back to English). Manual values: Auto, English, SimplifiedChinese, " +
                "TraditionalChinese, Japanese. Runtime log messages are always English.");

            VanillaViewRatio = config.Bind(
                "5.Diagnostics", "VanillaViewRatio", 0.45f,
                new ConfigDescription(
                    "Special-optics detection ratio. When the game's native camera FOV falls below " +
                    "(game setting FOV x this ratio), the plugin treats it as a scope / artillery / ground vehicle sight " +
                    "and fully yields control back to the game. Raise to yield more often; lower if normal breath zoom is misdetected.",
                    new AcceptableValueRange<float>(0.2f, 0.8f)));

            ExcludedCameraNames = config.Bind(
                "5.Diagnostics", "ExcludedCameraNames", "scope,sight,optic,sniper",
                "Cameras whose name contains any of these comma-separated keywords are never overridden. " +
                "Used to exclude dedicated scope cameras.");

            BlockInputViaPause = config.Bind(
                "5.Diagnostics", "BlockInputViaPause", false,
                "Optional fallback: also set the game's Pause.isPaused flag while the menu is open. " +
                "Enable only if the inventory gate does not stop firing on your setup. " +
                "Off by default because the game pause flag may affect multiplayer. " +
                "Time.timeScale is never frozen.");

            UseInventoryGate = config.Bind(
                "5.Diagnostics", "UseInventoryGate", true,
                "While the menu is open, set the game's InventoryPanel.isOpen flag so the game itself " +
                "blocks firing / movement / looking (the same gate used by the vanilla inventory). " +
                "Does not open the inventory UI and never freezes Time.timeScale.");

            DebugWarnings = config.Bind(
                "5.Diagnostics", "DebugWarnings", false,
                "Silent by default. When enabled, prints match / view mode / optics-yield diagnostics at Warning level.");

            VerboseDebug = config.Bind(
                "5.Diagnostics", "VerboseDebug", false,
                "Full diagnostic log, cfg-only (never shown in the in-game menu). OFF by default. " +
                "While enabled and in a match, writes a complete state snapshot to BepInEx/LogOutput.log " +
                "every 2 seconds plus an immediate line on any key state change. " +
                "Use it to diagnose FOV / optics / vehicle issues; leave it off for normal play.");
        }
    }
}
