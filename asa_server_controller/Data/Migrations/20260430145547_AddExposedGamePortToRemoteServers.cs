using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace asa_server_controller.Data.Migrations
{
	/// <inheritdoc />
	public partial class AddExposedGamePortToRemoteServers : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.AddColumn<int>(
					name: "ExposedGamePort",
					table: "RemoteServers",
					type: "INTEGER",
					nullable: true);
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropColumn(
					name: "ExposedGamePort",
					table: "RemoteServers");
		}
	}
}
