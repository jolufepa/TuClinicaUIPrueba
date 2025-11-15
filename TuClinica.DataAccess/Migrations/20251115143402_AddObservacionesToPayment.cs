using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuClinica.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddObservacionesToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Observaciones",
                table: "Payments",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Observaciones",
                table: "Payments");
        }
    }
}
