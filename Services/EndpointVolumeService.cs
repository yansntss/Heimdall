using NAudio.CoreAudioApi;

namespace Heimdall.Services;

/// <summary>
/// Volume e mudo de um dispositivo padrão do Windows via Core Audio API (NAudio) — saída
/// (<see cref="Speakers"/>, o volume geral do PC) ou captura (<see cref="Microphone"/>), os
/// mesmos níveis de Configurações → Som. Cada chamada resolve o dispositivo padrão de novo,
/// então trocar de dispositivo no Windows já reflete no próximo refresh do widget sem
/// precisar recarregar a barra.
/// </summary>
internal sealed class EndpointVolumeService
{
    public static readonly EndpointVolumeService Speakers = new(DataFlow.Render, Role.Multimedia);
    public static readonly EndpointVolumeService Microphone = new(DataFlow.Capture, Role.Console);

    private readonly DataFlow _flow;
    private readonly Role _role;

    private EndpointVolumeService(DataFlow flow, Role role)
    {
        _flow = flow;
        _role = role;
    }

    /// <summary>null quando não há dispositivo padrão (nenhum conectado ou todos desativados).</summary>
    public (float Volume, bool Muted, string Name)? GetState()
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

    public void SetVolume(float value)
    {
        using var device = GetDefaultDevice();
        if (device is null) return;

        try { device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f); }
        catch { /* dispositivo removido no meio do caminho */ }
    }

    public void SetMuted(bool muted)
    {
        using var device = GetDefaultDevice();
        if (device is null) return;

        try { device.AudioEndpointVolume.Mute = muted; }
        catch { /* dispositivo removido no meio do caminho */ }
    }

    private MMDevice? GetDefaultDevice()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDefaultAudioEndpoint(_flow, _role);
        }
        catch
        {
            // Sem dispositivo padrão disponível.
            return null;
        }
    }
}
