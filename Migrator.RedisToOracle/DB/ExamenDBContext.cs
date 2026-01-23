using Microsoft.EntityFrameworkCore;
using Migrator.RedisToOracle.DB.Entity;

namespace Migrator.RedisToOracle.DB;

internal class ExamenDBContext(DbContextOptions<ExamenDBContext> options) : DbContext(options) {
    public DbSet<AccionDB> Acciones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        var entity = modelBuilder.Entity<AccionDB>();

        entity.ToTable("FREEPDB1");
        entity.HasKey(e => e.EventoId);

        entity.HasIndex(e => e.EventoId)
              .HasDatabaseName("IDX_EVENTO_ID");

        entity.Property(e => e.Valor)
              .HasMaxLength(500); // Evita que cree un CLOB innecesario

        entity.Property(e => e.Timestamp)
              .HasColumnType("TIMESTAMP(6)");
    }
}
