using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuClinica.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddDateOfBirthToPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Patients",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Patients");
        }
    }
}