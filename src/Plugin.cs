using System;
using BepInEx;
using KaenoAdaptiveTraderUI.Patches;

namespace KaenoAdaptiveTraderUI
{
    /// <summary>
    /// 插件入口。
    /// GUID 故意复用旧 Kaeno 模组的 "com.kaeno.TraderScrolling"，以便直接复用/迁移
    /// BepInEx\config\com.kaeno.TraderScrolling.cfg，并作为旧模组的即插即用替代品。
    /// 注意：安装本插件前必须把旧的 Kaeno-TraderScrolling.dll 从 BepInEx\plugins 移走，
    /// 否则两个插件 GUID 相同，BepInEx 会报重复 GUID 并可能只加载其中一个。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kaeno.TraderScrolling";
        public const string PluginName = "Kaeno-AdaptiveTraderUI";
        public const string PluginVersion = "4.2.4";

        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log.Init(Logger);
            Log.Info("正在加载 v" + PluginVersion + "（GUID=" + PluginGuid + "，旧 Kaeno 模组替代版）");
            Log.Info("v4.2.4 修复版：界面A 滚动 viewport 改为实际可见区宽度、内容宽度按卡片末端计算、"
                     + "Use Elastic 恢复生效、配置重应用保留滚动位置。诊断数据仍写入 " + DiagFile.Path + "（可忽略/删除）。");

            try
            {
                ModConfig.Bind(Config);
            }
            catch (Exception ex)
            {
                Log.Exception("加载配置失败，插件将继续以默认值运行", ex);
            }

            // 每个 patch 单独 try/catch：任何一个 patch 启用失败都不影响其它功能。
            EnablePatch(new TraderScreensGroupPatch(), "EFT.UI.TraderScreensGroup.Show（商人界面顶部卡片条）");
            EnablePatch(new MerchantsListPatch(), "EFT.UI.MerchantsList.Show（商人选择列表）");
            EnablePatch(new PlayerCardPatch(), "EFT.UI.DisplayMoneyPanelTMPText.Show（玩家卡美化）");

            Log.Info("v" + PluginVersion + " 加载完成。merchantsMode=" + ModConfig.MerchantsMode
                     + " perRow=" + ModConfig.PerRow
                     + " traderStripScrolling=" + ModConfig.TraderStripScrollingEnabled
                     + " diagnosticDump=" + ModConfig.DiagnosticDumpEnabled);
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
