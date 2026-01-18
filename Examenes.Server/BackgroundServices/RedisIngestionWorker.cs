using Examenes.Domain;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Channels;

namespace Examenes.Server.BackgroundServices;

public class RedisIngestionWorker : BackgroundService
{
    private readonly ChannelReader<AccionEvento> _reader;
    private readonly IDatabase _db;

    public RedisIngestionWorker(ChannelReader<AccionEvento> r, IConnectionMultiplexer c)
    {
        _reader = r;
        _db = c.GetDatabase();
    }
    public IDatabase GetDatabase() => _db;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var buffer = new List<RedisValue>();

        while (!ct.IsCancellationRequested)
        {
            // Intentamos leer todo lo que haya en el canal en este momento
            while (_reader.TryRead(out var e))
            {
                buffer.Add(JsonSerializer.Serialize(e));
                if (buffer.Count >= 2000) break;
            }

            if (buffer.Count > 0)
            {
                // Usamos Push masivo: Un solo comando para 100 registros
                await _db.ListLeftPushAsync("cola:examen", buffer.ToArray());
                buffer.Clear();
            }
            else
            {
                // Si el canal está vacío, esperamos un poco para no quemar CPU
                await _reader.WaitToReadAsync(ct);
            }
        }
    }
}
