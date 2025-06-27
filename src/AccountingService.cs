using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public class AccountingService
    {
        private readonly SupplyChainDbContext _context;

        public AccountingService(SupplyChainDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Creates journal entries for invoice funding transaction
        /// </summary>
        public JournalEntry CreateInvoiceFundingEntry(Transaction transaction, decimal fundedAmount, decimal discountAmount)
        {
            if (transaction.Type != TransactionType.InvoiceFunding)
                throw new ArgumentException("Transaction must be of type InvoiceFunding");

            var journalEntry = new JournalEntry
            {
                TransactionReference = $"TXN-{transaction.Id}",
                TransactionDate = transaction.TransactionDate,
                Description = $"Invoice funding for {transaction.Invoice?.InvoiceNumber}",
                OrganizationId = transaction.OrganizationId,
                InvoiceId = transaction.InvoiceId,
                TransactionId = transaction.Id,
                Status = JournalEntryStatus.Pending
            };

            var journalLines = new List<JournalEntryLine>();

            // For Seller (who receives the funding)
            if (transaction.Invoice?.SellerId != null)
            {
                // Debit: Cash (Seller receives funded amount)
                journalLines.Add(new JournalEntryLine
                {
                    AccountId = GetAccountByCode("1100").Id, // Cash account
                    DebitAmount = fundedAmount,
                    CreditAmount = 0,
                    Description = $"Cash received from invoice factoring - {transaction.Invoice.InvoiceNumber}",
                    OrganizationId = transaction.Invoice.SellerId
                });

                // Debit: Factoring Fee Expense (Seller pays discount)
                journalLines.Add(new JournalEntryLine
                {
                    AccountId = GetAccountByCode("6100").Id, // Factoring Fee Expense
                    DebitAmount = discountAmount,
                    CreditAmount = 0,
                    Description = $"Factoring fee for invoice {transaction.Invoice.InvoiceNumber}",
                    OrganizationId = transaction.Invoice.SellerId
                });

                // Credit: Accounts Receivable (Seller transfers invoice to bank)
                journalLines.Add(new JournalEntryLine
                {
                    AccountId = GetAccountByCode("1200").Id, // Accounts Receivable
                    DebitAmount = 0,
                    CreditAmount = transaction.Amount,
                    Description = $"Transfer of invoice {transaction.Invoice.InvoiceNumber} to bank",
                    OrganizationId = transaction.Invoice.SellerId
                });
            }

            // For Bank
            // Debit: Loans to Customers (Bank's asset - amount due from buyer)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("1300").Id, // Loans to Customers
                DebitAmount = transaction.Amount,
                CreditAmount = 0,
                Description = $"Invoice financing loan - {transaction.Invoice?.InvoiceNumber}",
                OrganizationId = null // Bank entry
            });

            // Credit: Cash (Bank pays seller)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("1100").Id, // Cash
                DebitAmount = 0,
                CreditAmount = fundedAmount,
                Description = $"Payment to seller for invoice {transaction.Invoice?.InvoiceNumber}",
                OrganizationId = null // Bank entry
            });

            // Credit: Interest Income (Bank's revenue from discount)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("4100").Id, // Interest Income
                DebitAmount = 0,
                CreditAmount = discountAmount,
                Description = $"Discount income from invoice {transaction.Invoice?.InvoiceNumber}",
                OrganizationId = null // Bank entry
            });

            journalEntry.JournalEntryLines = journalLines;

            _context.JournalEntries.Add(journalEntry);
            _context.SaveChanges();

            return journalEntry;
        }

        /// <summary>
        /// Creates journal entries for payment transaction
        /// </summary>
        public JournalEntry CreatePaymentEntry(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Payment)
                throw new ArgumentException("Transaction must be of type Payment");

            var journalEntry = new JournalEntry
            {
                TransactionReference = $"TXN-{transaction.Id}",
                TransactionDate = transaction.TransactionDate,
                Description = $"Payment for invoice {transaction.Invoice?.InvoiceNumber}",
                OrganizationId = transaction.OrganizationId,
                InvoiceId = transaction.InvoiceId,
                TransactionId = transaction.Id,
                Status = JournalEntryStatus.Pending
            };

            var journalLines = new List<JournalEntryLine>();

            // For Buyer (who makes the payment)
            if (transaction.Invoice?.BuyerId != null)
            {
                // Credit: Cash (Buyer pays cash)
                journalLines.Add(new JournalEntryLine
                {
                    AccountId = GetAccountByCode("1100").Id, // Cash
                    DebitAmount = 0,
                    CreditAmount = transaction.Amount,
                    Description = $"Payment for invoice {transaction.Invoice.InvoiceNumber}",
                    OrganizationId = transaction.Invoice.BuyerId
                });

                // Debit: Accounts Payable (Buyer reduces payable)
                journalLines.Add(new JournalEntryLine
                {
                    AccountId = GetAccountByCode("2100").Id, // Accounts Payable
                    DebitAmount = transaction.Amount,
                    CreditAmount = 0,
                    Description = $"Payment of invoice {transaction.Invoice.InvoiceNumber}",
                    OrganizationId = transaction.Invoice.BuyerId
                });
            }

            // For Bank
            // Debit: Cash (Bank receives payment)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("1100").Id, // Cash
                DebitAmount = transaction.Amount,
                CreditAmount = 0,
                Description = $"Receipt of payment for invoice {transaction.Invoice?.InvoiceNumber}",
                OrganizationId = null // Bank entry
            });

            // Credit: Loans to Customers (Bank reduces loan balance)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("1300").Id, // Loans to Customers
                DebitAmount = 0,
                CreditAmount = transaction.Amount,
                Description = $"Payment received for invoice {transaction.Invoice?.InvoiceNumber}",
                OrganizationId = null // Bank entry
            });

            journalEntry.JournalEntryLines = journalLines;

            _context.JournalEntries.Add(journalEntry);
            _context.SaveChanges();

            return journalEntry;
        }

        /// <summary>
        /// Creates journal entries for fee charges
        /// </summary>
        public JournalEntry CreateFeeChargeEntry(Transaction transaction)
        {
            if (transaction.Type != TransactionType.FeeCharge)
                throw new ArgumentException("Transaction must be of type FeeCharge");

            var journalEntry = new JournalEntry
            {
                TransactionReference = $"TXN-{transaction.Id}",
                TransactionDate = transaction.TransactionDate,
                Description = transaction.Description,
                OrganizationId = transaction.OrganizationId,
                TransactionId = transaction.Id,
                Status = JournalEntryStatus.Pending
            };

            var journalLines = new List<JournalEntryLine>();

            // For Customer (who is charged the fee)
            // Debit: Bank Fee Expense
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("6200").Id, // Bank Fee Expense
                DebitAmount = transaction.Amount,
                CreditAmount = 0,
                Description = transaction.Description,
                OrganizationId = transaction.OrganizationId
            });

            // Credit: Accounts Payable (or Cash if paid immediately)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("2100").Id, // Accounts Payable
                DebitAmount = 0,
                CreditAmount = transaction.Amount,
                Description = transaction.Description,
                OrganizationId = transaction.OrganizationId
            });

            // For Bank
            // Debit: Accounts Receivable (or Cash if received immediately)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("1200").Id, // Accounts Receivable
                DebitAmount = transaction.Amount,
                CreditAmount = 0,
                Description = transaction.Description,
                OrganizationId = null // Bank entry
            });

            // Credit: Fee Income
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("4200").Id, // Fee Income
                DebitAmount = 0,
                CreditAmount = transaction.Amount,
                Description = transaction.Description,
                OrganizationId = null // Bank entry
            });

            journalEntry.JournalEntryLines = journalLines;

            _context.JournalEntries.Add(journalEntry);
            _context.SaveChanges();

            return journalEntry;
        }

        /// <summary>
        /// Creates journal entries for treasury funding
        /// </summary>
        public JournalEntry CreateTreasuryFundingEntry(Transaction transaction)
        {
            if (transaction.Type != TransactionType.TreasuryFunding)
                throw new ArgumentException("Transaction must be of type TreasuryFunding");

            var journalEntry = new JournalEntry
            {
                TransactionReference = $"TXN-{transaction.Id}",
                TransactionDate = transaction.TransactionDate,
                Description = transaction.Description,
                OrganizationId = transaction.OrganizationId,
                TransactionId = transaction.Id,
                Status = JournalEntryStatus.Pending
            };

            var journalLines = new List<JournalEntryLine>();

            // Debit: Cash (Loans department receives funding)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("1100").Id, // Cash
                DebitAmount = transaction.Amount,
                CreditAmount = 0,
                Description = transaction.Description,
                OrganizationId = null // Bank entry
            });

            // Credit: Due to Treasury (Internal liability)
            journalLines.Add(new JournalEntryLine
            {
                AccountId = GetAccountByCode("2300").Id, // Due to Treasury
                DebitAmount = 0,
                CreditAmount = transaction.Amount,
                Description = transaction.Description,
                OrganizationId = null // Bank entry
            });

            journalEntry.JournalEntryLines = journalLines;

            _context.JournalEntries.Add(journalEntry);
            _context.SaveChanges();

            return journalEntry;
        }

        /// <summary>
        /// Posts a journal entry (marks it as final)
        /// </summary>
        public void PostJournalEntry(int journalEntryId, int userId)
        {
            var journalEntry = _context.JournalEntries
                .Include(j => j.JournalEntryLines)
                .ThenInclude(l => l.Account)
                .FirstOrDefault(j => j.Id == journalEntryId);

            if (journalEntry == null)
                throw new ArgumentException("Journal entry not found");

            if (journalEntry.Status == JournalEntryStatus.Posted)
                throw new InvalidOperationException("Journal entry is already posted");

            if (!journalEntry.IsBalanced)
                throw new InvalidOperationException("Journal entry is not balanced and cannot be posted");

            // Update account balances
            foreach (var line in journalEntry.JournalEntryLines)
            {
                if (line.Account != null)
                {
                    // Update balance based on account type
                    switch (line.Account.Type)
                    {
                        case AccountType.Asset:
                        case AccountType.Expense:
                            line.Account.Balance += line.DebitAmount - line.CreditAmount;
                            break;
                        case AccountType.Liability:
                        case AccountType.Equity:
                        case AccountType.Revenue:
                            line.Account.Balance += line.CreditAmount - line.DebitAmount;
                            break;
                    }
                }
            }

            journalEntry.Status = JournalEntryStatus.Posted;
            journalEntry.PostedDate = DateTime.Now;
            journalEntry.PostedByUserId = userId;

            _context.SaveChanges();
        }

        /// <summary>
        /// Gets account by account code, creates if doesn't exist
        /// </summary>
        public Account GetAccountByCode(string accountCode)
        {
            var account = _context.Accounts.FirstOrDefault(a => a.AccountCode == accountCode);
            
            if (account == null)
            {
                account = CreateDefaultAccount(accountCode);
                _context.Accounts.Add(account);
                _context.SaveChanges();
            }

            return account;
        }

        /// <summary>
        /// Creates default chart of accounts if they don't exist
        /// </summary>
        public void InitializeChartOfAccounts()
        {
            var defaultAccounts = GetDefaultChartOfAccounts();
            
            foreach (var defaultAccount in defaultAccounts)
            {
                var existingAccount = _context.Accounts.FirstOrDefault(a => a.AccountCode == defaultAccount.AccountCode);
                if (existingAccount == null)
                {
                    _context.Accounts.Add(defaultAccount);
                }
            }
            
            _context.SaveChanges();
        }

        /// <summary>
        /// Creates a default account based on account code
        /// </summary>
        private Account CreateDefaultAccount(string accountCode)
        {
            return accountCode switch
            {
                "1100" => new Account { AccountCode = "1100", AccountName = "Cash", Type = AccountType.Asset, Category = AccountCategory.CurrentAssets },
                "1200" => new Account { AccountCode = "1200", AccountName = "Accounts Receivable", Type = AccountType.Asset, Category = AccountCategory.CurrentAssets },
                "1300" => new Account { AccountCode = "1300", AccountName = "Loans to Customers", Type = AccountType.Asset, Category = AccountCategory.CurrentAssets },
                "2100" => new Account { AccountCode = "2100", AccountName = "Accounts Payable", Type = AccountType.Liability, Category = AccountCategory.CurrentLiabilities },
                "2300" => new Account { AccountCode = "2300", AccountName = "Due to Treasury", Type = AccountType.Liability, Category = AccountCategory.CurrentLiabilities },
                "4100" => new Account { AccountCode = "4100", AccountName = "Interest Income", Type = AccountType.Revenue, Category = AccountCategory.OperatingRevenue },
                "4200" => new Account { AccountCode = "4200", AccountName = "Fee Income", Type = AccountType.Revenue, Category = AccountCategory.OperatingRevenue },
                "6100" => new Account { AccountCode = "6100", AccountName = "Factoring Fee Expense", Type = AccountType.Expense, Category = AccountCategory.OperatingExpenses },
                "6200" => new Account { AccountCode = "6200", AccountName = "Bank Fee Expense", Type = AccountType.Expense, Category = AccountCategory.OperatingExpenses },
                _ => throw new ArgumentException($"Unknown account code: {accountCode}")
            };
        }

        /// <summary>
        /// Gets the default chart of accounts
        /// </summary>
        public List<Account> GetDefaultChartOfAccounts()
        {
            return new List<Account>
            {
                // Assets
                new Account { AccountCode = "1100", AccountName = "Cash", Type = AccountType.Asset, Category = AccountCategory.CurrentAssets },
                new Account { AccountCode = "1200", AccountName = "Accounts Receivable", Type = AccountType.Asset, Category = AccountCategory.CurrentAssets },
                new Account { AccountCode = "1300", AccountName = "Loans to Customers", Type = AccountType.Asset, Category = AccountCategory.CurrentAssets },
                new Account { AccountCode = "1400", AccountName = "Allowance for Doubtful Accounts", Type = AccountType.Asset, Category = AccountCategory.CurrentAssets },
                new Account { AccountCode = "1500", AccountName = "Fixed Assets", Type = AccountType.Asset, Category = AccountCategory.FixedAssets },
                
                // Liabilities
                new Account { AccountCode = "2100", AccountName = "Accounts Payable", Type = AccountType.Liability, Category = AccountCategory.CurrentLiabilities },
                new Account { AccountCode = "2200", AccountName = "Accrued Expenses", Type = AccountType.Liability, Category = AccountCategory.CurrentLiabilities },
                new Account { AccountCode = "2300", AccountName = "Due to Treasury", Type = AccountType.Liability, Category = AccountCategory.CurrentLiabilities },
                new Account { AccountCode = "2400", AccountName = "Customer Deposits", Type = AccountType.Liability, Category = AccountCategory.CurrentLiabilities },
                new Account { AccountCode = "2500", AccountName = "Long-term Debt", Type = AccountType.Liability, Category = AccountCategory.LongTermLiabilities },
                
                // Equity
                new Account { AccountCode = "3100", AccountName = "Share Capital", Type = AccountType.Equity, Category = AccountCategory.ShareCapital },
                new Account { AccountCode = "3200", AccountName = "Retained Earnings", Type = AccountType.Equity, Category = AccountCategory.RetainedEarnings },
                
                // Revenue
                new Account { AccountCode = "4100", AccountName = "Interest Income", Type = AccountType.Revenue, Category = AccountCategory.OperatingRevenue },
                new Account { AccountCode = "4200", AccountName = "Fee Income", Type = AccountType.Revenue, Category = AccountCategory.OperatingRevenue },
                new Account { AccountCode = "4300", AccountName = "Other Income", Type = AccountType.Revenue, Category = AccountCategory.NonOperatingRevenue },
                
                // Expenses
                new Account { AccountCode = "6100", AccountName = "Factoring Fee Expense", Type = AccountType.Expense, Category = AccountCategory.OperatingExpenses },
                new Account { AccountCode = "6200", AccountName = "Bank Fee Expense", Type = AccountType.Expense, Category = AccountCategory.OperatingExpenses },
                new Account { AccountCode = "6300", AccountName = "Interest Expense", Type = AccountType.Expense, Category = AccountCategory.FinancingExpenses },
                new Account { AccountCode = "6400", AccountName = "Operating Expenses", Type = AccountType.Expense, Category = AccountCategory.OperatingExpenses },
                new Account { AccountCode = "6500", AccountName = "Bad Debt Expense", Type = AccountType.Expense, Category = AccountCategory.OperatingExpenses }
            };
        }

        /// <summary>
        /// Generates a trial balance as of a specific date
        /// </summary>
        public TrialBalance GenerateTrialBalance(DateTime asOfDate, int userId)
        {
            var accounts = _context.Accounts
                .Where(a => a.IsActive)
                .OrderBy(a => a.AccountCode)
                .ToList();

            var trialBalance = new TrialBalance
            {
                AsOfDate = asOfDate,
                GeneratedByUserId = userId,
                GeneratedDate = DateTime.Now
            };

            var lines = new List<TrialBalanceLine>();

            foreach (var account in accounts)
            {
                // Calculate balance based on posted journal entries up to the date
                var journalEntryLines = _context.JournalEntryLines
                    .Include(l => l.JournalEntry)
                    .Where(l => l.AccountId == account.Id && 
                               l.JournalEntry.Status == JournalEntryStatus.Posted &&
                               l.JournalEntry.PostedDate <= asOfDate)
                    .ToList();

                decimal balance = 0;
                decimal totalDebits = journalEntryLines.Sum(l => l.DebitAmount);
                decimal totalCredits = journalEntryLines.Sum(l => l.CreditAmount);

                // Calculate balance based on account type
                switch (account.Type)
                {
                    case AccountType.Asset:
                    case AccountType.Expense:
                        balance = totalDebits - totalCredits;
                        break;
                    case AccountType.Liability:
                    case AccountType.Equity:
                    case AccountType.Revenue:
                        balance = totalCredits - totalDebits;
                        break;
                }

                if (balance != 0) // Only include accounts with balances
                {
                    lines.Add(new TrialBalanceLine
                    {
                        AccountId = account.Id,
                        Account = account,
                        DebitBalance = balance > 0 ? balance : 0,
                        CreditBalance = balance < 0 ? Math.Abs(balance) : 0
                    });
                }
            }

            trialBalance.Lines = lines;
            
            _context.TrialBalances.Add(trialBalance);
            _context.SaveChanges();

            return trialBalance;
        }

        /// <summary>
        /// Gets journal entries for an organization
        /// </summary>
        public List<JournalEntry> GetJournalEntriesForOrganization(int organizationId)
        {
            return _context.JournalEntries
                .Include(j => j.JournalEntryLines)
                .ThenInclude(l => l.Account)
                .Where(j => j.OrganizationId == organizationId)
                .OrderByDescending(j => j.TransactionDate)
                .ToList();
        }

        /// <summary>
        /// Gets all journal entries (for bank view)
        /// </summary>
        public List<JournalEntry> GetAllJournalEntries()
        {
            return _context.JournalEntries
                .Include(j => j.JournalEntryLines)
                .ThenInclude(l => l.Account)
                .Include(j => j.Organization)
                .Include(j => j.Invoice)
                .OrderByDescending(j => j.TransactionDate)
                .ToList();
        }

        /// <summary>
        /// Gets all accounts in the chart of accounts
        /// </summary>
        public List<Account> GetAllAccounts()
        {
            return _context.Accounts
                .OrderBy(a => a.AccountCode)
                .ToList();
        }

        /// <summary>
        /// Gets unposted journal entries
        /// </summary>
        public List<JournalEntry> GetUnpostedJournalEntries()
        {
            return _context.JournalEntries
                .Include(j => j.JournalEntryLines)
                .ThenInclude(l => l.Account)
                .Where(j => j.Status == JournalEntryStatus.Pending)
                .OrderByDescending(j => j.TransactionDate)
                .ToList();
        }

        /// <summary>
        /// Gets account balances as a dictionary
        /// </summary>
        public Dictionary<Account, decimal> GetAccountBalances()
        {
            var accounts = _context.Accounts.ToList();
            var result = new Dictionary<Account, decimal>();

            foreach (var account in accounts)
            {
                result[account] = account.Balance;
            }

            return result;
        }

        /// <summary>
        /// Creates a manual journal entry
        /// </summary>
        public JournalEntry CreateJournalEntry(string transactionId, string description, 
            List<(int AccountId, decimal DebitAmount, decimal CreditAmount, string? EntityName)> lines)
        {
            var journalEntry = new JournalEntry
            {
                TransactionReference = transactionId,
                TransactionDate = DateTime.Now,
                Description = description,
                Status = JournalEntryStatus.Pending
            };

            var journalLines = new List<JournalEntryLine>();

            foreach (var line in lines)
            {
                journalLines.Add(new JournalEntryLine
                {
                    AccountId = line.AccountId,
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount,
                    Description = line.EntityName ?? description,
                    OrganizationId = null // Manual entries don't have specific organization
                });
            }

            journalEntry.JournalEntryLines = journalLines;

            _context.JournalEntries.Add(journalEntry);
            _context.SaveChanges();

            return journalEntry;
        }

        /// <summary>
        /// Creates invoice financing entry for manual creation
        /// </summary>
        public JournalEntry CreateInvoiceFinancingEntry(int invoiceId, decimal invoiceAmount, 
            decimal discountRate, string sellerName, string bankName)
        {
            decimal discountAmount = invoiceAmount * (discountRate / 100m);
            decimal fundedAmount = invoiceAmount - discountAmount;

            var journalEntry = new JournalEntry
            {
                TransactionReference = $"MANUAL-FUND-{invoiceId}",
                TransactionDate = DateTime.Now,
                Description = $"Manual invoice financing entry for invoice ID {invoiceId}",
                InvoiceId = invoiceId,
                Status = JournalEntryStatus.Pending
            };

            var journalLines = new List<JournalEntryLine>
            {
                // Seller receives cash (less discount)
                new JournalEntryLine
                {
                    AccountId = GetAccountByCode("1100").Id, // Cash
                    DebitAmount = fundedAmount,
                    CreditAmount = 0,
                    Description = $"Cash to seller {sellerName}",
                    OrganizationId = null
                },
                // Seller pays factoring fee
                new JournalEntryLine
                {
                    AccountId = GetAccountByCode("6100").Id, // Factoring Fee Expense
                    DebitAmount = discountAmount,
                    CreditAmount = 0,
                    Description = $"Factoring fee from seller {sellerName}",
                    OrganizationId = null
                },
                // Seller's accounts receivable is factored
                new JournalEntryLine
                {
                    AccountId = GetAccountByCode("1200").Id, // Accounts Receivable
                    DebitAmount = 0,
                    CreditAmount = invoiceAmount,
                    Description = $"Accounts receivable factored by {sellerName}",
                    OrganizationId = null
                },
                // Bank's loan to customer asset
                new JournalEntryLine
                {
                    AccountId = GetAccountByCode("1300").Id, // Loans to Customers
                    DebitAmount = invoiceAmount,
                    CreditAmount = 0,
                    Description = $"Loan to customer via factoring",
                    OrganizationId = null
                },
                // Bank pays out cash
                new JournalEntryLine
                {
                    AccountId = GetAccountByCode("1100").Id, // Cash
                    DebitAmount = 0,
                    CreditAmount = fundedAmount,
                    Description = $"Cash paid by {bankName}",
                    OrganizationId = null
                },
                // Bank earns interest income
                new JournalEntryLine
                {
                    AccountId = GetAccountByCode("4100").Id, // Interest Income
                    DebitAmount = 0,
                    CreditAmount = discountAmount,
                    Description = $"Interest income earned by {bankName}",
                    OrganizationId = null
                }
            };

            journalEntry.JournalEntryLines = journalLines;

            _context.JournalEntries.Add(journalEntry);
            _context.SaveChanges();

            return journalEntry;
        }

        /// <summary>
        /// Posts a journal entry by ID
        /// </summary>
        public void PostJournalEntry(int journalEntryId)
        {
            PostJournalEntry(journalEntryId, 1); // Default to user ID 1 (bank admin)
        }
    }
}
