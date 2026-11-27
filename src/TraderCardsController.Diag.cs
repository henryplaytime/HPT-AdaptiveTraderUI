using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveTraderUI
{
    /// <summary>
    /// v4.2.3-DIAG 专用诊断代码（partial，与 TraderCardsController 同类型，不改变任何布局/滚动行为）。
    ///
    /// 目的：
    ///   1) 记录界面A（顶部商人卡条）在 打开界面 / 配置变更重应用 / 滚动到最右端 / 检测到外部改动 时的完整几何状态；
    ///   2) 自动做"逻辑自检"：内容宽度 vs 卡片实际末端、滚动行程 vs 可见宽度、
    ///      计数一致性（childCount / _spawnedCards.Count / 不可见卡片）、movementType、ScrollRect 数量、锚点是否被外部改；
    ///   3) 全部写入 BepInEx\HPT-AdaptiveTraderUI-diag.txt，直接发给作者定位（仅在 Diagnostic Dump 开启时）。
    ///
    /// 设计约束：本文件所有代码都在 try/catch 中，绝不改变界面行为，异常只记警告。
    /// </summary>
    internal sealed partial class TraderCardsController
    {
        // ---------------------------------------------------------------- 诊断状态

        private int _diagApplySeq;
        private string _diagPendingReason = "<none>";
        private string _diagLastReason = "<none>";
        private string _diagMode = "<not-applied>";
        private Vector2 _diagBeforeAnchoredPosition;
        private Vector2 _diagBeforeSizeDelta;
        private Vector2 _diagBeforeNormalized;
        private bool _diagBeforeHadScroll;
        private bool _diagScrollEndDumped;
        private float _diagNextTick;
        private int _diagScrollRectCount = -1;
        private bool _diagMovementLogged;
        private bool _diagTargetLogged;
        private bool _diagAnchorLogged;
        private bool _diagExtraScrollLogged;
        private bool _diagHintLogged;
        private readonly List<string> _diagHistory = new List<string>();

        // 最近一次 ApplyCore 的中间量（供快照引用，避免重复测量产生偏差）
        private int _diagCards;
        private Vector2 _diagCardSize;
        private Vector2 _diagSpacing;
        private RectOffset _diagPadding = new RectOffset();
        private float _diagTotalWidth;
        private float _diagUsableWidth;

        // ---------------------------------------------------------------- 主文件调用入口

        /// <summary>RequestApply(reason) 时记录"本次应用是谁触发的"。</summary>
        private void DiagNoteRequest(string reason)
        {
            try
            {
                _diagPendingReason = string.IsNullOrEmpty(reason) ? "<empty>" : reason;
            }
            catch { }
        }

        /// <summary>ApplyCore 执行前：记录应用前的滚动状态（用于判断重应用是否把滚动位置重置了）。</summary>
        private void DiagBeforeApply()
        {
            if (!ModConfig.DiagnosticDumpEnabled)
            {
                return;
            }

            try
            {
                _diagLastReason = _diagPendingReason;
                _diagBeforeHadScroll = _scroll != null;
                if (_content != null)
                {
                    _diagBeforeAnchoredPosition = _content.anchoredPosition;
                    _diagBeforeSizeDelta = _content.sizeDelta;
                }

                _diagBeforeNormalized = _scroll != null ? _scroll.normalizedPosition : Vector2.zero;
            }
            catch { }
        }

        /// <summary>ApplyCore 执行后：写完整快照到文件，并在 BepInEx 日志里留一行结论摘要。</summary>
        private void DiagAfterApply()
        {
            if (!ModConfig.DiagnosticDumpEnabled)
            {
                return;
            }

            try
            {
                _diagApplySeq++;
                string shortVerdict;
                var text = BuildDiagSnapshot("apply", "本次应用前 → 后的状态对比已包含在快照里", out shortVerdict);
                DiagFile.Append("A-apply#" + _diagApplySeq, text);

                _diagHistory.Add(DateTime.Now.ToString("HH:mm:ss.fff")
                                 + " #" + _diagApplySeq
                                 + " reason=" + _diagLastReason
                                 + " mode=" + _diagMode
                                 + " cards=" + _diagCards
                                 + " totalW=" + _diagTotalWidth.ToString("0.#")
                                 + " usableW=" + _diagUsableWidth.ToString("0.#"));
                while (_diagHistory.Count > 12)
                {
                    _diagHistory.RemoveAt(0);
                }

                Log.Info("[DIAG-A] #" + _diagApplySeq + " reason=" + _diagLastReason + " | " + shortVerdict
                         + " | 详情已写入 " + DiagFile.Path);
            }
            catch (Exception ex)
            {
                Log.Warn("[DIAG-A] 快照记录失败（不影响游戏）: " + ex.Message);
            }
        }

        /// <summary>AttachKaenoFallback 成功后：挂上"滚动到最右端自动 dump"监听。</summary>
        private void DiagHookScroll(ScrollRect scroll)
        {
            try
            {
                if (scroll == null)
                {
                    return;
                }

                scroll.onValueChanged.RemoveListener(DiagOnScrollValueChanged);
                scroll.onValueChanged.AddListener(DiagOnScrollValueChanged);
                _diagScrollEndDumped = false;

                if (!_diagHintLogged && ModConfig.DiagnosticDumpEnabled)
                {
                    _diagHintLogged = true;
                    DiagFile.Hint();
                }
            }
            catch (Exception ex)
            {
                Log.Warn("[DIAG-A] 挂接滚动监听失败: " + ex.Message);
            }
        }

        private void DiagOnScrollValueChanged(Vector2 normalizedPosition)
        {
            if (!ModConfig.DiagnosticDumpEnabled)
            {
                return;
            }

            try
            {
                if (normalizedPosition.x >= 0.999f)
                {
                    if (!_diagScrollEndDumped)
                    {
                        _diagScrollEndDumped = true;
                        string summary;
                        var text = BuildDiagSnapshot("scroll-end",
                            "滚动到最右端（normalized=" + normalizedPosition.x.ToString("0.0000") + "）：此快照用于判断最后几张卡为何不可见",
                            out summary);
                        DiagFile.Append("A-scroll-end", text);
                        Log.Info("[DIAG-A] 滚动到最右端快照已写入 " + DiagFile.Path + " | " + summary);
                    }
                }
                else if (normalizedPosition.x < 0.95f)
                {
                    _diagScrollEndDumped = false;
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------- 周期检测外部改动

        private void Update()
        {
            if (!ModConfig.DiagnosticDumpEnabled)
            {
                return;
            }

            try
            {
                DiagTick();
            }
            catch { }
        }

        private void DiagTick()
        {
            if (Time.unscaledTime < _diagNextTick)
            {
                return;
            }

            _diagNextTick = Time.unscaledTime + 0.5f;
            if (_content == null)
            {
                return;
            }

            try
            {
                var srCount = _content.GetComponents<ScrollRect>().Length;
                if (_diagScrollRectCount < 0)
                {
                    _diagScrollRectCount = srCount;
                }

                if (srCount != _diagScrollRectCount)
                {
                    _diagScrollRectCount = srCount;
                    DiagEvent("external-scrollrect",
                        "TraderCards 上的 ScrollRect 数量=" + srCount + "（期望 1）。多个 ScrollRect 会互相抢 anchoredPosition，表现为跳动/空白。"
                        + " ScrollRect 组件列表=" + ListComponentsOfType<ScrollRect>(_content.gameObject));
                }

                if (srCount > 1 && !_diagExtraScrollLogged)
                {
                    _diagExtraScrollLogged = true;
                    DiagEvent("external-scrollrect-extra",
                        "检测到 TraderCards 上有 " + srCount + " 个 ScrollRect："
                        + ListComponentsOfType<ScrollRect>(_content.gameObject));
                }

                if (_scroll != null)
                {
                    // v1.1.0：期望值随 Use Elastic 推导（v4.2.4 起界面A 的 movementType 由该配置决定，
                    // 默认 true = Elastic）。旧实现写死 Clamped，默认配置下会误报“有外部代码在改它”。
                    var expectedMovementTick = ModConfig.Elastic
                        ? ScrollRect.MovementType.Elastic
                        : ScrollRect.MovementType.Clamped;
                    if (_scroll.movementType != expectedMovementTick)
                    {
                        if (!_diagMovementLogged)
                        {
                            _diagMovementLogged = true;
                            DiagEvent("external-movement",
                                "ScrollRect.movementType 当前=" + _scroll.movementType
                                + "（当前配置 UseElastic=" + ModConfig.Elastic + " 期望 " + expectedMovementTick
                                + "，与当前配置不一致，说明有外部代码在改它）");
                        }
                    }
                    else
                    {
                        _diagMovementLogged = false;
                    }

                    // v4.2.4：期望的 viewport 是可见区隐藏节点（未启用滚动时是 Container）
                    var expectedViewport = _kaenoFallback && _kaenoViewportRt != null
                        ? (RectTransform)_kaenoViewportRt
                        : _contentSnapshot.Parent as RectTransform;
                    if (!_diagTargetLogged
                        && ((UnityEngine.Object)expectedViewport == null
                            || _scroll.viewport != expectedViewport
                            || _scroll.content != _content))
                    {
                        _diagTargetLogged = true;
                        DiagEvent("external-target",
                            "ScrollRect 的 content/viewport 被改动：content=" + NameOf(_scroll.content)
                            + " viewport=" + NameOf(_scroll.viewport)
                            + "（期望 content=" + NameOf(_content) + " viewport=" + NameOf(expectedViewport) + "）");
                    }

                    if (_snapshotValid)
                    {
                        var anchorsChanged = !Approximately(_content.anchorMin, _contentSnapshot.AnchorMin)
                                             || !Approximately(_content.anchorMax, _contentSnapshot.AnchorMax)
                                             || !Approximately(_content.pivot, _contentSnapshot.Pivot);
                        if (anchorsChanged)
                        {
                            if (!_diagAnchorLogged)
                            {
                                _diagAnchorLogged = true;
                                DiagEvent("external-anchors",
                                    "TraderCards 的锚点/pivot 与原始快照不同：now aMin=" + FmtV(_content.anchorMin)
                                    + " aMax=" + FmtV(_content.anchorMax)
                                    + " pivot=" + FmtV(_content.pivot)
                                    + "；snapshot aMin=" + FmtV(_contentSnapshot.AnchorMin)
                                    + " aMax=" + FmtV(_contentSnapshot.AnchorMax)
                                    + " pivot=" + FmtV(_contentSnapshot.Pivot));
                            }
                        }
                        else
                        {
                            _diagAnchorLogged = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("[DIAG-A] 周期检测失败: " + ex.Message);
            }
        }

        private void DiagEvent(string tag, string message)
        {
            if (!ModConfig.DiagnosticDumpEnabled)
            {
                return;
            }

            try
            {
                string summary;
                var text = BuildDiagSnapshot(tag, message, out summary);
                DiagFile.Append("A-" + tag, text);
                Log.Warn("[DIAG-A] " + tag + ": " + message);
            }
            catch (Exception ex)
            {
                Log.Warn("[DIAG-A] 事件快照失败: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------- 快照主体

        private string BuildDiagSnapshot(string tag, string extra, out string shortVerdict)
        {
            var sb = new StringBuilder(8192);
            var checks = new List<string>();
            shortVerdict = "";

            try
            {
                AppendHeader(sb, tag, extra);
                AppendApplyDiff(sb);
                AppendCanvasInfo(sb);
                AppendHierarchyInfo(sb);
                AppendCardInfo(sb, checks);
                AppendScrollInfo(sb, checks);
                AppendLogicCheck(sb, checks);
                AppendHistory(sb);
            }
            catch (Exception ex)
            {
                sb.AppendLine("[snapshot-error] " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                var fails = new List<string>();
                foreach (var check in checks)
                {
                    if (check.IndexOf("FAIL", StringComparison.Ordinal) >= 0)
                    {
                        fails.Add(check);
                    }
                }

                shortVerdict = fails.Count == 0
                    ? "自检全部 PASS"
                    : string.Join("；", fails.ToArray());
            }
            catch
            {
                shortVerdict = "自检结论生成失败";
            }

            return sb.ToString();
        }

        private void AppendHeader(StringBuilder sb, string tag, string extra)
        {
            sb.AppendLine("[snapshot] tag=" + tag
                          + " reason=" + _diagLastReason
                          + " applySeq=" + _diagApplySeq
                          + " mode=" + _diagMode
                          + " time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            if (!string.IsNullOrEmpty(extra))
            {
                sb.AppendLine("[extra] " + extra);
            }

            sb.AppendLine("[build] plugin=" + Plugin.PluginVersion
                          + " diagBuild=true（诊断构建：行为与 v4.2.2 完全相同，仅增加记录）");
            sb.AppendLine("[config] TraderStripScrolling=" + BoolValue(ModConfig.TraderStripScrolling)
                          + " UseElastic=" + BoolValue(ModConfig.UseElastic)
                          + " 界面A 的 movementType 由 Use Elastic 决定（true=Elastic / false=Clamped）"
                          + " WheelSpeed=" + ModConfig.WheelSpeed.ToString("0.#")
                          + " DiagnosticDump(配置)=" + BoolValue(ModConfig.DiagnosticDump)
                          + " DebugLogging=" + BoolValue(ModConfig.DebugLogging)
                          + " LastChangedSetting=" + (ModConfig.LastChangedSetting ?? "?"));
            sb.AppendLine("[containerResolve] " + _containerResolveSource
                          + " baselineSize=" + FmtV(_baselineSize)
                          + " leftInset=" + _leftInset.ToString("0.##")
                          + " snapshotValid=" + _snapshotValid
                          + " lastAppliedSummary=" + _lastAppliedSummary);
        }

        private void AppendApplyDiff(StringBuilder sb)
        {
            sb.AppendLine("[apply] mode=" + _diagMode
                          + " 触发原因=" + _diagLastReason
                          + " 应用前是否有ScrollRect=" + _diagBeforeHadScroll);
            sb.AppendLine("[apply-before] content.anchoredPos=" + FmtV(_diagBeforeAnchoredPosition)
                          + " content.sizeDelta=" + FmtV(_diagBeforeSizeDelta)
                          + " scroll.normalized=" + FmtV(_diagBeforeNormalized));
            if (_content != null)
            {
                sb.AppendLine("[apply-after ] content.anchoredPos=" + FmtV(_content.anchoredPosition)
                              + " content.sizeDelta=" + FmtV(_content.sizeDelta)
                              + " scroll.normalized=" + (_scroll != null ? FmtV(_scroll.normalizedPosition) : "<无ScrollRect>"));
                if (_diagBeforeHadScroll
                    && _scroll != null
                    && Mathf.Abs(_diagBeforeAnchoredPosition.x - _content.anchoredPosition.x) > 1f)
                {
                    sb.AppendLine("[apply-jump] 重应用把卡片条滚动位置重置了：x "
                                  + _diagBeforeAnchoredPosition.x.ToString("0.##") + " → " + _content.anchoredPosition.x.ToString("0.##")
                                  + "（配置变更会触发重应用；若你是在滚动后改设置，就会看到卡条跳回开头）");
                }
            }
        }

        private void AppendCanvasInfo(StringBuilder sb)
        {
            sb.AppendLine("[screen] Screen=" + Screen.width + "x" + Screen.height);
            try
            {
                if (_screen != null)
                {
                    var canvas = _screen.GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        var scaler = canvas.GetComponent<CanvasScaler>();
                        sb.AppendLine("[canvas] name=" + canvas.name
                                      + " scaleFactor=" + canvas.scaleFactor.ToString("0.###")
                                      + " scaler=" + (scaler == null
                                          ? "<null>"
                                          : (scaler.uiScaleMode + " ref=" + FmtV(scaler.referenceResolution)
                                             + " match=" + scaler.matchWidthOrHeight.ToString("0.##"))));
                    }
                    else
                    {
                        sb.AppendLine("[canvas] <未找到父Canvas>");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("[canvas-error] " + ex.Message);
            }
        }

        private void AppendHierarchyInfo(StringBuilder sb)
        {
            sb.AppendLine("[hierarchy] screen=" + (_screen == null ? "<null>" : UILayoutTools.GetPath(_screen.transform)));

            var viewport = _scroll != null ? _scroll.viewport : _viewportRt;
            sb.AppendLine("[hierarchy] content=" + RectLine(_content));
            sb.AppendLine("[hierarchy] viewport=" + RectLine(viewport));
            sb.AppendLine("[hierarchy] content.parent=" + RectLine(_content != null ? _content.parent as RectTransform : null));
            sb.AppendLine("[hierarchy] content.parent.parent="
                          + RectLine(_content != null && _content.parent != null ? _content.parent.parent as RectTransform : null));

            if (_content != null)
            {
                sb.AppendLine("[components] content components=" + UILayoutTools.ComponentsOf(_content.gameObject, 400));
            }

            if (viewport != null)
            {
                sb.AppendLine("[components] viewport components=" + UILayoutTools.ComponentsOf(viewport.gameObject, 400));
            }

            var container = _content == null ? null : _content.parent as RectTransform;
            if (container != null)
            {
                sb.AppendLine("[components] container components=" + UILayoutTools.ComponentsOf(container.gameObject, 400));
                sb.AppendLine("[siblings] Container 的其它子节点（含 Right Person）:");
                for (var i = 0; i < container.childCount; i++)
                {
                    var child = container.GetChild(i) as RectTransform;
                    if (child == null)
                    {
                        continue;
                    }

                    sb.AppendLine("  sibling[" + i + "] " + RectLine(child)
                                  + " components=" + UILayoutTools.ComponentsOf(child.gameObject, 200));
                }
            }

            sb.AppendLine("[masks] ancestors=" + AncestorMasks(_content));
        }

        private void AppendCardInfo(StringBuilder sb, List<string> checks)
        {
            var childCount = _content == null ? 0 : _content.childCount;
            var activeSelfCount = 0;
            var activeInHierarchyCount = 0;
            var nonCardChildren = 0;
            var inactiveNames = new StringBuilder();
            var nonCardNames = new StringBuilder();
            var childrenExtentRight = float.NegativeInfinity;
            var childrenExtentLeft = float.PositiveInfinity;
            var laidOutChildren = 0;
            var lastChildIndex = -1;

            for (var i = 0; i < childCount; i++)
            {
                var child = _content.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                if (child.gameObject.activeSelf)
                {
                    activeSelfCount++;
                }

                if (!child.gameObject.activeInHierarchy)
                {
                    if (inactiveNames.Length < 240)
                    {
                        inactiveNames.Append(child.name).Append("@").Append(i).Append("; ");
                    }

                    continue;
                }

                activeInHierarchyCount++;
                laidOutChildren++;
                lastChildIndex = i;

                float minX;
                float maxX;
                if (TryGetLocalXRange(child, _content, out minX, out maxX))
                {
                    childrenExtentRight = Mathf.Max(childrenExtentRight, maxX);
                    childrenExtentLeft = Mathf.Min(childrenExtentLeft, minX);
                }

                if (child.GetComponent<TraderCard>() == null)
                {
                    nonCardChildren++;
                    if (nonCardNames.Length < 240)
                    {
                        nonCardNames.Append(child.name).Append("@").Append(i).Append("; ");
                    }
                }
            }

            if (laidOutChildren == 0)
            {
                childrenExtentRight = 0f;
                childrenExtentLeft = 0f;
            }

            // _spawnedCards 列表（游戏内部 List<TraderCard>）与子节点数量对比
            var spawnedCount = -1;
            var spawnedAlive = 0;
            var spawnedInactive = 0;
            var spawnedNotChildOfContent = 0;
            var spawnedBad = new StringBuilder();
            try
            {
                if (_spawnedCardsField != null && _screen != null)
                {
                    var list = _spawnedCardsField.GetValue(_screen) as IList;
                    if (list != null)
                    {
                        spawnedCount = list.Count;
                        for (var i = 0; i < list.Count; i++)
                        {
                            var card = list[i] as Component;
                            if (card == null)
                            {
                                spawnedNotChildOfContent++;
                                if (spawnedBad.Length < 240)
                                {
                                    spawnedBad.Append("<null或已销毁>@").Append(i).Append("; ");
                                }

                                continue;
                            }

                            spawnedAlive++;
                            if (!card.gameObject.activeSelf)
                            {
                                spawnedInactive++;
                            }

                            if (card.transform.parent != _content)
                            {
                                spawnedNotChildOfContent++;
                                if (spawnedBad.Length < 240)
                                {
                                    spawnedBad.Append(card.name).Append("@").Append(i)
                                        .Append("(parent=").Append(card.transform.parent == null ? "<null>" : card.transform.parent.name).Append("); ");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("[spawnedCards-error] " + ex.Message);
            }

            var measured = UILayoutTools.Measure(_content, null, _cardPrefab);
            var cardW = measured != null ? measured.CardSize.x : 0f;
            var spacingX = measured != null ? measured.Spacing.x : 0f;
            var pitch = cardW + spacingX;

            sb.AppendLine("[cards] childCount=" + childCount
                          + " activeSelf=" + activeSelfCount
                          + " activeInHierarchy(实际参与排版)=" + activeInHierarchyCount
                          + " nonCardChildren=" + nonCardChildren
                          + " lastLaidOutChildIndex=" + lastChildIndex);
            sb.AppendLine("[cards] spawnedCards.Count=" + spawnedCount
                          + " spawnedAlive=" + spawnedAlive
                          + " spawnedInactive=" + spawnedInactive
                          + " spawnedNotChildOfContent=" + spawnedNotChildOfContent);

            if (inactiveNames.Length > 0)
            {
                sb.AppendLine("[cards] 不可见(inactive)子节点: " + inactiveNames);
            }

            if (nonCardNames.Length > 0)
            {
                sb.AppendLine("[cards] 非 TraderCard 子节点: " + nonCardNames);
            }

            if (spawnedBad.Length > 0)
            {
                sb.AppendLine("[cards] 异常 spawnedCards: " + spawnedBad);
            }

            sb.AppendLine("[metrics] cardSize=" + FmtV(new Vector2(cardW, measured != null ? measured.CardSize.y : 0f))
                          + " spacingX=" + spacingX.ToString("0.##")
                          + " pitch=" + pitch.ToString("0.##")
                          + " padding=(" + (measured == null || measured.Padding == null
                              ? "?"
                              : measured.Padding.left + "," + measured.Padding.right + "," + measured.Padding.top + "," + measured.Padding.bottom)
                          + ")"
                          + " measuredFromChildren=" + (measured != null && measured.MeasuredFromChildren));
            sb.AppendLine("[metrics] apply时: cards=" + _diagCards
                          + " cardW=" + _diagCardSize.x.ToString("0.##")
                          + " spacingX=" + _diagSpacing.x.ToString("0.##")
                          + " padding=(" + (_diagPadding == null ? "?" : _diagPadding.left + "," + _diagPadding.right) + ")"
                          + " totalWidth(代码计算)=" + _diagTotalWidth.ToString("0.##")
                          + " usableWidth(apply时测量)=" + _diagUsableWidth.ToString("0.##"));

            // 头尾卡片明细（便于识别"哪一段没画出来"）
            AppendChildDetail(sb, 0);
            if (childCount > 1)
            {
                AppendChildDetail(sb, 1);
            }

            if (lastChildIndex > 1)
            {
                AppendChildDetail(sb, lastChildIndex - 1);
            }

            if (lastChildIndex > 0)
            {
                AppendChildDetail(sb, lastChildIndex);
            }

            // 供后续 section 使用的中间值 → 暂存在字段
            _diagCardsSnapshotChildCount = childCount;
            _diagCardsSnapshotActive = activeInHierarchyCount;
            _diagCardsSnapshotInactive = childCount - activeInHierarchyCount;
            _diagCardsSnapshotNonCard = nonCardChildren;
            _diagCardsSnapshotSpawned = spawnedCount;
            _diagCardsSnapshotSpawnedBad = spawnedNotChildOfContent + spawnedInactive;
            _diagCardsSnapshotChildrenRight = childrenExtentRight;
            _diagCardsSnapshotChildrenLeft = childrenExtentLeft;
            _diagCardsSnapshotPitch = pitch;
            _diagCardsSnapshotCardW = cardW;

            if (spawnedCount >= 0 && spawnedCount != childCount)
            {
                sb.AppendLine("[cards-warn] childCount(" + childCount + ") 与 _spawnedCards.Count(" + spawnedCount
                              + ") 不一致：CountCards() 取两者最大值，内容宽度会按较大值计算，可能产生多出来的空白卡位。");
            }
        }

        private int _diagCardsSnapshotChildCount;
        private int _diagCardsSnapshotActive;
        private int _diagCardsSnapshotInactive;
        private int _diagCardsSnapshotNonCard;
        private int _diagCardsSnapshotSpawned;
        private int _diagCardsSnapshotSpawnedBad;
        private float _diagCardsSnapshotChildrenRight;
        private float _diagCardsSnapshotChildrenLeft;
        private float _diagCardsSnapshotPitch;
        private float _diagCardsSnapshotCardW;

        private void AppendChildDetail(StringBuilder sb, int index)
        {
            try
            {
                if (_content == null || index < 0 || index >= _content.childCount)
                {
                    return;
                }

                var child = _content.GetChild(index) as RectTransform;
                if (child == null)
                {
                    return;
                }

                float minX, maxX;
                TryGetLocalXRange(child, _content, out minX, out maxX);
                var layoutElement = child.GetComponent<LayoutElement>();
                sb.AppendLine("[child@" + index + "] name=" + child.name
                              + " activeSelf=" + child.gameObject.activeSelf
                              + " activeInHierarchy=" + child.gameObject.activeInHierarchy
                              + " xRangeInContent=[" + minX.ToString("0.##") + "," + maxX.ToString("0.##") + "]"
                              + " width=" + (maxX - minX).ToString("0.##")
                              + " prefW=" + (layoutElement == null ? "-" : layoutElement.preferredWidth.ToString("0.##"))
                              + " " + UILayoutTools.RectOf(child)
                              + " isTraderCard=" + (child.GetComponent<TraderCard>() != null));
            }
            catch (Exception ex)
            {
                sb.AppendLine("[child@" + index + "-error] " + ex.Message);
            }
        }

        private void AppendScrollInfo(StringBuilder sb, List<string> checks)
        {
            if (_content == null)
            {
                sb.AppendLine("[scroll] <content 为 null，无法检查滚动几何>");
                return;
            }

            var viewport = _scroll != null ? _scroll.viewport : _viewportRt;
            if (viewport == null)
            {
                sb.AppendLine("[scroll] viewport=null（当前没有滚动组件，可能是原版/fit 模式）");
                return;
            }

            var viewportW = viewport.rect.width;
            var contentW = _content.rect.width;
            var contentSizeDeltaW = _content.sizeDelta.x;
            var anchorSpanW = (_content.anchorMax.x - _content.anchorMin.x) * viewportW;

            // 统一坐标系：以 viewport 左边界为 0 的“参考分辨率像素”（与 RectTransform 的 rect/sizeDelta 同单位）。
            // Canvas 缩放（如 2560x1440 下 scaleFactor≈1.333）只影响世界坐标，不影响 rect 尺寸，因此需要换算。
            var uiScale = 1f;
            try
            {
                if (_screen != null)
                {
                    var canvas = _screen.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.scaleFactor > 0.01f)
                    {
                        uiScale = canvas.scaleFactor;
                    }
                }
            }
            catch { }
            var safeScale = Mathf.Max(uiScale, 0.01f);

            var viewportLeftWorld = MinWorldX(viewport);
            var viewportRightWorld = MaxWorldX(viewport);
            var contentLeftWorldNow = MinWorldX(_content);
            var contentRightWorldNow = MaxWorldX(_content);
            var contentWidthWorld = contentRightWorldNow - contentLeftWorldNow;

            var visibleRight = viewportW; // 无 Right Person 时整条 viewport 都可见
            var rightPersonFound = false;
            var visibleRightWorld = viewportRightWorld;
            try
            {
                ResolveRightPerson();
                if (_rightPerson != null)
                {
                    visibleRightWorld = MinWorldX(_rightPerson);
                    visibleRight = (visibleRightWorld - viewportLeftWorld) / safeScale;
                    rightPersonFound = true;
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("[rightPerson-error] " + ex.Message);
            }

            // 卡片实际末端（content 本地单位即参考像素）
            var childrenRight = _diagCardsSnapshotChildrenRight;
            var childrenLeft = _diagCardsSnapshotChildrenLeft;
            var pitch = Mathf.Max(1f, _diagCardsSnapshotPitch);

            // 当前（可能已滚动）时刻的换算，便于和截图直接对照（参考像素，viewport 左边界=0）
            var contentLeftNowRef = (contentLeftWorldNow - viewportLeftWorld) / safeScale;
            var contentRightNowRef = (contentRightWorldNow - viewportLeftWorld) / safeScale;
            var lastCardRightNowRef = contentLeftNowRef + childrenRight;

            sb.AppendLine("[scroll] ScrollRect=" + (_scroll == null ? "<无>" : "有")
                          + " enabled=" + (_scroll == null ? "-" : _scroll.enabled.ToString())
                          + " horizontal=" + (_scroll == null ? "-" : _scroll.horizontal.ToString())
                          + " vertical=" + (_scroll == null ? "-" : _scroll.vertical.ToString())
                          + " movementType=" + (_scroll == null ? "-" : _scroll.movementType.ToString())
                          + " inertia=" + (_scroll == null ? "-" : _scroll.inertia.ToString())
                          + " sensitivity=" + (_scroll == null ? "-" : _scroll.scrollSensitivity.ToString("0.##")));
            if (_scroll != null)
            {
                sb.AppendLine("[scroll] content=" + NameOf(_scroll.content) + " viewport=" + NameOf(_scroll.viewport)
                              + " normalized=" + FmtV(_scroll.normalizedPosition)
                              + " velocity=" + FmtV(_scroll.velocity)
                              + " onContentScrollRectCount=" + _content.GetComponents<ScrollRect>().Length);
            }

            sb.AppendLine("[scroll-geom] uiScale=" + uiScale.ToString("0.###")
                          + " viewportW=" + viewportW.ToString("0.##")
                          + " contentRectW=" + contentW.ToString("0.##")
                          + " contentSizeDeltaW=" + contentSizeDeltaW.ToString("0.##")
                          + " anchorSpanW=" + anchorSpanW.ToString("0.##")
                          + " contentLeftNow(ref)=" + contentLeftNowRef.ToString("0.##")
                          + " contentRightNow(ref)=" + contentRightNowRef.ToString("0.##")
                          + " childrenExtentRight=" + childrenRight.ToString("0.##")
                          + " childrenExtentLeft=" + childrenLeft.ToString("0.##")
                          + " rightPersonFound=" + rightPersonFound
                          + " visibleRight=" + visibleRight.ToString("0.##"));
            sb.AppendLine("[scroll-now] 当前画面：最后一张卡右边缘(ref x)=" + lastCardRightNowRef.ToString("0.##")
                          + "（可与截图对照；若此时已滚到最右端，该值应接近 visibleRight）");

            var scrollRange = Mathf.Max(0f, contentW - viewportW);
            sb.AppendLine("[scroll-range] 可滚范围=" + scrollRange.ToString("0.##")
                          + "（= contentRectW - viewportW；屏幕最右端时 content 右边缘对齐 viewport 右边缘）");

            // 右侧被 Right Person 遮住、永远无法进入可视区的“卡位数量”
            var hiddenBehindPanel = Mathf.Max(0f, viewportW - visibleRight);
            var hiddenSlots = Mathf.CeilToInt(hiddenBehindPanel / pitch);
            sb.AppendLine("[scroll-visible] 卡片可见右边界=" + visibleRight.ToString("0.##")
                          + "（Right Person 左边缘；viewport 宽=" + viewportW.ToString("0.##") + "）"
                          + " → viewport 右侧有 " + hiddenBehindPanel.ToString("0.##") + "px 被面板盖住"
                          + " ≈ " + hiddenSlots + " 张卡位");

            // 关键自检 1：滚动行程是否足够让最后一张卡进入可见区
            // 最右端时 content 左边缘 = viewport 宽 - content 宽；卡片末端是 content 本地坐标
            var contentLeftAtMaxRef = viewportW - contentW;
            var lastCardRightAtMaxRef = contentLeftAtMaxRef + childrenRight;
            var travelDeficit = lastCardRightAtMaxRef - visibleRight;
            var travelDeficitCards = travelDeficit > 0.5f ? Mathf.CeilToInt(travelDeficit / pitch) : 0;
            var scrollMode = _diagMode.IndexOf("scroll", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!scrollMode)
            {
                travelDeficit = 0f;
                travelDeficitCards = 0;
            }

            sb.AppendLine("[logic-travel] 滚到最右端时：content左边缘=" + contentLeftAtMaxRef.ToString("0.##")
                          + " + 卡片末端=" + childrenRight.ToString("0.##")
                          + " → 最后一张卡右边缘会停在 x=" + lastCardRightAtMaxRef.ToString("0.##")
                          + "；可见右边界=" + visibleRight.ToString("0.##")
                          + "；差=" + travelDeficit.ToString("0.##") + "px"
                          + " ≈ " + travelDeficitCards + " 张卡看不到"
                          + (scrollMode ? "" : "（当前未启用滚动：fit/vanilla 模式，不适用）"));
            checks.Add("CHECK1 滚动行程 "
                       + (!scrollMode ? "PASS（未启用滚动，不适用）"
                           : (travelDeficit > 0.5f ? "FAIL" : "PASS"))
                       + "：最右端最后一张卡右边缘=" + lastCardRightAtMaxRef.ToString("0.##")
                       + "，可见右边界=" + visibleRight.ToString("0.##")
                       + "，差 " + travelDeficit.ToString("0.##") + "px"
                       + (travelDeficitCards > 0 ? "（≥ " + travelDeficitCards + " 张卡永远在 Right Person 面板后）" : ""));

            // 关键自检 2：内容宽度是否比卡片实际末端多出一截（→ 滚到底出现空白段）
            var blankTail = contentW - childrenRight;
            var blankTailSlots = blankTail / pitch;
            sb.AppendLine("[logic-blank] content宽=" + contentW.ToString("0.##")
                          + " - 卡片实际末端=" + childrenRight.ToString("0.##")
                          + " → 内容右侧空白=" + blankTail.ToString("0.##") + "px"
                          + " ≈ " + blankTailSlots.ToString("0.##") + " 张卡位");
            var blankTailBad = blankTail > pitch * 0.8f;
            checks.Add("CHECK2 内容宽度 vs 卡片末端 " + (blankTailBad ? "FAIL" : "PASS")
                       + "：空白尾=" + blankTail.ToString("0.##") + "px ≈ " + blankTailSlots.ToString("0.##") + " 张卡位"
                       + (blankTail < -pitch * 0.8f ? "（负值=卡片超出内容宽度，属原版溢出，通常无害）" : ""));

            // 关键自检 3：计数一致性
            var countsOk = _diagCardsSnapshotChildCount == _diagCardsSnapshotActive
                           && (_diagCardsSnapshotSpawned < 0 || _diagCardsSnapshotSpawned == _diagCardsSnapshotChildCount)
                           && _diagCardsSnapshotNonCard == 0;
            checks.Add("CHECK3 计数一致 " + (countsOk ? "PASS" : "FAIL")
                       + "：childCount=" + _diagCardsSnapshotChildCount
                       + " 实际排版(active)=" + _diagCardsSnapshotActive
                       + " inactive=" + _diagCardsSnapshotInactive
                       + " nonCard=" + _diagCardsSnapshotNonCard
                       + " spawnedCards=" + _diagCardsSnapshotSpawned
                       + " spawned异常=" + _diagCardsSnapshotSpawnedBad);

            // 关键自检 4：movementType（v1.1.0：期望值随 Use Elastic 推导；界面A 自 v4.2.4 起该项真实生效）
            var expectedMovement = ModConfig.Elastic
                ? ScrollRect.MovementType.Elastic
                : ScrollRect.MovementType.Clamped;
            var movement = _scroll == null ? "无ScrollRect" : _scroll.movementType.ToString();
            var movementOk = _scroll == null
                ? !scrollMode
                : _scroll.movementType == expectedMovement;
            checks.Add("CHECK4 movementType " + (movementOk ? "PASS" : "FAIL")
                       + "：" + movement
                       + (_scroll == null
                           ? (scrollMode ? "（应为滚动模式但没有 ScrollRect！）" : "（未启用滚动，不适用）")
                           : (movementOk
                               ? "（与当前配置一致；期望 " + expectedMovement + "，UseElastic=" + ModConfig.Elastic + "）"
                               : "（与当前配置不一致；期望 " + expectedMovement + "，UseElastic=" + ModConfig.Elastic
                                 + "，说明有外部代码在改它）")));

            // 关键自检 5：内容锚点是否被外部改
            var anchorsOk = _snapshotValid
                            && Approximately(_content.anchorMin, _contentSnapshot.AnchorMin)
                            && Approximately(_content.anchorMax, _contentSnapshot.AnchorMax)
                            && Approximately(_content.pivot, _contentSnapshot.Pivot);
            checks.Add("CHECK5 内容锚点 " + (anchorsOk ? "PASS" : "FAIL")
                       + "：now aMin=" + FmtV(_content.anchorMin) + " aMax=" + FmtV(_content.anchorMax)
                       + " pivot=" + FmtV(_content.pivot)
                       + "；snapshot aMin=" + FmtV(_contentSnapshot.AnchorMin)
                       + " aMax=" + FmtV(_contentSnapshot.AnchorMax)
                       + " pivot=" + FmtV(_contentSnapshot.Pivot));

            // 关键自检 6：ScrollRect 数量与目标
            var srCount = _content.GetComponents<ScrollRect>().Length;
            var expectedViewport = _kaenoFallback && _kaenoViewportRt != null
                ? (RectTransform)_kaenoViewportRt
                : _contentSnapshot.Parent as RectTransform;
            var targetOk = _scroll == null
                ? (!scrollMode && srCount == 0)
                : (srCount == 1 && expectedViewport != null && _scroll.viewport == expectedViewport && _scroll.content == _content);
            checks.Add("CHECK6 ScrollRect 数量/目标 " + (targetOk ? "PASS" : "FAIL")
                       + "：TraderCards 上=" + srCount + " 个"
                       + "，viewport=(" + (_scroll == null ? "-" : NameOf(_scroll.viewport)) + ")"
                       + "，content=(" + (_scroll == null ? "-" : NameOf(_scroll.content)) + ")"
                       + "（期望 1 个，viewport=" + NameOf(expectedViewport) + "，content=TraderCards）");

            // 关键自检 7：单位一致性（v4.2.4 已把 ComputeUsableWidth 改为参考像素，检查它是否与可见宽度一致）
            var usableNowUi = ComputeUsableWidth(Mathf.Max(_diagCardsSnapshotCardW, 1f));
            var usableAtRestRef = rightPersonFound ? visibleRight : viewportW;
            var usableAtApplyRef = _diagUsableWidth;
            var usableOk = Mathf.Abs(usableAtApplyRef - usableAtRestRef) <= 2f;
            checks.Add("CHECK7 可见宽度/单位一致性 " + (usableOk ? "PASS" : "FAIL")
                       + "：ApplyCore使用(参考像素)=" + usableAtApplyRef.ToString("0.##")
                       + " 静止可见宽度(参考像素)=" + usableAtRestRef.ToString("0.##")
                       + " 当前重算=" + usableNowUi.ToString("0.##")
                       + "（v4.2.4 起 ComputeUsableWidth 返回参考像素，不再受 Canvas 缩放/滚动位置影响）");

            // 关键自检 8：重应用时滚动位置是否被保留（v4.2.4）
            var appliedNowUi = usableNowUi;
            sb.AppendLine("[logic-scrollpos] 当前 normalized=" + (_scroll == null ? "-" : FmtV(_scroll.normalizedPosition))
                          + "；配置重应用会 restore 内容位置，v4.2.4 已把重应用前的 normalized 记录下来并在应用后恢复。");
            if (Mathf.Abs(appliedNowUi - usableAtRestRef) > 2f)
            {
                sb.AppendLine("[logic-usable-warn] 当前重算的可见宽度与 ApplyCore 时不一致（" + appliedNowUi.ToString("0.##")
                              + " vs " + usableAtRestRef.ToString("0.##") + "）：请检查 Right Person/Container 几何是否被其它模组改动。");
            }

            sb.AppendLine("[verdict] 自检结论:");
            foreach (var check in checks)
            {
                sb.AppendLine("  " + check);
            }

            sb.AppendLine("[interpretation]");
            sb.AppendLine("  - CHECK1 FAIL：滚到最右端时最后 " + travelDeficitCards + " 张卡仍藏在 Right Person 面板（x≥" + visibleRight.ToString("0.##") + "）后面，永远滑不出来 → 对应\"滑到底还缺商人\"。");
            sb.AppendLine("  - CHECK2 FAIL：内容右侧比卡片实际末端多 " + blankTail.ToString("0.##") + "px（≈" + blankTailSlots.ToString("0.##") + " 张卡位），滚到底会看到一大段空白 → 对应\"拉出一大段空白\"。");
            sb.AppendLine("  - CHECK3 FAIL：内容宽度按 CountCards() 的较大值计算，但实际参与排版的是 active 子节点数；差值会变成白卡位。");
            sb.AppendLine("  - CHECK4 FAIL：movementType 与当前配置不一致（UseElastic=" + ModConfig.Elastic + " → 期望 " + expectedMovement + "），说明有外部代码在改它；判定只看“是否与当前配置一致”，Elastic 的越界回弹属当前配置的正常行为。");
            sb.AppendLine("  - CHECK5/CHECK6 FAIL：TraderCards 的锚点/ScrollRect 被其它模组改过（本模组只在 打开界面/配置变更 时重新写回）。");
            sb.AppendLine("  - CHECK7 FAIL：ApplyCore 采用的可见宽度与静止几何重新计算的不一致（Right Person/Container 位置可能被其它模组改动，或测量时机不对）。");
        }

        private void AppendLogicCheck(StringBuilder sb, List<string> checks)
        {
            // 汇总（Actual verdict lines already appended in AppendScrollInfo；这里只补一条解释）
            if (checks.Count == 0)
            {
                sb.AppendLine("[verdict] 无 CHECK（界面A 未启用滚动或没有卡片）");
                return;
            }

            var failCount = 0;
            foreach (var check in checks)
            {
                if (check.IndexOf("FAIL", StringComparison.Ordinal) >= 0)
                {
                    failCount++;
                }
            }

            sb.AppendLine("[verdict-summary] FAIL=" + failCount + "/" + checks.Count);
        }

        private void AppendHistory(StringBuilder sb)
        {
            if (_diagHistory.Count == 0)
            {
                return;
            }

            sb.AppendLine("[history] 最近 " + _diagHistory.Count + " 次 ApplyCore：");
            foreach (var entry in _diagHistory)
            {
                sb.AppendLine("  " + entry);
            }
        }

        // ---------------------------------------------------------------- 小工具

        private static string NameOf(UnityEngine.Object obj)
        {
            try
            {
                return obj == null ? "<null>" : obj.name;
            }
            catch
            {
                return "<err>";
            }
        }

        private static string RectLine(RectTransform rect)
        {
            if (rect == null)
            {
                return "<null>";
            }

            try
            {
                return UILayoutTools.GetPath(rect) + " " + UILayoutTools.RectOf(rect);
            }
            catch
            {
                return rect.name;
            }
        }

        private static float MinWorldX(RectTransform rect)
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

        private static float MaxWorldX(RectTransform rect)
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

        private static bool TryGetLocalXRange(RectTransform target, RectTransform space, out float minX, out float maxX)
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

        private static string AncestorMasks(RectTransform start)
        {
            try
            {
                var sb = new StringBuilder();
                var current = start;
                for (var i = 0; i < 10 && current != null; i++)
                {
                    var mask2D = current.GetComponent<RectMask2D>();
                    var mask = current.GetComponent<Mask>();
                    if (mask2D != null || mask != null)
                    {
                        if (sb.Length > 0)
                        {
                            sb.Append(" | ");
                        }

                        sb.Append(UILayoutTools.GetPath(current)).Append(": ")
                            .Append(mask2D != null ? "RectMask2D " : string.Empty)
                            .Append(mask != null ? "Mask" : string.Empty);
                    }

                    current = current.parent as RectTransform;
                }

                return sb.Length == 0 ? "<none>" : sb.ToString();
            }
            catch
            {
                return "<err>";
            }
        }

        private static string ListComponentsOfType<T>(GameObject go) where T : Component
        {
            try
            {
                var components = go.GetComponents<T>();
                var sb = new StringBuilder();
                foreach (var component in components)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(component == null ? "<null>" : component.GetType().Name + "@" + component.GetInstanceID());
                }

                return sb.Length == 0 ? "<none>" : sb.ToString();
            }
            catch
            {
                return "<err>";
            }
        }

        private static string BoolValue(ConfigEntry<bool> entry)
        {
            try
            {
                return entry == null ? "<null>" : (entry.Value ? "true" : "false");
            }
            catch
            {
                return "?";
            }
        }

        private static string FmtV(Vector2 value)
        {
            return "[" + value.x.ToString("0.##") + "," + value.y.ToString("0.##") + "]";
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.01f && Mathf.Abs(a.y - b.y) < 0.01f;
        }
    }
}
