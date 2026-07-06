using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlassFactory.BillTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemModelHoleFeeAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LineAmount",
                table: "OrderItems",
                newName: "Amount");

            migrationBuilder.AddColumn<decimal>(
                name: "HoleFee",
                table: "OrderItems",
                type: "TEXT",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "OrderItems",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoleFee",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "OrderItems");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "OrderItems",
                newName: "LineAmount");
        }
    }
}
