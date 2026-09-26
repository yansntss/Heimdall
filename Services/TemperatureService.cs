using System.Security.Principal;
using LibreHardwareMonitor.Hardware;

namespace Heimdall.Services;

/// <summary>
/// Sensores de temperatura de CPU/GPU via LibreHardwareMonitorLib — WMI
/// (MSAcpi_ThermalZoneTemperature) é pouco confiável em hardware moderno. Precisa do
/// Heimdall rodando como administrador pra acessar os sensores (driver de baixo nível);
/// sem isso, nem tenta abrir — <see cref="IsAvailable"/> fica false e quem chama mostra
/// "—" em vez de arriscar uma exceção do driver.
/// </summary>
internal sealed class TemperatureService : IDisposable
{
    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware) sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    private readonly Computer? _computer;
    private readonly UpdateVisitor _visitor = new();

    public bool IsAvailable { get; }

    public TemperatureService()
    {
        if (!IsElevated()) return;

        try
        {
            _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            _computer.Open();
            IsAvailable = true;
        }
        catch
        {
            // Driver de sensores falhou em abrir (hardware incomum, política do SO, etc.)
            // — trata igual a "sem admin": widget mostra "—", nunca derruba o app.
            _computer = null;
            IsAvailable = false;
        }
    }

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>Temperatura atual de CPU/GPU em °C — null no campo que não achou sensor (ou se o serviço não está disponível).</summary>
    public (double? Cpu, double? Gpu) ReadTemperatures()
    {
        if (_computer is null) return (null, null);

        try
        {
            _computer.Accept(_visitor);

            double? cpu = null, gpu = null;
            foreach (var hardware in _computer.Hardware)
            {
                switch (hardware.HardwareType)
                {
                    case HardwareType.Cpu:
                        cpu ??= FindTemperature(hardware, "Package", "Tctl", "Tdie");
                        break;
                    case HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel:
                        gpu ??= FindTemperature(hardware, "Core", "Hot Spot");
                        break;
                }
            }
            return (cpu, gpu);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>Prefere um sensor de temperatura pelo nome (ex.: "CPU Package"); sem nenhum desses, usa o de maior valor (proxy razoável pro núcleo mais quente). Olha sub-hardware também (algumas GPUs reportam sensores lá).</summary>
    private static double? FindTemperature(IHardware hardware, params string[] preferredNameContains)
    {
        ISensor? best = null;

        void Scan(IEnumerable<ISensor> sensors)
        {
            foreach (var sensor in sensors)
            {
                if (sensor.SensorType != SensorType.Temperature || sensor.Value is not { } value) continue;
                foreach (var name in preferredNameContains)
                {
                    if (sensor.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    {
                        best = sensor;
                        return;
                    }
                }
                if (best is null || value > best.Value) best = sensor;
            }
        }

        Scan(hardware.Sensors);
        foreach (var sub in hardware.SubHardware) Scan(sub.Sensors);

        return best?.Value;
    }

    public void Dispose() => _computer?.Close();
}
