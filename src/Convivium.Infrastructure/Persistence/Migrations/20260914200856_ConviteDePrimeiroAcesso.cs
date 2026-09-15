using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Convivium.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConviteDePrimeiroAcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invite_token_expires_at",
                table: "people",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invite_token_hash",
                table: "people",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_people_invite_token_hash",
                table: "people",
                column: "invite_token_hash",
                unique: true,
                filter: "invite_token_hash IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_people_invite_token_hash",
                table: "people");

            migrationBuilder.DropColumn(
                name: "invite_token_expires_at",
                table: "people");

            migrationBuilder.DropColumn(
                name: "invite_token_hash",
                table: "people");
        }
    }
}
