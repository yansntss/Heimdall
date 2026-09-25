namespace InfoBar.Config;

public enum BackdropType { Solid, Translucent, Acrylic, Mica, None }

/// <summary>Define a aparência de um tema — embutido ou salvo em %AppData%\InfoBar\themes\*.json.</summary>
public sealed class ThemeDefinition
{
    public string Name { get; set; } = "";

    /// <summary>#AARRGGBB</summary>
    public string Background { get; set; } = "#FF1E1E1E";
    public string TextPrimary { get; set; } = "#FFFFFFFF";
    public string TextSecondary { get; set; } = "#FFB0B0B0";
    public string Accent { get; set; } = "#FF4A9EFF";
    public string Hover { get; set; } = "#22FFFFFF";
    public string Border { get; set; } = "#22FFFFFF";

    public double CornerRadius { get; set; }
    public BackdropType Backdrop { get; set; } = BackdropType.Solid;
    public string FontFamily { get; set; } = "Segoe UI";
}
