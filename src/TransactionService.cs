using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public class TransactionService
    {
        private readonly SupplyChainDbContext _context;
        private readonly AccountingService _accountingService;

        public TransactionService(SupplyChainDbContext context)
        {
            _context = context;
            _accountingService = new AccountingService(context);
        }

        public Transaction RecordTransaction(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            _context.SaveChanges();

            // Create accounting entries based on transaction type
            try
            {
                JournalEntry? journalEntry = null;

                switch (transaction.Type)
                {
                    case TransactionType.InvoiceFunding:
                        if (transaction.Invoice != null && transaction.InterestOrDiscountRate.HasValue)
                        {
                            decimal discountAmount = transaction.Amount * (transaction.InterestOrDiscountRate.Value / 100m);
                            decimal fundedAmount = transaction.Amount - discountAmount;
                            journalEntry = _accountingService.CreateInvoiceFundingEntry(transaction, fundedAmount, discountAmount);
                        }
                        break;

                    case TransactionType.Payment:
                        journalEntry = _accountingService.CreatePaymentEntry(transaction);
                        break;

                    case TransactionType.FeeCharge:
                        journalEntry = _accountingService.CreateFeeChargeEntry(transaction);
                        break;

                    case TransactionType.TreasuryFunding:
                        journalEntry = _accountingService.CreateTreasuryFundingEntry(transaction);
                        break;

                    case TransactionType.InvoiceUpload:
                    case TransactionType.LimitAdjustment:
                        // These transaction types don't require journal entries
                        break;
                }

                // Auto-post the journal entry for automated transactions
                if (journalEntry != null)
                {
                    _accountingService.PostJournalEntry(journalEntry.Id, 1); // Using system user ID 1
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the transaction
                Console.WriteLine($"Warning: Failed to create accounting entry for transaction {transaction.Id}: {ex.Message}");
            }

            return transaction;
        }

        public List<Transaction> GetTransactions(int organizationId)
        {
            return _context.Transactions
                .Include(t => t.Invoice)
                .Where(t => t.OrganizationId == organizationId)
                .OrderByDescending(t => t.TransactionDate)
                .ToList();
        }

        public AccountStatement GenerateAccountStatement(int organizationId, DateTime startDate, DateTime endDate)
        {
            var organization = _context.Organizations.Find(organizationId) 
                ?? throw new Exception($"Organization with ID {organizationId} not found");

            // Get transactions in the date range
            var transactions = _context.Transactions
                .Where(t => t.OrganizationId == organizationId &&
                            t.TransactionDate >= startDate &&
                            t.TransactionDate <= endDate)
                .OrderBy(t => t.TransactionDate)
                .ToList();

            // Calculate balances
            decimal openingBalance = 0;
            decimal closingBalance = 0;

            // Get previous transactions to calculate opening balance
            var previousTransactions = _context.Transactions
                .Where(t => t.OrganizationId == organizationId && t.TransactionDate < startDate)
                .ToList();

            foreach (var t in previousTransactions)
            {
                if (t.Type == TransactionType.InvoiceFunding || t.Type == TransactionType.FeeCharge)
                    openingBalance -= t.Amount;
                else if (t.Type == TransactionType.Payment)
                    openingBalance += t.Amount;
            }

            closingBalance = openingBalance;
            foreach (var t in transactions)
            {
                if (t.Type == TransactionType.InvoiceFunding || t.Type == TransactionType.FeeCharge)
                    closingBalance -= t.Amount;
                else if (t.Type == TransactionType.Payment)
                    closingBalance += t.Amount;
            }

            // Create statement
            var statement = new AccountStatement
            {
                OrganizationId = organizationId,
                StartDate = startDate,
                EndDate = endDate,
                GenerationDate = DateTime.Now,
                OpeningBalance = openingBalance,
                ClosingBalance = closingBalance,
                Transactions = transactions,
                StatementNumber = $"STMT-{DateTime.Now.Year}-{DateTime.Now.Month:D2}-{organizationId:D4}"
            };

            _context.AccountStatements.Add(statement);
            _context.SaveChanges();

            return statement;
        }

        public string GenerateStatementReport(AccountStatement statement)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"ACCOUNT STATEMENT");
            sb.AppendLine($"=====================================");
            sb.AppendLine($"Statement No: {statement.StatementNumber}");
            sb.AppendLine($"Organization: {statement.Organization?.Name}");
            sb.AppendLine($"Period: {statement.StartDate.ToShortDateString()} to {statement.EndDate.ToShortDateString()}");
            sb.AppendLine($"Generated: {statement.GenerationDate}\n");
            
            sb.AppendLine($"Opening Balance: ${statement.OpeningBalance:N2}");
            sb.AppendLine($"Closing Balance: ${statement.ClosingBalance:N2}\n");
            
            sb.AppendLine("TRANSACTIONS:");
            sb.AppendLine("-------------------------------------");
            
            foreach (var transaction in statement.Transactions)
            {
                string effect;
                string description;
                
                switch (transaction.Type)
                {
                    case TransactionType.InvoiceFunding:
                        effect = "+"; // Funding is a credit to the seller
                        description = "CREDIT: " + transaction.Description;
                        break;
                    case TransactionType.Payment:
                        effect = "+"; // Payment is a credit
                        description = "CREDIT: " + transaction.Description;
                        break;
                    default:
                        effect = "-"; // All other transactions are debits
                        description = transaction.Description;
                        break;
                }
                
                sb.AppendLine($"{transaction.TransactionDate.ToShortDateString()} | {description}");
                sb.AppendLine($"  {effect}${transaction.Amount:N2} | {transaction.Type} | {transaction.FacilityType}");
                
                if (transaction.InvoiceId.HasValue)
                {
                    sb.AppendLine($"  Invoice: {transaction.Invoice?.InvoiceNumber}");
                }
                
                if (transaction.InterestOrDiscountRate.HasValue)
                {
                    sb.AppendLine($"  Rate: {transaction.InterestOrDiscountRate:N2}%");
                }
                
                if (transaction.MaturityDate > DateTime.MinValue)
                {
                    sb.AppendLine($"  Maturity: {transaction.MaturityDate.ToShortDateString()}");
                }
                
                if (transaction.IsPaid)
                {
                    sb.AppendLine($"  Paid on: {transaction.PaymentDate?.ToShortDateString()}");
                }

                sb.AppendLine();
            }
            
            sb.AppendLine("-------------------------------------");
            sb.AppendLine($"Total Transactions: {statement.Transactions.Count}");
            
            return sb.ToString();
        }

        public Transaction RecordPaymentObligation(Invoice invoice, decimal amount, DateTime dueDate)
        {
            // Record the payment obligation for the buyer
            var transaction = new Transaction
            {
                Type = TransactionType.InvoiceUpload,  // Using InvoiceUpload type for payment obligation
                FacilityType = FacilityType.InvoiceFinancing,
                OrganizationId = invoice.BuyerId ?? 0,  // Default to 0 if null
                InvoiceId = invoice.Id,
                Description = $"Payment obligation for invoice {invoice.InvoiceNumber}",
                Amount = amount, // Full invoice amount
                TransactionDate = DateTime.Now,
                MaturityDate = dueDate,
                IsPaid = false
            };

            _context.Transactions.Add(transaction);
            _context.SaveChanges();
            return transaction;
        }
    }
}
