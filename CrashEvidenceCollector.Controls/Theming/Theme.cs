using System.Drawing;

namespace CrashEvidenceCollector.Theming;

/// <summary>
/// One complete set of named colours. Everything themed in CEC asks for a role —
/// "the surface a card sits on", "secondary text" — never a literal, so a whole
/// look is swapped by handing <see cref="ThemeManager"/> a different instance.
///
/// Saturated colour is reserved for meaning (success, warning, danger, hardware)
/// and for the one accent. Everything else is a ground or a line.
/// </summary>
public sealed record Theme
{
    public required string Name { get; init; }
    public required bool IsDark { get; init; }

    // ---- Grounds ------------------------------------------------------------------
    /// <summary>The navigation rail: the deepest ground in the window.</summary>
    public required Color Nav { get; init; }
    /// <summary>The window background panels sit on.</summary>
    public required Color Canvas { get; init; }
    /// <summary>Cards, panes, lists — anything that holds content.</summary>
    public required Color Surface { get; init; }
    /// <summary>One step up from a surface: hovers, selected tabs, secondary buttons.</summary>
    public required Color SurfaceRaised { get; init; }
    /// <summary>The interior of something that accepts typing.</summary>
    public required Color Input { get; init; }

    // ---- Lines --------------------------------------------------------------------
    public required Color Border { get; init; }
    public required Color BorderStrong { get; init; }

    // ---- Text ---------------------------------------------------------------------
    public required Color TextStrong { get; init; }
    public required Color Text { get; init; }
    public required Color TextMuted { get; init; }
    public required Color TextFaint { get; init; }

    // ---- Navigation rail ----------------------------------------------------------
    public required Color NavText { get; init; }
    public required Color NavTextActive { get; init; }
    public required Color NavActive { get; init; }

    // ---- Accent: "you are here" and "this commits the task" -------------------------
    public required Color Accent { get; init; }
    public required Color AccentStrong { get; init; }
    public required Color AccentSoft { get; init; }
    /// <summary>Text or glyphs drawn on top of <see cref="AccentStrong"/>.</summary>
    public required Color OnAccent { get; init; }

    // ---- Meanings ------------------------------------------------------------------
    public required Color Info { get; init; }
    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Caution { get; init; }
    public required Color Danger { get; init; }
    public required Color Hardware { get; init; }

    // ---- Tabs -----------------------------------------------------------------------
    public required Color TabStrip { get; init; }
    public required Color Tab { get; init; }
    public required Color TabHover { get; init; }
    public required Color TabSelected { get; init; }
    public required Color TabBorder { get; init; }

    /// <summary>How strongly a meaning colour tints a row or card wash.</summary>
    public required double WashStrength { get; init; }

    /// <summary>A meaning colour laid thinly over the surface, for row and card washes.</summary>
    public Color Wash(Color meaning) => Blend(Surface, meaning, WashStrength);

    /// <summary>Linear blend from <paramref name="from"/> towards <paramref name="to"/>.</summary>
    public static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        int Mix(int a, int b) => (int)Math.Round(a + (b - a) * amount);
        return Color.FromArgb(Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
    }

    /// <summary>
    /// Dark investigation theme: blue-grey grounds, soft off-white text, one blue
    /// accent and restrained semantic colours. Palette follows the open-source
    /// Blueprint dark scale, the toolkit behind Palantir's analyst tools.
    /// </summary>
    public static Theme Dark { get; } = new()
    {
        Name = "Dark",
        IsDark = true,
        Nav = Color.FromArgb(17, 20, 24),
        Canvas = Color.FromArgb(28, 33, 39),
        Surface = Color.FromArgb(37, 42, 49),
        SurfaceRaised = Color.FromArgb(47, 52, 60),
        Input = Color.FromArgb(24, 28, 34),
        Border = Color.FromArgb(56, 62, 71),
        BorderStrong = Color.FromArgb(73, 81, 94),
        TextStrong = Color.FromArgb(246, 247, 249),
        Text = Color.FromArgb(223, 227, 232),
        TextMuted = Color.FromArgb(171, 179, 191),
        TextFaint = Color.FromArgb(128, 138, 153),
        NavText = Color.FromArgb(171, 179, 191),
        NavTextActive = Color.FromArgb(246, 247, 249),
        NavActive = Color.FromArgb(37, 42, 49),
        Accent = Color.FromArgb(76, 144, 240),
        AccentStrong = Color.FromArgb(45, 114, 210),
        AccentSoft = Color.FromArgb(31, 52, 82),
        OnAccent = Color.White,
        Info = Color.FromArgb(34, 176, 168),
        Success = Color.FromArgb(50, 164, 103),
        Warning = Color.FromArgb(236, 154, 60),
        Caution = Color.FromArgb(251, 179, 96),
        Danger = Color.FromArgb(231, 106, 110),
        Hardware = Color.FromArgb(189, 107, 189),
        TabStrip = Color.FromArgb(28, 33, 39),
        Tab = Color.FromArgb(28, 33, 39),
        TabHover = Color.FromArgb(37, 42, 49),
        TabSelected = Color.FromArgb(47, 52, 60),
        TabBorder = Color.FromArgb(73, 81, 94),
        WashStrength = 0.16,
    };

    /// <summary>The original light look, kept so the theme can be switched back.</summary>
    public static Theme Light { get; } = new()
    {
        Name = "Light",
        IsDark = false,
        Nav = Color.FromArgb(13, 24, 39),
        Canvas = Color.FromArgb(242, 245, 249),
        Surface = Color.White,
        SurfaceRaised = Color.FromArgb(249, 251, 253),
        Input = Color.FromArgb(245, 250, 255),
        Border = Color.FromArgb(218, 225, 234),
        BorderStrong = Color.FromArgb(200, 210, 220),
        TextStrong = Color.FromArgb(22, 33, 48),
        Text = Color.FromArgb(22, 33, 48),
        TextMuted = Color.FromArgb(91, 104, 122),
        TextFaint = Color.FromArgb(133, 145, 160),
        NavText = Color.FromArgb(164, 184, 207),
        NavTextActive = Color.White,
        NavActive = Color.FromArgb(24, 42, 64),
        Accent = Color.FromArgb(37, 112, 202),
        AccentStrong = Color.FromArgb(40, 105, 190),
        AccentSoft = Color.FromArgb(230, 240, 252),
        OnAccent = Color.White,
        Info = Color.FromArgb(20, 139, 154),
        Success = Color.FromArgb(43, 133, 89),
        Warning = Color.FromArgb(190, 116, 25),
        Caution = Color.FromArgb(205, 137, 38),
        Danger = Color.FromArgb(184, 56, 62),
        Hardware = Color.FromArgb(153, 73, 180),
        TabStrip = Color.FromArgb(237, 245, 250),
        Tab = Color.FromArgb(237, 245, 250),
        TabHover = Color.FromArgb(228, 235, 240),
        TabSelected = Color.White,
        TabBorder = Color.FromArgb(128, 128, 128),
        WashStrength = 0.07,
    };
}
