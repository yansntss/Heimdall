using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace Heimdall.Services;

/// <summary>Release do GitHub mais nova que a versão rodando — <see cref="Url"/> é a página da release (O que mudou + downloads).</summary>
internal sealed record UpdateInfo(Version Version, string Tag, string Url);

/// <summary>
/// Consulta a última release publicada no GitHub (API pública, sem token) e compara com a
/// versão do executável. Qualquer falha (offline, rate limit, JSON inesperado) só resulta
/// em "sem atualização" — nunca é motivo pra incomodar o usuário.
/// </summary>
internal static class UpdateService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/yansntss/Heimdall/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    public static Version? CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? null : new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    /// <summary>Última versão disponível, já checada nesta sessão — null se não tem (ou ainda não checou).</summary>
    public static UpdateInfo? Available { get; private set; }

    /// <summary>Disparado (no thread de UI) quando uma versão nova é encontrada pela primeira vez.</summary>
    public static event Action<UpdateInfo>? UpdateFound;

    public static async Task CheckAsync()
    {
        var current = CurrentVersion;
        if (current is null) return;

        try
        {
            using var response = await Http.GetAsync(LatestReleaseApi);
            if (!response.IsSuccessStatusCode) return;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return;

            string tag = root.GetProperty("tag_name").GetString() ?? "";
            string url = root.GetProperty("html_url").GetString() ?? "";
            if (!TryParseTag(tag, out var latest) || latest <= current) return;
            if (Available?.Version == latest) return;

            Available = new UpdateInfo(latest, tag, url);
            UpdateFound?.Invoke(Available);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or InvalidOperationException or KeyNotFoundException)
        {
            // Sem rede / GitHub fora / formato mudou — tenta de novo na próxima checagem.
        }
    }

    /// <summary>"v1.2.0" (ou "v1.2.0-beta.1", ignorando o sufixo) → 1.2.0.</summary>
    private static bool TryParseTag(string tag, out Version version)
    {
        var core = tag.TrimStart('v', 'V').Split('-', '+')[0];
        if (Version.TryParse(core, out var parsed))
        {
            version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
            return true;
        }
        version = new Version();
        return false;
    }

    public static void OpenReleasePage()
    {
        if (Available is null) return;
        try { Process.Start(new ProcessStartInfo(Available.Url) { UseShellExecute = true }); }
        catch { /* sem navegador padrão configurado — nada a fazer */ }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // A API do GitHub recusa requisição sem User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Heimdall", CurrentVersion?.ToString() ?? "dev"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
