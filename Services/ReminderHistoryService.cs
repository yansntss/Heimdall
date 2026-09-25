using System.IO;
using System.Linq;
using Heimdall.Config;

namespace Heimdall.Services;

/// <summary>
/// Histórico de lembretes concluídos, um arquivo Markdown por mês. Fica em
/// %AppData%\Heimdall\docs\lembretes — não na pasta do executável, porque em
/// Program Files a gravação falha.
/// </summary>
internal static class ReminderHistoryService
{
    public static string HistoryDir => Path.Combine(ConfigService.ConfigDir, "docs", "lembretes");

    public static void AppendCompleted(string text, DateTime? createdAt, DateTime completedAt)
    {
        try
        {
            Directory.CreateDirectory(HistoryDir);
            string file = Path.Combine(HistoryDir, completedAt.ToString("yyyy-MM") + ".md");

            string created = createdAt?.ToString("dd/MM HH:mm") ?? "—";
            string completed = completedAt.ToString("dd/MM HH:mm");
            File.AppendAllText(file, $"- [x] {text} — criado {created} — concluído {completed}{Environment.NewLine}");
        }
        catch
        {
            // Histórico é um extra — não derruba o app se a gravação falhar.
        }
    }

    /// <summary>
    /// Lê todo o histórico, mais recente primeiro (arquivos por nome — "yyyy-MM" ordena
    /// certo — e linhas de cada arquivo invertidas, já que são gravadas por Append).
    /// </summary>
    public static List<(string MonthKey, List<string> Lines)> ReadAll()
    {
        var result = new List<(string, List<string>)>();
        if (!Directory.Exists(HistoryDir)) return result;

        foreach (var file in Directory.EnumerateFiles(HistoryDir, "*.md").OrderByDescending(f => f))
        {
            var lines = File.ReadAllLines(file)
                .Where(l => l.StartsWith("- [x]"))
                .Reverse()
                .ToList();
            if (lines.Count > 0)
                result.Add((Path.GetFileNameWithoutExtension(file), lines));
        }
        return result;
    }
}
