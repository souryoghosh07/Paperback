namespace Paperback.Core.Models;

public enum ThemeMode
{
    Light,      // Pure crisp white background, charcoal text
    Sepia,      // Warm parchment #F8F1E5, rich espresso text #382E2B
    MatteGray,  // Soft paper tone #E8E8EC, neutral dark text
    Dark,       // Deep charcoal #1C1C1E, warm white text #F2F2F7
    OledBlack   // Pure black #000000, soft white text #D1D1D6
}

public class TypographySettings
{
    public string FontFamily { get; set; } = "Georgia";
    public double FontSize { get; set; } = 18.0;
    public double LineHeight { get; set; } = 1.6;
    public double MarginRatio { get; set; } = 1.0;
    public bool JustifyText { get; set; } = true;
    public ThemeMode Theme { get; set; } = ThemeMode.Sepia;
    public bool TwoPageSpread { get; set; } = true;
    public string AnimationMode { get; set; } = "curl"; // "curl", "slide", "scroll"
}
