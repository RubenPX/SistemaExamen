using System.Text.Json.Serialization;

namespace Examenes.Domain;

public enum TipoAccion : byte { CargaExamen = 1, MarcaPregunta = 2, FinalizaExamen = 3 }

// Usamos Record para inmutabilidad y facilidad de transporte
public readonly record struct AccionEvento(
    Guid EventoId,
    int AlumnoId,
    int ExamenId,
    TipoAccion Accion,
    int? PreguntaId,
    string? Valor,
    DateTime Timestamp
);
