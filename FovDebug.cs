using System;
using System.Text;

namespace ER2FovUnlock
{
    /// <summary>
    /// Opt-in verbose diagnostics. This switch exists ONLY in ER2_FOVUnlock.cfg
    /// (VerboseDebug, off by default) and is never shown in the in-game menu.
    ///
    /// While enabled and in a match it writes a complete state snapshot to
    /// BepInEx/LogOutput.log every 2 seconds, plus one line whenever a key state
    /// changes. Everything is English and goes through LogWarning so it lands in
    /// the same log file as the rest of BepInEx.
    ///
    /// The per-frame part is allocation-free (a packed int state key), so leaving
    /// the option off - or even on - does not add per-frame garbage.
    /// </summary>
    internal static class FovDebug
    {
        private const long SnapshotIntervalMs = 2000;
        private static readonly StringBuilder Sb = new StringBuilder(768);
        private static long _nextSnapshotMs;
        private static int _lastStateKey = int.MinValue;
        private static bool _wasEnabled;
        private static bool _errorLogged;

        internal static void Tick(FovConfig cfg)
        {
            // Diagnostics must never be able to break the game loop.
            try
            {
                TickCore(cfg);
            }
            catch (Exception e)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    FovUnlockPlugin.Logger.LogWarning("[FOV][Verbose] Diagnostics error: " + e.Message);
                }
            }
        }

        private static void TickCore(FovConfig cfg)
        {
            bool on = cfg.VerboseDebug.Value;
            if (!on)
            {
                if (_wasEnabled)
                    FovUnlockPlugin.Logger.LogWarning("[FOV][Verbose] Diagnostics disabled.");
                _wasEnabled = false;
                _nextSnapshotMs = 0;
                _lastStateKey = int.MinValue;
                return;
            }

            if (!_wasEnabled)
            {
                _wasEnabled = true;
                _lastStateKey = int.MinValue;
                _nextSnapshotMs = 0;
                FovUnlockPlugin.Logger.LogWarning(
                    "[FOV][Verbose] Diagnostics ENABLED (ER2 FOV Unlock " +
                    FovUnlockPlugin.PluginVersion + "). Full snapshots every 2s while in a match.");
            }

            if (!GameState.InMatch)
                return;

            int key = StateKey();
            if (key != _lastStateKey)
            {
                _lastStateKey = key;
                WriteStateLine();
            }

            long now = Environment.TickCount64;
            if (now < _nextSnapshotMs)
                return;
            _nextSnapshotMs = now + SnapshotIntervalMs;
            WriteSnapshot(cfg);
        }

        /// <summary>Packs all important booleans into one int: no string allocation per frame.</summary>
        private static int StateKey()
        {
            int k = 0;
            if (GameState.InMatch) k |= 1;
            if (GameState.View == ViewMode.FirstPerson) k |= 2;
            if (GameState.IsInVehicle) k |= 4;
            if (GameState.IsAirVehicle) k |= 8;
            if (GameState.IsOnFoot) k |= 16;
            if (GameState.Aiming) k |= 32;
            if (GameState.AimPressed) k |= 64;
            if (GameState.HoldingBreath) k |= 128;
            if (FovEngine.VehicleSightYield) k |= 256;
            if (FovMenu.IsOpen) k |= 512;
            if (GameState.UsingScope) k |= 1024;
            if (FovEngine.ScopeProtection) k |= 2048;
            k |= Math.Min(FovEngine.YieldedCount, 15) << 16;
            return k;
        }

        private static void WriteStateLine()
        {
            Sb.Clear();
            Sb.Append("[FOV][Verbose] state -> inMatch=").Append(GameState.InMatch)
              .Append(" view=").Append(GameState.View == ViewMode.FirstPerson ? "FP" : "TP")
              .Append(" inVehicle=").Append(GameState.IsInVehicle)
              .Append(" air=").Append(GameState.IsAirVehicle)
              .Append(" onFoot=").Append(GameState.IsOnFoot)
              .Append(" aiming=").Append(GameState.Aiming)
              .Append(" aimPressed=").Append(GameState.AimPressed)
              .Append(" breath=").Append(GameState.HoldingBreath)
              .Append(" usingScope=").Append(GameState.UsingScope)
              .Append(" vehicleSight=").Append(FovEngine.VehicleSightYield)
              .Append(" scopeProtection=").Append(FovEngine.ScopeProtection)
              .Append(" yielded=").Append(FovEngine.YieldedCount)
              .Append(" menu=").Append(FovMenu.IsOpen);
            FovUnlockPlugin.Logger.LogWarning(Sb.ToString());
        }

        private static void WriteSnapshot(FovConfig cfg)
        {
            // Line 1: core state.
            Sb.Clear();
            Sb.Append("[FOV][Verbose] core: version=").Append(FovUnlockPlugin.PluginVersion)
              .Append(" enabled=").Append(cfg.Enabled.Value)
              .Append(" onlyInMatch=").Append(cfg.OnlyInMatch.Value)
              .Append(" inMatch=").Append(GameState.InMatch)
              .Append(" active=").Append(FovEngine.IsActive)
              .Append(" hasApplied=").Append(FovEngine.HasApplied)
              .Append(" | view=").Append(GameState.View == ViewMode.FirstPerson ? "FP" : "TP")
              .Append(" viewOverride=").Append(cfg.ViewModeOverride.Value)
              .Append(" | inVehicle=").Append(GameState.IsInVehicle)
              .Append(" air=").Append(GameState.IsAirVehicle)
              .Append(" onFoot=").Append(GameState.IsOnFoot)
              .Append(" | aiming=").Append(GameState.Aiming)
              .Append(" aimPressed=").Append(GameState.AimPressed)
              .Append(" breath=").Append(GameState.HoldingBreath)
              .Append(" flightZoom=").Append(cfg.FlightZoom.Value)
              .Append(" usingScope=").Append(GameState.UsingScope);
            FovUnlockPlugin.Logger.LogWarning(Sb.ToString());

            // Line 2: FOV numbers and zoom tuning.
            Sb.Clear();
            Sb.Append("[FOV][Verbose] fov: first=").Append(cfg.FovFirstPerson.Value)
              .Append(" third=").Append(cfg.FovThirdPerson.Value)
              .Append(" base=").Append(FovEngine.BaseTarget)
              .Append(" currentTarget=").Append(FovEngine.CurrentTarget)
              .Append(" ratio=").Append(FovEngine.AppliedRatio.ToString("0.00"))
              .Append(" | aimZoom=").Append(cfg.AimZoomRatio.Value)
              .Append(" breathZoom=").Append(cfg.HoldBreathZoomRatio.Value)
              .Append(" strength=").Append(cfg.AimZoomStrength.Value)
              .Append(" | smooth=").Append(cfg.ZoomSmoothSpeed.Value)
              .Append(" holdSmooth=").Append(cfg.HoldBreathSmoothSpeed.Value)
              .Append(" curve=").Append(cfg.HoldBreathCurve.Value)
              .Append(" enhancedBreath=").Append(cfg.EnhancedBreathOnFoot.Value);
            FovUnlockPlugin.Logger.LogWarning(Sb.ToString());

            // Line 3: optics / cameras / targets.
            Sb.Clear();
            Sb.Append("[FOV][Verbose] optics: threshold=").Append(FovEngine.OpticsThreshold.ToString("0.#"))
              .Append("deg native=").Append(FovEngine.ObservedNativeFov.ToString("0.#"))
              .Append("deg yielded=").Append(FovEngine.YieldedCount)
              .Append(" applied=").Append(FovEngine.AppliedCount)
              .Append(" cameras=").Append(FovEngine.CameraCount)
              .Append(" vehicleSight=").Append(FovEngine.VehicleSightYield)
              .Append(" scopeProtection=").Append(FovEngine.ScopeProtection)
              .Append(" mainCamId=").Append(FovEngine.MainCameraId)
              .Append(" vehicleYield=").Append(FovEngine.VehicleSightYield)
              .Append(" | vanillaRatio=").Append(cfg.VanillaViewRatio.Value)
              .Append(" excluded=\"").Append(cfg.ExcludedCameraNames.Value).Append('"')
              .Append(" | ref=").Append(cfg.Reference.Value)
              .Append(" target=").Append(cfg.Target.Value)
              .Append(" names=").Append(cfg.CameraNames.Value)
              .Append(" | status=\"").Append(FovEngine.Status).Append('"');
            FovUnlockPlugin.Logger.LogWarning(Sb.ToString());

            // Line 4: menu, hotkeys, language, gates.
            Sb.Clear();
            Sb.Append("[FOV][Verbose] ui: menuOpen=").Append(FovMenu.IsOpen)
              .Append(" capturing=").Append(FovMenu.Capturing)
              .Append(" unlockCursor=").Append(cfg.UnlockCursor.Value)
              .Append(" invGate=").Append(cfg.UseInventoryGate.Value)
              .Append(" pauseFallback=").Append(cfg.BlockInputViaPause.Value)
              .Append(" | lang=").Append(cfg.LanguageOption.Value)
              .Append(" resolved=").Append(L.Current)
              .Append(" | keys menu=").Append(cfg.MenuKey.Value)
              .Append(" toggle=").Append(cfg.ToggleKey.Value)
              .Append(" inc=").Append(cfg.FovIncreaseKey.Value)
              .Append(" dec=").Append(cfg.FovDecreaseKey.Value)
              .Append(" step=").Append(cfg.FovHotkeyStep.Value)
              .Append(" | debugWarnings=").Append(cfg.DebugWarnings.Value);
            FovUnlockPlugin.Logger.LogWarning(Sb.ToString());

            // Line 5: per-camera FOV breakdown.
            string cams = FovEngine.CameraDump();
            FovUnlockPlugin.Logger.LogWarning(
                cams.Length == 0
                    ? "[FOV][Verbose] cameras: (none)"
                    : "[FOV][Verbose] cameras: " + cams);
        }
    }
}
