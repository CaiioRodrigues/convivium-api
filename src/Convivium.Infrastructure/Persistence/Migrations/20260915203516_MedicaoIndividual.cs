using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Convivium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MedicaoIndividual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meter_readings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competence = table.Column<int>(type: "integer", nullable: false),
                    utility = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    previous_reading = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    current_reading = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    read_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meter_readings", x => x.id);
                    table.ForeignKey(
                        name: "fk_meter_readings_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_condominium_id_utility_competence_unit_id",
                table: "meter_readings",
                columns: new[] { "condominium_id", "utility", "competence", "unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meter_readings_unit_id_utility_competence",
                table: "meter_readings",
                columns: new[] { "unit_id", "utility", "competence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_readings");
        }
    }
}
