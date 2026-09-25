using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace InfoBar.Config;

public static class ConfigService
{
    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InfoBar");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void EnsureExists()
    {
        if (!File.Exists(ConfigPath)) Save(new AppConfig());
    }

    public static AppConfig Load()
    {
        try
        {
            EnsureExists();
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erro ao ler a configuração:\n{ex.Message}\n\nUsando configuração padrão.",
                "InfoBar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, Options));
    }
}
