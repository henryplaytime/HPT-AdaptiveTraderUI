using System;
using System.Reflection;
using EFT.UI;
using SPT.Reflection.Patching;

namespace KaenoAdaptiveTraderUI.Patches
{
    /// <summary>
    /// 在 MerchantsList.Show(...) 执行完后处理“商人选择列表”（Traders Container，850x480 的 FlowLayoutGroup）。
    /// 这是旧 AdjustableTraderRows 真正生效的界面：默认一行约 5 张商人卡，列数可通过本模组配置。
    /// 4.1.6 反编译事实：MerchantsList.Show 内部用 ViewList 把 TraderPanel 实例化到 _tradersContainer。
    /// </summary>
    internal sealed class MerchantsListPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return PatchTools.FindDeclaredMethod(typeof(MerchantsList), "Show", 7);
        }

        [PatchPostfix]
        private static void Postfix(MerchantsList __instance)
        {
            try
            {
                MerchantsListController.OnMerchantsListShown(__instance);
            }
            catch (Exception ex)
            {
                Log.Exception("MerchantsList.Show postfix", ex);
            }
        }
    }
}
