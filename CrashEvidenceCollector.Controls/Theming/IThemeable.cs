namespace CrashEvidenceCollector.Theming;

/// <summary>
/// For controls that paint themselves or hold colours the theme manager cannot
/// see from outside — owner-drawn charts, the status band, custom spinners.
/// <see cref="ThemeManager"/> hands the theme over and still themes the children.
/// </summary>
public interface IThemeable
{
    void ApplyTheme(Theme theme);
}
