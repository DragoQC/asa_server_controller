using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace asa_server_controller.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExposedGamePort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_NfsShareInvites_InviteKey",
                table: "NfsShareInvites",
                column: "InviteKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mods_CurseForgeModId",
                table: "Mods",
                column: "CurseForgeModId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginMethodTypes_Name",
                table: "LoginMethodTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OneTimeVpnKey",
                table: "Invitations",
                column: "OneTimeVpnKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NfsShareInvites_InviteKey",
                table: "NfsShareInvites");

            migrationBuilder.DropIndex(
                name: "IX_Mods_CurseForgeModId",
                table: "Mods");

            migrationBuilder.DropIndex(
                name: "IX_LoginMethodTypes_Name",
                table: "LoginMethodTypes");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_OneTimeVpnKey",
                table: "Invitations");
        }
    }
}
