using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuClinica.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPrescriptionDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eliminar la columna de texto antigua
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "PrescriptionItems");

            // Añadir la nueva columna numérica
            migrationBuilder.AddColumn<int>(
                name: "DurationInDays",
                table: "PrescriptionItems",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revierte los cambios: elimina la numérica
            migrationBuilder.DropColumn(
                name: "DurationInDays",
                table: "PrescriptionItems");

            // Vuelve a añadir la de texto
            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "PrescriptionItems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }
    }
}