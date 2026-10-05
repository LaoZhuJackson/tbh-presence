using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;

namespace TbhCompanion
{
    // UI language for the companion app. English is the source language: the
    // English string is both the default text and the lookup key, so a missing
    // translation falls back to readable English instead of a blank control.
    //
    // Only display text goes through here. Machine-readable values — BepInEx cfg
    // tokens, --once JSON keys/values, registry values, GitHub asset names,
    // autosynth-status.json fields — must never be translated, or the plugin and
    // the updater stop understanding each other.
    //
    // The Chinese table lives in this file, which is BOM-less UTF-8; build.ps1
    // passes /codepage:65001 to csc so it is not decoded as ANSI.
    static class Lang
    {
        // Dropdown order; the index is what gets persisted, never the label.
        public const int Auto = 0;
        public const int English = 1;
        public const int Chinese = 2;

        public static readonly string[] ChoiceNames = { "Auto", "English", "简体中文" };
        static readonly string[] ChoiceIds = { "auto", "en", "zh" };

        static int _choice = Auto;
        static bool _zh;
        static string _fontFamily;

        // Raised after the language has actually changed, so handlers re-text
        // against the new value. Subscribers must unsubscribe (see StatusForm's
        // FormClosed and TrayApp.Shutdown) or a closed window is kept alive.
        public static event Action Changed;

        public static int Choice { get { return _choice; } }
        public static bool IsChinese { get { return _zh; } }

        // Load the persisted choice. Call once, early in Main.
        public static void Load()
        {
            int i = Array.IndexOf(ChoiceIds, AppSettings.Language);
            Apply(i < 0 ? Auto : i, false);
        }

        // User picked a language from the dropdown: persist it and re-text.
        public static void Select(int choice)
        {
            if (choice < 0 || choice >= ChoiceIds.Length) choice = Auto;
            if (choice == _choice) return;
            Apply(choice, true);
        }

        // Dev/test override (--lang): switch without touching the saved setting.
        public static void Override(int choice)
        {
            if (choice < 0 || choice >= ChoiceIds.Length) return;
            if (choice == _choice) return;
            Apply(choice, false);
        }

        // "en" / "zh" / "auto" -> dropdown index, or -1 when unrecognised.
        public static int IndexOf(string id)
        {
            return id == null ? -1 : Array.IndexOf(ChoiceIds, id.Trim().ToLowerInvariant());
        }

        static void Apply(int choice, bool persist)
        {
            _choice = choice;
            if (persist) AppSettings.Language = ChoiceIds[choice];
            _zh = Resolve(choice);
            _fontFamily = null;
            var h = Changed;
            if (h != null) { try { h(); } catch { } }
        }

        static bool Resolve(int choice)
        {
            if (choice == Chinese) return true;
            if (choice == English) return false;
            // Auto follows Windows. Only Simplified gets Chinese — a Traditional
            // (zh-TW / zh-HK) user would rather read English than Simplified.
            try
            {
                var c = CultureInfo.InstalledUICulture;
                if (c.TwoLetterISOLanguageName != "zh") return false;
                string n = c.Name ?? "";
                return n == "zh" || n == "zh-CN" || n == "zh-SG"
                    || n.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // ---- lookup ----

        public static string T(string english)
        {
            if (english == null || !_zh) return english;
            string zh;
            return Zh.TryGetValue(english, out zh) ? zh : english;
        }

        // Templates are looked up first, then formatted — so the dictionary holds
        // "{0} chests" -> "{0} 个宝箱" and the argument is slotted in afterwards.
        public static string F(string englishTemplate, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, T(englishTemplate), args);
        }

        // ---- font ----

        // A CJK-capable UI family when Chinese is active, else null (keep Segoe UI).
        // Custom-painted controls call Theme.F inside OnPaint and pick this up; a
        // Label captured its Font at construction, so the retranslate pass has to
        // reassign it too.
        public static string FontFamily
        {
            get
            {
                if (!_zh) return null;
                if (_fontFamily == null)
                {
                    _fontFamily = "";
                    foreach (var name in new[] { "Microsoft YaHei UI", "Microsoft YaHei" })
                        if (HasFamily(name)) { _fontFamily = name; break; }
                }
                return _fontFamily.Length == 0 ? null : _fontFamily;
            }
        }

        // GDI+ silently substitutes a different family for an unknown name, so an
        // exact round-trip of the name is what proves the family is installed.
        static bool HasFamily(string name)
        {
            try
            {
                using (var f = new Font(name, 9f))
                    return string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // ---- Simplified Chinese table ----

        static readonly Dictionary<string, string> Zh = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Tray menu. "TBH Companion" is the product name and stays as-is.
            { "starting...", "启动中…" },
            { "Starting...", "启动中…" },
            { "Enable presence", "启用动态" },
            { "Status && Settings...", "状态与设置…" },
            { "Quit", "退出" },

            // Side rail
            { "Version", "版本" },
            { "checking for updates...", "正在检查更新…" },
            { "Launch game", "启动游戏" },
            { "Running", "运行中" },
            { "Launching…", "启动中…" },
            { "Install mods", "安装模组" },
            { "Remove mods", "移除模组" },
            { "Installing…", "正在安装…" },
            { "Removing…", "正在移除…" },

            // Live status cards
            { "Presence", "动态" },
            { "Loop", "循环" },
            { "Live", "在线" },
            { "Offline", "离线" },
            { "On", "开启" },
            { "Off", "关闭" },
            { "Paused", "已暂停" },
            { "Disabled", "已停用" },
            { "Waiting for game", "等待游戏" },
            { "not started", "未启动" },
            { "game not running", "游戏未运行" },
            { "status error", "状态异常" },
            { "{0} cycles", "{0} 轮" },
            { "{0} chests", "{0} 个宝箱" },
            { "{0} runes", "{0} 个符文" },
            { "{0} boss runs", "{0} 次首领战" },
            { "{0} offerings", "{0} 次供奉" },
            { "chests", "宝箱" },
            { "runes", "符文" },
            { "offering", "供奉" },
            { "every {0} min", "每 {0} 分钟" },

            // General
            { "General", "通用" },
            { "Start with Windows", "开机启动" },
            { "Language", "语言" },

            // Discord
            { "Discord Presence", "Discord 动态" },
            { "Show stage on Discord", "在 Discord 显示关卡" },

            // Scheduled restart
            { "Scheduled Restart", "定时重启" },
            { "Restart after uptime", "运行超时后重启" },
            { "Uptime limit", "运行时长上限" },
            { "days", "天" },

            // Mods
            { "Enable Mods", "启用模组" },
            { "Auto Loop", "自动循环" },
            { "Pause on mouse", "鼠标操作时暂停" },
            { "Resume after", "恢复等待" },
            { "sec", "秒" },
            { "Show BepInEx console", "显示 BepInEx 控制台" },
            { "Cycle interval", "循环间隔" },
            { "min", "分" },

            // Alchemy
            { "Alchemy", "炼金" },
            { "Enabled", "启用" },
            { "Melt below level", "熔炼低于等级" },
            { "Max rarity", "最高稀有度" },

            // Offering
            { "Offering", "献祭" },
            { "Max per cycle", "每轮上限" },

            // Chests / runes
            { "Chests", "宝箱" },
            { "Open chests", "自动开箱" },
            { "Runes", "符文" },
            { "Upgrade runes", "升级符文" },

            // Soulstones
            { "Soulstones", "灵魂石" },
            { "Spend on Act Bosses", "消耗于章节首领" },
            { "Tiers", "难度" },
            { "Runs per cycle", "每轮次数" },

            // Synthesis
            { "Synthesis", "合成" },
            { "Synthesize items", "自动合成" },
            { "Types", "类型" },
            { "Equipment max", "装备上限" },
            { "Materials max", "材料上限" },
            { "Accessories max", "饰品上限" },
            { "Target level", "目标等级" },
            { "Save", "保存" },

            // Synthesis types / soulstone tiers — display only. The cfg keeps the
            // English tokens (see SynthesisTypes / Tiers in StatusForm).
            { "Equipment", "装备" },
            { "Materials", "材料" },
            { "Accessories", "饰品" },
            { "Normal", "普通" },
            { "Nightmare", "噩梦" },
            { "Hell", "地狱" },
            { "Torment", "炼狱" },

            // Recipe dropdown: "Max" is ours; the Lv.x~y brackets mirror the game's
            // own Cube dropdown and are deliberately left untranslated.
            { "Max", "最高" },

            // Rarity grades
            { "Common", "普通" },
            { "Uncommon", "优秀" },
            { "Rare", "稀有" },
            { "Legendary", "传说" },
            { "Immortal", "不朽" },
            { "Arcana", "秘法" },
            { "Beyond", "超凡" },
            { "Celestial", "天界" },
            { "Divine", "神圣" },
            { "Cosmic", "宇宙" },

            // Presence engine / reader status (tray tooltip, status card sub-line,
            // console). Technical identifiers passed in as arguments are untouched.
            { "client id {0} - waiting for game", "客户端 {0} - 等待游戏" },
            { "waiting for TaskBarHero...", "等待 TaskBarHero 启动…" },
            { "attached (PID {0}) - resolving...", "已连接（PID {0}）- 正在解析…" },
            { "not ready ({0}) - retry 10s", "未就绪（{0}）- 10 秒后重试" },
            { "Discord not running - will retry", "Discord 未运行 - 将重试" },
            { "Discord lost ({0}) - reconnecting", "Discord 连接中断（{0}）- 正在重连" },
            { "game closed - waiting for restart...", "游戏已关闭 - 等待重启…" },
            { "address cache hit - no scan needed", "命中地址缓存 - 无需扫描" },
            { "tables cached - scanning live objects only (~30s)...", "表已缓存 - 仅扫描活动对象（约 30 秒）…" },
            { "first run for this game build - full memory scan (~90s)...", "该游戏版本首次运行 - 完整内存扫描（约 90 秒）…" },
            { "live stage statics not found - stage falls back to save data", "未找到实时关卡静态字段 - 关卡回退到存档数据" },

            // Dialog titles / bodies. The \n escapes here must match the ones in
            // the call site exactly, or the lookup misses and English shows.
            { "Update {0}", "更新 {0}" },
            { "Mods", "模组" },
            { "Companion", "本程序" },
            { "working...", "处理中…" },
            { "Please close TaskBarHero first, then try again.", "请先关闭 TaskBarHero，然后重试。" },
            { "Couldn't find the TaskBarHero folder.\n\nStart the game once so it can be located, then try again.",
              "找不到 TaskBarHero 文件夹。\n\n请先启动一次游戏以便定位，然后重试。" },
            { "This will install mods by:\n\n  - backing up your save file\n  - downloading BepInEx (the mod loader, ~35 MB)\n  - installing it into the TaskBarHero folder\n\nThe presence feature is unaffected. Continue?",
              "安装模组将会：\n\n  - 备份你的存档\n  - 下载 BepInEx（模组加载器，约 35 MB）\n  - 安装到 TaskBarHero 文件夹\n\nDiscord 动态功能不受影响。是否继续？" },
            { "This will remove mods by deleting BepInEx from the TaskBarHero folder.\n\nYour save and Discord presence are unaffected. Continue?",
              "移除模组会从 TaskBarHero 文件夹删除 BepInEx。\n\n你的存档与 Discord 动态不受影响。是否继续？" },
            { "Update the companion to {0} for game v{1}?\n\n  - downloads {0} from GitHub\n  - replaces this app and restarts it\n\n",
              "将本程序更新到 {0}（对应游戏 v{1}）？\n\n  - 从 GitHub 下载 {0}\n  - 替换本程序并重启\n\n" },
            { "Your save and settings are unaffected. Continue?", "你的存档与设置不受影响。是否继续？" },
            { "TaskBarHero is running, so the in-game plugin is refreshed once you close the game.\n\n",
              "TaskBarHero 正在运行，关闭游戏后会刷新游戏内插件。\n\n" },
            { "The in-game plugin is redeployed automatically after the restart.\n\n",
              "游戏内插件会在重启后自动重新部署。\n\n" },

            // Self-update / version row. Versions, tags and file names are passed
            // in as arguments and stay verbatim.
            { "mods", "模组" },
            { "companion", "本程序" },
            { "game not found — start TaskBarHero once", "未找到游戏 — 请先启动一次 TaskBarHero" },
            { "game version unreadable", "无法读取游戏版本" },
            { "{0} version unknown (dev build) — game v{1}", "{0} 版本未知（开发版）— 游戏 v{1}" },
            { "{0} matched (v{1} ↔ game v{2})", "{0} 已匹配（v{1} ↔ 游戏 v{2}）" },
            { "{0} v{1} newer than game v{2}", "{0} v{1} 比游戏 v{2} 更新" },
            { "Update check failed (retrying): {0}", "更新检查失败（将重试）：{0}" },
            { "unknown error", "未知错误" },
            { "Waiting for release v{0} (game v{1})", "等待发布 v{0}（游戏 v{1}）" },
            { "Release {0} has no {1} asset yet", "发布 {0} 还没有 {1} 资源" },
            { "Update available: game v{0} → release {1}", "有可用更新：游戏 v{0} → 发布 {1}" },
            { "Nothing to update.", "没有可更新的内容。" },
            { "Refusing an update from an unexpected location.", "更新来源异常，已拒绝。" },
            { "app folder not found", "找不到程序文件夹" },
            { "Cannot update in place: {0}. Move the app to a writable folder.",
              "无法就地更新：{0}。请把程序移到可写文件夹。" },
            { "Downloading {0}...", "正在下载 {0}…" },
            { "Download looks incomplete — please try again.", "下载似乎不完整 — 请重试。" },
            { "Restarting to finish the update...", "正在重启以完成更新…" },
            { "Update failed: {0}", "更新失败：{0}" },
            { "Could not start the updater: {0}", "无法启动更新程序：{0}" },
            // Written into the swap .cmd and read back as a failure marker.
            { "Last update could not replace the app - check folder permissions.",
              "上次更新无法替换程序 - 请检查文件夹权限。" },
            { "Last update could not replace the app.", "上次更新无法替换程序。" },

            // Scheduled restart (tray tooltip / console)
            { "scheduled restart after {0} day(s) — closing TaskBarHero...",
              "计划重启：运行 {0} 天后 — 正在关闭 TaskBarHero…" },
            { "scheduled restart: could not close the game", "计划重启：无法关闭游戏" },
            { "scheduled restart: game closed, but relaunch failed", "计划重启：游戏已关闭，但重新启动失败" },
            { "scheduled restart: launching via Steam...", "计划重启：正在通过 Steam 启动…" },
            { "scheduled restart: Steam didn't start the game — trying exe...", "计划重启：Steam 未能启动游戏 — 尝试直接运行 exe…" },
            { "scheduled restart: launching TaskBarHero...", "计划重启：正在启动 TaskBarHero…" },

            // Plugin deploy (tray tooltip / _cfgNote). Paths and dll names in the
            // arguments stay verbatim.
            { "autosynth: game folder not found, skipped", "autosynth：找不到游戏文件夹，已跳过" },
            { "autosynth: BepInEx not installed in {0}, skipped (see autosynth/README.md)",
              "autosynth：{0} 中未安装 BepInEx，已跳过（见 autosynth/README.md）" },
            { "autosynth: plugin dll not bundled, skipped", "autosynth：未内嵌插件 dll，已跳过" },
            { "autosynth: plugin deployed to {0} (active after next game start)",
              "autosynth：插件已部署到 {0}（下次启动游戏后生效）" },
            { "autosynth: plugin update pending (game is running)", "autosynth：插件待更新（游戏正在运行）" },
            { "autosynth: deploy skipped: {0}", "autosynth：部署已跳过：{0}" },
            { "autosynth: BepInEx console hidden by default (change it in Status & Settings)",
              "autosynth：BepInEx 控制台已默认隐藏（可在「状态与设置」中修改）" },

            // BepInEx install / removal (progress lands in _cfgNote)
            { "Could not find the TaskBarHero folder. Start the game once, then try again.",
              "找不到 TaskBarHero 文件夹。请先启动一次游戏，然后重试。" },
            { "BepInEx is already installed.", "BepInEx 已安装。" },
            { "Downloading BepInEx (~35 MB)...", "正在下载 BepInEx（约 35 MB）…" },
            { "Installing into the game folder...", "正在安装到游戏文件夹…" },
            { "Install finished but files look incomplete - please try again.", "安装完成，但文件似乎不完整 - 请重试。" },
            { "Done. Start TaskBarHero once to finish setup, then open the Cube panel.",
              "完成。请启动一次 TaskBarHero 以完成安装，然后打开 Cube 面板。" },
            { "Setup failed: {0}", "安装失败：{0}" },
            { "Nothing to remove — BepInEx is not installed.", "无需移除 — 未安装 BepInEx。" },
            { "Removing BepInEx...", "正在移除 BepInEx…" },
            { "Cleanup unfinished — close TaskBarHero and retry.", "清理未完成 — 请关闭 TaskBarHero 后重试。" },
            { "Done. Mods removed. Presence still works.", "完成。模组已移除。Discord 动态仍可用。" },
            { "Cleanup failed: {0}", "清理失败：{0}" },
            { "Removed {0}/", "已移除 {0}/" },
            { "Could not remove {0}: {1}", "无法移除 {0}：{1}" },
            { "Removed {0}", "已移除 {0}" },
            { "Backed up your save to {0}", "已备份存档到 {0}" },
            { "(Could not back up the save automatically: {0})", "（无法自动备份存档：{0}）" },

            // Config notes
            { "start the game once to create settings", "启动一次游戏以生成设置" },
            { "config unreadable: {0}", "配置无法读取：{0}" },
            { "save failed: {0}", "保存失败：{0}" },
            { "saved — console change needs a game restart", "已保存 — 控制台设置需重启游戏生效" },
            { "saved — applies in-game within ~10s", "已保存 — 约 10 秒内在游戏内生效" },
            { "saved — restart the game to apply (plugin update pending)", "已保存 — 需重启游戏生效（插件待更新）" },
        };
    }
}
