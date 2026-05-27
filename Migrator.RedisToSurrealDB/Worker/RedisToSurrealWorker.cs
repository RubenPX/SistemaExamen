using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Examenes.Domain;
using Microsoft.Extensions.Hosting;
using Migrator.RedisToSurrealDB.SurrealDB;
using StackExchange.Redis;
using SurrealDb.Net.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Migrator.RedisToSurrealDB.Worker;

internal class RedisToSurrealWorker(IConnectionMultiplexer redis, SurrealDBInstancer surrealInstancer) : BackgroundService {
    private const int BatchSize = 500;
    private int errores = 0;
    private long itemsProcesados = 0;
    private long itemsTotales = 0;
    private long last = 0;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var db = redis.GetDatabase();
        

        itemsTotales = await db.ListLengthAsync("cola:examen");
        if (itemsTotales == 0) {
            Console.WriteLine("[Oracle Exporter] No hay datos pendientes en Redis.");
            return;
        }

        _ = SenderMonitor(stoppingToken);
        var tasks = Enumerable.Range(0, 100).Select(idx => SurrealDBSender(db, stoppingToken));
        await Parallel.ForEachAsync(tasks, async (tsk, ct) => await tsk);
    }

    private async Task SenderMonitor(CancellationToken stoppingToken) {
        
        while (!stoppingToken.IsCancellationRequested) {
            double porcentaje = (double)itemsProcesados / itemsTotales * 100;
            double procesados = itemsProcesados - last;
            last = itemsProcesados;

            Console.WriteLine($"[Oracle Exporter] {porcentaje:F2}% | {itemsProcesados}/{itemsTotales} registros (rps: {procesados:N0})");

            await Task.Delay(1000); 
        }
    }

    public async Task SurrealDBSender(IDatabase db, CancellationToken stoppingToken) {
        var surrealConex = await surrealInstancer.NewConnection();

        while (!stoppingToken.IsCancellationRequested) {
            var items = await db.ListRightPopAsync("cola:examen", BatchSize);

            // Extracción
            var lote = new AccionRecord[0];
            if (items != null && items.Length != 0) {
                lote = items
                    .AsParallel()
                    .Select(item => JsonSerializer.Deserialize((string)item!, SourceGenerationContext.Default.AccionEvento))
                    .Select(item => item.ToAccionRecord())
                    .ToArray();
            } else {
                await Task.Delay(1000);
                break;
            }

            // Envio
            try {
                await surrealConex.Insert("eventos", lote);
                Interlocked.Add(ref itemsProcesados, lote.Length);
            } catch (Exception ex) {
                Console.WriteLine($"[Oracle Exporter][ERROR] fallo al procesar items. Devolviendo a redis... | {ex.Message}");
                await db.ListLeftPushAsync("cola:examen", items);
                errores++;
                if (errores > 5) {
                    Console.WriteLine($"[Oracle Exporter][ERROR] Se han detectado mas de 10 errores. Parando ejecución...");
                    return;
                }
            }
        }
    }
}
