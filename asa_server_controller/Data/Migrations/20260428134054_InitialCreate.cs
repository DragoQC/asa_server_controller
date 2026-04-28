using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace asa_server_controller.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClusterSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ClusterId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurseForgeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurseForgeSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpHost = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SmtpUsername = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SmtpPassword = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FromEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FromName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginMethodTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginMethodTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CurseForgeModId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    WebsiteUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    LogoUrl = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    DownloadCount = table.Column<long>(type: "INTEGER", nullable: false),
                    DateModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemoteServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    InviteStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ValidationStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    MapName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    MaxPlayers = table.Column<int>(type: "INTEGER", nullable: true),
                    GamePort = table.Column<int>(type: "INTEGER", nullable: true),
                    ServerInfoCheckedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ApiKeyHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VpnServerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AllowedIps = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PersistentKeepalive = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PresharedKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VpnServerSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemoteServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    RemoteUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ClusterId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    OneTimeVpnKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    InviteLink = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    InviteStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ValidationStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_RemoteServers_RemoteServerId",
                        column: x => x.RemoteServerId,
                        principalTable: "RemoteServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NfsShareInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemoteServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    InviteKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    InviteLink = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NfsShareInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NfsShareInvites_RemoteServers_RemoteServerId",
                        column: x => x.RemoteServerId,
                        principalTable: "RemoteServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemoteServerMods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RemoteServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModEntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteServerMods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteServerMods_Mods_ModEntityId",
                        column: x => x.ModEntityId,
                        principalTable: "Mods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemoteServerMods_RemoteServers_RemoteServerId",
                        column: x => x.RemoteServerId,
                        principalTable: "RemoteServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    TwoFactorSecret = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserLoginMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoginMethodTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLoginMethods_LoginMethodTypes_LoginMethodTypeId",
                        column: x => x.LoginMethodTypeId,
                        principalTable: "LoginMethodTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLoginMethods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OneTimeVpnKey",
                table: "Invitations",
                column: "OneTimeVpnKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RemoteServerId",
                table: "Invitations",
                column: "RemoteServerId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginMethodTypes_Name",
                table: "LoginMethodTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mods_CurseForgeModId",
                table: "Mods",
                column: "CurseForgeModId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NfsShareInvites_InviteKey",
                table: "NfsShareInvites",
                column: "InviteKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NfsShareInvites_RemoteServerId",
                table: "NfsShareInvites",
                column: "RemoteServerId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteServerMods_ModEntityId",
                table: "RemoteServerMods",
                column: "ModEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteServerMods_RemoteServerId_ModEntityId",
                table: "RemoteServerMods",
                columns: new[] { "RemoteServerId", "ModEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginMethods_LoginMethodTypeId",
                table: "UserLoginMethods",
                column: "LoginMethodTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginMethods_UserId_LoginMethodTypeId",
                table: "UserLoginMethods",
                columns: new[] { "UserId", "LoginMethodTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClusterSettings");

            migrationBuilder.DropTable(
                name: "CurseForgeSettings");

            migrationBuilder.DropTable(
                name: "EmailSettings");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "NfsShareInvites");

            migrationBuilder.DropTable(
                name: "RemoteServerMods");

            migrationBuilder.DropTable(
                name: "UserLoginMethods");

            migrationBuilder.DropTable(
                name: "VpnServerSettings");

            migrationBuilder.DropTable(
                name: "Mods");

            migrationBuilder.DropTable(
                name: "RemoteServers");

            migrationBuilder.DropTable(
                name: "LoginMethodTypes");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
