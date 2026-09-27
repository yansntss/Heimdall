using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;

namespace Heimdall.Config;

public static class ConfigService
{
    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Heimdall");

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
            var node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            MigrateClockConfig(node);
            MigrateWidgetLayout(node);
            var config = node.Deserialize<AppConfig>(Options) ?? new AppConfig();
            Services.Strings.Current = config.Language;
            return config;
        }
        catch (Exception ex)
        {
            Services.Strings.Current = AppLanguage.PtBr;
            MessageBox.Show(
                Services.Strings.ConfigLoadErrorBody(ex.Message),
                Services.Strings.ConfigLoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return new AppConfig();
        }
    }

    /// <summary>
    /// Config antigo tinha TimeFormat/DateFormat/VerticalDateFormat soltos; o novo usa
    /// Mode + Style. Sem migração, esses campos seriam simplesmente ignorados na
    /// deserialização (propriedade desconhecida) e o formato exato que a pessoa tinha
    /// configurado se perderia. Vira Mode:"Custom" com os dois formatos combinados —
    /// perde só a variante vertical (o Custom novo não é orientação-aware), aceitável
    /// pra um caso de borda; dá pra trocar pra um Style pronto depois se quiser.
    /// </summary>
    private static void MigrateClockConfig(JsonNode? root)
    {
        if (root?["Clock"] is not JsonObject clock) return;
        if (clock.ContainsKey("Mode")) return; // já no formato novo

        string? timeFormat = clock["TimeFormat"]?.GetValue<string>();
        string? dateFormat = clock["DateFormat"]?.GetValue<string>();
        if (string.IsNullOrEmpty(timeFormat) && string.IsNullOrEmpty(dateFormat)) return;

        string combined = string.Join("   ", new[] { dateFormat, timeFormat }
            .Where(s => !string.IsNullOrEmpty(s)));

        clock["Mode"] = "Custom";
        clock["CustomFormat"] = combined;
        clock.Remove("TimeFormat");
        clock.Remove("DateFormat");
        clock.Remove("VerticalDateFormat");
    }

    /// <summary>
    /// Config antigo tinha Widgets.Start/Center/End como lista de strings (só o Id); o novo
    /// usa objetos {Id, Pinned} pra dar suporte a fixar widget. Cada string solta vira
    /// {"Id": "<valor>", "Pinned": false}.
    /// </summary>
    private static void MigrateWidgetLayout(JsonNode? root)
    {
        if (root?["Widgets"] is not JsonObject widgets) return;
        MigrateZone(widgets["Start"] as JsonArray);
        MigrateZone(widgets["Center"] as JsonArray);
        MigrateZone(widgets["End"] as JsonArray);
    }

    private static void MigrateZone(JsonArray? zone)
    {
        if (zone is null) return;
        for (int i = 0; i < zone.Count; i++)
        {
            if (zone[i] is JsonValue value && value.TryGetValue<string>(out var id))
                zone[i] = new JsonObject { ["Id"] = id, ["Pinned"] = false };
        }
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, Options));
    }
}
