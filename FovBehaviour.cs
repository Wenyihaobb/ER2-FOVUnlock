using System;
using UnityEngine;

namespace ER2FovUnlock
{
    /// <summary>
    /// Persistent component attached through BepInEx's BasePlugin.AddComponent&lt;T&gt;().
    /// Only parameterless instance methods; all state is static to keep IL2CPP injection simple.
    /// </summary>
    public class FovBehaviour : MonoBehaviour
    {
        public FovBehaviour(IntPtr ptr) : base(ptr) { }

        private static long _nextConfigReloadMs;
        private static bool _cursorTouched;
        private static bool _prevCursorVisible;
        private static CursorLockMode _prevCursorLock;
        private static bool _pauseTouched;
        private static bool _prevPaused;
        private static bool _inventoryTouched;
        private static bool _prevInventoryOpen;
        private static int _warnings;

        private static KeyCode _repeatKey = KeyCode.None;
        private static int _repeatDelta;
        private static long _nextRepeatMs;

        private void Awake()
        {
            FovEngine.OnControllerAwake();
        }

        private void Update()
        {
            var cfg = Config();
            if (cfg == null)
                return;

            // Do not reload from disk while the menu or a key capture is active.
            long now = Environment.TickCount64;
            if (!FovMenu.IsOpen && FovMenu.Capturing == FovMenu.CaptureKind.None && now >= _nextConfigReloadMs)
            {
                _nextConfigReloadMs = now + 5000;
                try { FovUnlockPlugin.Instance.ReloadConfigFile(); }
                catch (Exception e) { Warn("[FOV] Config reload failed: " + e.Message); }
            }

            HandleHotkeys(cfg);
            ApplyMenuState(cfg);
            FovEngine.Tick(cfg);
        }

        private void LateUpdate()
        {
            var cfg = Config();
            if (cfg == null)
                return;
            ApplyMenuState(cfg);
            FovEngine.Tick(cfg);
        }

        private void OnGUI()
        {
            var cfg = Config();
            if (cfg == null)
                return;
            // OnGUI runs after Update/LateUpdate; re-apply in case the game re-locks the cursor.
            ApplyMenuState(cfg);
            FovMenu.Draw(cfg);
        }

        private void OnDestroy()
        {
            var cfg = Config();
            if (cfg != null && FovEngine.HasApplied)
                FovEngine.Restore(cfg);
            ReleaseCursor();
            RestoreGamePause();
            RestoreInventoryGate();
        }

        private static FovConfig Config()
        {
            var plugin = FovUnlockPlugin.Instance;
            return plugin == null ? null : plugin.FovConfig;
        }

        private static void Warn(string message)
        {
            if (_warnings++ <= 5)
                FovUnlockPlugin.Logger.LogWarning(message);
        }

        private static void HandleHotkeys(FovConfig cfg)
        {
            // While capturing a new binding, keys belong to the menu.
            if (FovMenu.Capturing != FovMenu.CaptureKind.None)
                return;

            try
            {
                var menuKey = cfg.MenuKey.Value;
                if (menuKey != KeyCode.None && Input.GetKeyDown(menuKey))
                    FovMenu.Toggle();

                var toggleKey = cfg.ToggleKey.Value;
                if (toggleKey != KeyCode.None && toggleKey != menuKey && Input.GetKeyDown(toggleKey))
                    FovEngine.SetEnabled(cfg, !cfg.Enabled.Value);

                HandleFovHotkeys(cfg, menuKey);
            }
            catch (Exception e)
            {
                Warn("[FOV] Key read failed: " + e.Message);
            }
        }

        /// <summary>Numpad +/- changes the FOV of the current view mode; hold to repeat.</summary>
        private static void HandleFovHotkeys(FovConfig cfg, KeyCode menuKey)
        {
            long now = Environment.TickCount64;
            var inc = cfg.FovIncreaseKey.Value;
            var dec = cfg.FovDecreaseKey.Value;
            if (inc == menuKey)
                inc = KeyCode.None;
            if (dec == menuKey || dec == inc)
                dec = KeyCode.None;

            if (inc != KeyCode.None && Input.GetKeyDown(inc))
            {
                FovEngine.AdjustFov(cfg, 1);
                _repeatKey = inc;
                _repeatDelta = 1;
                _nextRepeatMs = now + 300;
            }
            else if (dec != KeyCode.None && Input.GetKeyDown(dec))
            {
                FovEngine.AdjustFov(cfg, -1);
                _repeatKey = dec;
                _repeatDelta = -1;
                _nextRepeatMs = now + 300;
            }
            else if (_repeatKey != KeyCode.None && Input.GetKey(_repeatKey) && now >= _nextRepeatMs)
            {
                FovEngine.AdjustFov(cfg, _repeatDelta);
                _nextRepeatMs = now + 140;
            }
            else if (_repeatKey != KeyCode.None && !Input.GetKey(_repeatKey))
            {
                _repeatKey = KeyCode.None;
            }
        }

        /// <summary>
        /// Releases the cursor while the menu is open. Input blocking relies on the game's own
        /// InventoryPanel.isOpen gate, so the game keeps running (no timeScale freeze,
        /// no pause flag) - important for multiplayer.
        /// Do NOT Harmony-patch UnityEngine.Input here: patching its internal *Int methods
        /// breaks the Il2CppInterop trampoline and crashes the game (AccessViolationException).
        /// </summary>
        private static void ApplyMenuState(FovConfig cfg)
        {
            bool open = FovMenu.IsOpen;

            if (open && cfg.UnlockCursor.Value)
            {
                try
                {
                    if (!_cursorTouched)
                    {
                        _cursorTouched = true;
                        _prevCursorVisible = Cursor.visible;
                        _prevCursorLock = Cursor.lockState;
                    }
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                catch (Exception e)
                {
                    Warn("[FOV] Cursor state failed: " + e.Message);
                }
            }
            else if (_cursorTouched)
            {
                ReleaseCursor();
            }

            // Reuse the game's own inventory gate: while it is open the game blocks firing,
            // movement and looking by itself. We only flip the flag, never open the inventory UI.
            ApplyInventoryGate(cfg, open);

            // Purely opt-in: never touch the game's pause state unless the user asks for it.
            ApplyPauseFallback(cfg.BlockInputViaPause.Value && open);
        }

        private static void ApplyInventoryGate(FovConfig cfg, bool open)
        {
            if (!cfg.UseInventoryGate.Value)
            {
                if (_inventoryTouched)
                    RestoreInventoryGate();
                return;
            }

            try
            {
                if (open)
                {
                    if (!_inventoryTouched)
                    {
                        _inventoryTouched = true;
                        _prevInventoryOpen = InventoryPanel.isOpen;
                    }
                    if (!InventoryPanel.isOpen)
                        InventoryPanel.isOpen = true;
                }
                else if (_inventoryTouched)
                {
                    RestoreInventoryGate();
                }
            }
            catch (Exception e)
            {
                Warn("[FOV] Inventory gate failed: " + e.Message);
            }
        }

        private static void RestoreInventoryGate()
        {
            if (!_inventoryTouched)
                return;
            _inventoryTouched = false;
            try
            {
                if (!_prevInventoryOpen)
                    InventoryPanel.isOpen = false;
            }
            catch (Exception e)
            {
                Warn("[FOV] Failed to restore inventory gate: " + e.Message);
            }
        }

        private static void ApplyPauseFallback(bool active)
        {
            if (!active)
            {
                if (_pauseTouched)
                    RestoreGamePause();
                return;
            }

            try
            {
                if (!_pauseTouched)
                {
                    _pauseTouched = true;
                    _prevPaused = Pause.isPaused;
                }

                if (!Pause.isPaused)
                    Pause.isPaused = true;

                if (!_prevPaused)
                {
                    var pause = Pause.instance;
                    var menu = pause == null ? null : pause.pauseMenu;
                    if (menu != null && menu.activeSelf)
                        menu.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Warn("[FOV] Failed to set game pause: " + e.Message);
            }
        }

        private static void RestoreGamePause()
        {
            if (!_pauseTouched)
                return;
            _pauseTouched = false;
            try
            {
                if (!_prevPaused)
                    Pause.isPaused = false;
            }
            catch (Exception e)
            {
                Warn("[FOV] Failed to restore game pause: " + e.Message);
            }
        }

        private static void ReleaseCursor()
        {
            if (!_cursorTouched)
                return;
            _cursorTouched = false;
            try
            {
                Cursor.visible = _prevCursorVisible;
                Cursor.lockState = _prevCursorLock;
            }
            catch
            {
                // Ignore.
            }
        }
    }
}
