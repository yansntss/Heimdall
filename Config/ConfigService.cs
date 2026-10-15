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
            bool migratedTheme = MigrateToThemeV2(node);
            var config = node.Deserialize<AppConfig>(Options) ?? new AppConfig();
            Services.Strings.Current = config.Language;

            // Grava já migrado (com backup do original) — senão a migração rodaria de novo a
            // cada carga e uma troca de tema feita depois seria desfeita na próxima leitura.
            if (migratedTheme)
            {
                File.Copy(ConfigPath, ConfigPath + ".bak-v1", overwrite: true);
                Save(config);
            }
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

    /// <summary>Nomes dos 9 temas antigos → id do tema novo mais próximo (fundo sólido vs. translúcido, claro vs. escuro).</summary>
    private static readonly Dictionary<string, string> LegacyThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Escuro"] = "Bifrost",
        ["Claro"] = "Cristal",
        ["Translúcido Escuro"] = "Obsidiana",
        ["Translúcido Claro"] = "Cristal",
        ["Acrílico"] = "Obsidiana",
        ["Mica"] = "Obsidiana",
        ["Vidro"] = "Obsidiana",
        ["Destaque do Windows"] = "Obsidiana",
    };

    /// <summary>
    /// Config sem "ConfigVersion" é de antes do tema novo: troca o nome do tema antigo
    /// pelo equivalente novo ("Destaque do Windows" vira acento "system"; "Auto" escolhe
    /// claro/escuro pelo Windows na hora da migração) e sobe a espessura padrão antiga
    /// (28) pra nova (38) — quem tinha mudado a espessura na mão mantém a dela. Temas
    /// próprios (themes/*.json) passam intactos. Tolerante: campo ausente ou de tipo
    /// inesperado só não é migrado, nunca derruba a leitura.
    /// </summary>
    private static bool MigrateToThemeV2(JsonNode? root)
    {
        if (root is not JsonObject obj) return false;
        if (obj["ConfigVersion"] is JsonValue v && v.TryGetValue<int>(out int version) && version >= AppConfig.CurrentVersion)
            return false;

        string? theme = obj["Theme"] is JsonValue t && t.TryGetValue<string>(out var s) ? s : null;
        if (theme is null)
        {
            // Config bem antigo, sem "Theme": o padrão da época era "Escuro" (sólido).
            obj["Theme"] = "Bifrost";
        }
        else if (string.Equals(theme, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            obj["Theme"] = Services.ThemeService.IsSystemLightTheme() ? "Cristal" : "Obsidiana";
        }
        else if (LegacyThemes.TryGetValue(theme, out var mapped))
        {
            obj["Theme"] = mapped;
            if (string.Equals(theme, "Destaque do Windows", StringComparison.OrdinalIgnoreCase) && obj["AccentColor"] is null)
                obj["AccentColor"] = "system";
        }

        bool horizontal = obj["Edge"] is not JsonValue edge || !edge.TryGetValue<string>(out var e)
            || !(e.Equals("Left", StringComparison.OrdinalIgnoreCase) || e.Equals("Right", StringComparison.OrdinalIgnoreCase));
        if (horizontal && (obj["Thickness"] is null || (obj["Thickness"] is JsonValue th && th.TryGetValue<int>(out int old) && old == 28)))
            obj["Thickness"] = 38;

        obj["ConfigVersion"] = AppConfig.CurrentVersion;
        return true;
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

    /// <summary>Cópia profunda (ida e volta pelo JSON) — rascunho da tela de Configurações e prévia ao vivo.</summary>
    public static AppConfig Clone(AppConfig config) =>
        JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(config, Options), Options) ?? new AppConfig();

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, Options));
    }
}
