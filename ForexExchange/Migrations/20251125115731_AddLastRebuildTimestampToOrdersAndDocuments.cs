using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class AddLastRebuildTimestampToOrdersAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRebuildTimestamp",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRebuildTimestamp",
                table: "AccountingDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBalanceHistory_Type_Deleted",
                table: "CustomerBalanceHistory",
                columns: new[] { "TransactionType", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyPoolHistory_Type_Deleted",
                table: "CurrencyPoolHistory",
                columns: new[] { "TransactionType", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccountBalanceHistory_Type_Deleted",
                table: "BankAccountBalanceHistory",
                columns: new[] { "TransactionType", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerBalanceHistory_Type_Deleted",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropIndex(
                name: "IX_CurrencyPoolHistory_Type_Deleted",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropIndex(
                name: "IX_BankAccountBalanceHistory_Type_Deleted",
                table: "BankAccountBalanceHistory");

            migrationBuilder.DropColumn(
                name: "LastRebuildTimestamp",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "LastRebuildTimestamp",
                table: "AccountingDocuments");
        }
    }
}
