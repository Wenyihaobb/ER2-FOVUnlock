using System;
using UnityEngine;

namespace ER2FovUnlock
{
    internal enum ViewMode
    {
        FirstPerson,
        ThirdPerson
    }

    /// <summary>
    /// Read-only detection of the current state of the game: whether it is in a match, in first-person or third-person perspective, whether aiming or holding one's breath.
    /// All game APIs are wrapped within try/catch blocks. In the event that any interface fails, it automatically falls back to the fallback input or default behavior.
    /// </summary>
    internal static class GameState
    {
        internal static bool InMatch { get; private set; }
        internal static ViewMode View { get; private set; } = ViewMode.FirstPerson;
        internal static bool Aiming { get; private set; }
        internal static bool HoldingBreath { get; private set; }

        /// <summary>the control is over aircraft and other airborne vehicles. For airborne vehicles, the third-person perspective retains the phrase "breathe in deeply to zoom in".</summary>
        internal static bool IsAirVehicle { get; private set; }

        /// <summary>The player currently occupies a vehicle (ground or air).</summary>
        internal static bool IsInVehicle { get; private set; }

        /// <summary>Walking with a gun (not in a vehicle, nor at a fixed weapon/weapon position).</summary>
        internal static bool IsOnFoot { get; private set; } = true;

        /// <summary>
        /// True whenever aim is held, including vehicle gunner sights where the game's
        /// aiming API may stay false and only the right mouse button is reliable.
        /// </summary>
        internal static bool AimPressed { get; private set; }

        /// <summary>
        /// True while the held weapon is actually rendering through its scope optic.
        /// Read from GenericGun.IsUsingScope - a real game state, unlike the FOV-based
        /// optics guess. The dedicated scope camera keeps the vanilla FOV (excluded by
        /// name); the MAIN camera stays under plugin control so aim / breath ratios and
        /// the breath animation keep working outside the scope picture. Its FOV-threshold
        /// yield is suppressed while scoping to stop the intermittent hand-back glitch.
        /// </summary>
        internal static bool UsingScope { get; private set; }

        /// <summary>A summary of the status displayed in the menu.</summary>
        internal static string Summary { get; private set; } = "Status detection in progress...";

        private static string _lastLog = string.Empty;
        private static string _viewSignal = "?";
        private static int _signalWarnings;
        private static long _nextRefreshMs;
        private static bool _initialized;

        internal static void Refresh(FovConfig cfg)
        {
            long now = Environment.TickCount64;
            if (_initialized && now < _nextRefreshMs)
                return;
            _initialized = true;
            _nextRefreshMs = now + 33;

            bool mainMenu = false, mainMenuReadable = false;
            bool loading = false, anyReadable = false;
            bool hasController = false, hasSoldier = false;

            try
            {
                mainMenu = ResourcesManager.IsMainMenu;
                mainMenuReadable = true;
                anyReadable = true;
            }
            catch (Exception e)
            {
                WarnOnce("ResourcesManager.IsMainMenu", e);
            }

            try
            {
                loading = ResourcesManager.isLoadingScene;
                anyReadable = true;
            }
            catch (Exception e)
            {
                WarnOnce("ResourcesManager.isLoadingScene", e);
            }

            try
            {
                hasController = PlayerController.currentController != null;
                anyReadable = true;
            }
            catch (Exception e)
            {
                WarnOnce("PlayerController.currentController", e);
            }

            try
            {
                hasSoldier = Soldier.CurrentControlledSoldierOrNull() != null;
                anyReadable = true;
            }
            catch (Exception e)
            {
                WarnOnce("Soldier.CurrentControlledSoldierOrNull", e);
            }

            if (!cfg.OnlyInMatch.Value)
            {
                InMatch = true;
            }
            else if (!anyReadable)
            {
                // When the game API cannot be accessed at all, revert to the previous behavior.
                InMatch = true;
            }
            else
            {
                InMatch = !mainMenu && !loading && (hasController || hasSoldier || !mainMenuReadable);
            }

            ResolveVehicle();
            View = ResolveView(cfg);
            ResolveAim(cfg);
            ResolveScope();

            string viewText = View == ViewMode.FirstPerson ? "FP" : "TP";
            string zoomText = HoldingBreath ? "HoldBreath" : Aiming ? "Aim" : "Normal";

            // Summary is only consumed by diagnostics; avoid an interpolated string
            // allocation every 33 ms during normal play.
            if (cfg.DebugWarnings.Value || cfg.VerboseDebug.Value)
                Summary = InMatch ? $"{viewText} / {zoomText}" : "Main menu / loading";

            if (cfg.DebugWarnings.Value)
            {
                string line = $"inMatch={InMatch} (IsMainMenu={mainMenuReadable && mainMenu}, " +
                              $"loading={loading}, controller={hasController}, soldier={hasSoldier}) " +
                              $"view={viewText} [{_viewSignal}] vehicle={(IsAirVehicle ? "air" : "ground/foot")} " +
                              $"onFoot={IsOnFoot} aiming={Aiming} holdBreath={HoldingBreath}";
                if (line != _lastLog)
                {
                    _lastLog = line;
                    FovUnlockPlugin.Logger.LogWarning("[FOV][Debug] State changed: " + line);
                }
            }
        }

        private static ViewMode ResolveView(FovConfig cfg)
        {
            if (cfg.ViewModeOverride.Value == ViewModeSource.ForceFirstPerson)
            {
                _viewSignal = "Forced FP";
                return ViewMode.FirstPerson;
            }
            if (cfg.ViewModeOverride.Value == ViewModeSource.ForceThirdPerson)
            {
                _viewSignal = "Forced TP";
                return ViewMode.ThirdPerson;
            }

            // 1) The real-time static marking of the current perspective is the most accurate (isTps is a public static bool).
            try
            {
                bool isTps = PlayerController.isTps;
                _viewSignal = "isTps=" + isTps;
                return isTps ? ViewMode.ThirdPerson : ViewMode.FirstPerson;
            }
            catch (Exception e)
            {
                WarnOnce("PlayerController.isTps", e);
            }

            // 2) The general switch for the third-person function.
            try
            {
                if (PlayerController.TPSEnabled)
                {
                    _viewSignal = "TPSEnabled=true";
                    return ViewMode.ThirdPerson;
                }
            }
            catch (Exception e)
            {
                WarnOnce("PlayerController.TPSEnabled", e);
            }

            // 3) The infantry perspective in the game settings.
            try
            {
                var system = SavableData.Settings == null ? null : SavableData.Settings.system;
                if (system != null && system.isTPS_inf)
                {
                    _viewSignal = "isTPS_inf=true";
                    return ViewMode.ThirdPerson;
                }
            }
            catch (Exception e)
            {
                WarnOnce("GameSettingsSystem.isTPS_inf", e);
            }

            _viewSignal = "Default FP";
            return ViewMode.FirstPerson;
        }

        private static void ResolveVehicle()
        {
            try
            {
                var vehicle = Soldier.CurrentControlledVehicleOrNull();
                IsInVehicle = vehicle != null;
                IsAirVehicle = vehicle != null && vehicle.IsAirVehicle();

                // Fixed artillery pieces / fixed machine guns and other gun positions are not included in the Vehicle, but the player still occupies a Seat.
                bool onMountedWeapon = false;
                var soldier = Soldier.CurrentControlledSoldierOrNull();
                if (soldier != null)
                {
                    var seat = soldier.GetCurrentVehicleSeat();
                    onMountedWeapon = seat != null && seat.hasTurret;
                }
                IsOnFoot = vehicle == null && !onMountedWeapon;
            }
            catch
            {
                // If it cannot be read, handle it as "Infantry combat / Non-Airborne Vehicle" (the function remains intact), and keep the log silent.
                IsAirVehicle = false;
                IsInVehicle = false;
                IsOnFoot = true;
            }
        }

        private static void ResolveAim(FovConfig cfg)
        {
            bool apiAiming = false, apiBreath = false;
            bool aimingOk = false, breathOk = false;

            try { apiAiming = Soldier.CurrentPlayerIsAimingOrLimited(); aimingOk = true; }
            catch (Exception e) { WarnOnce("Soldier.CurrentPlayerIsAimingOrLimited", e); }
            try { apiBreath = Soldier.CurrentPlayerIsHoldingBreath(); breathOk = true; }
            catch (Exception e) { WarnOnce("Soldier.CurrentPlayerIsHoldingBreath", e); }

            // The aiming status of the infantry in the vehicle may not be reported. The right-click serves as a supplementary signal for "whether aiming is pressed".
            bool mouseAim = false, shift = false;
            try
            {
                mouseAim = Input.GetMouseButton(1);
                shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            }
            catch
            {
                // When the input module is unavailable, it completely relies on the game state API.
            }

            // Additional note: One must aim first before holding their breath; otherwise, simply pressing the Shift key will zoom in.
            // The only exception is "Air Vehicle + Third Person View", which retains the user's preference for the direct and magnified display.
            Aiming = apiAiming;
            bool aimPressed = apiAiming || mouseAim;
            AimPressed = aimPressed;
            bool airThirdPerson = IsAirVehicle && View == ViewMode.ThirdPerson;
            bool flightZoom = cfg.FlightZoom.Value;
            // In third-person aircraft the breath key normally zooms without aiming.
            // With FlightZoom off it must do nothing at all (not fall back to "aim first").
            bool breathAllowed = airThirdPerson ? flightZoom : aimPressed;
            HoldingBreath = apiBreath && breathAllowed;

            if (aimingOk && breathOk)
                return;

            // Only if the game state API reading fails, should the fallback input mechanism be triggered.
            if (!aimingOk)
                Aiming = mouseAim;

            if (!breathOk)
            {
                bool canHold = true;
                try
                {
                    var soldier = Soldier.CurrentControlledSoldierOrNull();
                    if (soldier != null)
                        canHold = soldier.CanHoldBreath();
                }
                catch (Exception e) { WarnOnce("Soldier.CanHoldBreath", e); }
                HoldingBreath = shift && canHold && (airThirdPerson ? flightZoom : mouseAim);
            }
        }

        /// <summary>
        /// Reads the game's own scope state. Falls back silently to the FOV-threshold
        /// optics detection if the API is unavailable, so behaviour never gets worse.
        /// </summary>
        private static void ResolveScope()
        {
            UsingScope = false;
            try
            {
                var soldier = Soldier.CurrentControlledSoldierOrNull();
                if (soldier == null)
                    return;
                var gun = soldier.GetHeldGun(0);
                if (gun != null)
                    // The AimPressed guard keeps this safe even if IsUsingScope turns out
                    // to mean "this weapon has a scope fitted" rather than "scoping now":
                    // without it the plugin would yield permanently.
                    UsingScope = gun.IsUsingScope && AimPressed;
            }
            catch (Exception e)
            {
                WarnOnce("GenericGun.IsUsingScope", e);
            }
        }

        private static void WarnOnce(string api, Exception e)
        {
            if (_signalWarnings++ > 8)
                return;
            FovUnlockPlugin.Logger.LogWarning("[FOV] Failed to read game state " + api + ": " + e.Message);
        }
    }
}
