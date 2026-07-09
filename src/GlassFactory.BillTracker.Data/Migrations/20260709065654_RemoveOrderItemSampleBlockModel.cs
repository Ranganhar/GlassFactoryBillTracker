using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlassFactory.BillTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrderItemSampleBlockModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SampleBlockModel",
                table: "OrderItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SampleBlockModel",
                table: "OrderItems",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }
    }
}
