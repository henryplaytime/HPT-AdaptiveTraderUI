using System;
using System.Reflection;
using EFT.UI;
using SPT.Reflection.Patching;

namespace KaenoAdaptiveTraderUI.Patches
{
    /// <summary>
    /// 在 TraderScreensGroup.Show(TraderScreenController) 执行完后处理顶部商人卡片条（TraderCards）。
    /// 4.1.6 反编译事实：Show() 内部先 method_4() 实例化所有 TraderCard，再 SelectTrader()，
    /// 所以 postfix 时卡片已生成，可以安全读取容器子节点数量。
    /// </summary>
    internal sealed class TraderScreensGroupPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return PatchTools.FindDeclaredMethod(typeof(TraderScreensGroup), "Show", 1);
        }

        [PatchPostfix]
        private static void Postfix(TraderScreensGroup __instance)
        {
            try
            {
                TraderCardsController.OnTraderScreenShown(__instance);
            }
            catch (Exception ex)
            {
                // 绝不把异常抛回游戏循环；最坏情况就是本模组不生效，原版界面照常可用。
                Log.Exception("TraderScreensGroup.Show postfix", ex);
            }
        }
    }
}
