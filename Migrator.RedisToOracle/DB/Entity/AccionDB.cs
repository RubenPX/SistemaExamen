using System.Text.Json.Serialization;

namespace Migrator.RedisToOracle.DB.Entity;

public class AccionDB {
    public string EventoId { get; set; }
    public int AlumnoId { get; set; }
    public int ExamenId { get; set; }
    public int AccionId { get; set; }
    public int? PreguntaId { get; set; }
    public string? Valor { get; set; }
    public DateTime Timestamp { get; set; }
}
