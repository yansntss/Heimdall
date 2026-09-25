using System.IO;
using NAudio.CoreAudioApi;

namespace Heimdall.Services;

/// <summary>
/// Volume por app via Core Audio API (NAudio) — o SMTC não controla volume, só
/// play/pause/skip. Resolve a sessão de áudio do app tocando (pelo
/// SourceAppUserModelId do SMTC) e cai pro volume master se não achar.
/// </summary>
internal static class AudioVolumeService
{
    public static float GetVolume(string? sourceAppUserModelId)
    {
        using var device = GetDefaultDevice();
        if (device is null) return 0f;

        var session = FindSession(device, sourceAppUserModelId);
        return session?.Volume ?? device.AudioEndpointVolume.MasterVolumeLevelScalar;
    }

    public static void SetVolume(string? sourceAppUserModelId, float value)
    {
        value = Math.Clamp(value, 0f, 1f);

        using var device = GetDefaultDevice();
        if (device is null) return;

        var session = FindSession(device, sourceAppUserModelId);
        if (session is not null) session.Volume = value;
        else device.AudioEndpointVolume.MasterVolumeLevelScalar = value;
    }

    public static bool GetMuted(string? sourceAppUserModelId)
    {
        using var device = GetDefaultDevice();
        if (device is null) return false;

        var session = FindSession(device, sourceAppUserModelId);
        return session?.Mute ?? device.AudioEndpointVolume.Mute;
    }

    public static void SetMuted(string? sourceAppUserModelId, bool muted)
    {
        using var device = GetDefaultDevice();
        if (device is null) return;

        var session = FindSession(device, sourceAppUserModelId);
        if (session is not null) session.Mute = muted;
        else device.AudioEndpointVolume.Mute = muted;
    }

    private static MMDevice? GetDefaultDevice()
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            // Sem dispositivo de saída padrão disponível.
            return null;
        }
    }

    /// <summary>
    /// Casa a sessão de áudio pelo processo do SourceAppUserModelId (ex: "Spotify.exe").
    /// Apps UWP têm um AUMID com "!" (ex: pacote!App) — nesse caso comparamos por
    /// substring, já que não há um nome de processo direto pra extrair.
    /// </summary>
    private static SimpleAudioVolume? FindSession(MMDevice device, string? sourceAppUserModelId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppUserModelId)) return null;

        string? exeNameHint = sourceAppUserModelId.Contains('!')
            ? null
            : Path.GetFileNameWithoutExtension(sourceAppUserModelId);

        try
        {
            var sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                try
                {
                    uint pid = session.GetProcessID;
                    if (pid == 0) continue;

                    using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                    bool matches = exeNameHint is not null
                        ? string.Equals(process.ProcessName, exeNameHint, StringComparison.OrdinalIgnoreCase)
                        : sourceAppUserModelId.Contains(process.ProcessName, StringComparison.OrdinalIgnoreCase);

                    if (matches) return session.SimpleAudioVolume;
                }
                catch
                {
                    // Processo fechou entre a enumeração e a checagem, ou sem permissão — ignora essa sessão.
                }
            }
        }
        catch
        {
            // AudioSessionManager indisponível.
        }

        return null;
    }
}
