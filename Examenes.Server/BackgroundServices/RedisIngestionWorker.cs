using System.Text.Json;
using System.Threading.Channels;
using Examenes.Domain;
using StackExchange.Redis;

namespace Examenes.Server.BackgroundServices;

public class RedisIngestionWorker(
    ChannelReader<AccionEvento> signalr_reader,
    IConnectionMultiplexer c
) : BackgroundService {
    private readonly IDatabase _db = c.GetDatabase();

    protected override async Task ExecuteAsync(CancellationToken ct) {
        await Task.WhenAll([
            EmpaquetadorWorker(ct),
            EmpaquetadorWorker(ct)
        ]);
    }

    private async Task EmpaquetadorWorker(CancellationToken ct) {
        const int MaxBatchSize = 10_000; // Buffer para extraer de channel
        var buffer = new RedisValue[MaxBatchSize];
        int count = 0;

        // Empaquetamos los datos en packs de 10.000
        try {
            while (await signalr_reader.WaitToReadAsync(ct)) {
                while (count < MaxBatchSize && signalr_reader.TryRead(out var e)) {
                    buffer[count++] = JsonSerializer.Serialize(e, SourceGenerationContext.Default.AccionEvento);
                }

                if (count > 0) {
                    // Envia los datos a redis en otro hilo
                    var batchToSend = new RedisValue[count];
                    Array.Copy(buffer, batchToSend, count);
                    await _db.ListLeftPushAsync("cola:examen", batchToSend, flags: CommandFlags.FireAndForget);
                    count = 0;
                }
            }
        } catch (OperationCanceledException) { /* Manejo normal al cerrar */ }
    }
}
