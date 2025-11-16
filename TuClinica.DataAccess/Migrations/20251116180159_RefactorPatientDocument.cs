using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuClinica.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPatientDocument : Migration
    {
        /// <inheritdoc />
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Renombrar la columna existente para no perder datos
            migrationBuilder.RenameColumn(
                name: "DniNie",
                table: "Patients",
                newName: "DocumentNumber");

            // 2. Añadir la nueva columna para el tipo de documento
            // (Por defecto, todos los pacientes existentes serán 'DNI')
            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0); // 0 = DNI (según tu Enum)
        }
        /// <inheritdoc />
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Renombrar la columna de vuelta
            migrationBuilder.RenameColumn(
                name: "DocumentNumber",
                table: "Patients",
                newName: "DniNie");

            // 2. Eliminar la columna de tipo
            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Patients");
        }
    }
}
