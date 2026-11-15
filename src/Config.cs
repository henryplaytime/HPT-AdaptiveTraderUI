using System;
using BepInEx.Configuration;
using UnityEngine;

namespace AdaptiveTraderUI
{
    /// <summary>Merchants List（商人选择列表）布局模式。</summary>
    internal enum MerchantsLayoutMode
    {
        /// <summary>完全不动（回退原版）。</summary>
        None = 0,

        /// <summary>保留 FlowLayoutGroup，每行 N 张卡；行数超出可视高度时加纵向滚动。</summary>
        Wrap = 1,

        /// <summary>所有商人卡排成一行，超出屏幕宽度时横向鼠标滚轮滚动。</summary>
        Horizontal = 2,
    }

    internal static class ModConfig
    {
        // ---- [Config] ----
        internal static ConfigEntry<float> ScrollWheelSpeed;
        internal static ConfigEntry<bool> TraderStripScrolling;
        internal static ConfigEntry<bool> UseElastic;
        internal static ConfigEntry<bool> EnablePlayerCardTweaks;

        // ---- [Layout] ----
        internal static ConfigEntry<string> MerchantsLayoutModeEntry;
        internal static ConfigEntry<int> MerchantsPerRow;
        internal static ConfigEntry<int> TraderColumnsLegacyAlias;

        // ---- [Advanced] ----
        internal static ConfigEntry<bool> DiagnosticDump;
        internal static ConfigEntry<bool> DebugLogging;

        /// <summary>布局相关配置变更（模式/列数/滚动开关）时触发，用于热更新。</summary>
        internal static event Action LayoutSettingsChanged;

        /// <summary>滚轮速度变更时触发，用于立即更新已有 ScrollRect。</summary>
        internal static event Action ScrollSpeedChanged;

        /// <summary>v4.2.3-DIAG：最后一次被修改的配置项名称，供诊断文件记录。</summary>
        internal static string LastChangedSetting = "<none>";

        internal static void Bind(ConfigFile config)
        {
            ScrollWheelSpeed = config.Bind(
                "Config",
                "Scroll Wheel Speed",
                30f,
                new ConfigDescription(
                    "鼠标滚轮滚动速度（每格滚动的像素量；旧 Kaeno 模组默认 30）。横向滚动和纵向滚动都使用该值。",
                    new AcceptableValueRange<float>(1f, 500f)));

            TraderStripScrolling = config.Bind(
                "Config",
                "Trader Strip Scrolling",
                true,
                "界面A（TraderScreensGroup 顶部商人卡片条）是否启用横向鼠标滚轮滚动。关闭后该界面完全保持原版（等价旧 Kaeno 的 None）。界面B（商人选择列表）由 [Layout] Merchants Layout Mode 单独控制。");

            UseElastic = config.Bind(
                "Config",
                "Use Elastic",
                true,
                "界面A（顶部商人卡条）滚动越界时是否弹性回弹：true = 弹性回弹（旧 Kaeno 行为），false = 硬性夹取。界面B（商人选择列表）固定使用硬性夹取，不受此项影响。v4.2.4 起该项对界面A 重新生效。");

            EnablePlayerCardTweaks = config.Bind(
                "Config",
                "Enable Player Card Tweaks",
                true,
                "玩家卡/金钱面板美化（Money 位置 +60x、Money 间距 10、Background Tile 激活并加宽到 500、Background 变黑）。默认开，所有节点均空引用保护。");

            MerchantsLayoutModeEntry = config.Bind(
                "Layout",
                "Merchants Layout Mode",
                "Wrap",
                "商人选择列表（Merchants List，默认一行约 5 张卡的那个界面）的布局模式：Wrap（一行 N 张、自动换行，超出高度则纵向滚动，默认）/ Horizontal（全部一行、横向滚动）/ None（不改造，回退原版）。大小写不敏感。");

            MerchantsPerRow = config.Bind(
                "Layout",
                "Merchants Per Row",
                8,
                new ConfigDescription(
                    "Wrap 模式下每行显示的商人卡数量；Horizontal 模式下仅用于兜底（实际会全部排成一行）。范围 1..20，会自动 clamp 到实际商人卡数量。",
                    new AcceptableValueRange<int>(1, 20)));

            TraderColumnsLegacyAlias = config.Bind(
                "Layout",
                "Trader Columns",
                0,
                new ConfigDescription(
                    "旧名兼容项（对应旧 AdjustableTraderRows 的 Trader Columns）。0 = 忽略；>0 时覆盖 Merchants Per Row。",
                    new AcceptableValueRange<int>(0, 20)));

            DiagnosticDump = config.Bind(
                "Advanced",
                "Diagnostic Dump",
                false,
                "诊断用（默认关闭）：把界面A 的 UI 几何、滚动范围、卡片计数和逻辑自检写入 BepInEx\\HPT-AdaptiveTraderUI-diag.txt，用于排查商人卡条/列表显示问题。反馈问题时临时打开，把该文件发给作者即可；正常游玩不需要开启。");

            DebugLogging = config.Bind(
                "Advanced",
                "Debug Logging",
                false,
                "输出更详细的调试日志（每次应用布局的中间值）。默认关闭。");

            MerchantsLayoutModeEntry.SettingChanged += (_, __) => RaiseLayout("Merchants Layout Mode");
            MerchantsPerRow.SettingChanged += (_, __) => RaiseLayout("Merchants Per Row");
            TraderColumnsLegacyAlias.SettingChanged += (_, __) => RaiseLayout("Trader Columns");
            TraderStripScrolling.SettingChanged += (_, __) => RaiseLayout("Trader Strip Scrolling");
            UseElastic.SettingChanged += (_, __) => RaiseLayout("Use Elastic");
            ScrollWheelSpeed.SettingChanged += (_, __) =>
            {
                LastChangedSetting = "Scroll Wheel Speed";
                SafeRaise(ScrollSpeedChanged);
            };

            Log.Info("配置已加载: merchantsMode=" + MerchantsMode
                     + " perRow=" + PerRow
                     + " traderStripScrolling=" + TraderStripScrollingEnabled
                     + " wheelSpeed=" + WheelSpeed
                     + " playerCardTweaks=" + PlayerCardTweaksEnabled
                     + " diagnosticDump=" + DiagnosticDumpEnabled);
        }

        private static void SafeRaise(Action handler)
        {
            try { handler?.Invoke(); }
            catch (Exception ex) { Log.Exception("配置变更回调", ex); }
        }

        private static void RaiseLayout(string settingName)
        {
            LastChangedSetting = settingName;
            SafeRaise(LayoutSettingsChanged);
        }

        internal static bool TraderStripScrollingEnabled => TraderStripScrolling == null || TraderStripScrolling.Value;
        internal static bool Elastic => UseElastic == null || UseElastic.Value;
        internal static bool DiagnosticDumpEnabled => DiagnosticDump != null && DiagnosticDump.Value;
        internal static bool PlayerCardTweaksEnabled => EnablePlayerCardTweaks == null || EnablePlayerCardTweaks.Value;

        internal static float WheelSpeed
        {
            get
            {
                try
                {
                    return Mathf.Clamp(ScrollWheelSpeed == null ? 30f : ScrollWheelSpeed.Value, 1f, 500f);
                }
                catch
                {
                    return 30f;
                }
            }
        }

        /// <summary>每行卡片数（含旧名 Trader Columns 覆盖）。</summary>
        internal static int PerRow
        {
            get
            {
                try
                {
                    var legacy = TraderColumnsLegacyAlias == null ? 0 : TraderColumnsLegacyAlias.Value;
                    var value = legacy > 0 ? legacy : (MerchantsPerRow == null ? 8 : MerchantsPerRow.Value);
                    return Mathf.Clamp(value, 1, 20);
                }
                catch
                {
                    return 8;
                }
            }
        }

        internal static MerchantsLayoutMode MerchantsMode
        {
            get
            {
                var raw = MerchantsLayoutModeEntry == null ? "Wrap" : MerchantsLayoutModeEntry.Value;
                return ParseMerchantsMode(raw, true);
            }
        }

        internal static MerchantsLayoutMode ParseMerchantsMode(string raw, bool logWarning)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return MerchantsLayoutMode.Wrap;
            }

            switch (raw.Trim().ToLowerInvariant())
            {
                case "none":
                case "vanilla":
                case "off":
                case "0":
                    return MerchantsLayoutMode.None;
                case "wrap":
                case "wrapverticalscroll":
                case "vertical":
                case "vscroll":
                case "1":
                    return MerchantsLayoutMode.Wrap;
                case "horizontal":
                case "horizontalscroll":
                case "hscroll":
                case "scroll":
                case "2":
                    return MerchantsLayoutMode.Horizontal;
                default:
                    if (logWarning)
                    {
                        Log.Warn("无法识别的 Merchants Layout Mode 值 \"" + raw + "\"，已回退为 Wrap。可用值：Wrap / Horizontal / None。");
                    }

                    return MerchantsLayoutMode.Wrap;
            }
        }
    }
}
