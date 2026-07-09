using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlassFactory.BillTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReworkSampleBlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SampleBlocks_Wires_WireId",
                table: "SampleBlocks");

            migrationBuilder.DropIndex(
                name: "IX_SampleBlocks_WireId",
                table: "SampleBlocks");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "SampleBlocks");

            migrationBuilder.DropColumn(
                name: "WireId",
                table: "SampleBlocks");

            migrationBuilder.AddColumn<string>(
                name: "Customer",
                table: "SampleBlocks",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrderTime",
                table: "SampleBlocks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SampleBlockAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SampleBlockId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleBlockAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleBlockAttachments_SampleBlocks_SampleBlockId",
                        column: x => x.SampleBlockId,
                        principalTable: "SampleBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SampleBlockAttachments_SampleBlockId",
                table: "SampleBlockAttachments",
                column: "SampleBlockId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SampleBlockAttachments");

            migrationBuilder.DropColumn(
                name: "Customer",
                table: "SampleBlocks");

            migrationBuilder.DropColumn(
                name: "OrderTime",
                table: "SampleBlocks");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "SampleBlocks",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "WireId",
                table: "SampleBlocks",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SampleBlocks_WireId",
                table: "SampleBlocks",
                column: "WireId");

            migrationBuilder.AddForeignKey(
                name: "FK_SampleBlocks_Wires_WireId",
                table: "SampleBlocks",
                column: "WireId",
                principalTable: "Wires",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
