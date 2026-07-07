using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class Role : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_UserRole_PermissionName",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "UserRole",
                table: "RolePermissions");

            migrationBuilder.AddColumn<string>(
                name: "RoleName",
                table: "RolePermissions",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName_PermissionName",
                table: "RolePermissions",
                columns: new[] { "RoleName", "PermissionName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleName_PermissionName",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "RoleName",
                table: "RolePermissions");

            migrationBuilder.AddColumn<int>(
                name: "UserRole",
                table: "RolePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_UserRole_PermissionName",
                table: "RolePermissions",
                columns: new[] { "UserRole", "PermissionName" },
                unique: true);
        }
    }
}
