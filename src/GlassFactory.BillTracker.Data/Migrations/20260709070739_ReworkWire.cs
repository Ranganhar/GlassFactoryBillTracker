using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlassFactory.BillTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReworkWire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Wires");

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "Wires",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WireAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WireId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WireAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WireAttachments_Wires_WireId",
                        column: x => x.WireId,
                        principalTable: "Wires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WireAttachments_WireId",
                table: "WireAttachments",
                column: "WireId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WireAttachments");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "Wires");

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Wires",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }
    }
}
