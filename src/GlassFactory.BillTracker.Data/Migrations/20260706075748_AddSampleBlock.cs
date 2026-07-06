using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlassFactory.BillTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleBlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SampleBlockModel",
                table: "OrderItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SampleBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    WireId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleBlocks_Wires_WireId",
                        column: x => x.WireId,
                        principalTable: "Wires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SampleBlocks_Model",
                table: "SampleBlocks",
                column: "Model",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SampleBlocks_WireId",
                table: "SampleBlocks",
                column: "WireId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SampleBlocks");

            migrationBuilder.DropColumn(
                name: "SampleBlockModel",
                table: "OrderItems");
        }
    }
}
