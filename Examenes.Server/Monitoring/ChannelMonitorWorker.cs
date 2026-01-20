using System.Threading.Channels;
using Examenes.Domain;

namespace Examenes.Server.Monitoring;

public class ChannelMonitorWorker(ChannelReader<AccionEvento> _reader, MonitorControl _control) : BackgroundService {
    private readonly int _capacity = 1_000_000;

    protected override async Task ExecuteAsync(CancellationToken ct) {
        int last = 0;
        while (!ct.IsCancellationRequested) {
            if (_control.Activo) {
                int actual = _reader.Count;
                double porcentaje = (double)actual / _capacity * 100;
                if (last != actual) {
                    last = actual;
                    Console.Write($"\r[CANAL] Uso: {porcentaje:F1}% ({actual}/{_capacity})");
                }
            }
            await Task.Delay(250, ct);
        }
    }
}
