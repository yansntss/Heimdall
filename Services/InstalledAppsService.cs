using Windows.Management.Deployment;

namespace Heimdall.Services;

/// <summary>Enumera apps instalados (Store/MSIX) do usuário atual — mesma fonte que alimenta o menu Iniciar.</summary>
internal static class InstalledAppsService
{
    public sealed record InstalledApp(string Name, string AppUserModelId);

    public static List<InstalledApp> GetAll()
    {
        var result = new List<InstalledApp>();
        try
        {
            var manager = new PackageManager();
            foreach (var package in manager.FindPackagesForUser(""))
            {
                if (package.IsFramework || package.IsResourcePackage) continue;

                try
                {
                    foreach (var entry in package.GetAppListEntries())
                        result.Add(new InstalledApp(entry.DisplayInfo.DisplayName, entry.AppUserModelId));
                }
                catch
                {
                    // Pacote sem entradas visíveis (só componentes) — ignora e segue.
                }
            }
        }
        catch
        {
            // API de pacotes indisponível — a lista fica vazia, sem derrubar o app.
        }

        return result.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
