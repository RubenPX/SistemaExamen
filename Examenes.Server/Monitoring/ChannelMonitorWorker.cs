using System.Threading.Channels;
using Examenes.Domain;

namespace Examenes.Server.Monitoring;

public class ChannelMonitorWorker : BackgroundService {
    private readonly ChannelReader<AccionEvento> _reader;
    private readonly MonitorControl _control;
    private readonly int _capacity = 100000;

    public ChannelMonitorWorker(ChannelReader<AccionEvento> reader, MonitorControl control) {
        _reader = reader;
        _control = control;
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            if (_control.Activo) {
                int actual = _reader.Count;
                double porcentaje = (double)actual / _capacity * 100;
                if (porcentaje != 0) Console.Write($"\r[CANAL] Uso: {porcentaje:F1}% ({actual}/{_capacity})    ");
            }

            // Si no está activo, simplemente no escribe nada en consola
            await Task.Delay(500, ct);
        }
    }
}
