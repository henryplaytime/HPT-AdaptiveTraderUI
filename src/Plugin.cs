using System;
using System.IO;
using BepInEx;
using AdaptiveTraderUI.Patches;

namespace AdaptiveTraderUI
{
    /// <summary>
    /// 插件入口。
    ///
    /// Forge 要求：
    ///   - GUID 使用本项目自己的反向域名：com.henryplaytime.adaptivetraderui
    ///   - [BepInPlugin] 名称必须是 "用户名-ModName"：henryplaytime-AdaptiveTraderUI
    ///   - 版本号必须与 csproj / 发布包 / Forge 页面一致：5.0.0
    ///
    /// 配置文件名由 GUID 派生：BepInEx\config\com.henryplaytime.adaptivetraderui.cfg
    ///
    /// 注意：本插件与原 Kaeno-TraderScrolling / AdjustableTraderRows 功能重叠。
    /// 安装前必须移除旧 DLL；启动时也会检测旧 Kaeno 插件，若仍存在则本次不启用任何 patch，
    /// 避免两个插件同时修改同一套商人界面。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.henryplaytime.adaptivetraderui";
        public const string PluginName = "henryplaytime-AdaptiveTraderUI";
        public const string PluginVersion = "5.0.0";

        private const string LegacyKaenoPluginFileName = "Kaeno-TraderScrolling.dll";

        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log.Init(Logger);
            Log.Info("正在加载 v" + PluginVersion + "（GUID=" + PluginGuid + "）");

            try
            {
                ModConfig.Bind(Config);
            }
            catch (Exception ex)
            {
                Log.Exception("加载配置失败，插件将继续以默认值运行", ex);
            }

            // 旧 Kaeno 模组与本插件修改同一套商人界面；同时启用会互相覆盖布局，必须阻止。
            if (DetectLegacyKaenoPlugin())
            {
                Log.Error("检测到旧 Kaeno-TraderScrolling.dll 仍安装在 BepInEx\\plugins 下。"
                          + "本插件不能与它同时启用，本次不启用任何 patch。"
                          + "请删除或改名旧的 Kaeno-TraderScrolling.dll（例如改为 .dll.bak）后重启游戏。");
                return;
            }

            // 每个 patch 单独 try/catch：任何一个 patch 启用失败都不影响其它功能。
            EnablePatch(new TraderScreensGroupPatch(), "EFT.UI.TraderScreensGroup.Show（商人界面顶部卡片条）");
            EnablePatch(new MerchantsListPatch(), "EFT.UI.MerchantsList.Show（商人选择列表）");
            EnablePatch(new PlayerCardPatch(), "EFT.UI.DisplayMoneyPanelTMPText.Show（玩家卡美化）");

            Log.Info("v" + PluginVersion + " 加载完成。merchantsMode=" + ModConfig.MerchantsMode
                     + " perRow=" + ModConfig.PerRow
                     + " traderStripScrolling=" + ModConfig.TraderStripScrollingEnabled
                     + " diagnosticDump=" + ModConfig.DiagnosticDumpEnabled);

            if (ModConfig.DiagnosticDumpEnabled)
            {
                Log.Info("诊断输出已启用：BepInEx\\HPT-AdaptiveTraderUI-diag.txt（仅排障时建议开启）");
            }
        }

        private static bool DetectLegacyKaenoPlugin()
        {
            try
            {
                var pluginPath = Path.Combine(BepInEx.Paths.BepInExRootPath, "plugins");
                if (string.IsNullOrEmpty(pluginPath) || !Directory.Exists(pluginPath))
                {
                    return false;
                }

                var matches = Directory.GetFiles(pluginPath, LegacyKaenoPluginFileName, SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    Log.Warn("发现旧模组文件：" + matches[0]);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warn("检测旧 Kaeno 模组失败（将按未安装处理）: " + ex.Message);
            }

            return false;
        }

        private static void EnablePatch(SPT.Reflection.Patching.ModulePatch patch, string description)
        {
            try
            {
                patch.Enable();
                Log.Info("已启用 patch: " + description);
            }
            catch (Exception ex)
            {
                Log.Error("启用 patch 失败（该功能将不可用，其它功能不受影响）: " + description);
                Log.Exception("patch 启用失败", ex);
            }
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}
