using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    // Implemented by every VStack host (vStackPanel / vStackRoundedPanel / VStackForm) so the shared engine
    // and the shared designer can drive any of them without caring what the base control is.
    internal interface IVStackHost
    {
        VStackEngine VStackEngine { get; }
    }

    // The control-driven vertical stacking engine, shared by all VStack hosts so the layout
    // logic lives in exactly one place. The host owns the chrome (plain panel / rounded panel /
    // rounded form); the engine owns the stacking.
    //
    // It is CONTROL-AGNOSTIC — it only ever reads generic Control members:
    //   * order   = TabIndex
    //   * height  = each control's own Height (drag it; layout just reads it back)
    //   * spacing = each control's Margin (top/bottom for gaps, left/right to inset)
    //   * width   = filled to the host's content area
    //
    // SPRING: Spring is a comma list of control NAMES that absorb spare vertical space
    // ("PreviewPanel" or "row2,row5" — multiple springs split the spare equally). Names, not
    // TabIndexes: TabIndex is volatile during InitializeComponent (ControlCollection.Add
    // auto-assigns transient values before the serialized ones apply), which let mid-init
    // layouts spring the wrong control. A control's Name is "" until its own property block
    // runs, so at worst a mid-init layout springs nothing — harmless. A spring's height is
    // computed PURELY from leftover space — never from its own Height — so it can't ratchet
    // and is reload-safe (no baseline needed).
    //
    // ⚠ DO NOT put SIZE-NEGOTIATING controls directly in a rule row.
    // The engine is a ONE-WAY system: it hands each row a height and expects obedience. It
    // breaks the moment a child negotiates its size BACK UP the tree, because then two layout
    // engines are chasing each other — and two greedy layout engines negotiating is just a
    // polite name for an infinite loop / flicker cascade. The test for a landmine is simple:
    // "does this control demand a size upward based on its own content or viewport?" If yes,
    // it does NOT belong as a raw row. Known offenders:
    //   * FlowLayoutPanel / any AutoScroll container — toggling a scrollbar changes its client
    //     area and it reports the new footprint upward (this is the dl_Batch download flicker;
    //     confirmed 2026-07-12). NEVER make an AutoScroll panel a spring.
    //   * Label / LinkLabel with AutoSize = true — remeasures width/height on every Text change.
    //   * RichTextBox with custom auto-height code (reading text metrics to grow vertically) —
    //     the native RichEdit reflow fires size-update requests upward mid-wrap.
    //   * DataGridView with AutoSizeRowsMode / AutoSizeColumnsMode = AllCells/DisplayedCells.
    //   * TableLayoutPanel with percentage rows + AutoSize (1px oscillation vs a resizing parent).
    //   * TreeView / ListView during rapid expand/load (native scroll notifications).
    //   * A NESTED custom-chrome control as a row (e.g. a vStackRoundedPanel that recomputes its
    //     rounded GraphicsPath on size/layout) — if its own layout mutates its bounds/margins it
    //     can form a microscopic feedback loop with this engine. Nest a plain vStackPanel instead,
    //     or firewall it (see below). A vStackRoundedPanel welds chrome + layout into one HWND, so
    //     a child expanding forces the whole panel to resize + geometry-repaint → cascade upward.
    // THE FIX = a FIREWALL: drop the offender inside a plain, fixed-size Panel (AutoScroll = false
    // on the wrapper) and make THAT panel the row / the spring. The wrapper absorbs the child's
    // upward size demands and reports a rock-solid, unchanging height to the engine — the noise
    // dies at the wall. A vStack is great WITHIN one update-domain; it is the wrong thing to span
    // a region that fights back. (Corollary: a live list + a live status + collapsible panels
    // should not share one vStack at all — split them into sibling control trees.)
    internal sealed partial class VStackEngine
    {
        private readonly Control _host;
        private bool _isApplyingLayout;
        private readonly Dictionary<Control, float> _baseFontSizes = new();
        private readonly Dictionary<Control, FontSignature> _fontBaselines = new();
        private readonly Dictionary<Control, FontSignature> _layoutFontTargets = new();
        private Form? _dpiSourceForm;
        private bool _dpiLayoutPending;
        private bool _dpiBaselineGuardActive;
        private string? _spring;
        private bool _springEnabled = true;
        private int _springMinimumHeight = 36;
        private bool _stepDownFontAtHighDpi;
        private readonly HashSet<string> _springControlNames = new(StringComparer.OrdinalIgnoreCase);

        // Per-spring vertical bias (px) applied by FlatSeparator splitters. Session-live (not
        // serialised): default 0 for every spring == the original even split.
        private readonly Dictionary<Control, int> _springBias = new();
        private readonly HashSet<string> _excludedControlNames = new(StringComparer.OrdinalIgnoreCase);
        private string? _excludedControls;
        private readonly VStackRuleCollection _rules = new();
        private readonly Func<Padding>? _contentPaddingProvider;

        internal Control Host => _host;

        public VStackEngine(Control host, Func<Padding>? contentPaddingProvider = null)
        {
            _host = host;
            _contentPaddingProvider = contentPaddingProvider;
            _rules.Owner = this;
            _host.HandleCreated += Host_HandleCreatedOrParentChanged;
            _host.ParentChanged += Host_HandleCreatedOrParentChanged;
            _host.Disposed += Host_Disposed;
        }

        // Authoring rules (spawn / order / margins / collapse). Layout still reads the live
        // controls — rules push their data down to the controls; see VStackEngine.Rules.cs.
        public VStackRuleCollection Rules => _rules;

        private bool IsInDesignMode()
        {
            return _host.Site?.DesignMode == true
                || _host.Parent?.Site?.DesignMode == true
                || System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime;
        }

        public string? Spring
        {
            get => _spring;
            set
            {
                if (string.Equals(_spring, value, StringComparison.Ordinal))
                {
                    return;
                }

                _spring = value;
                ParseSpring();
                SyncRuleSpringFlagsFromPanel();   // the OTHER direction — see below
                _host.PerformLayout();
            }
        }

        public bool SpringEnabled
        {
            get => _springEnabled;
            set
            {
                if (_springEnabled == value)
                {
                    return;
                }

                _springEnabled = value;
                _host.PerformLayout();
            }
        }

        public int SpringMinimumHeight
        {
            get => _springMinimumHeight;
            set
            {
                int newValue = Math.Max(0, value);
                if (_springMinimumHeight == newValue)
                {
                    return;
                }

                _springMinimumHeight = newValue;
                _host.PerformLayout();
            }
        }

        public bool StepDownFontAtHighDpi
        {
            get => _stepDownFontAtHighDpi;
            set
            {
                if (_stepDownFontAtHighDpi == value)
                {
                    return;
                }

                _stepDownFontAtHighDpi = value;
                _host.PerformLayout();
            }
        }

        public string? ExcludedControls
        {
            get => _excludedControls;
            set
            {
                if (string.Equals(_excludedControls, value, StringComparison.Ordinal))
                {
                    return;
                }

                _excludedControls = value;
                ParseExcludedControls();
                _host.PerformLayout();
            }
        }

        // Hook a newly added child so dragging its height (or hiding it) reflows the stack.
        public void AttachChild(Control child)
        {
            EnsureDesignerChangeServiceHooked();
            AttachToParentFormDpiChanged();
            child.SizeChanged += Child_Changed;
            child.VisibleChanged += Child_Changed;
            child.MarginChanged += Child_Changed;
            child.FontChanged += Child_FontChanged;
            CaptureFontBaseline(child);
            RequestAdopt(child); // design-time: a control dropped in gets a rule + next index
            Layout();
        }

        public void DetachChild(Control child)
        {
            child.SizeChanged -= Child_Changed;
            child.VisibleChanged -= Child_Changed;
            child.MarginChanged -= Child_Changed;
            child.FontChanged -= Child_FontChanged;
            _baseFontSizes.Remove(child);
            _fontBaselines.Remove(child);
            _layoutFontTargets.Remove(child);
            if (IsInDesignMode() && !_isSyncingRules)
            {
                QueueRuleRemovalForDeletedChild(child); // confirm after designer undo/redo settles
            }

            Layout();
        }

        // The live controls currently acting as springs (matched by Name). Used by the
        // designer's spacing action to know whether a spring fills the stack to the edge.
        public IReadOnlyList<Control> GetSpringControls() =>
            _host.Controls.Cast<Control>().Where(c => !IsLayoutExcluded(c) && IsSpring(c)).ToArray();

        // Lay the host's children out top-to-bottom inside its content area (ClientSize minus
        // Padding). Hosts with extra chrome (rounded bars etc.) pass an enlarged content padding.
        public void Layout() => Layout(_contentPaddingProvider?.Invoke() ?? _host.Padding);

        public void Layout(Padding padding)
        {
            if (_isApplyingLayout)
            {
                return;
            }

            Control[] items = _host.Controls
                .Cast<Control>()
                .Where(c => c.Visible && !IsLayoutExcluded(c))
                .OrderBy(c => c.TabIndex)
                .ThenBy(c => c.Top)
                .ToArray();

            if (items.Length == 0)
            {
                return;
            }

            ApplyDpiFontStep(items);

            int availableWidth = Math.Max(0, _host.ClientSize.Width - padding.Left - padding.Right);
            int availableHeight = Math.Max(0, _host.ClientSize.Height - padding.Top - padding.Bottom);

            Dictionary<Control, int>? springHeights = BuildSpringHeights(items, availableHeight);

            int y = padding.Top;

            _isApplyingLayout = true;
            try
            {
                foreach (Control item in items)
                {
                    int left = padding.Left + Math.Max(0, item.Margin.Left);
                    int width = Math.Max(0, availableWidth - Math.Max(0, item.Margin.Left) - Math.Max(0, item.Margin.Right));

                    int height = springHeights != null && springHeights.TryGetValue(item, out int sh)
                        ? sh
                        : item.Height;

                    // A collapsed row (height 0) takes no vertical space at all — not even its top
                    // or bottom margin — so a hidden row leaves no gap behind it.
                    if (height <= 0)
                    {
                        Rectangle collapsedBounds = new Rectangle(left, y, width, 0);
                        if (item.Bounds != collapsedBounds)
                        {
                            item.Bounds = collapsedBounds;
                        }
                        continue;
                    }

                    y += Math.Max(0, item.Margin.Top);

                    Rectangle bounds = new Rectangle(left, y, width, height);
                    if (item.Bounds != bounds)
                    {
                        item.Bounds = bounds;
                    }

                    y += height + Math.Max(0, item.Margin.Bottom);
                }
            }
            finally
            {
                _isApplyingLayout = false;
            }
        }

        /// <summary>
        /// Gives every ADJUSTABLE row (spring or piston) an equal share of what's left after the
        /// fixed rows and all the margins — the vertical mirror of the ARP's
        /// EqualizeAdjustableItems, so a split view opens evenly on any monitor instead of
        /// inheriting whatever pixel heights happened to be authored.
        ///
        /// Springs alone can't do this. Two springs DO come out equal (equal floors, equal share
        /// of the spare), but a piston keeps a fixed height by definition, so a piston/spring
        /// pair stays as lopsided as it was authored. Setting the piston's total is what evens
        /// it up; the spring then lands on the same number by absorbing the remainder.
        ///
        /// In memory only — nothing is written to the designer file. Returns the height each
        /// adjustable row was given, or 0 when there was nothing to do.
        /// </summary>
        public int EqualizeAdjustableRows()
        {
            Control[] visible = _host.Controls
                .Cast<Control>()
                .Where(c => c.Visible && !IsLayoutExcluded(c))
                .OrderBy(c => c.TabIndex)
                .ThenBy(c => c.Top)
                .ToArray();

            if (visible.Length == 0)
            {
                return 0;
            }

            Control[] adjustable = visible.Where(IsAdjustableRow).ToArray();
            if (adjustable.Length == 0)
            {
                return 0;
            }

            Padding padding = _host.Padding;
            int available = Math.Max(0, _host.ClientSize.Height - padding.Top - padding.Bottom);

            // Everything that is NOT adjustable keeps the height it has; only the remainder is
            // shared out. That leaves separators, spacers and fixed rows exactly as authored.
            foreach (Control child in visible)
            {
                available -= Math.Max(0, child.Margin.Top) + Math.Max(0, child.Margin.Bottom);
                if (!adjustable.Contains(child))
                {
                    available -= Math.Max(0, child.Height);
                }
            }

            if (available <= 0)
            {
                return 0;
            }

            int each = available / adjustable.Length;
            int remainder = available % adjustable.Length;

            for (int i = 0; i < adjustable.Length; i++)
            {
                // The remainder goes out a pixel at a time to the topmost rows, so the stack
                // fills exactly instead of leaving a stray pixel at the bottom.
                int height = each + (i < remainder ? 1 : 0);
                Control child = adjustable[i];
                child.Height = height;

                // Only pistons need the rule updating. A spring's height is recomputed from its
                // floor plus its share on every pass, so writing an ExpandedHeight for one would
                // be a number nothing ever reads.
                if (!IsSpring(child) && !string.IsNullOrWhiteSpace(child.Name))
                {
                    SetRowExpandedHeight(child.Name, height);
                }
            }

            _host.PerformLayout();
            _host.Invalidate(true);
            return each;
        }

        /// <summary>A row the equaliser is allowed to resize: spring or piston, and not collapsed.</summary>
        private bool IsAdjustableRow(Control control)
            => !IsRowCollapsed(control)
               && (IsSpring(control)
                   || (!string.IsNullOrWhiteSpace(control.Name) && GetRowIsPiston(control.Name)));

        private float GetScaleFactor()
        {
            int dpi = GetCurrentDpi();
            return dpi > 0 ? dpi / 96f : 1f;
        }

        private int GetCurrentDpi()
        {
            if (_host.IsHandleCreated)
            {
                try
                {
                    uint dpi = GetDpiForWindow(_host.Handle);
                    if (dpi > 0)
                    {
                        return (int)dpi;
                    }
                }
                catch (EntryPointNotFoundException)
                {
                }
                catch (DllNotFoundException)
                {
                }
            }

            return _host.DeviceDpi > 0 ? _host.DeviceDpi : 96;
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private void ApplyDpiFontStep(Control[] children)
        {
            if (IsInDesignMode())
            {
                return;
            }

            bool stepEnabled = ControlSettings.StepDownHighDpiFonts
                || _stepDownFontAtHighDpi;
            int reduction = (GetScaleFactor() >= 1.5f && stepEnabled) ? 2 : 0;

            bool previous = _isApplyingLayout;
            _isApplyingLayout = true;
            try
            {
                foreach (Control child in children)
                {
                    if (!ShouldStepDirectChildFont(child))
                    {
                        continue;
                    }

                    CaptureFontBaseline(child);
                    float basePoints = _baseFontSizes[child];

                    float targetPoints = Math.Max(1f, basePoints - reduction);
                    ApplySteppedFont(child, targetPoints);
                }
            }
            finally
            {
                _isApplyingLayout = previous;
            }
        }

        private static bool ShouldStepDirectChildFont(Control child)
        {
            if (child is IVStackHost
                || child is AdaptiveRowPanel
                || child is VirtualizingAdaptiveRowPanel   // steps its own children
                || child is vStackSpacer)
            {
                return false;
            }

            if (child is IAdaptiveRowPanelItem)
            {
                return true;
            }

            return child is not Panel && !child.HasChildren;
        }

        private void CaptureFontBaseline(Control child)
        {
            if (!ShouldStepDirectChildFont(child))
            {
                return;
            }

            float? ruleBaseline = GetRuleFontBaselineSize(child);
            if (ruleBaseline.HasValue)
            {
                _baseFontSizes[child] = ruleBaseline.Value;
                _fontBaselines[child] = FontSignature.From(child.Font);
                return;
            }

            if (_baseFontSizes.ContainsKey(child))
            {
                return;
            }

            _baseFontSizes[child] = child.Font.SizeInPoints;
            _fontBaselines[child] = FontSignature.From(child.Font);
        }

        private void ApplySteppedFont(Control child, float targetPoints)
        {
            if (Math.Abs(child.Font.SizeInPoints - targetPoints) <= 0.05f)
            {
                return;
            }

            Font steppedFont = new Font(child.Font.FontFamily, targetPoints, child.Font.Style, GraphicsUnit.Point);
            _layoutFontTargets[child] = FontSignature.From(steppedFont);
            child.Font = steppedFont;
        }

        private bool IsLayoutFontTarget(Control child)
        {
            return _layoutFontTargets.TryGetValue(child, out FontSignature target)
                && target.Equals(FontSignature.From(child.Font));
        }

        private bool IsDpiScaledBaselineFont(Control child)
        {
            if (!_baseFontSizes.TryGetValue(child, out float basePoints)
                || !_fontBaselines.TryGetValue(child, out FontSignature baseline)
                || !baseline.HasSameFaceAs(child.Font))
            {
                return false;
            }

            float currentPoints = child.Font.SizeInPoints;
            if (currentPoints <= basePoints + 0.5f)
            {
                return false;
            }

            float scale = Math.Max(1f, GetScaleFactor());
            if (scale < 1.1f)
            {
                return false;
            }

            float expectedScaledPoints = basePoints * scale;
            return currentPoints <= expectedScaledPoints + 1.5f;
        }

        private readonly struct FontSignature : IEquatable<FontSignature>
        {
            private readonly string _familyName;
            private readonly float _sizeInPoints;
            private readonly FontStyle _style;
            private readonly GraphicsUnit _unit;

            private FontSignature(Font font)
            {
                _familyName = font.FontFamily.Name;
                _sizeInPoints = font.SizeInPoints;
                _style = font.Style;
                _unit = font.Unit;
            }

            public static FontSignature From(Font font) => new FontSignature(font);

            public bool HasSameFaceAs(Font font)
            {
                return string.Equals(_familyName, font.FontFamily.Name, StringComparison.Ordinal)
                    && _style == font.Style
                    && _unit == font.Unit;
            }

            public bool Equals(FontSignature other)
            {
                return string.Equals(_familyName, other._familyName, StringComparison.Ordinal)
                    && Math.Abs(_sizeInPoints - other._sizeInPoints) <= 0.05f
                    && _style == other._style
                    && _unit == other._unit;
            }

            public override bool Equals(object? obj) => obj is FontSignature other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(_familyName, _sizeInPoints, _style, _unit);
        }

        private int GetSpringBias(Control spring) =>
            _springBias.TryGetValue(spring, out int bias) ? bias : 0;

        // The spring a FlatSeparator splitter at this position would resize, or null if there's
        // nothing to absorb (used to decide whether the splitter is live / shows a resize cursor).
        // Prefers the nearest spring ABOVE (dragging down grows it); falls back to the nearest
        // spring below with inverted sign so a splitter above the only spring still works.
        private Control? FindSplitterTargetSpring(Control splitter, out int sign)
        {
            sign = 1;
            Control[] items = _host.Controls
                .Cast<Control>()
                .Where(c => c.Visible && !IsLayoutExcluded(c))
                .OrderBy(c => c.TabIndex)
                .ThenBy(c => c.Top)
                .ToArray();

            int idx = Array.IndexOf(items, splitter);
            if (idx < 0)
            {
                return null;
            }

            for (int i = idx - 1; i >= 0; i--)
            {
                if (IsSpring(items[i])) { sign = 1; return items[i]; }
            }
            for (int i = idx + 1; i < items.Length; i++)
            {
                if (IsSpring(items[i])) { sign = -1; return items[i]; }
            }
            return null;
        }

        // The PISTON a splitter at this position would resize: the nearest row ABOVE whose rule
        // is marked IsPiston (and not collapsed). A piston keeps a FIXED height the user sets;
        // dragging the splitter adjusts that height directly and the spring absorbs the
        // difference. Pistons take precedence over the legacy spring-bias drag.
        private Control? FindSplitterPiston(Control splitter) =>
            FindSplitterPiston(splitter, out _, out _);

        // collapsedPiston reports the "piston exists but is collapsed" case separately from
        // "no piston at all": a collapsed piston DISABLES the splitter entirely (by design —
        // there's nothing sensible to resize), whereas no piston falls back to the legacy
        // spring-bias drag for stacks that predate pistons.
        // Searches ABOVE first, then BELOW, carrying a sign the way FindSplitterTargetSpring
        // already does: +1 for a piston above (drag down grows it), -1 for one below (drag down
        // shrinks it). One-directional search meant a splitter could only ever resize a piston
        // above it, so a piston in the LAST row — the batch status/debug pane — could only be
        // driven by a splitter sitting below it, at the very bottom edge of the panel.
        private Control? FindSplitterPiston(Control splitter, out bool collapsedPiston, out int sign)
        {
            collapsedPiston = false;
            sign = 1;

            Control[] items = _host.Controls
                .Cast<Control>()
                .Where(c => c.Visible && !IsLayoutExcluded(c))
                .OrderBy(c => c.TabIndex)
                .ThenBy(c => c.Top)
                .ToArray();

            int idx = Array.IndexOf(items, splitter);
            if (idx < 0)
            {
                return null;
            }

            for (int i = idx - 1; i >= 0; i--)
            {
                VStackRule? rule = FindRuleByControlName(items[i].Name);

                // Same adjacency rule as the downward walk. Without it a splitter reaches PAST
                // the spring next to it to claim a distant piston: with vStackQuick a piston at
                // the top and the status pane a piston at the bottom, the pane's own divider
                // walked up over the spring, grabbed the quick row, and dragging it resized the
                // wrong end of the panel.
                if (rule is { IsSpring: true })
                {
                    break;
                }

                if (rule is { IsPiston: true })
                {
                    if (rule.IsCollapsed)
                    {
                        collapsedPiston = true;
                        return null;
                    }

                    sign = 1;
                    return items[i];
                }
            }

            for (int i = idx + 1; i < items.Length; i++)
            {
                VStackRule? rule = FindRuleByControlName(items[i].Name);

                // Stop at a SPRING. A splitter drives the piston it is ADJACENT to; reaching
                // across a spring to grab a distant piston is action at a distance — the user
                // drags a divider between A and B and something at the far end of the panel
                // moves. Concretely: the quick/actions divider would seize the status pane's
                // piston the moment the debug pane opened, while the divider actually touching
                // that pane sat inert. The upward walk is deliberately NOT gated this way —
                // it predates pistons and existing stacks rely on its reach.
                if (rule is { IsSpring: true })
                {
                    break;
                }

                if (rule is { IsPiston: true })
                {
                    if (rule.IsCollapsed)
                    {
                        collapsedPiston = true;
                        return null;
                    }

                    sign = -1;
                    return items[i];
                }
            }

            return null;
        }

        // True when a FlatSeparator splitter at this position has a piston or spring to resize.
        // A COLLAPSED piston kills the splitter outright (no cursor, no drag) rather than
        // falling through to the spring path — dragging a collapsed section makes no sense.
        public bool SplitterCanResize(Control splitter)
        {
            Control? piston = FindSplitterPiston(splitter, out bool collapsedPiston, out _);
            if (piston != null)
                return true;
            if (collapsedPiston)
                return false;

            return FindSplitterTargetSpring(splitter, out _) != null;
        }

        // Live splitter drag. Piston mode: set the piston row's height directly (serialised in
        // its rule, so a designer-set height is the startup default) and let the spring absorb.
        // Legacy mode: bias the nearest spring — kept for stacks without pistons.
        public void ApplySplitterDrag(Control splitter, int deltaY)
        {
            if (deltaY == 0)
            {
                return;
            }

            Control? piston = FindSplitterPiston(splitter, out bool collapsedPiston, out int pistonSign);
            if (piston != null)
            {
                ResizePiston(piston, deltaY * pistonSign);
                return;
            }

            if (collapsedPiston)
                return;   // collapsed piston: splitter is disabled, never spring-bias instead

            Control? target = FindSplitterTargetSpring(splitter, out int sign);
            if (target == null)
            {
                return;
            }

            // Biasing one spring by B only moves the boundary by B*(n-1)/n, because the remaining
            // spare re-splits evenly across all n springs. Scale by n/(n-1) so the divider tracks
            // the cursor 1:1 (and a preview-line drop lands exactly where the ghost was).
            int springCount = _host.Controls
                .Cast<Control>()
                .Count(c => c.Visible && !IsLayoutExcluded(c) && IsSpring(c));

            int applied = springCount > 1
                ? (int)Math.Round(deltaY * springCount / (double)(springCount - 1), MidpointRounding.AwayFromZero)
                : deltaY;

            int previousBias = GetSpringBias(target);
            int heightBefore = target.Height;

            _springBias[target] = previousBias + (sign * applied);
            _host.PerformLayout();

            // If the layout couldn't honour it (a spring bottomed out at its floor), roll the bias
            // back. Otherwise it keeps accumulating past the limit and you'd have to drag all the
            // way back through a dead zone before the divider responded again.
            if (target.Height == heightBefore)
            {
                _springBias[target] = previousBias;
            }
        }

        // Piston drag: clamp between the piston's own floor and what the springs can still give
        // up (they never contract past their floors), then write the new height through the rule
        // so it applies to the child AND persists as the rule's expanded height.
        private void ResizePiston(Control piston, int deltaY)
        {
            int floor = Math.Max(_springMinimumHeight, piston.MinimumSize.Height);

            int springHeadroom = _host.Controls
                .Cast<Control>()
                .Where(c => c.Visible && !IsLayoutExcluded(c) && IsSpring(c))
                .Sum(c => Math.Max(0, c.Height - SpringFloor(c)));

            int target = Math.Clamp(piston.Height + deltaY, floor, piston.Height + springHeadroom);
            if (target == piston.Height)
            {
                return;
            }

            SetRowExpandedHeight(piston.Name, target);
        }

        // Returns the computed height for each spring item, or null when there are no springs.
        private Dictionary<Control, int>? BuildSpringHeights(Control[] items, int availableHeight)
        {
            if (!_springEnabled)
            {
                return null;
            }

            Control[] springs = items.Where(IsSpring).ToArray();
            if (springs.Length == 0)
            {
                return null;
            }

            // Space taken by everything that isn't flexible: all margins, every non-spring item's
            // height, and each spring's floor. Whatever is left is the spare.
            int used = 0;
            foreach (Control item in items)
            {
                // A collapsed row (height 0) takes NO space in the positioning pass — not even
                // its margins (see the layout loop). It must not be charged for any here
                // either, or the spring pays for pixels nobody occupies and comes out short:
                // David's list lost exactly arpScanning's 3+2 margins (491 -> 486) with the
                // spring on, and every row below floated up by the same 5px.
                if (!IsSpring(item) && item.Height <= 0)
                {
                    continue;
                }

                used += Math.Max(0, item.Margin.Top) + Math.Max(0, item.Margin.Bottom);
                used += IsSpring(item) ? SpringFloor(item) : item.Height;
            }

            int spare = Math.Max(0, availableHeight - used);

            // Splitter biases shift px between springs; use the NET sum so a shrunk spring
            // (negative bias) returns its freed space to the pool and the others expand to fill.
            // Default bias 0 for every spring == the original even split, byte-for-byte.
            int totalBias = 0;
            foreach (Control spring in springs)
            {
                totalBias += GetSpringBias(spring);
            }

            int shared = Math.Max(0, spare - totalBias);
            int perSpring = shared / springs.Length;
            int remainder = shared % springs.Length;

            Dictionary<Control, int> result = new();
            foreach (Control spring in springs)
            {
                int extra = perSpring + (remainder > 0 ? 1 : 0);
                if (remainder > 0)
                {
                    remainder--;
                }

                result[spring] = SpringFloor(spring) + Math.Max(0, extra + GetSpringBias(spring));
            }

            return result;
        }

        // Collapse BEATS spring (David: "fix the vstack so you can collapse springs"). A
        // collapsed row whose rule says Spring used to keep springing — the allocator never
        // consulted collapse, so IsCollapsed on a spring row was silently ignored. Excluding
        // it here (the one choke point: allocation, positioning, splitter and piston all route
        // through IsSpring) means a collapsed spring takes 0 and the remaining springs absorb
        // the space — expand it again and it springs again.
        private bool IsSpring(Control control) =>
            !string.IsNullOrWhiteSpace(control.Name)
            && _springControlNames.Contains(control.Name)
            && !IsRowCollapsed(control);

        private bool IsRowCollapsed(Control control)
        {
            VStackRule? rule = control.Tag is string id && id.Length > 0
                ? Rules.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal))
                : null;
            rule ??= FindRuleByControlName(control.Name);
            return rule?.IsCollapsed == true;
        }

        private bool IsLayoutExcluded(Control control)
        {
            return !string.IsNullOrWhiteSpace(control.Name)
                && _excludedControlNames.Contains(control.Name);
        }

        // A spring never contracts below this: SpringMinimumHeight (default 36, standard ARP
        // height), or the child's own MinimumSize.Height if larger.
        private int SpringFloor(Control control) => Math.Max(_springMinimumHeight, control.MinimumSize.Height);

        private void Child_Changed(object? sender, EventArgs e)
        {
            if (_isApplyingLayout)
            {
                return;
            }

            if (sender is Control child)
            {
                CaptureRuleLayoutFromChild(child);
            }

            if (!_host.IsDisposed && !_host.Disposing)
            {
                Layout();
            }
        }

        private void Child_FontChanged(object? sender, EventArgs e)
        {
            if (_isApplyingLayout)
            {
                return;
            }

            if (sender is Control child)
            {
                if (IsLayoutFontTarget(child))
                {
                    return;
                }

                if (_dpiBaselineGuardActive && IsDpiScaledBaselineFont(child))
                {
                    QueueDpiLayoutRefresh();
                    return;
                }

                _baseFontSizes[child] = GetRuleFontBaselineSize(child) ?? child.Font.SizeInPoints;
                _fontBaselines[child] = FontSignature.From(child.Font);
                _layoutFontTargets.Remove(child);
            }

            if (!_host.IsDisposed && !_host.Disposing)
            {
                Layout();
            }
        }

        private void Host_HandleCreatedOrParentChanged(object? sender, EventArgs e)
        {
            AttachToParentFormDpiChanged();
            QueueDpiLayoutRefresh();
        }

        private void Host_Disposed(object? sender, EventArgs e)
        {
            UnhookDesignerChangeService();
            DetachFromParentFormDpiChanged();
        }

        private void AttachToParentFormDpiChanged()
        {
            Form? currentForm = _host.FindForm();
            if (ReferenceEquals(_dpiSourceForm, currentForm))
            {
                return;
            }

            DetachFromParentFormDpiChanged();
            _dpiSourceForm = currentForm;
            if (_dpiSourceForm != null)
            {
                _dpiSourceForm.DpiChanged += ParentForm_DpiChanged;
            }
        }

        private void DetachFromParentFormDpiChanged()
        {
            if (_dpiSourceForm != null)
            {
                _dpiSourceForm.DpiChanged -= ParentForm_DpiChanged;
                _dpiSourceForm = null;
            }
        }

        private void ParentForm_DpiChanged(object? sender, DpiChangedEventArgs e)
        {
            _dpiBaselineGuardActive = true;
            QueueDpiLayoutRefresh();
        }

        private void QueueDpiLayoutRefresh()
        {
            if (_host.IsDisposed || _host.Disposing)
            {
                return;
            }

            Layout();

            if (_dpiLayoutPending || !_host.IsHandleCreated)
            {
                if (!_host.IsHandleCreated)
                {
                    _dpiBaselineGuardActive = false;
                }

                return;
            }

            _dpiLayoutPending = true;
            try
            {
                _host.BeginInvoke(new Action(() =>
                {
                    _dpiLayoutPending = false;
                    if (!_host.IsDisposed && !_host.Disposing)
                    {
                        Layout();
                    }

                    _dpiBaselineGuardActive = false;
                }));
            }
            catch (InvalidOperationException)
            {
                _dpiLayoutPending = false;
                _dpiBaselineGuardActive = false;
            }
        }

        private void ParseSpring()
        {
            _springControlNames.Clear();
            if (string.IsNullOrWhiteSpace(_spring))
            {
                return;
            }

            foreach (string part in _spring.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length > 0)
                {
                    _springControlNames.Add(name);
                }
            }
        }

        private void ParseExcludedControls()
        {
            _excludedControlNames.Clear();
            if (string.IsNullOrWhiteSpace(_excludedControls))
            {
                return;
            }

            foreach (string part in _excludedControls.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim();
                if (name.Length > 0)
                {
                    _excludedControlNames.Add(name);
                }
            }
        }
    }
}
