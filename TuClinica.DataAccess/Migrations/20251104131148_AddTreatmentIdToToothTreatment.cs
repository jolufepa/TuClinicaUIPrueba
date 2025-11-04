using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuClinica.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTreatmentIdToToothTreatment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Etapa 1: Añadir la columna como NULLABLE (sin restricción FK) ---
            // Esto es compatible con SQLite y no dispara el error 19.
            migrationBuilder.AddColumn<int>(
                name: "TreatmentId",
                table: "ToothTreatments",
                type: "INTEGER",
                nullable: true, // <--- CAMBIO CRÍTICO: Permitimos nulos temporalmente
                defaultValue: null);
                
            // --- Etapa 2: Asegurar la validez de los datos y llenado ---
            
            // 1. Insertar un tratamiento por defecto con ID=1 si el catálogo está vacío.
            // Es NECESARIO que exista una fila en la tabla "Treatments" con ID=1 para la clave foránea.
            // Usamos INSERT OR IGNORE para no fallar si ya existe.
            migrationBuilder.Sql(
                @"INSERT OR IGNORE INTO ""Treatments"" (""Id"", ""Name"", ""Description"", ""DefaultPrice"", ""IsActive"") 
                VALUES (1, 'Tratamiento Antiguo', 'Placeholder para datos de migración', 0.00, 0);"
            );

            // 2. Actualizar las filas existentes en ToothTreatments
            // para que apunten a ese ID=1 (en lugar de NULL, que fallaría).
            migrationBuilder.Sql(
                @"UPDATE ""ToothTreatments"" SET ""TreatmentId"" = 1 WHERE ""TreatmentId"" IS NULL;"
            );

            // --- Etapa 3: Añadir la Clave Foránea y el Índice (Ahora que no hay NULOS) ---

            // Añadimos el índice y la restricción FK con el campo establecido como NOT NULL.
            migrationBuilder.CreateIndex(
                name: "IX_ToothTreatments_TreatmentId",
                table: "ToothTreatments",
                column: "TreatmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ToothTreatments_Treatments_TreatmentId",
                table: "ToothTreatments",
                column: "TreatmentId",
                principalTable: "Treatments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // 4. Actualizar la columna a NOT NULL (solo para asegurar la estructura final del modelo)
            // Esto se logra con un paso final de ALTER COLUMN que EF Core maneja internamente.
            migrationBuilder.AlterColumn<int>(
                name: "TreatmentId",
                table: "ToothTreatments",
                nullable: false,
                defaultValue: 1); // El valor por defecto asegura que las futuras inserciones tengan ID 1 si no se especifica.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ToothTreatments_Treatments_TreatmentId",
                table: "ToothTreatments");

            migrationBuilder.DropIndex(
                name: "IX_ToothTreatments_TreatmentId",
                table: "ToothTreatments");

            migrationBuilder.DropColumn(
                name: "TreatmentId",
                table: "ToothTreatments");
        }
    }
}
