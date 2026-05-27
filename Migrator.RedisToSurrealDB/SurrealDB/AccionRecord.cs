using System;
using System.Collections.Generic;
using System.Text;
using Examenes.Domain;
using SurrealDb.Net.Models;

namespace Migrator.RedisToSurrealDB.SurrealDB;

public class AccionRecord : IRecord {
    public RecordId? Id { get; set; } = RecordId.From("AccionRecord", Guid.NewGuid());
    public int AlumnoId { get; set; }
    public int ExamenId { get; set; }
    public TipoAccion Accion { get; set; }
    public int? PreguntaId { get; set; }
    public string? Valor { get; set; }
    public DateTime Timestamp { get; set; }
}

public static class Mapper {
    public static AccionRecord ToAccionRecord(this AccionEvento ev) {
        return new AccionRecord() {
            AlumnoId = ev.AlumnoId,
            ExamenId = ev.ExamenId,
            PreguntaId = ev.PreguntaId,
            Timestamp = ev.Timestamp,
            Accion = ev.Accion,
            Valor = ev.Valor
        };
    }
}
