using NAudio.CoreAudioApi;

namespace Heimdall.Services;

/// <summary>
/// Volume e mudo do microfone padrão do Windows (dispositivo de captura) via Core Audio
/// API (NAudio) — o mesmo nível que aparece em Configurações → Som → Entrada. Cada chamada
/// resolve o dispositivo padrão de novo, então trocar de microfone no Windows já reflete
/// no próximo refresh do widget sem precisar recarregar a barra.
/// </summary>
internal static class MicVolumeService
{
    /// <summary>null quando não há microfone padrão (nenhum conectado ou todos desativados).</summary>
    public static (float Volume, bool Muted, string Name)? GetState()
    {
        using var device = GetDefaultDevice();
        if (device is null) return null;

        try
        {
            var endpoint = device.AudioEndpointVolume;
            return (endpoint.MasterVolumeLevelScalar, endpoint.Mute, device.FriendlyName);
        }
        catch
        {
            // Dispositivo removido entre o GetDefaultAudioEndpoint e a leitura.
            return null;
        }
    }

    public static void SetVolume(float value)
    {
        using var device = GetDefaultDevice();
        if (device is null) return;

        try { device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f); }
        catch { /* dispositivo removido no meio do caminho */ }
    }

    public static void SetMuted(bool muted)
    {
        using var device = GetDefaultDevice();
        if (device is null) return;

        try { device.AudioEndpointVolume.Mute = muted; }
        catch { /* dispositivo removido no meio do caminho */ }
    }

    private static MMDevice? GetDefaultDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
        }
        catch
        {
            // Sem dispositivo de captura padrão disponível.
            return null;
        }
    }
}
