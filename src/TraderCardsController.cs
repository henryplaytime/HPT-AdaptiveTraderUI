using System;
using System.Collections;
using System.Reflection;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KaenoAdaptiveTraderUI
{
    /// <summary>
    /// 界面A：TraderScreensGroup 顶栏商人卡片条（TraderCards，Kaeno 目标）。
    ///
    /// 实测层级（SPT 4.1.6，ui-recon 字节级解析）：
    ///   TopPanel[803] 1920x171 → Container[5604] 全拉伸 1920x171（无 Mask/ScrollRect）
    ///     → TraderCards[51]：HorizontalLayoutGroup(padding=0, childAlignment=MiddleLeft, spacing=5,
    ///        childControlWidth/Height=true, childForceExpand=false)，容器 1105x172，aMin/aMax=(0,1) pivot=(0,0) pos=(8,-171)
    ///     → Right Person[976]：TradingPlayerPanel 650 宽（x≥1270，会遮住溢出卡片）
    ///   卡片 prefab "Trader Card"：RectTransform 120x172，LayoutElement preferredWidth=118/preferredHeight=168。
    ///
    /// 做法：
    ///   1. 卡片总宽 = 卡片数*cardWidth + (卡片数-1)*spacing + 左右各 8px（实测公式，弃用 Kaeno 的 anchor 递推）。
    ///   2. 放得下（总宽 <= 可用宽 ~1262）时完全不动，保持原版。
    ///   3. 放不下时在 Container 下新建 viewport 节点（全拉伸、offsetMax.x 裁到 Right Person 左侧 + RectMask2D），
    ///      把 TraderCards 移入并拉宽到总宽，加 ScrollRect(horizontal only)；滚轮由 Unity 2022.3 原生
    ///      ScrollRect.OnScroll 在 horizontal && !vertical 时自动把纵向滚轮映射为横向。
    ///   4. 若 viewport 创建失败，回退到旧 Kaeno 已验证写法（ScrollRect 直接加在 TraderCards 上、viewport=Container、无遮罩，
    ///      依赖 Right Person 不透明面板遮挡溢出卡片）。
    /// </summary>
    internal sealed partial class TraderCardsController : MonoBehaviour
    {
        private const string ViewportObjectName = "KaenoAdaptiveViewport";
        private const float SideMargins = 16f;      // 左右各 8px，来自实测公式
        private const float DefaultRightReserved = 650f; // Right Person 宽度 fallback
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool _reflectionResolved;
        private static FieldInfo _containerField;
        private static FieldInfo _spawnedCardsField;
        private static FieldInfo _cardPrefabField;

        private TraderScreensGroup _screen;
        private RectTransform _content;
        private Component _cardPrefab;
        private string _containerResolveSource = "?";

        private RectTransformSnapshot _contentSnapshot;
        private bool _snapshotValid;
        private Vector2 _baselineSize;
        private float _leftInset = 8f;

        private GameObject _viewportGo;
        private RectTransform _viewportRt;
        private ScrollRect _scroll;
        private bool _kaenoFallback;

        // v4.2.4：Kaeno 回退方案专用的“可见区 viewport”（Container 的子节点，无 Graphic、无 Mask，
        // 只用于给 ScrollRect 提供正确宽度的滚动边界，避免用整条 Container(1920) 当 viewport 导致
        // 最右端最后 5~6 张卡停在 Right Person 面板后面）。
        private const string KaenoViewportObjectName = "KaenoTraderCardsViewport";
        private GameObject _kaenoViewportGo;
        private RectTransform _kaenoViewportRt;

        private bool _needsApply;
        private bool _dumpPending;
        private bool _warnedNoCards;
        private Coroutine _delayedApply;
        private string _lastAppliedSummary = "none";

        private RectTransform _rightPerson;
        private bool _rightPersonResolved;

        // v4.2.4：内容宽度/可见宽度诊断量（也用于日志）
        private int _lastActiveCards;
        private float _lastFormulaWidth;

        // v4.2.4：配置变更重应用时保留滚动位置（避免“改设置→卡条跳回开头”）
        private float _preservedNormalized = -1f;

        // ------------------------------------------------------------------ 静态入口

        internal static void OnTraderScreenShown(TraderScreensGroup screen)
        {
            try
            {
                if (screen == null)
                {
                    Log.Warn("界面A（TraderCards）：TraderScreensGroup 实例为空，跳过。");
                    return;
                }

                ResolveReflection();
                var container = ResolveContainer(screen, out var source);
                if (container == null)
                {
                    Log.Warn("界面A（TraderCards）：找不到 TraderCards 容器（_traderCardsContainer 字段与 GameObject.Find 均失败），保持原版布局。");
                    return;
                }

                var controller = container.GetComponent<TraderCardsController>();
                if (controller == null)
                {
                    controller = container.gameObject.AddComponent<TraderCardsController>();
                }

                controller.Configure(screen, container, source);
                controller.RequestApply("Show");
            }
            catch (Exception ex)
            {
                Log.Exception("界面A（TraderCards）初始化", ex);
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
                var type = typeof(TraderScreensGroup);
                _containerField = FindField(type, "_traderCardsContainer");
                _spawnedCardsField = FindField(type, "_spawnedCards");
                _cardPrefabField = FindField(type, "_traderCardPrefab");
                Log.Debug("界面A 反射字段: container=" + (_containerField != null)
                          + " spawnedCards=" + (_spawnedCardsField != null)
                          + " cardPrefab=" + (_cardPrefabField != null));
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 反射解析", ex);
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

        private static RectTransform ResolveContainer(TraderScreensGroup screen, out string source)
        {
            source = "none";
            try
            {
                if (_containerField != null)
                {
                    if (_containerField.GetValue(screen) is Transform transform && transform is RectTransform rect)
                    {
                        source = "field _traderCardsContainer";
                        return rect;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 读取 _traderCardsContainer", ex);
            }

            try
            {
                var containers = screen.GetComponentsInChildren<Transform>(true);
                foreach (var candidate in containers)
                {
                    if (candidate != null && candidate.name == "TraderCards")
                    {
                        source = "GetComponentsInChildren";
                        return candidate as RectTransform;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 在界面内查找 TraderCards", ex);
            }

            try
            {
                var found = GameObject.Find("TraderCards");
                if (found != null)
                {
                    source = "GameObject.Find";
                    return found.transform as RectTransform;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面A GameObject.Find", ex);
            }

            return null;
        }

        // ------------------------------------------------------------------ 生命周期

        private void Configure(TraderScreensGroup screen, RectTransform container, string source)
        {
            _screen = screen;
            _content = container;
            _containerResolveSource = source;
            _rightPersonResolved = false;
            _rightPerson = null;
            _dumpPending = ModConfig.DiagnosticDumpEnabled;

            if (!_snapshotValid)
            {
                _contentSnapshot.Capture(_content);
                _baselineSize = _content.rect.size;
                if (_baselineSize.x < 1f || _baselineSize.y < 1f)
                {
                    _baselineSize = _content.sizeDelta;
                }

                _leftInset = ComputeLeftInset();
                _snapshotValid = true;
                Log.Debug("界面A 基线: size=" + _baselineSize + " leftInset=" + _leftInset + " source=" + source);
            }

            try
            {
                _cardPrefab = _cardPrefabField == null ? null : _cardPrefabField.GetValue(_screen) as Component;
            }
            catch
            {
                _cardPrefab = null;
            }
        }

        private float ComputeLeftInset()
        {
            try
            {
                if (_content == null || _contentSnapshot.Parent == null)
                {
                    return 8f;
                }

                var contentCorners = new Vector3[4];
                _content.GetWorldCorners(contentCorners);
                var parentRt = _contentSnapshot.Parent as RectTransform;
                if (parentRt == null)
                {
                    return 8f;
                }

                var parentCorners = new Vector3[4];
                parentRt.GetWorldCorners(parentCorners);
                var insetWorld = Mathf.Min(contentCorners[0].x, contentCorners[1].x)
                                 - Mathf.Min(parentCorners[0].x, parentCorners[1].x);
                // v4.2.4：换算回参考分辨率单位（世界像素 / Canvas.scaleFactor），
                // 否则 2560x1440 下会得到 10.67 而不是 8，viewport 左边缘会错位。
                var scale = UILayoutTools.CanvasScale(_screen);
                return Mathf.Max(0f, insetWorld / Mathf.Max(scale, 0.01f));
            }
            catch
            {
                return 8f;
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
                Log.Exception("界面A 订阅配置事件", ex);
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
                Log.Exception("界面A 取消配置事件", ex);
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

                // v4.2.4：销毁滚动边界 viewport
                if (_kaenoViewportGo != null)
                {
                    UnityEngine.Object.Destroy(_kaenoViewportGo);
                    _kaenoViewportGo = null;
                    _kaenoViewportRt = null;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 销毁 viewport", ex);
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
                Log.Exception("界面A 配置热更新", ex);
            }
        }

        private void OnScrollSpeedChanged()
        {
            try
            {
                if (_scroll != null)
                {
                    _scroll.scrollSensitivity = ModConfig.WheelSpeed;
                    Log.Debug("界面A 滚轮速度已更新为 " + ModConfig.WheelSpeed);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 更新滚轮速度", ex);
            }
        }

        // ------------------------------------------------------------------ 应用

        private void RequestApply(string reason)
        {
            DiagNoteRequest(reason); // v4.2.3-DIAG：记录本次应用是谁触发的（Show / 配置变更 / ...）

            // v4.2.4：配置变更重应用时记住当前滚动位置，应用完再恢复，避免“改设置→卡条跳回开头”。
            // 重新打开界面（Show）不保留，保持原版从第一张卡开始的行为。
            if (reason == "config-change" && _scroll != null && _scroll.enabled && _kaenoFallback)
            {
                try
                {
                    _preservedNormalized = _scroll.horizontalNormalizedPosition;
                }
                catch
                {
                    _preservedNormalized = -1f;
                }
            }

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
                Log.Exception("界面A 启动延迟应用协程失败", ex);
                ApplyNow();
            }
        }

        private IEnumerator DelayedApply()
        {
            yield return null;
            _delayedApply = null;

            try { Canvas.ForceUpdateCanvases(); } catch { }
            try { UILayoutTools.ForceRebuild(_content); } catch { }

            ApplyNow();
        }

        private void ApplyNow()
        {
            _needsApply = false;
            DiagBeforeApply(); // v4.2.3-DIAG：记录应用前的滚动状态
            try
            {
                ApplyCore();
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 布局应用", ex);
            }

            ApplyPreservedScrollPosition(); // v4.2.4：配置变更重应用后恢复滚动位置（不跳回开头）

            DiagAfterApply(); // v4.2.3-DIAG：把完整几何/自检结论写入诊断文件

            if (_dumpPending)
            {
                _dumpPending = false;
                try
                {
                    DumpDiagnostics();
                }
                catch (Exception ex)
                {
                    Log.Exception("界面A 诊断 dump", ex);
                }
            }
        }

        private void ApplyCore()
        {
            if (_content == null || _screen == null)
            {
                return;
            }

            var cards = CountCards();
            _diagCards = cards;
            if (cards <= 0)
            {
                _diagMode = "no-cards";
                if (!_warnedNoCards)
                {
                    _warnedNoCards = true;
                    Log.Warn("界面A（TraderCards）：暂时没有商人卡片（cards=0），本次跳过；若反复出现请提供 Diagnostic Dump。");
                }

                return;
            }

            _warnedNoCards = false;

            if (!ModConfig.TraderStripScrollingEnabled)
            {
                _diagMode = "scrolling-disabled(vanilla-restore)";
                RestoreVanilla("scrolling-disabled");
                _lastAppliedSummary = "mode=None(scrolling disabled) cards=" + cards;
                Log.Info(_lastAppliedSummary + " container=TraderCards viewport=- applied(vanilla)");
                return;
            }

            var metrics = UILayoutTools.Measure(_content, null, _cardPrefab);
            var card = metrics.CardSize;
            var spacing = metrics.Spacing;
            var padding = metrics.Padding ?? new RectOffset();

            if (card.x < 1f)
            {
                _diagMode = "measure-failed";
                Log.Warn("界面A（TraderCards）：无法测量卡片宽度，保持原版布局。");
                return;
            }

            // v4.2.4：内容宽度改为“实际参与排版的卡片末端”，不再用 CountCards() 乘公式。
            // 旧实现里 cards = max(childCount, _spawnedCards.Count)，只要两者不一致（或多出不可见子节点），
            // 内容就会比卡片实际末端宽出一截，滚到底时表现为右侧一大段空白（用户反馈的“拉出一大段”）。
            var formulaWidth = padding.left + padding.right
                               + cards * card.x
                               + Mathf.Max(0, cards - 1) * Mathf.Max(0f, spacing.x)
                               + SideMargins;

            int activeChildren;
            var childrenExtent = ComputeChildrenExtent(card, spacing, padding, out activeChildren);
            if (childrenExtent < card.x)
            {
                childrenExtent = Mathf.Max(formulaWidth, card.x);
            }

            var totalWidth = childrenExtent;
            var usableWidth = ComputeVisibleWidth(card.x);
            _diagCardSize = card;
            _diagSpacing = spacing;
            _diagPadding = padding;
            _diagTotalWidth = totalWidth;
            _diagUsableWidth = usableWidth;
            _lastActiveCards = activeChildren;
            _lastFormulaWidth = formulaWidth;

            if (usableWidth >= totalWidth - 1f)
            {
                _diagMode = "fit(restore-vanilla)";
                RestoreVanilla("fit");
                _lastAppliedSummary = "mode=HorizontalScroll cards=" + cards
                                      + " activeCards=" + activeChildren
                                      + " cardWidth=" + card.x.ToString("0.##")
                                      + " spacing=" + spacing.x.ToString("0.##")
                                      + " totalWidth=" + totalWidth.ToString("0.##")
                                      + " formulaWidth=" + formulaWidth.ToString("0.##")
                                      + " visibleWidth=" + usableWidth.ToString("0.##")
                                      + " needsScroll=false";
                Log.Info(_lastAppliedSummary + " container=TraderCards viewport=- applied(fit, vanilla)");
                return;
            }

            // v4.2.4：继续用无遮罩 ScrollRect（不会改变卡片父级/锚点），但 viewport 改为
            // 只覆盖“卡片可见区”的隐藏 RectTransform（左=TraderCards 原始左边缘 8，右=Right Person 左边缘），
            // 这样可滚范围 = 内容宽 - 可见宽，滚到最右端时最后一张卡正好停在可见右边界。
            var created = AttachKaenoFallback(totalWidth, card);

            if (!created)
            {
                _diagMode = "attach-failed";
                Log.Warn("界面A（TraderCards）：创建 viewport/ScrollRect 失败，保持原版布局。");
                return;
            }

            _diagMode = "scroll(kaeno-fallback-viewport)";

            _lastAppliedSummary = "mode=HorizontalScroll cards=" + cards
                                  + " activeCards=" + activeChildren
                                  + " cardWidth=" + card.x.ToString("0.##")
                                  + " spacing=" + spacing.x.ToString("0.##")
                                  + " totalWidth=" + totalWidth.ToString("0.##")
                                  + " formulaWidth=" + formulaWidth.ToString("0.##")
                                  + " visibleWidth=" + usableWidth.ToString("0.##")
                                  + " wheelSpeed=" + ModConfig.WheelSpeed.ToString("0.#")
                                  + (_kaenoFallback ? " fallback=kaeno-viewport" : string.Empty);
            Log.Info(_lastAppliedSummary + " container=TraderCards source=" + _containerResolveSource
                     + " viewport=" + (_kaenoFallback ? KaenoViewportObjectName : ViewportObjectName) + " applied(scroll)");
        }

        private int CountCards()
        {
            var fromChildren = _content != null ? _content.childCount : 0;
            var fromList = 0;

            try
            {
                if (_spawnedCardsField != null && _screen != null)
                {
                    var value = _spawnedCardsField.GetValue(_screen) as IList;
                    if (value != null)
                    {
                        fromList = value.Count;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("读取 _spawnedCards 失败（忽略）: " + ex.Message);
            }

            return Mathf.Max(fromChildren, fromList);
        }

        /// <summary>
        /// v4.2.4：卡片可见区宽度（参考分辨率像素）= TraderCards 原始左边缘 → Right Person 左边缘。
        /// 与 content 当前是否已滚动无关（旧实现用 content 当前世界坐标，滚动后会被放大，导致 fit 误判）。
        /// </summary>
        private float ComputeVisibleWidth(float cardWidth)
        {
            var fallback = Mathf.Max(_baselineSize.x, cardWidth + 2f);

            try
            {
                ResolveRightPerson();
                var parent = _contentSnapshot.Parent as RectTransform;
                if (_rightPerson == null || parent == null)
                {
                    return fallback;
                }

                var scale = UILayoutTools.CanvasScale(_screen);
                var parentLeft = UILayoutTools.MinWorldX(parent);
                var rightPersonLeft = UILayoutTools.MinWorldX(_rightPerson);
                var containerWidth = parent.rect.width;
                var visible = (rightPersonLeft - parentLeft) / scale - _leftInset;

                if (visible < cardWidth * 1.5f || visible > containerWidth + 6000f)
                {
                    return fallback;
                }

                return Mathf.Max(cardWidth + 2f, visible);
            }
            catch (Exception ex)
            {
                Log.Debug("计算可见宽度失败，回退 baseline: " + ex.Message);
                return fallback;
            }
        }

        /// <summary>保留旧名字给诊断代码用（现在返回稳定的可见区宽度）。</summary>
        private float ComputeUsableWidth(float cardWidth)
        {
            return ComputeVisibleWidth(cardWidth);
        }

        /// <summary>
        /// v4.2.4：测量实际参与排版的卡片末端（content 本地坐标 = 参考像素）。
        /// HorizontalLayoutGroup 会跳过 inactive 子节点，因此这里只统计 activeInHierarchy 的卡片；
        /// 取“布局公式值”和“实际 rect 末端”的较大值，避免布局尚未刷新时算小。
        /// </summary>
        private float ComputeChildrenExtent(Vector2 card, Vector2 spacing, RectOffset padding, out int activeChildren)
        {
            activeChildren = 0;
            var actual = 0f;

            try
            {
                if (_content != null)
                {
                    for (var i = 0; i < _content.childCount; i++)
                    {
                        var child = _content.GetChild(i) as RectTransform;
                        if (child == null || !child.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        activeChildren++;
                        float minX;
                        float maxX;
                        if (UILayoutTools.TryGetLocalXRange(child, _content, out minX, out maxX))
                        {
                            actual = Mathf.Max(actual, maxX);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("测量卡片末端失败（使用布局公式兜底）: " + ex.Message);
            }

            var formula = 0f;
            if (activeChildren > 0)
            {
                formula = padding.left + padding.right
                          + activeChildren * card.x
                          + Mathf.Max(0, activeChildren - 1) * Mathf.Max(0f, spacing.x);
            }

            var extent = Mathf.Max(formula, actual);
            if (extent < 1f)
            {
                extent = Mathf.Max(_baselineSize.x, card.x);
            }

            return extent;
        }

        private void ResolveRightPerson()
        {
            // v4.2.4：只有真正找到时才认为“已解析”，否则下次调用继续重试
            // （旧实现一旦在层级尚未就绪时解析失败，之后永远不会再找）。
            if (_rightPersonResolved && _rightPerson != null)
            {
                return;
            }

            try
            {
                if (_content == null || _content.parent == null)
                {
                    return;
                }

                var parent = _content.parent;
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child != null && string.Equals(child.name, "Right Person", StringComparison.Ordinal))
                    {
                        _rightPerson = child as RectTransform;
                        _rightPersonResolved = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("查找 Right Person 失败: " + ex.Message);
            }
        }

        /// <summary>主方案：新建 viewport 节点（裁剪掉 Right Person 占用的右侧区域），TraderCards 移入并加滚动。</summary>
        private bool EnsureViewport(float totalWidth, Vector2 card)
        {
            if (_kaenoFallback)
            {
                return false;
            }

            if (_viewportGo != null)
            {
                ConfigureScrollForViewport(totalWidth, card);
                return true;
            }

            if (!_snapshotValid || _contentSnapshot.Parent == null)
            {
                return false;
            }

            try
            {
                var wrapper = new GameObject(ViewportObjectName, typeof(RectTransform));
                var rect = (RectTransform)wrapper.transform;
                rect.SetParent(_contentSnapshot.Parent, false);

                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = new Vector2(GetRightMargin(), 0f);

                if (rect.GetComponent<RectMask2D>() == null && rect.GetComponent<Mask>() == null)
                {
                    wrapper.AddComponent<RectMask2D>();
                }

                var scroll = wrapper.AddComponent<ScrollRect>();
                scroll.viewport = rect;
                scroll.content = _content;
                scroll.horizontal = true;
                scroll.vertical = false;
                scroll.inertia = true;
                scroll.movementType = ModConfig.Elastic ? ScrollRect.MovementType.Elastic : ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = ModConfig.WheelSpeed;

                _viewportGo = wrapper;
                _viewportRt = rect;
                _scroll = scroll;

                ConfigureScrollForViewport(totalWidth, card);
                Log.Debug("界面A 已创建 viewport=" + ViewportObjectName + " rightMargin=" + GetRightMargin());
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 创建 viewport", ex);
                return false;
            }
        }

        private float GetRightMargin()
        {
            // offsetMax.x = Right Person 左边缘 - Container 右边缘（负数，约 -650）。
            try
            {
                ResolveRightPerson();
                if (_rightPerson != null && _contentSnapshot.Parent != null)
                {
                    var parentRect = _contentSnapshot.Parent as RectTransform;
                    if (parentRect != null)
                    {
                        var parentCorners = new Vector3[4];
                        parentRect.GetWorldCorners(parentCorners);
                        var parentRight = Mathf.Max(parentCorners[2].x, parentCorners[3].x);

                        var rightPersonCorners = new Vector3[4];
                        _rightPerson.GetWorldCorners(rightPersonCorners);
                        var rightPersonLeft = Mathf.Min(rightPersonCorners[0].x, rightPersonCorners[1].x);

                        var margin = rightPersonLeft - parentRight;
                        if (margin < -8f)
                        {
                            return margin;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("计算右侧保留宽度失败: " + ex.Message);
            }

            return -DefaultRightReserved;
        }

        private void ConfigureScrollForViewport(float totalWidth, Vector2 card)
        {
            try
            {
                if (_viewportRt == null || _content == null)
                {
                    return;
                }

                _viewportRt.offsetMax = new Vector2(GetRightMargin(), 0f);

                _content.SetParent(_viewportRt, false);
                _content.anchorMin = new Vector2(0f, 1f);
                _content.anchorMax = new Vector2(0f, 1f);
                _content.pivot = new Vector2(0f, 1f);
                _content.anchoredPosition = new Vector2(_leftInset, 0f);
                _content.sizeDelta = new Vector2(Mathf.Max(totalWidth, 1f), Mathf.Max(_baselineSize.y, card.y, 1f));

                if (_scroll != null)
                {
                    _scroll.horizontal = true;
                    _scroll.vertical = false;
                    _scroll.movementType = ModConfig.Elastic ? ScrollRect.MovementType.Elastic : ScrollRect.MovementType.Clamped;
                    _scroll.scrollSensitivity = ModConfig.WheelSpeed;
                    _scroll.enabled = true;
                }

                UILayoutTools.MarkForRebuild(_content);
                UILayoutTools.MarkForRebuild(_viewportRt);
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 配置滚动几何", ex);
            }
        }

        /// <summary>
        /// v4.2.4 回退方案：ScrollRect 直接加在 TraderCards 上（不改变父级/锚点、无遮罩），
        /// 但 viewport 改为“卡片可见区”的隐藏 RectTransform（见 <see cref="EnsureKaenoViewport"/>）。
        /// 这样可滚范围 = 内容宽 - 可见宽，滚到最右端时最后一张卡正好停在 Right Person 左边缘。
        /// </summary>
        private bool AttachKaenoFallback(float totalWidth, Vector2 card)
        {
            try
            {
                if (!_snapshotValid || _contentSnapshot.Parent == null)
                {
                    return false;
                }

                var viewport = EnsureKaenoViewport(card);
                if (viewport == null)
                {
                    return false;
                }

                var scroll = _content.GetComponent<ScrollRect>();
                if (scroll == null)
                {
                    scroll = _content.gameObject.AddComponent<ScrollRect>();
                }

                // v4.2.4：如果 TraderCards 上有多个 ScrollRect（其它模组也加过），
                // 只保留我们用的这个，禁用其它，避免互相抢 anchoredPosition 产生跳动/空白。
                var allScrolls = _content.GetComponents<ScrollRect>();
                if (allScrolls.Length > 1)
                {
                    for (var i = 0; i < allScrolls.Length; i++)
                    {
                        var other = allScrolls[i];
                        if (other != null && other != scroll && other.enabled)
                        {
                            other.enabled = false;
                            Log.Warn("界面A：检测到 TraderCards 上存在多个 ScrollRect（共 " + allScrolls.Length
                                     + " 个），已禁用多余的一个（可能有其它模组也改了这个界面）。");
                        }
                    }
                }

                _contentSnapshot.Restore(_content);
                _content.sizeDelta = new Vector2(Mathf.Max(totalWidth, 1f), Mathf.Max(_baselineSize.y, card.y, 1f));

                scroll.content = _content;
                scroll.viewport = viewport;
                scroll.horizontal = true;
                scroll.vertical = false;
                scroll.inertia = true;
                // v4.2.4：恢复 Use Elastic 的语义（界面A 由用户配置决定；界面B 仍固定 Clamped）。
                // 默认配置为 true（旧 Kaeno 行为）；关闭则硬夹取。
                scroll.movementType = ModConfig.Elastic
                    ? ScrollRect.MovementType.Elastic
                    : ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = ModConfig.WheelSpeed;
                scroll.enabled = true;

                _scroll = scroll;
                _viewportRt = viewport;
                _kaenoFallback = true;
                UILayoutTools.MarkForRebuild(_content);
                Log.Debug("界面A 已启用修复版滚动：viewport=" + KaenoViewportObjectName
                          + " width=" + viewport.rect.width.ToString("0.##")
                          + " movementType=" + scroll.movementType);
                DiagHookScroll(scroll); // v4.2.3-DIAG：挂接"滚到最右端自动 dump"
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("界面A Kaeno 回退方案", ex);
                return false;
            }
        }

        /// <summary>
        /// v4.2.4：创建/更新“卡片可见区 viewport”。
        /// 它是 Container 下隐藏的 RectTransform（无 Graphic、无 Mask、不改 TraderCards 父级），
        /// 左边缘 = TraderCards 原始左边缘（_leftInset），宽度 = Right Person 左边缘 - 左边缘。
        /// ScrollRect 只拿它做滚动边界计算，不会产生任何视觉变化。
        /// </summary>
        private RectTransform EnsureKaenoViewport(Vector2 card)
        {
            try
            {
                var parent = _contentSnapshot.Parent as RectTransform;
                if (parent == null)
                {
                    return null;
                }

                var visibleWidth = ComputeVisibleWidth(card.x);
                if (visibleWidth < card.x * 1.5f)
                {
                    return null;
                }

                if (_kaenoViewportRt == null || _kaenoViewportGo == null)
                {
                    _kaenoViewportGo = new GameObject(KaenoViewportObjectName, typeof(RectTransform));
                    _kaenoViewportRt = (RectTransform)_kaenoViewportGo.transform;
                    Log.Debug("界面A 已创建可见区 viewport: " + KaenoViewportObjectName);
                }

                var rt = _kaenoViewportRt;
                rt.SetParent(parent, false);
                rt.SetAsLastSibling(); // 纯 RectTransform，无渲染，放在最后不影响任何绘制顺序
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(_leftInset, 0f);
                rt.sizeDelta = new Vector2(Mathf.Max(visibleWidth, 1f), 0f);

                return rt;
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 创建可见区 viewport", ex);
                return null;
            }
        }

        /// <summary>
        /// v4.2.4：配置变更重应用后恢复用户原本的滚动位置（避免“改设置→卡条跳回开头”）。
        /// </summary>
        private void ApplyPreservedScrollPosition()
        {
            var value = _preservedNormalized;
            _preservedNormalized = -1f;
            if (value < 0f)
            {
                return;
            }

            try
            {
                if (_scroll == null || !_scroll.enabled || !_kaenoFallback || _content == null)
                {
                    return;
                }

                Canvas.ForceUpdateCanvases();
                UILayoutTools.ForceRebuild(_content);
                _scroll.horizontalNormalizedPosition = Mathf.Clamp01(value);
                Log.Debug("界面A 配置重应用后已恢复滚动位置 normalized=" + value.ToString("0.###"));
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 恢复滚动位置", ex);
            }
        }

        private void RestoreVanilla(string reason)
        {
            try
            {
                if (_kaenoFallback)
                {
                    if (_scroll != null)
                    {
                        _scroll.enabled = false;
                    }

                    if (_snapshotValid)
                    {
                        _contentSnapshot.Restore(_content);
                    }

                    if (_kaenoViewportGo != null)
                    {
                        UnityEngine.Object.Destroy(_kaenoViewportGo);
                        _kaenoViewportGo = null;
                        _kaenoViewportRt = null;
                    }

                    _scroll = null;
                    _viewportRt = null;
                    _kaenoFallback = false;
                    Log.Debug("界面A 已还原 Kaeno 回退方案（原因: " + reason + "）");
                    return;
                }

                if (_viewportGo != null)
                {
                    if (_scroll != null)
                    {
                        _scroll.enabled = false;
                    }

                    if (_snapshotValid && _contentSnapshot.Parent != null)
                    {
                        _content.SetParent(_contentSnapshot.Parent, false);
                        _contentSnapshot.Restore(_content);
                    }

                    UnityEngine.Object.Destroy(_viewportGo);
                    _viewportGo = null;
                    _viewportRt = null;
                    _scroll = null;
                    Log.Debug("界面A 已还原原版布局（原因: " + reason + "）");
                }
                else if (_snapshotValid)
                {
                    _contentSnapshot.Restore(_content);
                }

                // v4.2.4：无论走哪条还原路径，都确保销毁辅助 viewport（避免残留隐藏节点）
                if (_kaenoViewportGo != null)
                {
                    UnityEngine.Object.Destroy(_kaenoViewportGo);
                    _kaenoViewportGo = null;
                    _kaenoViewportRt = null;
                }
            }
            catch (Exception ex)
            {
                Log.Exception("界面A 还原原版布局", ex);
            }
        }

        // ------------------------------------------------------------------ 诊断

        private void DumpDiagnostics()
        {
            try
            {
                var cards = CountCards();
                var metrics = UILayoutTools.Measure(_content, null, _cardPrefab);
                DiagnosticDumper.DumpTraderCards(
                    _screen,
                    _content,
                    _viewportRt,
                    _scroll,
                    cards,
                    metrics,
                    _baselineSize,
                    _lastAppliedSummary,
                    _containerResolveSource);
            }
            catch (Exception ex)
            {
                Log.Exception("界面A dump", ex);
            }
        }
    }
}
