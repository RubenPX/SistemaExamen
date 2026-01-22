using System.Threading.Channels;
using Examenes.Domain;

namespace Examenes.Server.Monitoring;

public class ChannelMonitorWorker(ChannelReader<AccionEvento> _reader) : BackgroundService {
    private readonly int _capacity = 1_000_000;

    protected override async Task ExecuteAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            int actual = _reader.Count;
            double porcentaje = (double)actual / _capacity * 100;
            if (actual > 100) {
                Console.Write($"\r[CANAL] Uso: {porcentaje:00}% ({actual}/{_capacity})");
            }
            await Task.Delay(250, ct);
        }
    }
}
