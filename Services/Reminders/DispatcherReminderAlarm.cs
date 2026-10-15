using System.Windows.Threading;

namespace Heimdall.Services.Reminders;

/// <summary>O alarme do agendador no thread da interface: um DispatcherTimer de um tiro só.</summary>
internal sealed class DispatcherReminderAlarm : IReminderAlarm
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal);
    private Action? _callback;

    public DispatcherReminderAlarm()
    {
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            var callback = _callback;
            _callback = null;
            callback?.Invoke();
        };
    }

    public void Set(TimeSpan delay, Action callback)
    {
        _timer.Stop();
        _callback = callback;
        // DispatcherTimer com intervalo zero ainda passa pela fila — dispara logo, sem reentrar.
        _timer.Interval = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        _timer.Start();
    }

    public void Cancel()
    {
        _timer.Stop();
        _callback = null;
    }
}
