using System.Text.Json.Serialization;

namespace Examenes.Domain;

// Serialización Optimizada (Source Generator)
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AccionEvento))]
public partial class SourceGenerationContext : JsonSerializerContext { }

public enum TipoAccion : byte {
    CargaExamen = 1,
    MarcaPregunta = 2,
    FinalizaExamen = 3
}

// Usamos Record para inmutabilidad y facilidad de transporte
public readonly record struct AccionEvento(
    int AlumnoId,
    int ExamenId,
    TipoAccion Accion,
    int? PreguntaId,
    string? Valor,
    DateTime Timestamp
);
