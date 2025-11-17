// En: TuClinica.DataAccess/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking; // Para ChangeTracker
using System;                                     // Para IServiceProvider
using System.Collections.Generic;                 // Para List
using System.Linq;                                // Para Linq
using System.Text.Json;                           // Para JsonSerializer
using System.Threading.Tasks;                     // Para Task
using TuClinica.Core.Interfaces.Services;         // ¡¡IMPORTANTE!! Para IAuthService
using TuClinica.Core.Models;

namespace TuClinica.DataAccess
{
    public class AppDbContext : DbContext
    {
        // --- INYECTAR IAuthService ---
        private readonly IAuthService _authService;

        // --- Tablas Existentes ---
        public DbSet<User> Users { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Treatment> Treatments { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<BudgetLineItem> BudgetLineItems { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<Dosage> Dosages { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<PrescriptionItem> PrescriptionItems { get; set; }

        // --- Tabla de Log ---
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<ClinicalEntry> ClinicalEntries { get; set; }
        public DbSet<ToothTreatment> ToothTreatments { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentAllocation> PaymentAllocations { get; set; }

        // --- INICIO DE LA MODIFICACIÓN ---
        // 1. Añadir la nueva tabla de Plan de Tratamiento
        public DbSet<TreatmentPlanItem> TreatmentPlanItems { get; set; }
        // 2. Añadir la nueva tabla de documentos
        public DbSet<LinkedDocument> LinkedDocuments { get; set; }

        // --- NUEVA TABLA DE PACKS (DE LA MEJORA ANTERIOR) ---
        public DbSet<TreatmentPackItem> TreatmentPackItems { get; set; }
        // --- FIN DE LA MODIFICACIÓN ---


        // --- CONSTRUCTOR MODIFICADO ---
        // La inyección de dependencias es suficientemente inteligente
        // para pasar DbContextOptions Y TAMBIÉN IAuthService.
        public AppDbContext(DbContextOptions<AppDbContext> options, IAuthService authService)
            : base(options)
        {
            _authService = authService; // Guardamos el servicio de autenticación
        }

        // --- MÉTODO OnModelCreating (CORREGIDO) ---
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Regla existente para que BudgetNumber sea único
            modelBuilder.Entity<Budget>()
                .HasIndex(b => b.BudgetNumber)
                .IsUnique();

            // --- INICIO DE LA CORRECCIÓN DE ERROR ---
            // Aquí le decimos a EF cómo manejar las dos relaciones
            // entre Treatment y TreatmentPackItem.

            // 1. Relación "Pack" -> "Items"
            // Un Tratamiento (Padre/Pack) tiene MUCHOS PackItems.
            // La colección `t_parent.PackItems` se llena usando la clave `pi.ParentTreatmentId`.
            modelBuilder.Entity<Treatment>()
                .HasMany(t_parent => t_parent.PackItems)    // La colección ICollection<TreatmentPackItem> en Treatment
                .WithOne(pi => pi.ParentTreatment)          // La propiedad de navegación 'ParentTreatment' en TreatmentPackItem
                .HasForeignKey(pi => pi.ParentTreatmentId)  // La clave foránea que las une
                .OnDelete(DeleteBehavior.Cascade); // Si borras el Pack, se borran sus filas de componentes.

            // 2. Relación "Componente" -> "UsadoEnPacks"
            // Un Tratamiento (Hijo/Componente) puede estar en MUCHOS PackItems.
            // No tenemos una colección de vuelta en Treatment (lo cual está bien).
            modelBuilder.Entity<TreatmentPackItem>()
                .HasOne(pi => pi.ChildTreatment)            // La propiedad 'ChildTreatment' en TreatmentPackItem
                .WithMany()                                 // No hay propiedad de colección de vuelta
                .HasForeignKey(pi => pi.ChildTreatmentId)   // La clave foránea que las une
                .OnDelete(DeleteBehavior.Restrict); // IMPIDE borrar un tratamiento (ej. "Limpieza") si está siendo usado en un pack.

            // --- FIN DE LA CORRECCIÓN DE ERROR ---
        }

        // --- SOBRESCRIBIR SaveChangesAsync ---
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // 1. Obtener la lista de cambios ANTES de guardarlos
            var logEntries = GetLogEntriesBeforeSave();

            // 2. Guardar los cambios principales (Patient, Budget, etc.)
            var result = await base.SaveChangesAsync(cancellationToken);

            // 3. Si hubo cambios que registrar, guardamos los logs
            if (logEntries.Any())
            {
                // Asignamos los IDs generados por la BD a las entradas de log "Create"
                UpdateLogEntriesWithGeneratedIds(logEntries);

                await this.ActivityLogs.AddRangeAsync(logEntries.Select(le => le.Log), cancellationToken);
                await base.SaveChangesAsync(cancellationToken); // Guarda los logs
            }

            return result;
        }

        private List<LogEntryTemp> GetLogEntriesBeforeSave()
        {
            ChangeTracker.DetectChanges();
            var entries = new List<LogEntryTemp>();
            var username = _authService.CurrentUser?.Username ?? "System"; // Obtener usuario loguado

            foreach (var entry in ChangeTracker.Entries())
            {
                // Ignorar los propios logs, entidades sin cambios o no rastreadas
                if (entry.Entity is ActivityLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                // --- ¡FILTRO DE ENTIDADES! ---
                // Aquí decides qué entidades auditar. Empezamos con Patient.
                // Puedes añadir: || entry.Entity is Budget || entry.Entity is Prescription
                // --- INICIO DE LA MODIFICACIÓN ---
                // 2. Añadir TreatmentPlanItem y LinkedDocument al filtro de auditoría
                if (!(entry.Entity is Patient || entry.Entity is User || entry.Entity is Budget || entry.Entity is ClinicalEntry || entry.Entity is TreatmentPlanItem || entry.Entity is LinkedDocument))
                    continue;
                // --- FIN DE LA MODIFICACIÓN ---

                var logEntry = new LogEntryTemp(entry)
                {
                    Log = new ActivityLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Username = username,
                        EntityType = entry.Entity.GetType().Name
                    }
                };

                switch (entry.State)
                {
                    case EntityState.Added:
                        logEntry.Log.ActionType = "Create";
                        // Guardamos el objeto entero como "nuevo"
                        logEntry.Log.Details = JsonSerializer.Serialize(entry.CurrentValues.ToObject());
                        break;

                    case EntityState.Modified:
                        logEntry.Log.ActionType = "Update";
                        logEntry.Log.EntityId = entry.Property("Id").CurrentValue as int?;

                        var changes = new Dictionary<string, object>();
                        foreach (var prop in entry.OriginalValues.Properties)
                        {
                            var original = entry.OriginalValues[prop];
                            var current = entry.CurrentValues[prop];
                            if (!Equals(original, current))
                            {
                                changes[prop.Name] = new { old = original, @new = current };
                            }
                        }
                        logEntry.Log.Details = JsonSerializer.Serialize(changes);
                        break;

                    case EntityState.Deleted:
                        logEntry.Log.ActionType = "Delete";
                        logEntry.Log.EntityId = entry.Property("Id").CurrentValue as int?;
                        // Guardamos el objeto entero que se borró
                        logEntry.Log.Details = JsonSerializer.Serialize(entry.OriginalValues.ToObject());
                        break;
                }
                entries.Add(logEntry);
            }
            return entries;
        }

        private void UpdateLogEntriesWithGeneratedIds(List<LogEntryTemp> logEntries)
        {
            foreach (var logEntry in logEntries)
            {
                if (logEntry.Log.ActionType == "Create")
                {
                    // El EntityEntry ahora tiene el ID asignado por la BD
                    logEntry.Log.EntityId = logEntry.Entry.Property("Id").CurrentValue as int?;

                    // Actualizamos el JSON con el ID
                    var obj = logEntry.Entry.CurrentValues.ToObject();
                    logEntry.Log.Details = JsonSerializer.Serialize(obj);
                }
            }
        }

        // Clase auxiliar interna para mantener la referencia al EntityEntry
        private class LogEntryTemp
        {
            public EntityEntry Entry { get; }
            public ActivityLog Log { get; set; }
            public LogEntryTemp(EntityEntry entry) { Entry = entry; }
        }
    }
}