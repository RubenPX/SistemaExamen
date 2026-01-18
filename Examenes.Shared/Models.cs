using System.Text.Json.Serialization;

namespace Examenes.Domain;

public enum TipoAccion : byte { CargaExamen = 1, MarcaPregunta = 2, FinalizaExamen = 3 }

// Usamos Record para inmutabilidad y facilidad de transporte
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AccionEvento))]
public record AccionEvento(
    Guid EventoId,
    int AlumnoId,
    int ExamenId,
    TipoAccion Accion,
    int? PreguntaId,
    string? Valor,
    DateTime Timestamp
);

public class EstadoAlumno {
    public int AlumnoId { get; set; }
    public Dictionary<int, string> Respuestas { get; set; } = new();
    public bool Finalizado { get; set; }
    public DateTime UltimaActividad { get; set; }
}