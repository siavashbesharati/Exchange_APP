using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForexExchange.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyIdNavigationProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add CurrencyId columns (nullable for gradual migration)
            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "AccountingDocuments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "CustomerBalances",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "CustomerBalanceHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "CurrencyPoolHistory",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "BankAccounts",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "BankAccountBalances",
                type: "INTEGER",
                nullable: true);

            // Create foreign keys
            migrationBuilder.CreateIndex(
                name: "IX_AccountingDocuments_CurrencyId",
                table: "AccountingDocuments",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingDocuments_Currencies_CurrencyId",
                table: "AccountingDocuments",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBalances_CurrencyId",
                table: "CustomerBalances",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBalances_Currencies_CurrencyId",
                table: "CustomerBalances",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBalanceHistory_CurrencyId",
                table: "CustomerBalanceHistory",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerBalanceHistory_Currencies_CurrencyId",
                table: "CustomerBalanceHistory",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyPoolHistory_CurrencyId",
                table: "CurrencyPoolHistory",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_CurrencyPoolHistory_Currencies_CurrencyId",
                table: "CurrencyPoolHistory",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CurrencyId",
                table: "BankAccounts",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Currencies_CurrencyId",
                table: "BankAccounts",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccountBalances_CurrencyId",
                table: "BankAccountBalances",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccountBalances_Currencies_CurrencyId",
                table: "BankAccountBalances",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Create conditional unique indexes for CurrencyId
            migrationBuilder.CreateIndex(
                name: "IX_CustomerBalances_CustomerId_CurrencyId",
                table: "CustomerBalances",
                columns: new[] { "CustomerId", "CurrencyId" },
                unique: true,
                filter: "[CurrencyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccountBalances_BankAccountId_CurrencyId",
                table: "BankAccountBalances",
                columns: new[] { "BankAccountId", "CurrencyId" },
                unique: true,
                filter: "[CurrencyId] IS NOT NULL");

            // Create performance indexes for CurrencyId
            migrationBuilder.CreateIndex(
                name: "IX_CustomerBalanceHistory_Customer_CurrencyId_Latest",
                table: "CustomerBalanceHistory",
                columns: new[] { "CustomerId", "CurrencyId", "TransactionDate", "Id" },
                filter: "[CurrencyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyPoolHistory_CurrencyId_Latest",
                table: "CurrencyPoolHistory",
                columns: new[] { "CurrencyId", "TransactionDate", "Id" },
                filter: "[CurrencyId] IS NOT NULL");

            // Data migration: Populate CurrencyId from CurrencyCode
            migrationBuilder.Sql(@"
                UPDATE AccountingDocuments
                SET CurrencyId = (
                    SELECT Id FROM Currencies 
                    WHERE UPPER(TRIM(Currencies.Code)) = UPPER(TRIM(AccountingDocuments.CurrencyCode))
                    LIMIT 1
                )
                WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
            ");

            migrationBuilder.Sql(@"
                UPDATE CustomerBalances
                SET CurrencyId = (
                    SELECT Id FROM Currencies 
                    WHERE UPPER(TRIM(Currencies.Code)) = UPPER(TRIM(CustomerBalances.CurrencyCode))
                    LIMIT 1
                )
                WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
            ");

            migrationBuilder.Sql(@"
                UPDATE CustomerBalanceHistory
                SET CurrencyId = (
                    SELECT Id FROM Currencies 
                    WHERE UPPER(TRIM(Currencies.Code)) = UPPER(TRIM(CustomerBalanceHistory.CurrencyCode))
                    LIMIT 1
                )
                WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
            ");

            migrationBuilder.Sql(@"
                UPDATE CurrencyPoolHistory
                SET CurrencyId = (
                    SELECT Id FROM Currencies 
                    WHERE UPPER(TRIM(Currencies.Code)) = UPPER(TRIM(CurrencyPoolHistory.CurrencyCode))
                    LIMIT 1
                )
                WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
            ");

            migrationBuilder.Sql(@"
                UPDATE BankAccounts
                SET CurrencyId = (
                    SELECT Id FROM Currencies 
                    WHERE UPPER(TRIM(Currencies.Code)) = UPPER(TRIM(BankAccounts.CurrencyCode))
                    LIMIT 1
                )
                WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
            ");

            migrationBuilder.Sql(@"
                UPDATE BankAccountBalances
                SET CurrencyId = (
                    SELECT Id FROM Currencies 
                    WHERE UPPER(TRIM(Currencies.Code)) = UPPER(TRIM(BankAccountBalances.CurrencyCode))
                    LIMIT 1
                )
                WHERE CurrencyId IS NULL AND CurrencyCode IS NOT NULL AND CurrencyCode != '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_CurrencyPoolHistory_CurrencyId_Latest",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropIndex(
                name: "IX_CustomerBalanceHistory_Customer_CurrencyId_Latest",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropIndex(
                name: "IX_BankAccountBalances_BankAccountId_CurrencyId",
                table: "BankAccountBalances");

            migrationBuilder.DropIndex(
                name: "IX_CustomerBalances_CustomerId_CurrencyId",
                table: "CustomerBalances");

            // Drop foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccountBalances_Currencies_CurrencyId",
                table: "BankAccountBalances");

            migrationBuilder.DropIndex(
                name: "IX_BankAccountBalances_CurrencyId",
                table: "BankAccountBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_Currencies_CurrencyId",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CurrencyId",
                table: "BankAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_CurrencyPoolHistory_Currencies_CurrencyId",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropIndex(
                name: "IX_CurrencyPoolHistory_CurrencyId",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBalanceHistory_Currencies_CurrencyId",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropIndex(
                name: "IX_CustomerBalanceHistory_CurrencyId",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerBalances_Currencies_CurrencyId",
                table: "CustomerBalances");

            migrationBuilder.DropIndex(
                name: "IX_CustomerBalances_CurrencyId",
                table: "CustomerBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingDocuments_Currencies_CurrencyId",
                table: "AccountingDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AccountingDocuments_CurrencyId",
                table: "AccountingDocuments");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "BankAccountBalances");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "CurrencyPoolHistory");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "CustomerBalanceHistory");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "CustomerBalances");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "AccountingDocuments");
        }
    }
}
