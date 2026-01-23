
using System.Threading.Channels;
using Examenes.Domain;
using StackExchange.Redis;

namespace Examenes.Server.BackgroundServices {
    public class ChannelMonitor(ChannelReader<AccionEvento> signalrReader, ChannelReader<RedisValue[]> redisReader) : BackgroundService {
        protected override Task ExecuteAsync(CancellationToken ct) {
            return Task.WhenAll([
                Monitor(signalrReader, ChannelManager.MAX_SIGANLR_SIZE, "SIGNALR", ct),
                Monitor(redisReader, ChannelManager.MAX_REDIS_SIZE, " REDIS ", ct) // Este estara siempre al 100%
            ]);
        }

        protected async Task Monitor<T>(ChannelReader<T> _reader, int capacity, string context, CancellationToken ct) {
            while (!ct.IsCancellationRequested) {
                int actual = _reader.Count;
                double porcentaje = (double)actual / capacity * 100;
                if (actual > 0) {
                    Console.WriteLine($"[CANAL {context}] Uso: {porcentaje:000}% ({actual}/{capacity})");
                }
                await Task.Delay(250, ct);
            }
        }
    }
}
