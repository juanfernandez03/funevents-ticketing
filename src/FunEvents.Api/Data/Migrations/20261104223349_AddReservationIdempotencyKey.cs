using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FunEvents.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "reservations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reservations_idempotency_key",
                table: "reservations",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reservations_idempotency_key",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "reservations");
        }
    }
}
