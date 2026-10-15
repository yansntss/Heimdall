using System.Runtime.InteropServices;

namespace Heimdall.Native;

/// <summary>
/// "O Windows pediu silêncio?" — Assistente de Foco (Não incomodar) ou modo de apresentação.
/// Tela cheia/jogo sozinho NÃO conta: aí o aviso aparece por cima (é pra isso que o toast
/// não rouba foco). Se o Windows ligar o Não incomodar automaticamente durante jogos, vale
/// o que ele decidiu.
/// </summary>
internal static class FocusAssist
{
    // Estado do Assistente de Foco: 0 = desligado, 1 = só prioridade, 2 = só alarmes.
    // Não é API documentada, mas é o que o próprio shell publica (WNF) desde o Windows 10 1803.
    private const ulong WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED = 0x0D83063EA3BF1C75;

    private const int QUNS_PRESENTATION_MODE = 4;
    private const int QUNS_QUIET_TIME = 6;

    [DllImport("ntdll.dll")]
    private static extern int NtQueryWnfStateData(
        ref ulong stateName, IntPtr typeId, IntPtr explicitScope, out uint changeStamp, out int buffer, ref uint bufferSize);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    public static bool IsQuiet
    {
        get
        {
            try
            {
                ulong name = WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED;
                uint size = sizeof(int);
                if (NtQueryWnfStateData(ref name, IntPtr.Zero, IntPtr.Zero, out _, out int profile, ref size) >= 0 && size >= sizeof(int))
                    if (profile != 0) return true;
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }

            try
            {
                return SHQueryUserNotificationState(out int state) == 0 && state is QUNS_PRESENTATION_MODE or QUNS_QUIET_TIME;
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}
