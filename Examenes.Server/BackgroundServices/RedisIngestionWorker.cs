using System.Text.Json;
using System.Threading.Channels;
using Examenes.Domain;
using StackExchange.Redis;

namespace Examenes.Server.BackgroundServices;

public class RedisIngestionWorker(ChannelReader<AccionEvento> r, IConnectionMultiplexer c) : BackgroundService {
    private readonly ChannelReader<AccionEvento> _reader = r;
    private readonly IDatabase _db = c.GetDatabase();

    protected override async Task ExecuteAsync(CancellationToken ct) {
        const int MaxBatchSize = 10000; // Buffer para extraer de channel
        var buffer = new RedisValue[MaxBatchSize];
        int count = 0;

        try {
            while (await _reader.WaitToReadAsync(ct)) {
                while (count < MaxBatchSize && _reader.TryRead(out var e)) {
                    buffer[count++] = JsonSerializer.Serialize(e, SourceGenerationContext.Default.AccionEvento);
                }

                if (count > 0) {
                    // Envia los datos a redis en otro hilo
                    var batchToSend = new RedisValue[count];
                    Array.Copy(buffer, batchToSend, count);
                    _ = EnviarARedis(batchToSend);
                    count = 0;
                }
            }
        } catch (OperationCanceledException) { /* Manejo normal al cerrar */ }
    }

    private async Task EnviarARedis(RedisValue[] batch) {
        while (true) {
            try {
                await _db.ListLeftPushAsync("cola:examen", batch);
                break;
            } catch (Exception ex) {
                Console.WriteLine($"Error en Redis: {ex.Message}");
            }
            Console.WriteLine("Reintentando en 5 segundos");
            await Task.Delay(5000);
        }
    }
}
