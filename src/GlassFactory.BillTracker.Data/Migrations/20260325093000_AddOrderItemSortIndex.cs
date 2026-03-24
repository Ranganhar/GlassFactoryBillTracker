using Microsoft.EntityFrameworkCore.Migrations;
using GlassFactory.BillTracker.Data.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace GlassFactory.BillTracker.Data.Migrations
{
    [DbContext(typeof(BillTrackerDbContext))]
    [Migration("20260325093000_AddOrderItemSortIndex")]
    public partial class AddOrderItemSortIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortIndex",
                table: "OrderItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId_SortIndex",
                table: "OrderItems",
                columns: new[] { "OrderId", "SortIndex" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderItems_OrderId_SortIndex",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SortIndex",
                table: "OrderItems");
        }
    }
}
