using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace devopsnet.Migrations
{
    /// <inheritdoc />
    public partial class AddNodePortToPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NodePort",
                table: "Pipelines",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NodePort",
                table: "Pipelines");
        }
    }
}
