using Examenes.Domain;

namespace Migrator.RedisToOracle.DB.Entity.Mappers;

public static class AccionDBMapper {
    public static AccionDB ToEntity(this AccionEvento dto) => new() {
        EventoId = Guid.NewGuid().ToString(),
        AlumnoId = dto.AlumnoId,
        ExamenId = dto.ExamenId,
        AccionId = (int)dto.Accion,
        PreguntaId = dto.PreguntaId,
        Valor = dto.Valor,
        Timestamp = dto.Timestamp
    };
}
