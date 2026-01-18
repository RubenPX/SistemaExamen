using System.Diagnostics;
using Examenes.Domain;
using Microsoft.AspNetCore.SignalR.Client;

Console.WriteLine("Iniciando Simulador Masivo...");
var sw = Stopwatch.StartNew();
long accionesEnviadas = 0;

string? serverUrl = Environment.GetEnvironmentVariable("services__server__http__0");
Console.WriteLine($"URL Conection: {serverUrl}/examenHub");

// Conectamos 5000 alumnos en grupos de 100 para no saturar el handshake
var alumnos = new List<HubConnection>();
for (int i = 1; i <= 500; i++) {
    var conn = new HubConnectionBuilder()
        .WithUrl($"{serverUrl}/examenHub")
        .WithAutomaticReconnect()
        .Build();

    await conn.StartAsync();
    alumnos.Add(conn);
    if (i % 200 == 0) Console.WriteLine($"[Simulador] {i} alumnos conectados...");
}

Console.WriteLine($"[Simulador] Conexión completada en {sw.Elapsed.TotalSeconds}s. Iniciando ráfaga.");

// 2. Hilo de monitoreo (Velocímetro)
_ = Task.Run(async () => {
    while (true) {
        long antes = Interlocked.Read(ref accionesEnviadas);
        await Task.Delay(1000);
        long ahora = Interlocked.Read(ref accionesEnviadas);

        long diff = ahora - antes;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SIMULADOR] Velocidad: {diff} acciones/seg | Total: {ahora}");
        Console.ResetColor();
    }
});

// 3. Lógica de envío masivo
var tasks = alumnos.Select(async (conn, index) => {
    var rnd = new Random();
    int alumnoId = index + 1;
    while (true) {
        await Task.Delay(rnd.Next(10, 50));
        var ev = new AccionEvento(Guid.NewGuid(), alumnoId, 1, TipoAccion.MarcaPregunta, rnd.Next(1, 50), "A", DateTime.UtcNow);

        try {
            await conn.InvokeAsync("RegistrarAccion", ev);
            // INCREMENTO DEL CONTADOR
            Interlocked.Increment(ref accionesEnviadas);

            long contador = Interlocked.Read(ref accionesEnviadas);
            if (contador > 500_000) break;
        } catch {
            // Opcional: Contador de errores
        }
    }
});

var swe = System.Diagnostics.Stopwatch.StartNew();
await Task.WhenAll(tasks);
swe.Stop();

long contador = Interlocked.Read(ref accionesEnviadas);
Console.WriteLine($"[Simulador] Se han enviado {contador} eventos en {swe.Elapsed.TotalSeconds} segundos");