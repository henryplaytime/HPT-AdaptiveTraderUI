using System;
using System.Reflection;
using EFT.UI;
using SPT.Reflection.Patching;

namespace KaenoAdaptiveTraderUI.Patches
{
    /// <summary>
    /// 旧 Kaeno 附带的玩家卡/金钱面板美化：每次金钱面板刷新时尝试应用（幂等，重复调用不会累加偏移）。
    /// 4.1.6 反编译事实：EFT.UI.DisplayMoneyPanelTMPText.Show(IEnumerable&lt;Item&gt;) 存在且非静态；
    /// 该组件正好挂在 "Right Person" 下的 "Money" 节点上，所以可以直接从 __instance.transform 向上定位玩家卡，
    /// 避免 GameObject.Find("Right Person") 误命中 TraderDealScreen 下的同名节点。
    /// </summary>
    internal sealed class PlayerCardPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return PatchTools.FindDeclaredMethod(typeof(DisplayMoneyPanelTMPText), "Show", 1);
        }

        [PatchPostfix]
        private static void Postfix(DisplayMoneyPanelTMPText __instance)
        {
            try
            {
                PlayerCardTweak.Apply(__instance != null ? __instance.transform : null);
            }
            catch (Exception ex)
            {
                Log.Exception("DisplayMoneyPanelTMPText.Show postfix", ex);
            }
        }
    }
}
