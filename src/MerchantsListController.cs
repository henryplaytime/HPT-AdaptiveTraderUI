using System;
using System.Collections;
using System.Reflection;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KaenoAdaptiveTraderUI
{
    /// <summary>
    /// 界面B：TradingScreen 的 Merchants List（商人选择列表）——旧 AdjustableTraderRows 真正生效的界面。
    ///
    /// 实测层级（SPT 4.1.6，ui-recon/Lead 字节级解析）：
    ///   Merchants List[485]（MerchantsList, CanvasGroup, Canvas, GraphicRaycaster；1920 宽、全屏高）
    ///     └ Traders Container[611]（ContentSizeFitter(水平/垂直=PreferredSize) + UnityEngine.UI.Extensions.FlowLayoutGroup,
    ///        850x480 居中）—— 原版一行 5 个：floor((850+10)/(160+10))=5
    ///   TraderPanel 模板：RectTransform 160x205，LayoutElement preferredWidth=160/preferredHeight=205；
    ///   FlowLayoutGroup 间距字段为 SpacingX/SpacingY（float，不是 Vector2），fallback 10。
    ///
    /// 模式（[Layout] Merchants Layout Mode）：
    ///   Wrap（默认）：保留 FlowLayoutGroup + ContentSizeFitter，只改容器宽 = N*(cardW+spacing)-spacing，
    ///                 FlowLayoutGroup 自动换行、ContentSizeFitter 自动增高；内容高度超出视口时加纵向滚动。
    ///   Horizontal：所有卡片排成一行、容器拉宽到总宽，超出屏幕宽度时加横向滚轮滚动。
    ///   None：恢复原版快照，完全不动。
    ///
    /// 加滚动时在 Merchants List 下新建 viewport 节点（铺满、加 RectMask2D），容器移入其下；
    /// 不直接给带 Canvas 的 Merchants List 加 RectMask2D（会把 TraderTooltip 一起裁掉，且嵌套 Canvas 裁剪不稳定）。
    /// </summary>
    internal sealed class MerchantsListController : MonoBehaviour
    {
        private const string ViewportObjectName = "KaenoMerchantsViewport";
        private const float FallbackCardWidth = 177.25f;
        private const float FallbackCardHeight = 205f;
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _reflectionResolved;
        private static FieldInfo _tradersContainerField;
        private static FieldInfo _traderPanelTemplateField;

        private MerchantsList _list;
        private RectTransform _container;
        private RectTransform _listRect;
        private string _resolveSource = "?";

        private RectTransformSnapshot _containerSnapshot;
        private bool _snapshotValid;
        private LayoutGroup _flow;
        private ContentSizeFitter _fitter;
        private ContentSizeFitter.FitMode _origFitH;
        private ContentSizeFitter.FitMode _origFitV;
        private bool _fitterCaptured;

        private GameObject _viewportGo;
        private RectTransform _viewportRt;
        private ScrollRect _scroll;

        private bool _needsApply;
        private bool _dumpPending;
        private bool _warnedNoCards;
        private Coroutine _delayedApply;
        private string _lastSummary = "none";

        private Vector2 _lastCardSize;
        private Vector2 _lastSpacing;
        private RectOffset _lastPadding;
        private int _lastCount;
        private int _lastCols;
        private int _lastRows;
        private float _lastContainerWidth;
        private float _lastContentHeight;
        private Vector2 _lastVisibleSize;

        // ------------------------------------------------------------------ 静态入口

        internal static void OnMerchantsListShown(MerchantsList list)
        {
            try
            {
                if (list == null)
                {
                    Log.Warn("界面B（Merchants List）：MerchantsList 实例为空，跳过。");
                    return;
                }

                ResolveReflection();
                var container = ResolveContainer(list, out var source);
                if (container == null)
                {
                    Log.Warn("界面B（Merchants List）：找不到 Traders Container（_tradersContainer 字段与按名字查找均失败），保持原版布局。");
                    return;
                }

                var controller = list.gameObject.GetComponent<MerchantsListController>();
                if (controller == null)
                {
                    controller = list.gameObject.AddComponent<MerchantsListController>();
                }

                controller.Configure(list, container, source);
                controller.RequestApply("Show");
            }
            catch (Exception ex)
            {
                Log.Exception("界面B（Merchants List）初始化", ex);
            }
        }

        private static void ResolveReflection()
        {
            if (_reflectionResolved)
            {
                return;
            }

            _reflectionResolved = true;
            try
            {
                _tradersContainerField = FindField(typeof(MerchantsList), "_tradersContainer");
                _traderPanelTemplateField = FindField(typeof(MerchantsList), "_traderPanelTemplate");
                Log.Debug("界面B 反射字段: tradersContainer=" + (_tradersContainerField != null)
                          + " traderPanelTemplate=" + (_traderPanelTemplateField != null));
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 反射解析", ex);
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            var current = type;
            while (current != null)
            {
                var field = current.GetField(name, FieldFlags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                current = current.BaseType;
            }

            return null;
        }

        private static RectTransform ResolveContainer(MerchantsList list, out string source)
        {
            source = "none";

            try
            {
                if (_tradersContainerField != null)
                {
                    if (_tradersContainerField.GetValue(list) is RectTransform rect)
                    {
                        source = "field _tradersContainer";
                        return rect;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 读取 _tradersContainer", ex);
            }

            try
            {
                var found = UILayoutTools.FindDescendant(list.transform, "Traders Container", 6);
                if (found != null)
                {
                    source = "child lookup";
                    return found;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 在界面内查找 Traders Container", ex);
            }

            try
            {
                var found = GameObject.Find("Traders Container");
                if (found != null)
                {
                    source = "GameObject.Find";
                    return found.transform as RectTransform;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面B GameObject.Find", ex);
            }

            return null;
        }

        // ------------------------------------------------------------------ 生命周期

        private void Configure(MerchantsList list, RectTransform container, string source)
        {
            _list = list;
            _container = container;
            _resolveSource = source;
            _listRect = list.transform as RectTransform;
            _dumpPending = ModConfig.DiagnosticDumpEnabled;

            if (!_snapshotValid)
            {
                _containerSnapshot.Capture(_container);
                _snapshotValid = true;
            }

            try
            {
                _flow = UILayoutTools.FindCustomLayoutGroup(_container);
                _fitter = UILayoutTools.FindFitter(_container);

                if (!_fitterCaptured && _fitter != null)
                {
                    _fitterCaptured = true;
                    _origFitH = _fitter.horizontalFit;
                    _origFitV = _fitter.verticalFit;
                }

                Log.Debug("界面B 组件: layoutGroup=" + (_flow == null ? "<none>" : _flow.GetType().FullName)
                          + " fitter=" + (_fitter == null ? "<none>" : (_fitter.horizontalFit + "/" + _fitter.verticalFit))
                          + " container=" + UILayoutTools.GetPath(_container));
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 读取布局组件", ex);
            }
        }

        private void OnEnable()
        {
            try
            {
                ModConfig.LayoutSettingsChanged += OnLayoutSettingsChanged;
                ModConfig.ScrollSpeedChanged += OnScrollSpeedChanged;
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 订阅配置事件", ex);
            }

            if (_needsApply)
            {
                ApplyNow();
            }
        }

        private void OnDisable()
        {
            try
            {
                ModConfig.LayoutSettingsChanged -= OnLayoutSettingsChanged;
                ModConfig.ScrollSpeedChanged -= OnScrollSpeedChanged;
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 取消配置事件", ex);
            }

            if (_delayedApply != null)
            {
                try { StopCoroutine(_delayedApply); } catch { }
                _delayedApply = null;
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (_viewportGo != null)
                {
                    UnityEngine.Object.Destroy(_viewportGo);
                    _viewportGo = null;
                    _viewportRt = null;
                    _scroll = null;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 销毁 viewport", ex);
            }
        }

        private void OnLayoutSettingsChanged()
        {
            try
            {
                RequestApply("config-change");
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 配置热更新", ex);
            }
        }

        private void OnScrollSpeedChanged()
        {
            try
            {
                if (_scroll != null)
                {
                    _scroll.scrollSensitivity = ModConfig.WheelSpeed;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 更新滚轮速度", ex);
            }
        }

        // ------------------------------------------------------------------ 应用

        private void RequestApply(string reason)
        {
            _needsApply = true;
            if (!isActiveAndEnabled)
            {
                ApplyNow();
                return;
            }

            if (_delayedApply != null)
            {
                return;
            }

            try
            {
                _delayedApply = StartCoroutine(DelayedApply());
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 启动延迟应用协程失败", ex);
                ApplyNow();
            }
        }

        private IEnumerator DelayedApply()
        {
            // postfix 时 ViewList 是否已生成完子节点不确定：等一帧 + 强制更新 Canvas 后再测量/应用。
            yield return null;
            _delayedApply = null;

            try { Canvas.ForceUpdateCanvases(); } catch { }
            try { UILayoutTools.ForceRebuild(_container); } catch { }

            ApplyNow();
        }

        private void ApplyNow()
        {
            _needsApply = false;

            try
            {
                ApplyCore();
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 布局应用", ex);
            }

            if (_dumpPending)
            {
                _dumpPending = false;
                try
                {
                    DumpDiagnostics();
                }
                catch (Exception ex)
                {
                    Log.Exception("界面B 诊断 dump", ex);
                }
            }
        }

        private void ApplyCore()
        {
            if (_list == null || _container == null)
            {
                return;
            }

            var mode = ModConfig.MerchantsMode;
            if (mode == MerchantsLayoutMode.None)
            {
                RestoreAll();
                _lastSummary = "mode=None";
                Log.Info("mode=None container=TradersContainer applied(vanilla)");
                return;
            }

            var cards = _container.childCount;
            if (cards <= 0)
            {
                if (!_warnedNoCards)
                {
                    _warnedNoCards = true;
                    Log.Warn("界面B（Merchants List）：Traders Container 下暂时没有卡片（cards=0），本次跳过；若反复出现请提供 Diagnostic Dump。");
                }

                return;
            }

            _warnedNoCards = false;

            if (_flow == null)
            {
                Log.Warn("界面B（Merchants List）：Traders Container 上未找到 FlowLayoutGroup/LayoutGroup，无法安全调整列数，保持原版布局。");
                _lastSummary = "mode=" + mode + " skipped(no-layout-group)";
                return;
            }

            var template = GetTemplate();
            var metrics = UILayoutTools.Measure(_container, null, template);
            var card = metrics.CardSize;
            var spacing = metrics.Spacing;
            var padding = metrics.Padding ?? new RectOffset();
            var perRow = Mathf.Clamp(ModConfig.PerRow, 1, 20);
            var visible = GetVisibleSize();

            int cols;
            int rows;
            float containerWidth;
            float contentHeight;

            if (mode == MerchantsLayoutMode.Wrap)
            {
                cols = Mathf.Min(perRow, cards);

                var maxColsByWidth = Mathf.Max(1, Mathf.FloorToInt(
                    (visible.x - padding.left - padding.right + spacing.x - 1f) / Mathf.Max(1f, card.x + spacing.x)));
                if (cols > maxColsByWidth)
                {
                    Log.Debug("界面B Wrap 列数被屏幕宽度限制: " + cols + " -> " + maxColsByWidth);
                    cols = maxColsByWidth;
                }

                rows = Mathf.CeilToInt(cards / (float)cols);
                containerWidth = Mathf.Max(1f, cols * (card.x + spacing.x) - spacing.x
                                                  + padding.left + padding.right);
                contentHeight = padding.top + padding.bottom + rows * card.y + Mathf.Max(0, rows - 1) * spacing.y;
            }
            else
            {
                cols = cards;
                rows = 1;
                containerWidth = Mathf.Max(1f, cards * (card.x + spacing.x) - spacing.x
                                                  + padding.left + padding.right);
                contentHeight = padding.top + padding.bottom + card.y;
            }

            _lastCardSize = card;
            _lastSpacing = spacing;
            _lastPadding = padding;
            _lastCount = cards;
            _lastCols = cols;
            _lastRows = rows;
            _lastContainerWidth = containerWidth;
            _lastContentHeight = contentHeight;
            _lastVisibleSize = visible;

            var needVerticalScroll = false;
            var needHorizontalScroll = false;

            if (mode == MerchantsLayoutMode.Wrap)
            {
                ApplyWrapLayout(containerWidth, contentHeight);
                needVerticalScroll = contentHeight > visible.y - 24f;
                if (needVerticalScroll)
                {
                    EnsureScroll(true, containerWidth, contentHeight);
                }
                else
                {
                    RestoreScroll("wrap-fit");
                }
            }
            else
            {
                ApplyHorizontalLayout(containerWidth, contentHeight);
                needHorizontalScroll = containerWidth > visible.x - 24f;
                if (needHorizontalScroll)
                {
                    EnsureScroll(false, containerWidth, contentHeight);
                }
                else
                {
                    RestoreScroll("horizontal-fit");
                }
            }

            _lastSummary = "mode=" + mode
                           + " cards=" + cards
                           + " perRow=" + (mode == MerchantsLayoutMode.Wrap ? cols : cards)
                           + " rows=" + rows
                           + " card=" + card.x.ToString("0.##") + "x" + card.y.ToString("0.##")
                           + " spacing=" + spacing.x.ToString("0.##") + "," + spacing.y.ToString("0.##")
                           + " containerWidth=" + containerWidth.ToString("0.##")
                           + " contentHeight=" + contentHeight.ToString("0.##")
                           + " visible=" + visible.x.ToString("0.##") + "x" + visible.y.ToString("0.##")
                           + (needVerticalScroll ? " scrollV=true" : needHorizontalScroll ? " scrollH=true" : " scroll=none");

            Log.Info(_lastSummary + " container=TradersContainer source=" + _resolveSource
                     + " viewport=" + (_viewportGo == null ? "-" : ViewportObjectName) + " applied");
        }

        private Component GetTemplate()
        {
            try
            {
                if (_traderPanelTemplateField != null && _list != null)
                {
                    return _traderPanelTemplateField.GetValue(_list) as Component;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("读取 _traderPanelTemplate 失败（忽略）: " + ex.Message);
            }

            return null;
        }

        private Vector2 GetVisibleSize()
        {
            try
            {
                if (_listRect != null)
                {
                    var size = _listRect.rect.size;
                    if (size.x > 100f && size.y > 100f)
                    {
                        return size;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("读取 Merchants List 尺寸失败: " + ex.Message);
            }

            return new Vector2(Mathf.Max(Screen.width, 1280), Mathf.Max(Screen.height, 720));
        }

        // ------------------------------------------------------------------ 布局

        private void ApplyWrapLayout(float containerWidth, float contentHeight)
        {
            try
            {
                if (_fitter != null)
                {
                    // 宽度由我们控制；高度继续交给 ContentSizeFitter 自动增高。
                    _fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                }

                if (_viewportGo == null && _snapshotValid)
                {
                    // 不滚动：恢复原锚点/位置（居中），只改宽度。
                    _containerSnapshot.Restore(_container);
                    var height = _container.sizeDelta.y;
                    if (_fitter == null || _fitter.verticalFit == ContentSizeFitter.FitMode.Unconstrained)
                    {
                        height = contentHeight;
                    }

                    _container.sizeDelta = new Vector2(containerWidth, height);
                }
                else
                {
                    _container.sizeDelta = new Vector2(containerWidth, _container.sizeDelta.y);
                    if (_fitter == null || _fitter.verticalFit == ContentSizeFitter.FitMode.Unconstrained)
                    {
                        _container.sizeDelta = new Vector2(containerWidth, contentHeight);
                    }
                }

                UILayoutTools.MarkForRebuild(_container);
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 应用 Wrap 布局", ex);
            }
        }

        private void ApplyHorizontalLayout(float containerWidth, float contentHeight)
        {
            try
            {
                if (_fitter != null)
                {
                    _fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    _fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }

                if (_viewportGo == null && _snapshotValid)
                {
                    _containerSnapshot.Restore(_container);
                }

                _container.sizeDelta = new Vector2(containerWidth, contentHeight);
                UILayoutTools.MarkForRebuild(_container);
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 应用 Horizontal 布局", ex);
            }
        }

        // ------------------------------------------------------------------ 滚动结构

        private bool EnsureScroll(bool vertical, float containerWidth, float contentHeight)
        {
            try
            {
                if (_viewportGo == null)
                {
                    var parent = _containerSnapshot.Parent != null ? _containerSnapshot.Parent : _container.parent;
                    if (parent == null)
                    {
                        Log.Warn("界面B：Traders Container 没有父节点，无法创建 viewport。");
                        return false;
                    }

                    var wrapper = new GameObject(ViewportObjectName, typeof(RectTransform));
                    var rect = (RectTransform)wrapper.transform;
                    rect.SetParent(parent, false);
                    var initialVisible = GetVisibleSize();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(
                        Mathf.Max(200f, Mathf.Min(containerWidth + 2f, Mathf.Max(200f, initialVisible.x - 48f))),
                        Mathf.Max(200f, Mathf.Min(contentHeight + 2f, Mathf.Max(200f, initialVisible.y - 48f))));
                    try
                    {
                        if (_containerSnapshot.SiblingIndex >= 0)
                        {
                            rect.SetSiblingIndex(Mathf.Min(_containerSnapshot.SiblingIndex, parent.childCount - 1));
                        }
                    }
                    catch { }

                    if (rect.GetComponent<RectMask2D>() == null && rect.GetComponent<Mask>() == null)
                    {
                        wrapper.AddComponent<RectMask2D>();
                    }

                    var scroll = wrapper.AddComponent<ScrollRect>();
                    scroll.viewport = rect;
                    scroll.content = _container;
                    scroll.inertia = true;
                    scroll.movementType = ScrollRect.MovementType.Clamped;
                    scroll.scrollSensitivity = ModConfig.WheelSpeed;

                    _viewportGo = wrapper;
                    _viewportRt = rect;
                    _scroll = scroll;

                    _container.SetParent(rect, false);
                    Log.Debug("界面B 已创建 viewport=" + ViewportObjectName + " vertical=" + vertical);
                }

                if (_scroll != null)
                {
                    _scroll.horizontal = !vertical;
                    _scroll.vertical = vertical;
                    _scroll.movementType = ScrollRect.MovementType.Clamped;
                    _scroll.scrollSensitivity = ModConfig.WheelSpeed;
                    _scroll.enabled = true;
                }

                if (_fitter != null)
                {
                    // 关键修复：滚动时内容高度必须由我们精确控制（= FlowLayoutGroup 实际排版高度），
                    // 否则原 ContentSizeFitter(PreferredSize) 会把高度改回旧值，ScrollRect 可滚范围≈0 → 滚轮弹回。
                    _fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    _fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }

                var visible = GetVisibleSize();
                var viewportWidth = Mathf.Max(200f, Mathf.Min(containerWidth + 2f, visible.x - 48f));
                var viewportHeight = Mathf.Max(200f, Mathf.Min(contentHeight + 2f, visible.y - 48f));
                if (_viewportRt != null)
                {
                    _viewportRt.anchorMin = new Vector2(0.5f, 0.5f);
                    _viewportRt.anchorMax = new Vector2(0.5f, 0.5f);
                    _viewportRt.pivot = new Vector2(0.5f, 0.5f);
                    _viewportRt.anchoredPosition = Vector2.zero;
                    _viewportRt.sizeDelta = new Vector2(viewportWidth, viewportHeight);
                }

                _container.anchorMin = new Vector2(0.5f, 1f);
                _container.anchorMax = new Vector2(0.5f, 1f);
                _container.pivot = new Vector2(0.5f, 1f);
                _container.anchoredPosition = Vector2.zero;
                _container.sizeDelta = new Vector2(Mathf.Max(containerWidth, 1f), Mathf.Max(contentHeight, 1f));

                UILayoutTools.MarkForRebuild(_container);
                UILayoutTools.ForceRebuild(_viewportRt);
                UILayoutTools.ForceRebuild(_container);

                try
                {
                    if (_scroll != null)
                    {
                        _scroll.horizontal = containerWidth > viewportWidth + 0.5f;
                        _scroll.vertical = contentHeight > viewportHeight + 0.5f;
                        _scroll.scrollSensitivity = ModConfig.WheelSpeed;
                        _scroll.enabled = true;
                        if (_scroll.vertical)
                        {
                            _scroll.verticalNormalizedPosition = 1f;
                        }

                        if (_scroll.horizontal)
                        {
                            _scroll.horizontalNormalizedPosition = 0f;
                        }
                    }
                }
                catch { }

                Log.Info("界面B 滚动已就绪: cards=" + _lastCount + " cols=" + _lastCols
                         + " contentH=" + contentHeight.ToString("0.##")
                         + " viewport=" + viewportWidth.ToString("0.##") + "x" + viewportHeight.ToString("0.##")
                         + " v=" + (contentHeight > viewportHeight + 0.5f)
                         + " h=" + (containerWidth > viewportWidth + 0.5f));

                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 创建滚动结构", ex);
                return false;
            }
        }

        private void RestoreScroll(string reason)
        {
            try
            {
                if (_viewportGo == null)
                {
                    return;
                }

                if (_scroll != null)
                {
                    _scroll.enabled = false;
                }

                if (_snapshotValid && _containerSnapshot.Parent != null)
                {
                    _container.SetParent(_containerSnapshot.Parent, false);
                    _containerSnapshot.Restore(_container);
                }

                UnityEngine.Object.Destroy(_viewportGo);
                _viewportGo = null;
                _viewportRt = null;
                _scroll = null;
                Log.Debug("界面B 已移除 viewport（原因: " + reason + "）");
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 移除滚动结构", ex);
            }
        }

        private void RestoreAll()
        {
            try
            {
                RestoreScroll("mode-none");
                if (_snapshotValid && _container != null)
                {
                    _containerSnapshot.Restore(_container);
                }

                if (_fitter != null && _fitterCaptured)
                {
                    _fitter.horizontalFit = _origFitH;
                    _fitter.verticalFit = _origFitV;
                }

                UILayoutTools.MarkForRebuild(_container);
            }
            catch (Exception ex)
            {
                Log.Exception("界面B 完全还原", ex);
            }
        }

        // ------------------------------------------------------------------ 诊断

        private void DumpDiagnostics()
        {
            try
            {
                DiagnosticDumper.DumpMerchantsList(
                    _list,
                    _container,
                    _viewportRt,
                    _scroll,
                    _lastCount,
                    _lastCardSize,
                    _lastSpacing,
                    _lastPadding,
                    _lastCols,
                    _lastRows,
                    _lastContainerWidth,
                    _lastContentHeight,
                    _lastVisibleSize,
                    _lastSummary,
                    _resolveSource,
                    _flow);
            }
            catch (Exception ex)
            {
                Log.Exception("界面B dump", ex);
            }
        }
    }
}
