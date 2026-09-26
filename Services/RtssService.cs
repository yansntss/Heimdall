using System.IO;
using System.IO.MemoryMappedFiles;
using Heimdall.Native;

namespace Heimdall.Services;

/// <summary>
/// Lê o FPS calculado pelo próprio RTSS (RivaTuner Statistics Server / MSI Afterburner)
/// pro processo em primeiro plano, via memória compartilhada (mesmo mecanismo que
/// overlays de terceiros usam — layout documentado publicamente, mas não por API oficial).
/// Layout confirmado empiricamente (assinatura, versão e offsets de dwProcessID/szName/
/// dwFlags/dwTime0/dwTime1/dwFrames/dwFrameTime batendo com processos reais monitorados
/// pelo RTSS, frametime em microssegundos): não é garantido que se mantenha idêntico em
/// toda atualização futura do RTSS, mas é estável há anos nas ferramentas que dependem
/// dele. Qualquer coisa fora do esperado (assinatura errada, exceção, RTSS fechado) só
/// esconde o widget — nunca derruba o app.
/// </summary>
internal static class RtssService
{
    private const string MapName = "RTSSSharedMemoryV2";
    private const uint Signature = 0x52545353; // 'RTSS' como DWORD multichar do C (bytes em memória, little-endian: 53 53 54 52 = "SSTR")

    private const int NameOffset = 4;
    private const int NameLength = 260; // MAX_PATH
    private const int FlagsOffset = NameOffset + NameLength; // 264
    private const int Time0Offset = FlagsOffset + 4;         // 268
    private const int Time1Offset = Time0Offset + 4;         // 272
    private const int FramesOffset = Time1Offset + 4;        // 276
    private const int FrameTimeOffset = FramesOffset + 4;    // 280 — frametime do último frame, em microssegundos
    private const int MinEntrySize = FrameTimeOffset + 4;    // 284 — o mínimo que precisamos ler de cada entrada

    /// <summary>FPS instantâneo do processo em primeiro plano — null se o RTSS não estiver rodando, o processo não estiver sendo monitorado, ou ele ainda não ter renderizado nenhum frame.</summary>
    public static double? GetForegroundFps()
    {
        var hWnd = NativeMethods.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return null;

        NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        if (pid == 0) return null;

        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(MapName);
            using var header = mmf.CreateViewAccessor(0, 32, MemoryMappedFileAccess.Read);

            if (header.ReadUInt32(0) != Signature) return null;

            uint appEntrySize = header.ReadUInt32(8);
            uint appArrOffset = header.ReadUInt32(12);
            uint appArrSize = header.ReadUInt32(16);
            // Limites de sanidade — nunca confiar cegamente em bytes de uma memória
            // compartilhada de outro processo pra decidir quanto alocar/percorrer.
            if (appEntrySize < MinEntrySize || appArrSize == 0 || appArrSize > 4096) return null;

            using var apps = mmf.CreateViewAccessor(appArrOffset, (long)appEntrySize * appArrSize, MemoryMappedFileAccess.Read);
            for (uint i = 0; i < appArrSize; i++)
            {
                long entryOffset = (long)i * appEntrySize;
                if (apps.ReadUInt32(entryOffset) != pid) continue;

                uint frames = apps.ReadUInt32(entryOffset + FramesOffset);
                uint frameTimeMicros = apps.ReadUInt32(entryOffset + FrameTimeOffset);
                if (frames == 0 || frameTimeMicros == 0) return null;

                return 1_000_000.0 / frameTimeMicros;
            }

            return null;
        }
        catch (FileNotFoundException)
        {
            return null; // RTSS não está rodando
        }
        catch
        {
            return null; // layout inesperado, acesso negado, etc. — nunca propaga
        }
    }
}
