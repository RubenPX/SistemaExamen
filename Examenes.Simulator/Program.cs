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
    }
});
sw.Stop();
cts.Cancel(); // Detenemos el monitor de conexiones

Console.WriteLine($"[Simulador] {alumnos.Count} conectados en {sw.Elapsed.TotalSeconds} segundos. Errores: {fallidos}. Tiempo: {sw.Elapsed.TotalSeconds}s");
if (fallidos > 0) return;

int eventosMaximos = 1_000_000;
long accionesEnviadas = 0;
long errores = 0;

if (int.TryParse(Environment.GetEnvironmentVariable("MAX_EVENTS"), out int maxEvents)) {
    eventosMaximos = maxEvents; // Establece el maximo en base a la variable de entorno
}

// 2. Velocímetro (Vital para ver el caos)
_ = Task.Run(async () => {
    while (true) {
        long antes = Interlocked.Read(ref accionesEnviadas);
        await Task.Delay(1000);
        long ahora = Interlocked.Read(ref accionesEnviadas);
        Console.WriteLine($"[STRESS] {ahora - antes} req/s | Total: {ahora} | Errores: {Interlocked.Read(ref errores)}");
    }
});

// 3. Envío Agresivo de eventos
sw = Stopwatch.StartNew();
var tareasDeAtaque = alumnos.Select(conn => Task.Run(async () => {
    while (Interlocked.Read(ref accionesEnviadas) < eventosMaximos) { // Límite de 1M de acciones
        var ev = new AccionEvento(Guid.NewGuid(), Random.Shared.Next(1, 10000), 1, TipoAccion.MarcaPregunta, Random.Shared.Next(1, 50), "A", DateTime.UtcNow);

        try {
            // SendAsync no espera respuesta del servidor, solo pone el mensaje en el pipe
            await conn.SendAsync("RegistrarAccion", ev);
            Interlocked.Increment(ref accionesEnviadas);

            // En lugar de Delay(1), deja que la tarea respire cada 10 mensajes para no bloquear el ThreadPool por completo.
            /**
             * diferencia de Task.Delay(1), que le dice al procesador "duerme por X milisegundos", Task.Yield() 
             * le dice: "He terminado mi turno por ahora, si hay alguien más esperando, adelante. Si no, 
             * vuelvo enseguida".
             * 
             * Al usar % 10, le estás diciendo al programa: "Ejecuta 9 envíos a toda velocidad (síncronamente),
             * y en el envío número 10, haz una pausa estratégica".
             */
            if (accionesEnviadas % 10 == 0) {
                await Task.Yield();
            }
        } catch (Exception ex) {
            Console.WriteLine(ex.Message);
            Interlocked.Increment(ref errores);
        }
    }
}));

await Task.WhenAll(tareasDeAtaque);

sw.Stop();
Console.WriteLine($"Prueba finalizada en {sw.Elapsed.TotalSeconds} segundos | Acciones enviadas: {accionesEnviadas}, fallidas: {errores}");
