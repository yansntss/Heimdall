using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Heimdall.Config;
using Heimdall.Native;

namespace Heimdall.Services;

/// <summary>
/// Extrai ícones em alta resolução via IShellItemImageFactory (funciona pra .exe, .lnk,
/// pastas e apps da Store) e guarda em cache como PNG — evita reextrair a cada inicialização.
/// </summary>
internal static class IconCacheService
{
    private static string CacheDir => Path.Combine(ConfigService.ConfigDir, "cache", "icons");

    /// <summary>Resolve .lnk pro destino real (pega o ícone certo) antes de extrair; outros tipos usam o path como veio.</summary>
    public static string ResolveTarget(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return path;

        try
        {
            var type = Type.GetTypeFromCLSID(ShellInterop.CLSID_ShellLink)!;
            var link = (ShellInterop.IShellLinkW)Activator.CreateInstance(type)!;
            ((ShellInterop.IPersistFile)link).Load(path, 0);

            var sb = new StringBuilder(260);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            var target = sb.ToString();
            return string.IsNullOrWhiteSpace(target) ? path : target;
        }
        catch
        {
            return path;
        }
    }

    /// <summary>Ícone em cache pra esse path+tamanho, extraindo e salvando em disco na primeira vez.</summary>
    public static BitmapSource? GetOrExtract(string path, int sizePx)
    {
        string resolved = ResolveTarget(path);
        string cacheFile = Path.Combine(CacheDir, $"{HashKey(resolved)}_{sizePx}.png");

        if (File.Exists(cacheFile))
        {
            var cached = TryLoadPng(cacheFile);
            if (cached is not null) return cached;
        }

        var extracted = Extract(resolved, sizePx);
        if (extracted is null) return null;

        TrySavePng(extracted, cacheFile);
        return extracted;
    }

    private static BitmapSource? Extract(string path, int sizePx)
    {
        try
        {
            var riid = ShellInterop.IID_IShellItemImageFactory;
            int hr = ShellInterop.SHCreateItemFromParsingName(path, IntPtr.Zero, ref riid, out object obj);
            if (hr != 0 || obj is not ShellInterop.IShellItemImageFactory factory) return null;

            var size = new ShellInterop.SIZE(sizePx);
            hr = factory.GetImage(size, ShellInterop.SIIGBF.IconOnly | ShellInterop.SIIGBF.BiggerSizeOk, out IntPtr hbitmap);
            if (hr != 0 || hbitmap == IntPtr.Zero) return null;

            try
            {
                var bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                ShellInterop.DeleteObject(hbitmap);
            }
        }
        catch
        {
            // Path inválido, sem shell item, ou COM indisponível — widget segue sem ícone.
            return null;
        }
    }

    private static BitmapImage? TryLoadPng(string file)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(file);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            // Cache corrompido/parcial — deixa reextrair.
            return null;
        }
    }

    private static void TrySavePng(BitmapSource bitmap, string file)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(file);
            encoder.Save(stream);
        }
        catch
        {
            // Falha ao gravar cache não impede de usar o ícone já extraído.
        }
    }

    private static string HashKey(string input)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
