using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KaenoAdaptiveTraderUI
{
    /// <summary>
    /// 旧 Kaeno 附带的玩家卡/金钱面板美化（全部空引用保护 + 幂等）：
    ///   1. "Right Person" 下 "Money" 的 anchoredPosition.x += 60（按实例缓存原始位置，重复调用不会累加）；
    ///   2. "Money" 上的 HorizontalLayoutGroup.spacing = 10；
    ///   3. "Background Tile" 的 Image.type = Simple、SetActive(true)、sizeDelta = (500, 0)；
    ///   4. "Background" 的 Image 颜色 = 黑色不透明。
    /// 目标面板优先通过 DisplayMoneyPanelTMPText 的父链定位（避免 GameObject.Find("Right Person") 拿到
    /// TraderDealScreen 下另一个同名节点）。
    /// </summary>
    internal static class PlayerCardTweak
    {
        private const float MoneyOffsetX = 60f;
        private const float MoneySpacing = 10f;
        private static readonly Vector2 TileSizeDelta = new Vector2(500f, 0f);

        private static readonly Dictionary<int, Vector2> MoneyOriginalPositions = new Dictionary<int, Vector2>();
        private static readonly HashSet<string> WarnedMissing = new HashSet<string>();
        private static bool _warnedMissingRoot;

        internal static void Apply(Transform source)
        {
            try
            {
                if (!ModConfig.PlayerCardTweaksEnabled)
                {
                    return;
                }

                var rightPerson = ResolveRightPerson(source);
                if (rightPerson == null)
                {
                    if (!_warnedMissingRoot)
                    {
                        _warnedMissingRoot = true;
                        Log.Warn("玩家卡美化：未找到 \"Right Person\" 节点（界面结构可能变化），本次跳过。");
                    }

                    return;
                }

                TryStep("Money 位置", () => ApplyMoneyPosition(rightPerson));
                TryStep("Money 间距", () => ApplyMoneySpacing(rightPerson));
                TryStep("Background Tile", () => ApplyBackgroundTile(rightPerson));
                TryStep("Background 颜色", () => ApplyBackgroundColor(rightPerson));
            }
            catch (Exception ex)
            {
                Log.Exception("玩家卡美化", ex);
            }
        }

        // ------------------------------------------------------------------ 定位

        private static Transform ResolveRightPerson(Transform source)
        {
            try
            {
                // 1) 从 DisplayMoneyPanelTMPText（Money 节点）向上找，最精确。
                if (source != null)
                {
                    var current = source;
                    for (var i = 0; i < 8 && current != null; i++)
                    {
                        if (string.Equals(current.name, "Right Person", StringComparison.Ordinal))
                        {
                            return current;
                        }

                        current = current.parent;
                    }

                    // 2) 没找到就退而求其次：在 source 的最近公共父级下找
                    var root = source.root;
                    if (root != null)
                    {
                        var found = UILayoutTools.FindDescendant(root, "Right Person", 12);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("玩家卡美化：从 Money 父链定位 Right Person 失败: " + ex.Message);
            }

            try
            {
                var menu = Singleton<MenuUI>.Instance;
                if (menu != null && menu.TraderScreensGroup != null)
                {
                    var found = UILayoutTools.FindDescendant(menu.TraderScreensGroup.transform, "Right Person", 10);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("玩家卡美化：通过 MenuUI.TraderScreensGroup 定位失败: " + ex.Message);
            }

            try
            {
                var found = GameObject.Find("Right Person");
                if (found != null)
                {
                    return found.transform;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("玩家卡美化：GameObject.Find 失败: " + ex.Message);
            }

            return null;
        }

        private static void TryStep(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Warn("玩家卡美化步骤 \"" + name + "\" 失败（已跳过）: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ 各步骤

        private static void ApplyMoneyPosition(Transform rightPerson)
        {
            var money = FindDirectOrDescendant(rightPerson, "Money");
            if (money == null)
            {
                WarnMissingOnce("Money", "\"Money\" 节点");
                return;
            }

            var rect = money as RectTransform;
            if (rect == null)
            {
                WarnMissingOnce("Money-Rect", "\"Money\" 的 RectTransform");
                return;
            }

            var id = rect.GetInstanceID();
            Vector2 original;
            if (!MoneyOriginalPositions.TryGetValue(id, out original))
            {
                original = rect.anchoredPosition;
                MoneyOriginalPositions[id] = original;
            }

            var target = new Vector2(original.x + MoneyOffsetX, original.y);
            if (rect.anchoredPosition != target)
            {
                rect.anchoredPosition = target;
                Log.Debug("玩家卡美化：Money 位置 " + original + " -> " + target);
            }
        }

        private static void ApplyMoneySpacing(Transform rightPerson)
        {
            try
            {
                var groups = rightPerson.GetComponentsInChildren<HorizontalLayoutGroup>(true);
                HorizontalLayoutGroup moneyGroup = null;
                foreach (var group in groups)
                {
                    if (group != null && string.Equals(group.gameObject.name, "Money", StringComparison.Ordinal))
                    {
                        moneyGroup = group;
                        break;
                    }
                }

                if (moneyGroup == null)
                {
                    WarnMissingOnce("Money-HLG", "\"Money\" 上的 HorizontalLayoutGroup");
                    return;
                }

                if (!Mathf.Approximately(moneyGroup.spacing, MoneySpacing))
                {
                    moneyGroup.spacing = MoneySpacing;
                    Log.Debug("玩家卡美化：Money HorizontalLayoutGroup.spacing -> " + MoneySpacing);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("玩家卡美化：Money 间距", ex);
            }
        }

        private static void ApplyBackgroundTile(Transform rightPerson)
        {
            try
            {
                var tile = FindDirectOrDescendant(rightPerson, "Background Tile");
                if (tile == null)
                {
                    WarnMissingOnce("BackgroundTile", "\"Background Tile\" 节点");
                    return;
                }

                var image = tile.GetComponent<Image>();
                if (image != null && image.type != Image.Type.Simple)
                {
                    image.type = Image.Type.Simple;
                }

                if (!tile.gameObject.activeSelf)
                {
                    tile.gameObject.SetActive(true);
                }

                var rect = tile as RectTransform;
                if (rect != null && rect.sizeDelta != TileSizeDelta)
                {
                    rect.sizeDelta = TileSizeDelta;
                }

                Log.Debug("玩家卡美化：Background Tile 已设为 Simple/激活/sizeDelta=500x0");
            }
            catch (Exception ex)
            {
                Log.Exception("玩家卡美化：Background Tile", ex);
            }
        }

        private static void ApplyBackgroundColor(Transform rightPerson)
        {
            try
            {
                var images = rightPerson.GetComponentsInChildren<Image>(true);
                Image background = null;
                foreach (var image in images)
                {
                    if (image != null && string.Equals(image.gameObject.name, "Background", StringComparison.Ordinal))
                    {
                        background = image;
                        break;
                    }
                }

                if (background == null)
                {
                    WarnMissingOnce("Background", "\"Background\" 的 Image");
                    return;
                }

                var black = new Color(0f, 0f, 0f, 1f);
                if (background.color != black)
                {
                    background.color = black;
                    Log.Debug("玩家卡美化：Background 颜色 -> 黑色不透明");
                }
            }
            catch (Exception ex)
            {
                Log.Exception("玩家卡美化：Background 颜色", ex);
            }
        }

        // ------------------------------------------------------------------ 工具

        private static Transform FindDirectOrDescendant(Transform root, string name)
        {
            try
            {
                if (root == null)
                {
                    return null;
                }

                for (var i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (child != null && string.Equals(child.name, name, StringComparison.Ordinal))
                    {
                        return child;
                    }
                }

                return UILayoutTools.FindDescendant(root, name, 8);
            }
            catch
            {
                return null;
            }
        }

        private static void WarnMissingOnce(string key, string description)
        {
            if (WarnedMissing.Add(key))
            {
                Log.Warn("玩家卡美化：未找到 " + description + "，该步骤已跳过（其它步骤不受影响）。");
            }
        }
    }
}
