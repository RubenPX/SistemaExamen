using System.Collections.Concurrent;
using System.Diagnostics;
using Examenes.Domain;
using Microsoft.AspNetCore.SignalR.Client;

Console.WriteLine("--- MODO BERSERKER ACTIVADO ---");
var sw = Stopwatch.StartNew();

// 1. Conexión Masiva en Paralelo
int cantidadConexionesObjetivo = 100;

if (int.TryParse(Environment.GetEnvironmentVariable("MAX_CONNECTIONS"), out int maxConex)) {
    cantidadConexionesObjetivo = maxConex; // Establece el maximo en base a la variable de entorno
}

int conectados = 0;
int fallidos = 0;
var alumnos = new ConcurrentBag<HubConnection>();

// Monitor de progreso de conexiones (Hilo separado)
var cts = new CancellationTokenSource();
_ = Task.Run(async () => {
    while (!cts.Token.IsCancellationRequested) {
        Console.WriteLine($"[CONECTANDO] Exitos: {conectados} | Fallidos: {fallidos} | Progreso: {(conectados + fallidos) * 100 / cantidadConexionesObjetivo}%");
        await Task.Delay(500); // Actualiza cada medio segundo
    }
}, cts.Token);


// Servidor al que se atacara
if (Environment.GetEnvironmentVariable("services__server__http__0") is null) throw new Exception("Fallo al leer variables de entorno");
string? serverUrl = Environment.GetEnvironmentVariable("services__server__http__0") ?? "http://localhost:5000";

await Parallel.ForEachAsync(Enumerable.Range(1, cantidadConexionesObjetivo), new ParallelOptions { MaxDegreeOfParallelism = 100 }, async (_, _) => {
    var conn = new HubConnectionBuilder()
        .WithUrl($"{serverUrl}/examenHub")
        .WithAutomaticReconnect()
        .Build();

    try {
        await conn.StartAsync();
        Interlocked.Increment(ref conectados);
        alumnos.Add(conn);
    } catch {
        Interlocked.Increment(ref fallidos);
        throw;
    }
});
sw.Stop();
cts.Cancel(); // Detenemos el monitor de conexiones

Console.WriteLine($"[Simulador] {alumnos.Count} conectados en {sw.Elapsed.TotalSeconds} segundos. Errores: {fallidos}. Tiempo: {sw.Elapsed.TotalSeconds}s");
if (fallidos > 0) return;

int eventosMaximos = 500;
long accionesEnviadas = 0;
long errores = 0;

if (int.TryParse(Environment.GetEnvironmentVariable("MAX_EVENTS")?.Replace("_", ""), out int maxEvents)) {
    eventosMaximos = maxEvents; // Establece el maximo en base a la variable de entorno
}

// 2. Velocímetro (Vital para ver el caos)
_ = Task.Run(async () => {
    while (true) {
        long antes = Interlocked.Read(ref accionesEnviadas);
        await Task.Delay(1000);
        long ahora = Interlocked.Read(ref accionesEnviadas);
        Console.WriteLine($"[STRESS] {(ahora - antes) * 2:N0} req/s | Total: {ahora} | Errores: {Interlocked.Read(ref errores)}");
    }
});

// 3. Envío Agresivo de eventos
sw = Stopwatch.StartNew();

var tareasAtaque = alumnos.Select(async conn => {
    var ev = new AccionEvento(Guid.NewGuid(), 1, 1, TipoAccion.MarcaPregunta, 1, "A", DateTime.UtcNow);

    while (Interlocked.Read(ref accionesEnviadas) < eventosMaximos) {
        try {
            await conn.SendAsync("RegistrarAccion", ev);
            Interlocked.Increment(ref accionesEnviadas);
            if (accionesEnviadas % 100 == 0) await Task.Delay(1);
        } catch {
            Interlocked.Increment(ref errores);
            break;
        }
    }
});

await Task.WhenAll(tareasAtaque);
Console.WriteLine($"Prueba finalizada en {sw.Elapsed.TotalSeconds} segundos | Acciones enviadas: {accionesEnviadas}, fallidas: {errores}");
