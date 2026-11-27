using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveTraderUI
{
    /// <summary>RectTransform 状态快照，用于把容器恢复成游戏原本的样子（Layout Mode=None 或不需要滚动时）。</summary>
    internal struct RectTransformSnapshot
    {
        internal bool Captured;
        internal Transform Parent;
        internal int SiblingIndex;
        internal Vector2 AnchorMin;
        internal Vector2 AnchorMax;
        internal Vector2 Pivot;
        internal Vector2 AnchoredPosition;
        internal Vector2 SizeDelta;

        internal void Capture(RectTransform rt)
        {
            if (rt == null)
            {
                Captured = false;
                return;
            }

            Captured = true;
            Parent = rt.parent;
            try { SiblingIndex = rt.GetSiblingIndex(); } catch { SiblingIndex = -1; }
            AnchorMin = rt.anchorMin;
            AnchorMax = rt.anchorMax;
            Pivot = rt.pivot;
            AnchoredPosition = rt.anchoredPosition;
            SizeDelta = rt.sizeDelta;
        }

        internal void Restore(RectTransform rt)
        {
            if (!Captured || rt == null)
            {
                return;
            }

            try
            {
                if (Parent != null && rt.parent != Parent)
                {
                    rt.SetParent(Parent, false);
                }

                rt.anchorMin = AnchorMin;
                rt.anchorMax = AnchorMax;
                rt.pivot = Pivot;
                rt.anchoredPosition = AnchoredPosition;
                rt.sizeDelta = SizeDelta;

                if (SiblingIndex >= 0)
                {
                    rt.SetSiblingIndex(SiblingIndex);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("RectTransform 快照恢复", ex);
            }
        }
    }

    /// <summary>从实际卡片/布局组件测量出来的尺寸信息（不写死旧模组的 177.25 之类数值）。</summary>
    internal sealed class CardMetrics
    {
        internal Vector2 CardSize;
        internal Vector2 Spacing;
        internal RectOffset Padding;
        internal bool MeasuredFromChildren;

        internal CardMetrics()
        {
            CardSize = Vector2.zero;
            Spacing = new Vector2(10f, 10f);
            Padding = new RectOffset();
        }
    }

    internal static class UILayoutTools
    {
        internal static readonly Vector2 FallbackCardSize = new Vector2(160f, 172f);

        // ---------------------------------------------------------------- 层级/组件查找

        internal static RectTransform FindDescendant(Transform root, string name, int maxDepth = 10)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            try
            {
                var queue = new Queue<KeyValuePair<Transform, int>>();
                queue.Enqueue(new KeyValuePair<Transform, int>(root, 0));
                while (queue.Count > 0)
                {
                    var pair = queue.Dequeue();
                    var current = pair.Key;
                    if (current == null)
                    {
                        continue;
                    }

                    if (string.Equals(current.name, name, StringComparison.Ordinal))
                    {
                        return current as RectTransform;
                    }

                    if (pair.Value >= maxDepth)
                    {
                        continue;
                    }

                    for (var i = 0; i < current.childCount; i++)
                    {
                        var child = current.GetChild(i);
                        if (child != null)
                        {
                            queue.Enqueue(new KeyValuePair<Transform, int>(child, pair.Value + 1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("查找子节点 " + name, ex);
            }

            return null;
        }

        internal static string GetPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            try
            {
                var builder = new StringBuilder(transform.name);
                var current = transform.parent;
                var guard = 0;
                while (current != null && guard++ < 24)
                {
                    builder.Insert(0, current.name + "/");
                    current = current.parent;
                }

                return builder.ToString();
            }
            catch
            {
                return transform.name;
            }
        }

        internal static GridLayoutGroup FindGrid(RectTransform content)
        {
            return content == null ? null : content.GetComponent<GridLayoutGroup>();
        }

        internal static HorizontalLayoutGroup FindHorizontal(RectTransform content)
        {
            return content == null ? null : content.GetComponent<HorizontalLayoutGroup>();
        }

        internal static VerticalLayoutGroup FindVertical(RectTransform content)
        {
            return content == null ? null : content.GetComponent<VerticalLayoutGroup>();
        }

        internal static ContentSizeFitter FindFitter(RectTransform content)
        {
            return content == null ? null : content.GetComponent<ContentSizeFitter>();
        }

        /// <summary>
        /// 找“非 Grid/Horizontal/Vertical”的自定义布局组件（例如 SPT 商人列表上的
        /// UnityEngine.UI.Extensions.FlowLayoutGroup）。该类型不在任何游戏 Managed dll 里，
        /// 无法编译期引用，所以只能按 LayoutGroup 基类做运行时判断。
        /// </summary>
        internal static LayoutGroup FindCustomLayoutGroup(RectTransform content)
        {
            if (content == null)
            {
                return null;
            }

            try
            {
                var groups = content.GetComponents<LayoutGroup>();
                if (groups == null)
                {
                    return null;
                }

                foreach (var group in groups)
                {
                    if (group == null)
                    {
                        continue;
                    }

                    if (group is GridLayoutGroup || group is HorizontalOrVerticalLayoutGroup)
                    {
                        continue;
                    }

                    return group;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("查找自定义布局组件", ex);
            }

            return null;
        }

        // ---------------------------------------------------------------- 测量

        /// <summary>从布局组件 + 实际卡片子节点测量卡片尺寸、间距、边距；失败时回退到 prefab。</summary>
        internal static CardMetrics Measure(RectTransform content, GridLayoutGroup ourGrid, Component cardPrefabFallback)
        {
            var metrics = new CardMetrics();
            if (content == null)
            {
                metrics.CardSize = FallbackCardSize;
                return metrics;
            }

            try
            {
                if (ourGrid != null)
                {
                    metrics.CardSize = ourGrid.cellSize;
                    metrics.Spacing = ourGrid.spacing;
                    metrics.Padding = ourGrid.padding ?? new RectOffset();
                }
                else
                {
                    var grid = FindGrid(content);
                    if (grid != null)
                    {
                        metrics.CardSize = grid.cellSize;
                        metrics.Spacing = grid.spacing;
                        metrics.Padding = grid.padding ?? new RectOffset();
                    }

                    var horizontal = FindHorizontal(content);
                    if (horizontal != null)
                    {
                        metrics.Spacing = new Vector2(horizontal.spacing, metrics.Spacing.y);
                        metrics.Padding = horizontal.padding ?? new RectOffset();
                    }

                    var vertical = FindVertical(content);
                    if (vertical != null)
                    {
                        metrics.Spacing = new Vector2(metrics.Spacing.x, vertical.spacing);
                        metrics.Padding = vertical.padding ?? new RectOffset();
                    }

                    var custom = FindCustomLayoutGroup(content);
                    if (custom != null)
                    {
                        Vector2 spacing;
                        RectOffset padding;
                        if (TryReadCustomLayoutParams(custom, out spacing, out padding))
                        {
                            metrics.Spacing = spacing;
                            if (padding != null)
                            {
                                metrics.Padding = padding;
                            }
                        }
                    }
                }

                // 卡片实际尺寸：优先 LayoutElement.preferred（布局未刷新时 rect 还是 prefab 的 120x172，
                // 而 HorizontalLayoutGroup 实际使用 LayoutElement 的 118x168），再退到 rect/sizeDelta。
                var first = content.childCount > 0 ? content.GetChild(0) as RectTransform : null;
                var second = content.childCount > 1 ? content.GetChild(1) as RectTransform : null;

                if (first != null)
                {
                    var layoutElement = first.GetComponent<LayoutElement>();
                    if (layoutElement != null
                        && layoutElement.preferredWidth > 1f
                        && layoutElement.preferredHeight > 1f)
                    {
                        metrics.CardSize = new Vector2(layoutElement.preferredWidth, layoutElement.preferredHeight);
                        metrics.MeasuredFromChildren = true;
                    }
                    else
                    {
                        var rect = first.rect.size;
                        if (rect.x > 1f && rect.y > 1f)
                        {
                            metrics.CardSize = rect;
                            metrics.MeasuredFromChildren = true;
                        }
                        else if (first.sizeDelta.x > 1f && first.sizeDelta.y > 1f)
                        {
                            metrics.CardSize = first.sizeDelta;
                            metrics.MeasuredFromChildren = true;
                        }
                    }

                    // 用前两个子节点的位置差反推间距（对自定义 FlowLayoutGroup 尤其有用）
                    if (second != null && metrics.CardSize.x > 1f)
                    {
                        var delta = second.anchoredPosition - first.anchoredPosition;
                        if (Mathf.Abs(delta.x) > 1f && Mathf.Abs(delta.y) < Mathf.Abs(metrics.CardSize.y))
                        {
                            var spacingX = Mathf.Abs(delta.x) - metrics.CardSize.x;
                            if (spacingX >= 0f && spacingX < 400f)
                            {
                                metrics.Spacing = new Vector2(spacingX, metrics.Spacing.y);
                            }
                        }
                        else if (Mathf.Abs(delta.y) > 1f)
                        {
                            var spacingY = Mathf.Abs(delta.y) - metrics.CardSize.y;
                            if (spacingY >= 0f && spacingY < 400f)
                            {
                                metrics.Spacing = new Vector2(metrics.Spacing.x, spacingY);
                            }
                        }
                    }
                }

                if (metrics.CardSize.x < 1f || metrics.CardSize.y < 1f)
                {
                    var prefabSize = GetPrefabCardSize(cardPrefabFallback);
                    if (prefabSize.x > 1f && prefabSize.y > 1f)
                    {
                        metrics.CardSize = prefabSize;
                    }
                }

                if (metrics.CardSize.x < 1f || metrics.CardSize.y < 1f)
                {
                    metrics.CardSize = FallbackCardSize;
                    Log.Debug("无法测量卡片尺寸，使用回退值 " + FallbackCardSize);
                }
            }
            catch (Exception ex)
            {
                metrics.CardSize = FallbackCardSize;
                metrics.Spacing = new Vector2(8f, 8f);
                metrics.Padding = new RectOffset();
                Log.Exception("测量卡片尺寸", ex);
            }

            return metrics;
        }

        internal static Vector2 GetPrefabCardSize(Component prefab)
        {
            try
            {
                if (prefab == null)
                {
                    return Vector2.zero;
                }

                var rect = prefab.transform as RectTransform;
                if (rect == null)
                {
                    return Vector2.zero;
                }

                var size = rect.rect.size;
                if (size.x > 1f && size.y > 1f)
                {
                    return size;
                }

                return rect.sizeDelta;
            }
            catch
            {
                return Vector2.zero;
            }
        }

        internal static bool TryReadCustomLayoutParams(LayoutGroup group, out Vector2 spacing, out RectOffset padding)
        {
            spacing = new Vector2(10f, 10f);
            padding = null;

            try
            {
                var type = group.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                // EFT 的 UnityEngine.UI.Extensions.FlowLayoutGroup 用两个 float 字段：
                // SpacingX / SpacingY（不是 Vector2）。优先读它们。
                var spacingX = TryReadFloat(type, group, "SpacingX,_spacingX,spacingX,m_SpacingX");
                var spacingY = TryReadFloat(type, group, "SpacingY,_spacingY,spacingY,m_SpacingY");
                if (spacingX.HasValue || spacingY.HasValue)
                {
                    spacing = new Vector2(
                        spacingX.HasValue ? Mathf.Max(0f, spacingX.Value) : 10f,
                        spacingY.HasValue ? Mathf.Max(0f, spacingY.Value) : 10f);
                }
                else
                {
                    const string spacingFieldNames = "m_Spacing,_spacing,spacing,Spacing";
                    var vector = TryReadVector2(type, group, spacingFieldNames, flags);
                    if (vector.HasValue && vector.Value.magnitude < 500f)
                    {
                        spacing = vector.Value;
                    }
                }

                const string paddingFieldNames = "m_Padding,_padding,padding,Padding";
                var offset = TryReadObject<RectOffset>(type, group, paddingFieldNames, flags);
                if (offset != null)
                {
                    padding = offset;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static T TryReadObject<T>(Type type, object instance, string names, BindingFlags flags) where T : class
        {
            try
            {
                foreach (var name in names.Split(','))
                {
                    var field = type.GetField(name, flags);
                    if (field != null && typeof(T).IsAssignableFrom(field.FieldType))
                    {
                        return field.GetValue(instance) as T;
                    }

                    var property = type.GetProperty(name, flags);
                    if (property != null && property.CanRead && typeof(T).IsAssignableFrom(property.PropertyType))
                    {
                        return property.GetValue(instance, null) as T;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static Vector2? TryReadVector2(Type type, object instance, string names, BindingFlags flags)
        {
            try
            {
                foreach (var name in names.Split(','))
                {
                    var field = type.GetField(name, flags);
                    if (field != null && field.FieldType == typeof(Vector2))
                    {
                        return (Vector2)field.GetValue(instance);
                    }

                    var property = type.GetProperty(name, flags);
                    if (property != null && property.CanRead && property.PropertyType == typeof(Vector2))
                    {
                        return (Vector2)property.GetValue(instance, null);
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static float? TryReadFloat(Type type, object instance, string names)
        {
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var name in names.Split(','))
                {
                    var field = type.GetField(name, flags);
                    if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
                    {
                        return Convert.ToSingle(field.GetValue(instance));
                    }

                    var property = type.GetProperty(name, flags);
                    if (property != null && property.CanRead
                        && (property.PropertyType == typeof(float) || property.PropertyType == typeof(int)))
                    {
                        return Convert.ToSingle(property.GetValue(instance, null));
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        // ---------------------------------------------------------------- 滚动结构

        internal static ScrollRect FindScrollInAncestors(RectTransform content)
        {
            try
            {
                if (content == null)
                {
                    return null;
                }

                var current = content;
                for (var i = 0; i < 10 && current != null; i++)
                {
                    var scroll = current.GetComponent<ScrollRect>();
                    if (scroll != null)
                    {
                        return scroll;
                    }

                    current = current.parent as RectTransform;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("查找已有 ScrollRect", ex);
            }

            return null;
        }

        internal static void ForceRebuild(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            try
            {
                LayoutRebuilder.MarkLayoutForRebuild(rect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
            catch (Exception ex)
            {
                Log.Exception("重建布局", ex);
            }
        }

        internal static void MarkForRebuild(RectTransform rect)
        {
            try
            {
                if (rect != null)
                {
                    LayoutRebuilder.MarkLayoutForRebuild(rect);
                }
            }
            catch
            {
                // ignore
            }
        }

        internal static string ComponentsOf(GameObject gameObject, int maxLength = 220)
        {
            if (gameObject == null)
            {
                return "<null>";
            }

            try
            {
                var components = gameObject.GetComponents<Component>();
                var builder = new StringBuilder();
                var count = 0;
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        continue;
                    }

                    if (count++ > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(component.GetType().Name);
                    if (builder.Length > maxLength)
                    {
                        builder.Append("...");
                        break;
                    }
                }

                return builder.ToString();
            }
            catch
            {
                return "<error>";
            }
        }

        internal static string RectOf(RectTransform rect)
        {
            if (rect == null)
            {
                return "<null>";
            }

            try
            {
                var size = rect.rect.size;
                return "size=[" + size.x.ToString("0.##") + "," + size.y.ToString("0.##") + "]"
                       + " aMin=[" + rect.anchorMin.x.ToString("0.##") + "," + rect.anchorMin.y.ToString("0.##") + "]"
                       + " aMax=[" + rect.anchorMax.x.ToString("0.##") + "," + rect.anchorMax.y.ToString("0.##") + "]"
                       + " pivot=[" + rect.pivot.x.ToString("0.##") + "," + rect.pivot.y.ToString("0.##") + "]"
                       + " pos=[" + rect.anchoredPosition.x.ToString("0.##") + "," + rect.anchoredPosition.y.ToString("0.##") + "]"
                       + " sizeDelta=[" + rect.sizeDelta.x.ToString("0.##") + "," + rect.sizeDelta.y.ToString("0.##") + "]";
            }
            catch
            {
                return "<error>";
            }
        }

        // ---------------------------------------------------------------- 世界坐标 / 参考像素

        /// <summary>RectTransform 四个角点的最小世界 x。</summary>
        internal static float MinWorldX(RectTransform rect)
        {
            if (rect == null)
            {
                return 0f;
            }

            try
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                return Mathf.Min(Mathf.Min(corners[0].x, corners[1].x), Mathf.Min(corners[2].x, corners[3].x));
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>RectTransform 四个角点的最大世界 x。</summary>
        internal static float MaxWorldX(RectTransform rect)
        {
            if (rect == null)
            {
                return 0f;
            }

            try
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                return Mathf.Max(Mathf.Max(corners[0].x, corners[1].x), Mathf.Max(corners[2].x, corners[3].x));
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// 当前 Canvas 的缩放系数（世界像素 / 参考分辨率像素）。
        /// 2560x1440 下通常约 1.333；1920x1080 下为 1。
        /// </summary>
        internal static float CanvasScale(Component context)
        {
            try
            {
                if (context != null)
                {
                    var canvas = context.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.scaleFactor > 0.01f)
                    {
                        return canvas.scaleFactor;
                    }
                }
            }
            catch
            {
            }

            return 1f;
        }

        /// <summary>
        /// 计算 target 四角在 space 的本地坐标系中的 x 范围（用于测量卡片在 content 里的位置/末端）。
        /// 返回 false 表示测量失败。
        /// </summary>
        internal static bool TryGetLocalXRange(RectTransform target, RectTransform space, out float minX, out float maxX)
        {
            minX = 0f;
            maxX = 0f;
            if (target == null || space == null)
            {
                return false;
            }

            try
            {
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                var a = space.InverseTransformPoint(corners[0]);
                var b = space.InverseTransformPoint(corners[2]);
                minX = Mathf.Min(a.x, b.x);
                maxX = Mathf.Max(a.x, b.x);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
