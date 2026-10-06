using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameNetManager.Server.Data.Migrations;

public partial class CustomerOpenAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsCustomerAccount",
            table: "Invoices",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "SessionId",
            table: "InvoiceItems",
            type: "TEXT",
            nullable: true);

        migrationBuilder.Sql(
            "CREATE TEMP TABLE TempCustomerOpenAccounts(CustomerId TEXT PRIMARY KEY, AccountId TEXT NOT NULL);");

        migrationBuilder.Sql(
            "INSERT INTO TempCustomerOpenAccounts(CustomerId, AccountId) " +
            "SELECT CustomerId, MIN(Id) FROM Invoices " +
            "WHERE Status = 'Draft' AND (SessionId IS NULL OR EXISTS (" +
            "SELECT 1 FROM Sessions s WHERE s.Id = Invoices.SessionId AND s.State = 'Completed')) " +
            "GROUP BY CustomerId;");

        migrationBuilder.Sql(
            "UPDATE InvoiceItems SET InvoiceId = (" +
            "SELECT t.AccountId FROM TempCustomerOpenAccounts t " +
            "JOIN Invoices old ON old.Id = InvoiceItems.InvoiceId " +
            "WHERE t.CustomerId = old.CustomerId AND old.Id <> t.AccountId) " +
            "WHERE InvoiceId IN (" +
            "SELECT old.Id FROM Invoices old JOIN TempCustomerOpenAccounts t ON t.CustomerId = old.CustomerId WHERE old.Id <> t.AccountId);");

        migrationBuilder.Sql(
            "UPDATE InvoicePayments SET InvoiceId = (" +
            "SELECT t.AccountId FROM TempCustomerOpenAccounts t " +
            "JOIN Invoices old ON old.Id = InvoicePayments.InvoiceId " +
            "WHERE t.CustomerId = old.CustomerId AND old.Id <> t.AccountId) " +
            "WHERE InvoiceId IN (" +
            "SELECT old.Id FROM Invoices old JOIN TempCustomerOpenAccounts t ON t.CustomerId = old.CustomerId WHERE old.Id <> t.AccountId);");

        migrationBuilder.Sql(
            "UPDATE SessionCharges SET InvoiceId = (" +
            "SELECT t.AccountId FROM TempCustomerOpenAccounts t " +
            "JOIN Invoices old ON old.Id = SessionCharges.InvoiceId " +
            "WHERE t.CustomerId = old.CustomerId AND old.Id <> t.AccountId) " +
            "WHERE InvoiceId IN (" +
            "SELECT old.Id FROM Invoices old JOIN TempCustomerOpenAccounts t ON t.CustomerId = old.CustomerId WHERE old.Id <> t.AccountId);");

        migrationBuilder.Sql(
            "UPDATE WalletTransactions SET ReferenceInvoiceId = (" +
            "SELECT t.AccountId FROM TempCustomerOpenAccounts t " +
            "JOIN Invoices old ON old.Id = WalletTransactions.ReferenceInvoiceId " +
            "WHERE t.CustomerId = old.CustomerId AND old.Id <> t.AccountId) " +
            "WHERE ReferenceInvoiceId IN (" +
            "SELECT old.Id FROM Invoices old JOIN TempCustomerOpenAccounts t ON t.CustomerId = old.CustomerId WHERE old.Id <> t.AccountId);");

        migrationBuilder.Sql(
            "UPDATE BenefitTransactions SET ReferenceInvoiceId = (" +
            "SELECT t.AccountId FROM TempCustomerOpenAccounts t " +
            "JOIN Invoices old ON old.Id = BenefitTransactions.ReferenceInvoiceId " +
            "WHERE t.CustomerId = old.CustomerId AND old.Id <> t.AccountId) " +
            "WHERE ReferenceInvoiceId IN (" +
            "SELECT old.Id FROM Invoices old JOIN TempCustomerOpenAccounts t ON t.CustomerId = old.CustomerId WHERE old.Id <> t.AccountId);");

        migrationBuilder.Sql(
            "UPDATE InventoryTransactions SET ReferenceInvoiceId = (" +
            "SELECT t.AccountId FROM TempCustomerOpenAccounts t " +
            "JOIN Invoices old ON old.Id = InventoryTransactions.ReferenceInvoiceId " +
            "WHERE t.CustomerId = old.CustomerId AND old.Id <> t.AccountId) " +
            "WHERE ReferenceInvoiceId IN (" +
            "SELECT old.Id FROM Invoices old JOIN TempCustomerOpenAccounts t ON t.CustomerId = old.CustomerId WHERE old.Id <> t.AccountId);");

        migrationBuilder.Sql(
            "UPDATE Invoices SET IsCustomerAccount = 1 " +
            "WHERE Id IN (SELECT AccountId FROM TempCustomerOpenAccounts);");

        migrationBuilder.Sql(
            "UPDATE Invoices SET TotalAmount = COALESCE((" +
            "SELECT SUM(ii.Amount) FROM InvoiceItems ii WHERE ii.InvoiceId = Invoices.Id), 0) " +
            "WHERE Id IN (SELECT AccountId FROM TempCustomerOpenAccounts);");

        migrationBuilder.Sql(
            "DELETE FROM Invoices WHERE Id IN (" +
            "SELECT old.Id FROM Invoices old " +
            "JOIN TempCustomerOpenAccounts t ON t.CustomerId = old.CustomerId " +
            "WHERE old.Id <> t.AccountId AND old.Status = 'Draft' AND " +
            "(old.SessionId IS NULL OR EXISTS (" +
            "SELECT 1 FROM Sessions s WHERE s.Id = old.SessionId AND s.State = 'Completed')));");

        migrationBuilder.Sql("DROP TABLE TempCustomerOpenAccounts;");

        migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Invoices_SessionId;");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_SessionId",
            table: "Invoices",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_CustomerId",
            table: "Invoices",
            column: "CustomerId",
            unique: true,
            filter: "Status = 'Draft' AND IsCustomerAccount = 1");

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceItems_SessionId",
            table: "InvoiceItems",
            column: "SessionId");

        migrationBuilder.AddForeignKey(
            name: "FK_InvoiceItems_Sessions_SessionId",
            table: "InvoiceItems",
            column: "SessionId",
            principalTable: "Sessions",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_InvoiceItems_Sessions_SessionId",
            table: "InvoiceItems");

        migrationBuilder.DropIndex(name: "IX_InvoiceItems_SessionId", table: "InvoiceItems");
        migrationBuilder.DropIndex(name: "IX_Invoices_CustomerId", table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_SessionId", table: "Invoices");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_SessionId",
            table: "Invoices",
            column: "SessionId",
            unique: true);

        migrationBuilder.DropColumn(name: "SessionId", table: "InvoiceItems");
        migrationBuilder.DropColumn(name: "IsCustomerAccount", table: "Invoices");
    }
}
