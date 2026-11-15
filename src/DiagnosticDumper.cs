using System;
using System.Text;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveTraderUI
{
    /// <summary>
    /// 诊断 dump：仅在 [Advanced] Diagnostic Dump = true 时输出，方便用户把 BepInEx\LogOutput.log 发给作者定位问题。
    /// 限制：单次 dump 最多 400 行；子树最多 4 层、120 个节点。
    /// </summary>
    internal static class DiagnosticDumper
    {
        private const int MaxLines = 400;
        private const int MaxTreeDepth = 4;
        private const int MaxTreeNodes = 120;

        // ---------------------------------------------------------------- 界面A

        internal static void DumpTraderCards(
            TraderScreensGroup screen,
            RectTransform content,
            RectTransform viewport,
            ScrollRect scroll,
            int cards,
            CardMetrics metrics,
            Vector2 baselineSize,
            string lastSummary,
            string source)
        {
            try
            {
                var sb = new StringBuilder();
                var lineCount = 0;

                Action<string> line = text =>
                {
                    if (lineCount >= MaxLines)
                    {
                        return;
                    }

                    lineCount++;
                    sb.AppendLine("[dumpA] " + text);
                };

                line("===== 界面A（TraderCards 顶栏）诊断开始 =====");
                line("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                     + " screen=" + Screen.width + "x" + Screen.height
                     + " mode=" + (ModConfig.TraderStripScrollingEnabled ? "HorizontalScroll" : "None(scrolling disabled)")
                     + " wheelSpeed=" + ModConfig.WheelSpeed);
                line("summary: " + lastSummary);
                line("resolveSource: " + source);
                line("cards=" + cards + " baselineSize=" + Fmt(baselineSize)
                     + " cardSize=" + Fmt(metrics != null ? metrics.CardSize : Vector2.zero)
                     + " spacing=" + Fmt(metrics != null ? metrics.Spacing : Vector2.zero)
                     + " measuredFromChildren=" + (metrics != null && metrics.MeasuredFromChildren));

                if (screen != null)
                {
                    line("screenType=" + screen.GetType().FullName + " path=" + UILayoutTools.GetPath(screen.transform));
                }

                DumpRect(line, "content", content);
                DumpLayoutComponents(line, content);
                DumpParents(line, content, 6);
                DumpAncestorMasks(line, content);

                if (viewport != null)
                {
                    DumpRect(line, "viewport(" + viewport.name + ")", viewport);
                    line("viewportComponents=" + UILayoutTools.ComponentsOf(viewport.gameObject));
                }

                if (scroll != null)
                {
                    line("scrollRect: horizontal=" + scroll.horizontal + " vertical=" + scroll.vertical
                         + " sensitivity=" + scroll.scrollSensitivity
                         + " movementType=" + scroll.movementType
                         + " content=" + (scroll.content == null ? "<null>" : scroll.content.name)
                         + " viewport=" + (scroll.viewport == null ? "<null>" : scroll.viewport.name));
                }

                DumpTree(line, content, 0, ref lineCount, 0);

                if (content != null && content.childCount > 0)
                {
                    for (var i = 0; i < content.childCount && i < 3; i++)
                    {
                        var child = content.GetChild(i) as RectTransform;
                        if (child != null)
                        {
                            DumpRect(line, "card[" + i + "]", child);
                            line("card[" + i + "] components=" + UILayoutTools.ComponentsOf(child.gameObject));
                        }
                    }
                }

                line("===== 界面A 诊断结束（共 " + lineCount + " 行）=====");
                Log.Info("[diagnostic] " + Environment.NewLine + sb);
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 诊断 dump", ex);
            }
        }

        // ---------------------------------------------------------------- 界面B

        internal static void DumpMerchantsList(
            MerchantsList list,
            RectTransform container,
            RectTransform viewport,
            ScrollRect scroll,
            int count,
            Vector2 cardSize,
            Vector2 spacing,
            RectOffset padding,
            int cols,
            int rows,
            float containerWidth,
            float contentHeight,
            Vector2 visibleSize,
            string lastSummary,
            string source,
            LayoutGroup flowLayout)
        {
            try
            {
                var sb = new StringBuilder();
                var lineCount = 0;

                Action<string> line = text =>
                {
                    if (lineCount >= MaxLines)
                    {
                        return;
                    }

                    lineCount++;
                    sb.AppendLine("[dumpB] " + text);
                };

                line("===== 界面B（Merchants List 商人列表）诊断开始 =====");
                line("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                     + " screen=" + Screen.width + "x" + Screen.height
                     + " mode=" + ModConfig.MerchantsMode
                     + " perRow=" + ModConfig.PerRow
                     + " legacyTraderColumns=" + (ModConfig.TraderColumnsLegacyAlias == null ? 0 : ModConfig.TraderColumnsLegacyAlias.Value));
                line("summary: " + lastSummary);
                line("resolveSource: " + source);
                line("cards=" + count
                     + " cardSize=" + Fmt(cardSize)
                     + " spacing=" + Fmt(spacing)
                     + " padding=" + (padding == null ? "<null>" : (padding.left + "," + padding.right + "," + padding.top + "," + padding.bottom))
                     + " cols=" + cols + " rows=" + rows
                     + " containerWidth=" + containerWidth.ToString("0.##")
                     + " contentHeight=" + contentHeight.ToString("0.##")
                     + " visibleSize=" + Fmt(visibleSize));
                line("computed: needScrollV=" + (contentHeight > visibleSize.y - 24f)
                     + " needScrollH=" + (containerWidth > visibleSize.x - 24f));

                if (list != null)
                {
                    line("merchantsList path=" + UILayoutTools.GetPath(list.transform)
                         + " components=" + UILayoutTools.ComponentsOf(list.gameObject));
                }

                DumpRect(line, "container", container);
                line("containerComponents=" + (container == null ? "<null>" : UILayoutTools.ComponentsOf(container.gameObject)));
                DumpLayoutComponents(line, container);

                if (flowLayout != null)
                {
                    Vector2 fs;
                    RectOffset fp;
                    UILayoutTools.TryReadCustomLayoutParams(flowLayout, out fs, out fp);
                    line("flowLayout type=" + flowLayout.GetType().FullName
                         + " spacing=" + Fmt(fs)
                         + " padding=" + (fp == null ? "<null>" : (fp.left + "," + fp.right + "," + fp.top + "," + fp.bottom))
                         + " childAlignment=" + SafeChildAlignment(flowLayout));
                }
                else
                {
                    line("flowLayout: <none>");
                }

                var fitter = UILayoutTools.FindFitter(container);
                if (fitter != null)
                {
                    line("contentSizeFitter: horizontalFit=" + fitter.horizontalFit + " verticalFit=" + fitter.verticalFit);
                }

                var template = GetTemplateRect(list);
                if (template != null)
                {
                    line("template TraderPanel: " + UILayoutTools.RectOf(template)
                         + " components=" + UILayoutTools.ComponentsOf(template.gameObject));
                }

                DumpParents(line, container, 6);
                DumpAncestorMasks(line, container);

                if (viewport != null)
                {
                    DumpRect(line, "viewport(" + viewport.name + ")", viewport);
                    line("viewportComponents=" + UILayoutTools.ComponentsOf(viewport.gameObject));
                }

                if (scroll != null)
                {
                    line("scrollRect: horizontal=" + scroll.horizontal + " vertical=" + scroll.vertical
                         + " sensitivity=" + scroll.scrollSensitivity
                         + " movementType=" + scroll.movementType
                         + " content=" + (scroll.content == null ? "<null>" : scroll.content.name)
                         + " viewport=" + (scroll.viewport == null ? "<null>" : scroll.viewport.name));
                }

                if (container != null && container.childCount > 0)
                {
                    for (var i = 0; i < container.childCount && i < 3; i++)
                    {
                        var child = container.GetChild(i) as RectTransform;
                        if (child != null)
                        {
                            DumpRect(line, "merchantCard[" + i + "]", child);
                            line("merchantCard[" + i + "] components=" + UILayoutTools.ComponentsOf(child.gameObject));
                        }
                    }
                }

                line("===== 界面B 诊断结束（共 " + lineCount + " 行）=====");
                Log.Info("[diagnostic] " + Environment.NewLine + sb);
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 诊断 dump", ex);
            }
        }

        // ---------------------------------------------------------------- 通用

        private static string SafeChildAlignment(LayoutGroup group)
        {
            try
            {
                return group.childAlignment.ToString();
            }
            catch
            {
                return "?";
            }
        }

        private static RectTransform GetTemplateRect(MerchantsList list)
        {
            try
            {
                if (list == null)
                {
                    return null;
                }

                var type = typeof(MerchantsList);
                var field = type.GetField("_traderPanelTemplate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var component = field == null ? null : field.GetValue(list) as Component;
                return component == null ? null : component.transform as RectTransform;
            }
            catch
            {
                return null;
            }
        }

        private static void DumpRect(Action<string> line, string label, RectTransform rect)
        {
            if (rect == null)
            {
                line(label + ": <null>");
                return;
            }

            line(label + " name=" + rect.name + " active=" + rect.gameObject.activeSelf
                 + " path=" + UILayoutTools.GetPath(rect)
                 + " " + UILayoutTools.RectOf(rect)
                 + " components=" + UILayoutTools.ComponentsOf(rect.gameObject));
        }

        private static void DumpLayoutComponents(Action<string> line, RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            try
            {
                var grid = rect.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    line("GridLayoutGroup: cellSize=" + Fmt(grid.cellSize)
                         + " spacing=" + Fmt(grid.spacing)
                         + " constraint=" + grid.constraint
                         + " constraintCount=" + grid.constraintCount
                         + " padding=" + PaddingToString(grid.padding)
                         + " childAlignment=" + grid.childAlignment
                         + " enabled=" + grid.enabled);
                }

                var horizontal = rect.GetComponent<HorizontalLayoutGroup>();
                if (horizontal != null)
                {
                    line("HorizontalLayoutGroup: spacing=" + horizontal.spacing
                         + " padding=" + PaddingToString(horizontal.padding)
                         + " childAlignment=" + horizontal.childAlignment
                         + " childForceExpandWidth=" + horizontal.childForceExpandWidth
                         + " childForceExpandHeight=" + horizontal.childForceExpandHeight
                         + " enabled=" + horizontal.enabled);
                }

                var vertical = rect.GetComponent<VerticalLayoutGroup>();
                if (vertical != null)
                {
                    line("VerticalLayoutGroup: spacing=" + vertical.spacing
                         + " padding=" + PaddingToString(vertical.padding)
                         + " enabled=" + vertical.enabled);
                }

                var custom = UILayoutTools.FindCustomLayoutGroup(rect);
                if (custom != null)
                {
                    line("CustomLayoutGroup: type=" + custom.GetType().FullName + " enabled=" + custom.enabled);
                }
            }
            catch (Exception ex)
            {
                line("DumpLayoutComponents error: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void DumpParents(Action<string> line, RectTransform start, int levels)
        {
            try
            {
                var current = start;
                for (var i = 0; i < levels && current != null; i++)
                {
                    var rect = current;
                    line("parent[" + i + "] " + UILayoutTools.GetPath(rect)
                         + " active=" + rect.gameObject.activeSelf
                         + " " + UILayoutTools.RectOf(rect)
                         + " components=" + UILayoutTools.ComponentsOf(rect.gameObject));
                    current = rect.parent as RectTransform;
                }
            }
            catch (Exception ex)
            {
                line("DumpParents error: " + ex.Message);
            }
        }

        private static void DumpAncestorMasks(Action<string> line, RectTransform start)
        {
            try
            {
                var masks = new StringBuilder();
                var current = start;
                for (var i = 0; i < 10 && current != null; i++)
                {
                    var mask2D = current.GetComponent<RectMask2D>();
                    var mask = current.GetComponent<Mask>();
                    if (mask2D != null || mask != null)
                    {
                        if (masks.Length > 0)
                        {
                            masks.Append(" | ");
                        }

                        masks.Append(UILayoutTools.GetPath(current))
                            .Append(": ")
                            .Append(mask2D != null ? "RectMask2D " : string.Empty)
                            .Append(mask != null ? "Mask" : string.Empty);
                    }

                    current = current.parent as RectTransform;
                }

                line("ancestorMasks=" + (masks.Length == 0 ? "<none>" : masks.ToString()));
            }
            catch (Exception ex)
            {
                line("DumpAncestorMasks error: " + ex.Message);
            }
        }

        private static void DumpTree(Action<string> line, RectTransform root, int depth, ref int lineCount, int nodes)
        {
            if (root == null || depth > MaxTreeDepth || nodes >= MaxTreeNodes || lineCount >= MaxLines)
            {
                return;
            }

            try
            {
                var builder = new StringBuilder();
                for (var i = 0; i < depth; i++)
                {
                    builder.Append("  ");
                }

                builder.Append(root.name)
                    .Append(" active=").Append(root.gameObject.activeSelf)
                    .Append(" ").Append(UILayoutTools.RectOf(root))
                    .Append(" comps=[").Append(UILayoutTools.ComponentsOf(root.gameObject, 120)).Append("]");
                line("tree " + builder);

                for (var i = 0; i < root.childCount && nodes < MaxTreeNodes; i++)
                {
                    var child = root.GetChild(i) as RectTransform;
                    if (child == null)
                    {
                        continue;
                    }

                    DumpTree(line, child, depth + 1, ref lineCount, nodes + i);
                }
            }
            catch (Exception ex)
            {
                line("DumpTree error: " + ex.Message);
            }
        }

        private static string Fmt(Vector2 value)
        {
            return "[" + value.x.ToString("0.##") + "," + value.y.ToString("0.##") + "]";
        }

        private static string PaddingToString(RectOffset padding)
        {
            if (padding == null)
            {
                return "<null>";
            }

            return "(" + padding.left + "," + padding.right + "," + padding.top + "," + padding.bottom + ")";
        }
    }
}
