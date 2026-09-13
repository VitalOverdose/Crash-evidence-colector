using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using ProfessorSnowsVideoDownloader.CustomControls;
using ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl;

namespace CrashEvidenceCollector.Theming;

/// <summary>
/// Holds the current <see cref="Theme"/> and applies it to windows.
///
/// How it colours a control:
/// <list type="bullet">
/// <item>Controls that implement <see cref="IThemeable"/> theme themselves.</item>
/// <item>The copied shell controls are coloured by role — "input interior",
/// "hover border" — through their public colour properties.</item>
/// <item>Everything else has its <em>original</em> BackColor and ForeColor looked
/// up in a table of the light palette's values and replaced with the matching role.
/// Colours not in the table are left alone, so intentional colours survive.</item>
/// </list>
///
/// Originals are remembered per control, so applying a different theme maps from
/// the designer's value rather than from the last theme's. Controls added to a
/// themed window later are themed as they arrive.
/// </summary>
public static class ThemeManager
{
    private static Theme _current = Theme.Light;
    private static readonly object Marker = new();
    private static readonly ConditionalWeakTable<Control, object> Watched = new();
    private static readonly ConditionalWeakTable<Control, Dictionary<string, Color>> Originals = new();
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> ColorProperties = new();

    public static Theme Current => _current;

    /// <summary>Raised after a theme has been applied to every open window.</summary>
    public static event Action<Theme>? ThemeChanged;

    /// <summary>Makes <paramref name="theme"/> current and re-themes every open window.</summary>
    public static void SetTheme(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        _current = theme;
        ToolStripManager.Renderer = new ThemedToolStripRenderer(theme);
        foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            Apply(form);
        ThemeChanged?.Invoke(theme);
    }

    /// <summary>Themes <paramref name="root"/> and everything inside it, now and as children are added.</summary>
    public static void Apply(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        ApplyTree(root);
        root.Invalidate(true);
    }

    private static void ApplyTree(Control control)
    {
        Watch(control);
        try { ApplyOne(control, _current); }
        catch { /* A control that refuses a colour must never break the window. */ }
        foreach (Control child in control.Controls)
            ApplyTree(child);
    }

    private static void Watch(Control control)
    {
        if (Watched.TryGetValue(control, out _)) return;
        Watched.Add(control, Marker);
        control.ControlAdded += (_, e) =>
        {
            if (e.Control is not null) Apply(e.Control);
        };
    }

    // ---------------------------------------------------------------------------------
    // Per-control roles
    // ---------------------------------------------------------------------------------

    private static void ApplyOne(Control control, Theme t)
    {
        if (control is IThemeable themeable)
        {
            themeable.ApplyTheme(t);
            return;
        }

        Color ground = EffectiveBack(control.Parent, t);

        switch (control)
        {
            case WebBrowser_TabHeader:
                control.Invalidate(); // paints straight from ThemeManager.Current
                return;

            case TabHeadersControl strip:
                strip.HeaderBackColor = t.TabStrip;
                strip.BackColor = t.TabStrip;
                strip.Invalidate();
                return;

            case RoundedTextBox when control is not RoundedNumericTextBox:
                Set(control, "BackColor", ground);
                Set(control, "TextBoxBackColor", t.Input);
                Set(control, "BorderColor", t.BorderStrong);
                Set(control, "FocusColor", t.Accent);
                Set(control, "HoverBackColor", t.SurfaceRaised);
                Set(control, "ForeColor", t.Text);
                return;

            case RoundedNumericTextBox:
                Set(control, "BackColor", ground);
                InputRoles(control, t);
                Set(control, "NumericFocusBackColor", t.Input);
                Set(control, "ButtonColor", t.SurfaceRaised);
                Set(control, "ButtonHoverColor", t.Border);
                Set(control, "ButtonPressColor", t.AccentSoft);
                Set(control, "FocusColor", t.Accent);
                return;

            case RoundedComboBox:
            case RoundedMultiSelectComboBox:
                if (control.BackColor.A == 255) Set(control, "BackColor", ground);
                InputRoles(control, t);
                Set(control, "InnerGradientEndColor", t.Input);
                Set(control, "ItemHoverColor", t.SurfaceRaised);
                return;

            case AdaptiveRowLabel:
                // A label sits flush on whatever it is placed on.
                foreach (var role in new[] { "BackgroundColor", "SurfaceColor", "HoverColor", "PressedColor", "BorderColor", "HoverBorderColor", "PressedBorderColor" })
                    Set(control, role, ground);
                Color labelText = IsLarge(control) ? t.TextStrong : t.Text;
                Set(control, "TextColor", labelText);
                Set(control, "ForeColor", labelText);
                return;

            case ModernButton:
                Set(control, "BackgroundColor", t.SurfaceRaised);
                Set(control, "HoverColor", t.Border);
                Set(control, "PressedColor", t.AccentSoft);
                Set(control, "BorderColor", t.Border);
                Set(control, "HoverBorderColor", t.Accent);
                Set(control, "PressedBorderColor", t.Accent);
                Set(control, "TextColor", t.Text);
                Set(control, "ForeColor", t.Text);
                return;

            case IconButton:
                Set(control, "TextColor", t.Text);
                Set(control, "ForeColor", t.Text);
                return;

            case VirtualAdaptiveLineBreak:
                Set(control, "LineColor", t.Border);
                return;

            case VirtualizingAdaptiveRowPanel:
                Set(control, "BorderColor", t.BorderStrong);
                Set(control, "LineColor", t.Border);
                Set(control, "FocusBorderColor", t.Accent);
                Set(control, "SelectedBorderColor", t.Accent);
                Set(control, "FocusBackColor", t.SurfaceRaised);
                MapGround(control, t);
                return;

            case ListView list:
                MapGround(list, t, fallback: t.Surface);
                Set(list, "ForeColor", t.Text);
                return;

            case TextBoxBase box:
                MapGround(box, t, fallback: t.Input);
                MapText(box, t, fallback: t.Text);
                return;

            case NumericUpDown or ComboBox or DateTimePicker:
                Set(control, "BackColor", t.Input);
                Set(control, "ForeColor", t.Text);
                return;

            case ButtonBase button when button.FlatStyle == FlatStyle.Flat:
                MapGround(button, t);
                MapText(button, t);
                Color border = Original(button, "FlatBorder", button.FlatAppearance.BorderColor);
                if (MapBackground(border, t) is { } themedBorder) button.FlatAppearance.BorderColor = themedBorder;
                return;

            case ButtonBase:
                // System-styled buttons are drawn by Windows' own dark mode.
                return;

            default:
                // Spinners and similar custom controls expose an accent pair.
                Set(control, "AccentColor", t.Accent);
                Set(control, "SoftColor", t.Border);
                MapGround(control, t);
                MapText(control, t);
                return;
        }
    }

    private static void InputRoles(Control control, Theme t)
    {
        Set(control, "InnerColor", t.Input);
        Set(control, "HoverInnerColor", t.SurfaceRaised);
        Set(control, "SelectedInnerColor", t.AccentSoft);
        Set(control, "BorderColor", t.BorderStrong);
        Set(control, "HoverBorderColor", t.Accent);
        Set(control, "SelectedBorderColor", t.Accent);
        Set(control, "SeparatorColor", t.Border);
        Set(control, "DisabledInnerColor", t.Surface);
        Set(control, "DisabledBorderColor", t.Border);
        Set(control, "DisabledTextColor", t.TextFaint);
        Set(control, "ForeColor", t.Text);
    }

    /// <summary>Large text in a label is a heading; it gets the strongest text colour.</summary>
    private static bool IsLarge(Control control) => control.Font.SizeInPoints >= 14f;

    // ---------------------------------------------------------------------------------
    // Mapping light-palette literals to roles
    // ---------------------------------------------------------------------------------

    private static void MapGround(Control control, Theme t, Color? fallback = null)
    {
        // A control that inherits its parent's colour needs nothing: the parent is
        // themed first, and the ambient colour follows it.
        if (control.Parent is not null && control.BackColor == control.Parent.BackColor && !Originals.TryGetValue(control, out _)) return;
        Color original = Original(control, "BackColor", control.BackColor);
        if (original.A < 255) return;
        Color? mapped = MapBackground(original, t) ?? fallback;
        if (mapped is { } value && control.BackColor != value) control.BackColor = value;
    }

    private static void MapText(Control control, Theme t, Color? fallback = null)
    {
        Color original = Original(control, "ForeColor", control.ForeColor);
        Color? mapped = MapForeground(original, t) ?? fallback;
        if (mapped is { } value && control.ForeColor != value) control.ForeColor = value;
    }

    /// <summary>Light-theme ground colours found in designer files and older code, by role.</summary>
    private static Color? MapBackground(Color original, Theme t) => (original.R, original.G, original.B) switch
    {
        (255, 255, 255) => t.Surface,
        (246, 248, 251) or (242, 245, 249) or (240, 240, 240) => t.Canvas,
        (249, 251, 253) or (237, 245, 250) => t.SurfaceRaised,
        (245, 250, 255) or (17, 28, 45) => t.Input,
        (220, 226, 234) or (218, 225, 234) or (200, 210, 220) => t.Border,
        (13, 24, 39) => t.Nav,
        (24, 42, 64) => t.NavActive,
        (48, 70, 95) => t.BorderStrong,
        (230, 240, 252) => t.AccentSoft,
        (255, 238, 184) => t.Wash(t.Warning),
        _ => null,
    };

    /// <summary>Light-theme text colours found in designer files and older code, by role.</summary>
    private static Color? MapForeground(Color original, Theme t) => (original.R, original.G, original.B) switch
    {
        (0, 0, 0) or (31, 31, 31) or (60, 60, 60) or (22, 33, 48) or (20, 35, 59) or (35, 45, 58) or (220, 232, 248) or (100, 100, 100) => t.Text,
        (75, 85, 99) or (88, 101, 118) or (91, 104, 122) => t.TextMuted,
        (120, 132, 148) or (133, 145, 160) => t.TextFaint,
        (153, 179, 207) or (164, 184, 207) => t.NavText,
        (95, 63, 0) or (140, 75, 20) => t.Warning,
        _ => null,
    };

    private static Color EffectiveBack(Control? control, Theme t)
    {
        for (var current = control; current is not null; current = current.Parent)
            if (current.BackColor.A == 255) return current.BackColor;
        return t.Canvas;
    }

    // ---------------------------------------------------------------------------------
    // Property access
    // ---------------------------------------------------------------------------------

    private static Color Original(Control control, string key, Color current)
    {
        var map = Originals.GetOrCreateValue(control);
        if (!map.TryGetValue(key, out Color original))
        {
            original = current;
            map[key] = original;
        }
        return original;
    }

    /// <summary>
    /// Sets a public Color property by name when the control has one. Controls in the
    /// copied library expose colours under differing names and hierarchies; missing
    /// properties are simply skipped.
    /// </summary>
    private static void Set(Control control, string property, Color value)
    {
        PropertyInfo? info = ColorProperties.GetOrAdd((control.GetType(), property), key => FindColorProperty(key.Item1, key.Item2));
        if (info is null) return;
        Original(control, property, (Color)info.GetValue(control)!);
        if (!Equals(info.GetValue(control), value))
            info.SetValue(control, value);
    }

    private static PropertyInfo? FindColorProperty(Type type, string name)
    {
        // A property hidden with `new` appears twice; the most-derived declaration wins.
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name == name && p.PropertyType == typeof(Color) && p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .OrderByDescending(p => Depth(p.DeclaringType))
            .FirstOrDefault();

        static int Depth(Type? declaring)
        {
            int depth = 0;
            for (var current = declaring; current is not null; current = current.BaseType) depth++;
            return depth;
        }
    }
}
