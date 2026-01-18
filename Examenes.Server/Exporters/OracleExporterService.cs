using System.Data;
using System.Text.Json;
using Examenes.Domain;
using Oracle.ManagedDataAccess.Client;
using StackExchange.Redis;

namespace Examenes.Server.Exporters;

public class OracleExporterService {
    private readonly IDatabase _redis;
    private readonly string _connectionString;

    public OracleExporterService(IConnectionMultiplexer r, IConfiguration cfg) {
        _redis = r.GetDatabase();
        _connectionString = cfg.GetConnectionString("oracle")!;
    }

    public static async Task InitializeAsync(string cs) {
        using var conn = new OracleConnection(cs);
        int retries = 0;
        while (retries < 10) {
            try { await conn.OpenAsync(); break; } catch { retries++; Console.WriteLine("Esperando Oracle..."); await Task.Delay(5000); }
        }
        var cmd = new OracleCommand("SELECT count(*) FROM user_tables WHERE table_name = 'EXAMEN_RESPUESTAS'", conn);
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 0) {
            using var create = new OracleCommand(@"CREATE TABLE EXAMEN_RESPUESTAS (
                EVENTO_ID VARCHAR2(50) PRIMARY KEY, ALUMNO_ID NUMBER, EXAMEN_ID NUMBER, 
                ACCION_ID NUMBER, PREGUNTA_ID NUMBER, VALOR VARCHAR2(4000), FECHA_REGISTRO TIMESTAMP)", conn);
            await create.ExecuteNonQueryAsync();
        }
    }

    public async Task ExportarDeRedisAOracleAsync() {
        await OracleExporterService.InitializeAsync(_connectionString);

        long totalInicial = await _redis.ListLengthAsync("cola:examen");
        if (totalInicial == 0) {
            Console.WriteLine("[Oracle] No hay datos pendientes en Redis.");
            return;
        }

        using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        try {
            // 1. PREPARAR TERRENO (Mover a máxima velocidad)
            Console.WriteLine("[Oracle] Optimizando tabla para carga masiva...");
            await EjecutarComando(conn, "ALTER TABLE EXAMEN_RESPUESTAS NOLOGGING");
            try { await EjecutarComando(conn, "DROP INDEX idx_alumno_id"); } catch { /* Por si no existe */ }

            long procesados = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 2. BUCLE DE EXPORTACIÓN
            while (true) {
                var swPush = System.Diagnostics.Stopwatch.StartNew();
                var lote = new List<AccionEvento>();
                var items = await _redis.ListRightPopAsync("cola:examen", 10000);

                if (items != null) {
                    lote = items
                        .AsParallel()
                        .Select(item => JsonSerializer.Deserialize<AccionEvento>((string)item!, SourceGenerationContext.Default.AccionEvento)!)
                        .ToList();
                }

                if (lote.Count == 0) break;

                // Enviamos el lote usando Direct Path indirectamente vía BulkCopy
                await GuardarEnOracleBulk(conn, lote);
                procesados += lote.Count;

                // Visualización de progreso
                double porcentaje = (double)procesados / totalInicial * 100;
                swPush.Stop();
                Console.WriteLine($"[Oracle Exporter] {porcentaje:F2}% | {procesados}/{totalInicial} registros ({procesados} en {swPush.Elapsed.TotalMilliseconds})");
            }

            sw.Stop();
            Console.WriteLine($"[Oracle] Carga masiva terminada en {sw.Elapsed.TotalSeconds:F2}s");

        } finally {
            // 3. RECONSTRUIR (Volver al estado normal)
            Console.WriteLine("[Oracle] Reconstruyendo índices y activando LOGGING...");
            await EjecutarComando(conn, "CREATE INDEX idx_alumno_id ON EXAMEN_RESPUESTAS(ALUMNO_ID)");
            await EjecutarComando(conn, "ALTER TABLE EXAMEN_RESPUESTAS LOGGING");
            Console.WriteLine("[Oracle] Sistema listo para consultas.");
        }
    }

    private async Task GuardarEnOracleBulk(OracleConnection conn, List<AccionEvento> eventos) {
        using var dt = new DataTable();
        dt.Columns.Add("EVENTO_ID");
        dt.Columns.Add("ALUMNO_ID", typeof(int));
        dt.Columns.Add("EXAMEN_ID", typeof(int));
        dt.Columns.Add("ACCION_ID", typeof(int));
        dt.Columns.Add("PREGUNTA_ID", typeof(int));
        dt.Columns.Add("VALOR");
        dt.Columns.Add("FECHA_REGISTRO", typeof(DateTime));

        foreach (var e in eventos)
            dt.Rows.Add(e.EventoId.ToString(), e.AlumnoId, e.ExamenId, (int)e.Accion, e.PreguntaId, e.Valor, e.Timestamp);

        // Configuración de BulkCopy optimizada
        using var bulk = new OracleBulkCopy(conn) {
            DestinationTableName = "EXAMEN_RESPUESTAS",
            BulkCopyTimeout = 300,
            BatchSize = 10000 // Coincide con el tamaño de nuestro buffer
        };
        await Task.Run(() => bulk.WriteToServer(dt));
    }

    private async Task EjecutarComando(OracleConnection conn, string sql) {
        using var cmd = new OracleCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
