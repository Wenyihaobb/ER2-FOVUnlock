namespace ER2FovUnlock
{
    internal enum PluginLanguage
    {
        Auto,
        English,
        SimplifiedChinese,
        TraditionalChinese,
        Japanese
    }

    /// <summary>
    /// In-game menu strings. Runtime log messages are always English and never go through here.
    /// Auto mode reads the game's own Language.currentlanguage (Languages enum).
    /// </summary>
    internal static class L
    {
        internal static string Title = "ER2 FOV UNLOCK";
        internal static string Enable = "Enable FOV Override";
        internal static string FovFirst = "First Person FOV";
        internal static string FovThird = "Third Person FOV";
        internal static string AimZoom = "Aim Zoom";
        internal static string BreathZoom = "Hold Breath Zoom";
        internal static string EnhancedBreath = "Breath-holding Animation Improvement";
        internal static string LanguageLabel = "Language";
        internal static string KeyMenu = "Menu Key";
        internal static string KeyToggle = "Toggle Key";
        internal static string KeyIncrease = "FOV +";
        internal static string KeyDecrease = "FOV -";
        internal static string Rebind = "Bind";
        internal static string Clear = "Clear";
        internal static string Unbound = "None";
        internal static string PressKey = "Press a key... (Esc to cancel)";
        internal static string Close = "Close";
        internal static string StartupHint = "ER2 FOV Unlock: press {0} to open settings";

        internal static string NameAuto = "Auto";
        internal static string NameEnglish = "English";
        internal static string NameSimplified = "Simplified Chinese";
        internal static string NameTraditional = "Traditional Chinese";
        internal static string NameJapanese = "Japanese";

        private static PluginLanguage _applied = (PluginLanguage)(-1);
        private static PluginLanguage _lastSetting = (PluginLanguage)(-1);
        private static bool _initialized;

        internal static PluginLanguage Current { get; private set; } = PluginLanguage.English;

        /// <summary>
        /// Resolves the language only when it can actually change: once on the first call
        /// after the game loads, and whenever the player changes the Language setting.
        /// </summary>
        internal static void Refresh(FovConfig cfg)
        {
            var setting = cfg.LanguageOption.Value;
            if (_initialized && setting == _lastSetting)
                return;

            _initialized = true;
            _lastSetting = setting;

            var resolved = setting == PluginLanguage.Auto ? DetectGameLanguage() : setting;
            Current = resolved;
            if (resolved == _applied)
                return;
            _applied = resolved;
            Apply(resolved);
        }

        private static PluginLanguage DetectGameLanguage()
        {
            try
            {
                var game = Language.currentlanguage;
                if (game == Languages.ChineseSimplified)
                    return PluginLanguage.SimplifiedChinese;
                if (game == Languages.ChineseTraditional)
                    return PluginLanguage.TraditionalChinese;
                if (game == Languages.Japanese)
                    return PluginLanguage.Japanese;
            }
            catch
            {
                // Language API unavailable: fall back to English.
            }
            return PluginLanguage.English;
        }

        internal static string LangName(PluginLanguage lang)
        {
            switch (lang)
            {
                case PluginLanguage.Auto: return NameAuto;
                case PluginLanguage.SimplifiedChinese: return NameSimplified;
                case PluginLanguage.TraditionalChinese: return NameTraditional;
                case PluginLanguage.Japanese: return NameJapanese;
                default: return NameEnglish;
            }
        }

        internal static PluginLanguage Next(PluginLanguage lang)
        {
            switch (lang)
            {
                case PluginLanguage.Auto: return PluginLanguage.English;
                case PluginLanguage.English: return PluginLanguage.SimplifiedChinese;
                case PluginLanguage.SimplifiedChinese: return PluginLanguage.TraditionalChinese;
                case PluginLanguage.TraditionalChinese: return PluginLanguage.Japanese;
                default: return PluginLanguage.Auto;
            }
        }

        internal static PluginLanguage Prev(PluginLanguage lang)
        {
            switch (lang)
            {
                case PluginLanguage.Auto: return PluginLanguage.Japanese;
                case PluginLanguage.Japanese: return PluginLanguage.TraditionalChinese;
                case PluginLanguage.TraditionalChinese: return PluginLanguage.SimplifiedChinese;
                case PluginLanguage.SimplifiedChinese: return PluginLanguage.English;
                default: return PluginLanguage.Auto;
            }
        }

        private static void Apply(PluginLanguage lang)
        {
            if (lang == PluginLanguage.SimplifiedChinese)
            {
                Title = "ER2 FOV UNLOCK";
                Enable = "启用 FOV 接管";
                FovFirst = "第一人称 FOV";
                FovThird = "第三人称 FOV";
                AimZoom = "瞄准缩放";
                BreathZoom = "屏息缩放";
                EnhancedBreath = "屏息动画改进";
                LanguageLabel = "语言";
                KeyMenu = "菜单热键";
                KeyToggle = "功能热键";
                KeyIncrease = "增加 FOV";
                KeyDecrease = "减少 FOV";
                Rebind = "改键";
                Clear = "清除";
                Unbound = "未绑定";
                PressKey = "请按下新按键…（Esc 取消）";
                Close = "关闭菜单";
                StartupHint = "ER2 FOV Unlock：按 {0} 打开设置菜单";
                NameAuto = "自动";
                NameEnglish = "English";
                NameSimplified = "简体中文";
                NameTraditional = "繁體中文";
                NameJapanese = "日语";
                return;
            }

            if (lang == PluginLanguage.TraditionalChinese)
            {
                Title = "ER2 FOV UNLOCK";
                Enable = "啟用 FOV 接管";
                FovFirst = "第一人稱 FOV";
                FovThird = "第三人稱 FOV";
                AimZoom = "瞄準縮放";
                BreathZoom = "屏息縮放";
                EnhancedBreath = "屏息動畫改進";
                LanguageLabel = "語言";
                KeyMenu = "選單熱鍵";
                KeyToggle = "功能熱鍵";
                KeyIncrease = "增加 FOV";
                KeyDecrease = "減少 FOV";
                Rebind = "改鍵";
                Clear = "清除";
                Unbound = "未綁定";
                PressKey = "請按下新按鍵…（Esc 取消）";
                Close = "關閉選單";
                StartupHint = "ER2 FOV Unlock：按 {0} 開啟設定選單";
                NameAuto = "自動";
                NameEnglish = "English";
                NameSimplified = "簡體中文";
                NameTraditional = "繁體中文";
                NameJapanese = "日語";
                return;
            }

            if (lang == PluginLanguage.Japanese)
            {
                Title = "ER2 FOV UNLOCK";
                Enable = "FOV 上書きを有効化";
                FovFirst = "一人称 FOV";
                FovThird = "三人称 FOV";
                AimZoom = "エイム倍率";
                BreathZoom = "息止め倍率";
                EnhancedBreath = "息を止めるアニメーション改善";
                LanguageLabel = "言語";
                KeyMenu = "メニューキー";
                KeyToggle = "切替キー";
                KeyIncrease = "FOV +";
                KeyDecrease = "FOV -";
                Rebind = "変更";
                Clear = "解除";
                Unbound = "なし";
                PressKey = "キーを押してください…（Esc でキャンセル）";
                Close = "閉じる";
                StartupHint = "ER2 FOV Unlock: {0} で設定を開く";
                NameAuto = "自動";
                NameEnglish = "English";
                NameSimplified = "中国語(簡体)";
                NameTraditional = "中国語(繁体)";
                NameJapanese = "日本語";
                return;
            }

            Title = "ER2 FOV UNLOCK";
            Enable = "Enable FOV Override";
            FovFirst = "First Person FOV";
            FovThird = "Third Person FOV";
            AimZoom = "Aim Zoom";
            BreathZoom = "Hold Breath Zoom";
            EnhancedBreath = "Breath-holding Animation Improvement";
            LanguageLabel = "Language";
            KeyMenu = "Menu Key";
            KeyToggle = "Toggle Key";
            KeyIncrease = "FOV +";
            KeyDecrease = "FOV -";
            Rebind = "Bind";
            Clear = "Clear";
            Unbound = "None";
            PressKey = "Press a key... (Esc to cancel)";
            Close = "Close";
            StartupHint = "ER2 FOV Unlock: press {0} to open settings";
            NameAuto = "Auto";
            NameEnglish = "English";
            NameSimplified = "Simplified Chinese";
            NameTraditional = "Traditional Chinese";
            NameJapanese = "Japanese";
        }
    }
}
