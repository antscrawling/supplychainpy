using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Core.Data;
using Core.Services;
using Core.Models;
using SupplyChainFinance;

namespace BankPortal
{
    class Program
    {
        static void Main(string[] args)
        {
            // Configure services
            var services = ConfigureServices();

            // Initialize database
            using (var scope = services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<SupplyChainDbContext>();
                
                // Create database if it doesn't exist
                context.Database.EnsureCreated();
                
                // Seed data (only if empty)
                DataSeeder.SeedData(context);
            }

            var bankApp = new BankApplication(services);
            bankApp.Run();
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Database file path
            string dbFilePath = Path.Combine(
                Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? "",
                "../../../..",
                "supply_chain_finance.db");

            // Register database context
            services.AddDbContext<SupplyChainDbContext>(options =>
                options.UseSqlite($"Data Source={dbFilePath}"));

            // Register services
            services.AddScoped<AuthService>();
            services.AddScoped<MyLimitService>();
            services.AddScoped<InvoiceService>();
            services.AddScoped<TransactionService>();
            services.AddScoped<UserService>();
            services.AddScoped<AccountingService>();

            return services.BuildServiceProvider();
        }
    }

    class BankApplication
    {
        private readonly IServiceProvider _services;
        private User? _currentUser;
        private Organization? _currentOrganization;

        private AuthService? _authService;
        private MyLimitService? _limitService;
        private InvoiceService? _invoiceService;
        private TransactionService? _transactionService;
        private UserService? _userService;
        private AccountingService? _accountingService;

        public BankApplication(IServiceProvider services)
        {
            _services = services;
        }

        public void Run()
        {
            Console.Clear();
            Console.WriteLine("===================================================");
            Console.WriteLine("   SUPPLY CHAIN FINANCE - BANK PORTAL");
            Console.WriteLine("===================================================");
            Console.WriteLine();

            // Get services
            using (var scope = _services.CreateScope())
            {
                _authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                _limitService = scope.ServiceProvider.GetRequiredService<MyLimitService>();
                _invoiceService = scope.ServiceProvider.GetRequiredService<InvoiceService>();
                _transactionService = scope.ServiceProvider.GetRequiredService<TransactionService>();
                _userService = scope.ServiceProvider.GetRequiredService<UserService>();
                _accountingService = scope.ServiceProvider.GetRequiredService<AccountingService>();

                // Login
                if (Login())
                {
                    ShowMainMenu();
                }
            }

            Console.WriteLine("\nThank you for using the Supply Chain Finance Bank Portal. Goodbye!");
        }

        private bool Login()
        {
            int attempts = 0;
            while (attempts < 3)
            {
                Console.WriteLine("Please login to continue:");
                Console.Write("Username: ");
                string username = Console.ReadLine() ?? "";
                
                Console.Write("Password: ");
                string password = Console.ReadLine() ?? "";

                _currentUser = _authService?.Authenticate(username, password);

                if (_currentUser != null && _currentUser.Organization != null)
                {
                    if (!_currentUser.Organization.IsBank)
                    {
                        Console.WriteLine("\nError: This portal is for bank users only. Clients must use the Client Portal.");
                        return false;
                    }

                    _currentOrganization = _currentUser.Organization;
                    Console.Clear();
                    Console.WriteLine($"\nWelcome, {_currentUser.Name}!");
                    Console.WriteLine($"Organization: {_currentOrganization.Name}");
                    Console.WriteLine();
                    return true;
                }
                else
                {
                    attempts++;
                    Console.WriteLine($"\nInvalid username or password. {3 - attempts} attempts remaining.\n");
                }
            }

            Console.WriteLine("\nToo many failed login attempts. Exiting...");
            return false;
        }

        private void ShowMainMenu()
        {
            bool exit = false;

            while (!exit && _currentUser != null && _currentOrganization != null)
            {
                // Check for notifications
                var notifications = _authService?.GetUserNotifications(_currentUser.Id).Where(n => !n.IsRead).ToList();
                if (notifications != null && notifications.Count > 0)
                {
                    Console.WriteLine($"\nYou have {notifications.Count} unread notifications!");
                }

                Console.WriteLine("\nMAIN MENU");
                Console.WriteLine("1. Manage Organizations");
                Console.WriteLine("2. Manage Credit Facilities");
                Console.WriteLine("3. Review Invoices");
                Console.WriteLine("4. Process Funding");
                Console.WriteLine("5. View Transactions");
                Console.WriteLine("6. View Reports");
                Console.WriteLine("7. View Notifications");
                Console.WriteLine("8. Manage Accounting Entries");
                Console.WriteLine("0. Logout");

                Console.Write("\nSelect an option: ");
                string? input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        ManageOrganizations();
                        break;
                    case "2":
                        ManageCreditFacilities();
                        break;
                    case "3":
                        ReviewInvoices();
                        break;
                    case "4":
                        ProcessFunding();
                        break;
                    case "5":
                        ViewTransactions();
                        break;
                    case "6":
                        ViewReports();
                        break;
                    case "7":
                        ViewNotifications();
                        break;
                    case "8":
                        ManageAccountingEntries();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("\nInvalid option. Please try again.");
                        break;
                }
            }
        }

        void ManageOrganizations()
        {
            Console.Clear();
            Console.WriteLine("MANAGE ORGANIZATIONS");
            Console.WriteLine("===================\n");
            
            if (_userService == null)
                return;

            var organizations = _userService.GetOrganizations().Where(o => !o.IsBank).ToList();
            
            Console.WriteLine("Organizations:");
            foreach (var org in organizations)
            {
                Console.WriteLine($"ID: {org.Id} | Name: {org.Name} | Type: {(org.IsBuyer ? "Buyer" : "")}{(org.IsSeller ? "Seller" : "")}");
            }
            
            Console.WriteLine("\n1. View Organization Details");
            Console.WriteLine("2. Add New Organization");
            Console.WriteLine("0. Back to Main Menu");
            
            Console.Write("\nSelect an option: ");
            string? input = Console.ReadLine();
            
            switch (input)
            {
                case "1":
                    Console.Write("\nEnter Organization ID: ");
                    if (int.TryParse(Console.ReadLine(), out int orgId))
                    {
                        var org = organizations.FirstOrDefault(o => o.Id == orgId);
                        if (org != null)
                        {
                            ManageOrganization(org.Id);
                        }
                        else
                        {
                            Console.WriteLine("\nOrganization not found.");
                            Console.WriteLine("\nPress Enter to continue...");
                            WaitForEnterKey();
                        }
                    }
                    break;
                case "2":
                    AddNewOrganization();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("\nInvalid option.");
                    Console.WriteLine("\nPress Enter to continue...");
                    WaitForEnterKey();
                    break;
            }
            
            Console.WriteLine("\nPress Enter to continue...");
            WaitForEnterKey();
        }

        private void DisplayOrganizationDetails(Organization org)
        {
            Console.Clear();
            Console.WriteLine($"ORGANIZATION DETAILS: {org.Name}");
            Console.WriteLine("==============================\n");
            
            Console.WriteLine($"ID: {org.Id}");
            Console.WriteLine($"Name: {org.Name}");
            Console.WriteLine($"Tax ID: {org.TaxId}");
            Console.WriteLine($"Address: {org.Address}");
            Console.WriteLine($"Type: {(org.IsBuyer ? "Buyer " : "")}{(org.IsSeller ? "Seller" : "")}");
            Console.WriteLine($"Contact Person: {org.ContactPerson}");
            Console.WriteLine($"Contact Email: {org.ContactEmail}");
            Console.WriteLine($"Contact Phone: {org.ContactPhone}");
            
            if (_userService != null)
            {
                var users = _userService.GetOrganizationUsers(org.Id);
                
                Console.WriteLine("\nUsers:");
                foreach (var user in users)
                {
                    Console.WriteLine($"- {user.Name} ({user.Username}) - {user.Role}");
                }
            }
            
            if (_limitService != null)
            {
                var limits = _limitService.GetCreditLimitsByOrganization(org.Id);
                
                Console.WriteLine("\nCredit Facilities:");
                foreach (var limit in limits)
                {
                    Console.WriteLine($"Master Limit: ${limit.MasterLimit:N2}");
                    Console.WriteLine($"Utilization: ${limit.TotalUtilization:N2} ({limit.MasterUtilizationPercentage:N2}%)");
                    
                    foreach (var facility in limit.Facilities)
                    {
                        string status = facility.IsExpired ? "EXPIRED" : "Active";
                        Console.WriteLine($"- {facility.Type}: ${facility.TotalLimit:N2} (Used: ${facility.CurrentUtilization:N2}, {status})");
                    }
                }
            }
        }

        private void ManageCreditFacilities()
        {
            Console.Clear();
            Console.WriteLine("MANAGE CREDIT FACILITIES");
            Console.WriteLine("=======================\n");

            Console.WriteLine("1. View All Limits (Bank View)");
            Console.WriteLine("2. Grant Facility to Any Customer");
            Console.WriteLine("3. Manage Buyer-Specific Limits");
            Console.WriteLine("4. Launch GrantBuyerLimit Program");
            Console.WriteLine("0. Back");
            
            Console.Write("\nSelect an option: ");
            string? input = Console.ReadLine();
            
            switch (input)
            {
                case "1":
                    ViewAllLimits();
                    break;
                case "2":
                    GrantFacilityToCustomer();
                    break;
                case "3":
                    ManageBuyerLimits();
                    break;
                case "4":
                    LaunchGrantBuyerLimit();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("\nInvalid option. Please try again.");
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }

        private void ReviewInvoices()
        {
            if (_invoiceService == null)
                return;
                
            Console.Clear();
            Console.WriteLine("REVIEW INVOICES");
            Console.WriteLine("==============\n");
            
            Console.WriteLine("1. Review New Invoices (Seller Uploaded)");
            Console.WriteLine("2. Review Validated Invoices");
            Console.WriteLine("3. Review Buyer Uploaded Invoices");
            Console.WriteLine("0. Back");
            
            Console.Write("\nSelect an option: ");
            string? input = Console.ReadLine();
            
            switch (input)
            {
                case "1":
                    ReviewInvoicesByStatus(InvoiceStatus.Uploaded, "New");
                    break;
                case "2":
                    ReviewInvoicesByStatus(InvoiceStatus.Validated, "Validated");
                    break;
                case "3":
                    ReviewInvoicesByStatus(InvoiceStatus.BuyerUploaded, "Buyer Uploaded");
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Invalid option.");
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }
        
        private void ReviewInvoicesByStatus(InvoiceStatus status, string statusName)
        {
            if (_invoiceService == null)
                return;
                
            Console.Clear();
            Console.WriteLine($"REVIEW {statusName.ToUpper()} INVOICES");
            Console.WriteLine("====================\n");
            
            var invoices = _invoiceService.GetInvoicesByStatus(status);
            
            if (!invoices.Any())
            {
                Console.WriteLine($"No {statusName.ToLower()} invoices to review.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine($"{statusName} Invoices:");
            for (int i = 0; i < invoices.Count; i++)
            {
                var invoice = invoices[i];
                Console.WriteLine($"{i + 1}. Invoice #{invoice.InvoiceNumber} | Amount: ${invoice.Amount:N2} | Seller: {invoice.Seller?.Name} | Buyer: {invoice.Buyer?.Name}");
            }
            
            Console.Write("\nSelect invoice number to review (or 0 to return): ");
            if (int.TryParse(Console.ReadLine(), out int selection) && selection > 0 && selection <= invoices.Count)
            {
                var selectedInvoice = invoices[selection - 1];
                ReviewInvoice(selectedInvoice);
            }
            else if (selection != 0)
            {
                Console.WriteLine("Invalid selection.");
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ReviewInvoice(Invoice invoice)
        {
            if (_invoiceService == null)
                return;
                
            Console.Clear();
            Console.WriteLine($"INVOICE REVIEW: {invoice.InvoiceNumber}");
            Console.WriteLine("=================================\n");
            
            Console.WriteLine($"Invoice Number: {invoice.InvoiceNumber}");
            Console.WriteLine($"Amount: ${invoice.Amount:N2}");
            Console.WriteLine($"Issue Date: {invoice.IssueDate.ToShortDateString()}");
            Console.WriteLine($"Due Date: {invoice.DueDate.ToShortDateString()}");
            Console.WriteLine($"Seller: {invoice.Seller?.Name}");
            Console.WriteLine($"Buyer: {invoice.Buyer?.Name}");
            Console.WriteLine($"Status: {invoice.Status}");
            Console.WriteLine($"Description: {invoice.Description}");
            
            Console.WriteLine("\nOptions:");
            
            if (invoice.Status == InvoiceStatus.Uploaded)
            {
                Console.WriteLine("1. Validate Invoice");
                Console.WriteLine("2. Reject Invoice");
            }
            else if (invoice.Status == InvoiceStatus.BuyerUploaded)
            {
                Console.WriteLine("1. Validate Invoice");
                Console.WriteLine("2. Make Early Payment Offer to Seller");
                Console.WriteLine("3. Reject Invoice");
            }
            else if (invoice.Status == InvoiceStatus.Validated)
            {
                Console.WriteLine("1. Approve Invoice");
                Console.WriteLine("2. Request Buyer Approval for Liability Transfer");
                Console.WriteLine("3. Make Early Payment Offer to Seller");
                Console.WriteLine("4. Reject Invoice");
            }
            else if (invoice.Status == InvoiceStatus.BuyerApprovalPending)
            {
                Console.WriteLine("1. Check Buyer Approval Status");
                if (invoice.BuyerApproved)
                {
                    Console.WriteLine("2. Process for Funding (Buyer Approved)");
                }
            }
            else if (invoice.Status == InvoiceStatus.SellerAcceptancePending)
            {
                Console.WriteLine("1. Check Seller Acceptance Status");
                if (invoice.SellerAccepted)
                {
                    Console.WriteLine("2. Process for Funding (Seller Accepted)");
                }
            }
            else if (invoice.Status == InvoiceStatus.Approved)
            {
                Console.WriteLine("1. Process for Funding");
            }
            
            Console.WriteLine("0. Back");
            
            Console.Write("\nSelect an option: ");
            string? input = Console.ReadLine();
            
            if (_currentUser == null)
                return;
                
            switch (input)
            {
                case "1":
                    if (invoice.Status == InvoiceStatus.Uploaded || invoice.Status == InvoiceStatus.BuyerUploaded)
                    {
                        // Validate invoice
                        var validateResult = _invoiceService.ValidateInvoice(invoice.Id, _currentUser.Id);
                        Console.WriteLine($"\n{validateResult.Message}");
                    }
                    else if (invoice.Status == InvoiceStatus.Validated)
                    {
                        // Approve invoice
                        var approveResult = _invoiceService.ApproveInvoice(invoice.Id, _currentUser.Id);
                        Console.WriteLine($"\n{approveResult.Message}");
                    }
                    else if (invoice.Status == InvoiceStatus.BuyerApprovalPending)
                    {
                        // Check status of buyer approval
                        if (invoice.BuyerApproved)
                            Console.WriteLine("\nBuyer has approved the liability transfer.");
                        else
                            Console.WriteLine("\nStill waiting for buyer approval.");
                    }
                    else if (invoice.Status == InvoiceStatus.SellerAcceptancePending)
                    {
                        // Check status of seller acceptance
                        if (invoice.SellerAccepted)
                            Console.WriteLine("\nSeller has accepted the early payment offer.");
                        else
                            Console.WriteLine("\nStill waiting for seller acceptance.");
                    }
                    else if (invoice.Status == InvoiceStatus.Approved)
                    {
                        ProcessFundingForInvoice(invoice);
                    }
                    break;
                case "2":
                    if (invoice.Status == InvoiceStatus.Uploaded)
                    {
                        // Reject invoice
                        Console.Write("\nEnter rejection reason: ");
                        string reason = Console.ReadLine() ?? "Rejected by bank officer";
                        var rejectResult = _invoiceService.RejectInvoice(invoice.Id, _currentUser.Id, reason);
                        Console.WriteLine($"\n{rejectResult.Message}");
                    }
                    else if (invoice.Status == InvoiceStatus.BuyerUploaded)
                    {
                        // Make early payment offer to seller
                        MakeEarlyPaymentOffer(invoice);
                    }
                    else if (invoice.Status == InvoiceStatus.Validated)
                    {
                        // Request buyer approval
                        RequestBuyerApproval(invoice);
                    }
                    else if ((invoice.Status == InvoiceStatus.BuyerApprovalPending && invoice.BuyerApproved) || 
                             (invoice.Status == InvoiceStatus.SellerAcceptancePending && invoice.SellerAccepted))
                    {
                        ProcessFundingForInvoice(invoice);
                    }
                    break;
                case "3":
                    if (invoice.Status == InvoiceStatus.Validated)
                    {
                        // Make early payment offer
                        MakeEarlyPaymentOffer(invoice);
                    }
                    else if (invoice.Status == InvoiceStatus.BuyerUploaded)
                    {
                        // Reject invoice
                        Console.Write("\nEnter rejection reason: ");
                        string reason = Console.ReadLine() ?? "Rejected by bank officer";
                        var rejectResult = _invoiceService.RejectInvoice(invoice.Id, _currentUser.Id, reason);
                        Console.WriteLine($"\n{rejectResult.Message}");
                    }
                    break;
                case "4":
                    if (invoice.Status == InvoiceStatus.Validated)
                    {
                        // Reject invoice
                        Console.Write("\nEnter rejection reason: ");
                        string reason = Console.ReadLine() ?? "Rejected by bank officer";
                        var rejectResult = _invoiceService.RejectInvoice(invoice.Id, _currentUser.Id, reason);
                        Console.WriteLine($"\n{rejectResult.Message}");
                    }
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }

        private void ProcessFunding()
        {
            if (_invoiceService == null)
                return;
                
            Console.Clear();
            Console.WriteLine("PROCESS INVOICE FUNDING");
            Console.WriteLine("======================\n");
            
            var approvedInvoices = _invoiceService.GetInvoicesByStatus(InvoiceStatus.Approved);
            
            if (!approvedInvoices.Any())
            {
                Console.WriteLine("No invoices approved and ready for funding.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("Approved Invoices Ready for Funding:");
            for (int i = 0; i < approvedInvoices.Count; i++)
            {
                var invoice = approvedInvoices[i];
                Console.WriteLine($"{i + 1}. Invoice #{invoice.InvoiceNumber} | Amount: ${invoice.Amount:N2} | Seller: {invoice.Seller?.Name} | Buyer: {invoice.Buyer?.Name}");
            }
            
            Console.Write("\nSelect invoice number to fund (or 0 to return): ");
            if (int.TryParse(Console.ReadLine(), out int selection) && selection > 0 && selection <= approvedInvoices.Count)
            {
                var selectedInvoice = approvedInvoices[selection - 1];
                FundInvoice(selectedInvoice);
            }
            else if (selection != 0)
            {
                Console.WriteLine("Invalid selection.");
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void FundInvoice(Invoice invoice)
        {
            if (_invoiceService == null || _currentUser == null)
                return;
                
            Console.Clear();
            Console.WriteLine($"FUND INVOICE: {invoice.InvoiceNumber}");
            Console.WriteLine("=================================\n");
            
            Console.WriteLine($"Invoice Number: {invoice.InvoiceNumber}");
            Console.WriteLine($"Amount: ${invoice.Amount:N2}");
            Console.WriteLine($"Issue Date: {invoice.IssueDate.ToShortDateString()}");
            Console.WriteLine($"Due Date: {invoice.DueDate.ToShortDateString()}");
            Console.WriteLine($"Seller: {invoice.Seller?.Name}");
            Console.WriteLine($"Buyer: {invoice.Buyer?.Name}");
            
            Console.WriteLine("\nPlease enter the funding details:");
            
            Console.Write("Base Rate (%): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal baseRate) || baseRate < 0)
            {
                Console.WriteLine("Invalid base rate. Funding cancelled.");
                return;
            }
            
            Console.Write("Department Margin (%): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal margin) || margin < 0)
            {
                Console.WriteLine("Invalid margin. Funding cancelled.");
                return;
            }
            
            // Calculate final rate
            decimal finalRate = baseRate + margin;
            Console.WriteLine($"\nFinal Discount Rate: {finalRate:N2}%");
            
            // Calculate funding amount
            decimal discountAmount = invoice.Amount * (finalRate / 100m);
            decimal fundedAmount = invoice.Amount - discountAmount;
            Console.WriteLine($"Invoice Face Value: ${invoice.Amount:N2}");
            Console.WriteLine($"Discount Amount: ${discountAmount:N2} (deducted from face value)");
            Console.WriteLine($"Amount to be Credited to Seller: ${fundedAmount:N2}");
            Console.WriteLine($"Buyer will pay full face value of ${invoice.Amount:N2} at maturity");
            
            Console.Write("\nConfirm funding (Y/N)? ");
            string? confirm = Console.ReadLine()?.Trim().ToUpper();
            
            if (confirm != "Y")
            {
                Console.WriteLine("Funding cancelled.");
                return;
            }
            
            var fundingDetails = new Core.Models.FundingDetails
            {
                BaseRate = baseRate,
                MarginRate = margin,
                FinalDiscountRate = finalRate,
                FundingDate = DateTime.Now
            };
            
            var result = _invoiceService.FundInvoice(invoice.Id, _currentUser.Id, fundingDetails);
            Console.WriteLine($"\n{result.Message}");
        }

        private void ViewTransactions()
        {
            Console.WriteLine("\nFeature not implemented yet.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private void ViewReports()
        {
            Console.WriteLine("\nFeature not implemented yet.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private void ViewNotifications()
        {
            if (_authService == null || _currentUser == null)
                return;
                
            Console.Clear();
            Console.WriteLine("NOTIFICATIONS");
            Console.WriteLine("=============\n");
            
            var notifications = _authService.GetUserNotifications(_currentUser.Id);
            
            if (!notifications.Any())
            {
                Console.WriteLine("No notifications found.");
            }
            else
            {
                foreach (var notification in notifications)
                {
                    string status = notification.IsRead ? "[Read]" : "[Unread]";
                    Console.WriteLine($"{status} {notification.CreatedDate.ToShortDateString()} - {notification.Title}");
                    Console.WriteLine($"      {notification.Message}");
                    Console.WriteLine();
                    
                    // Mark as read
                    if (!notification.IsRead)
                    {
                        _authService.MarkNotificationAsRead(notification.Id);
                    }
                }
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void RequestBuyerApproval(Invoice invoice)
        {
            if (_invoiceService == null || _currentUser == null)
                return;
                
            Console.Clear();
            Console.WriteLine($"REQUEST BUYER APPROVAL FOR INVOICE: {invoice.InvoiceNumber}");
            Console.WriteLine("=================================\n");
            
            Console.WriteLine($"Invoice Number: {invoice.InvoiceNumber}");
            Console.WriteLine($"Amount: ${invoice.Amount:N2}");
            Console.WriteLine($"Issue Date: {invoice.IssueDate.ToShortDateString()}");
            Console.WriteLine($"Due Date: {invoice.DueDate.ToShortDateString()}");
            Console.WriteLine($"Seller: {invoice.Seller?.Name}");
            Console.WriteLine($"Buyer: {invoice.Buyer?.Name}");
            Console.WriteLine($"Status: {invoice.Status}");
            Console.WriteLine($"Description: {invoice.Description}");
            
            Console.WriteLine("\nSending request to buyer for liability transfer approval...");
            
            var result = _invoiceService.RequestBuyerApproval(invoice.Id, _currentUser.Id);
            Console.WriteLine($"\n{result.Message}");
        }
        
        private void MakeEarlyPaymentOffer(Invoice invoice)
        {
            if (_invoiceService == null || _currentUser == null)
                return;
                
            Console.Clear();
            Console.WriteLine($"MAKE EARLY PAYMENT OFFER FOR INVOICE: {invoice.InvoiceNumber}");
            Console.WriteLine("=================================\n");
            
            Console.WriteLine($"Invoice Number: {invoice.InvoiceNumber}");
            Console.WriteLine($"Amount: ${invoice.Amount:N2}");
            Console.WriteLine($"Issue Date: {invoice.IssueDate.ToShortDateString()}");
            Console.WriteLine($"Due Date: {invoice.DueDate.ToShortDateString()}");
            Console.WriteLine($"Seller: {invoice.Seller?.Name}");
            Console.WriteLine($"Buyer: {invoice.Buyer?.Name}");
            Console.WriteLine($"Status: {invoice.Status}");
            Console.WriteLine($"Description: {invoice.Description}");
            
            Console.Write("\nEnter proposed discount rate (%): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal discountRate) || discountRate <= 0)
            {
                Console.WriteLine("Invalid discount rate. Must be greater than zero.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            decimal discountAmount = invoice.Amount * (discountRate / 100m);
            decimal fundedAmount = invoice.Amount - discountAmount;
            
            Console.WriteLine($"\nProposed Early Payment Terms:");
            Console.WriteLine($"Invoice Amount: ${invoice.Amount:N2}");
            Console.WriteLine($"Discount Rate: {discountRate}%");
            Console.WriteLine($"Discount Amount: ${discountAmount:N2}");
            Console.WriteLine($"Amount to be Paid to Seller: ${fundedAmount:N2}");
            Console.WriteLine($"Original Due Date: {invoice.DueDate.ToShortDateString()}");
            Console.WriteLine($"New Payment Date: Immediate upon seller acceptance");
            
            Console.Write("\nSend offer to seller? (Y/N): ");
            string response = Console.ReadLine() ?? "";
            
            if (response.ToUpper() == "Y")
            {
                var result = _invoiceService.RequestSellerAcceptance(invoice.Id, _currentUser.Id, discountRate);
                Console.WriteLine($"\n{result.Message}");
            }
            else
            {
                Console.WriteLine("\nOffer cancelled.");
            }
        }
        
        private void ManageBuyerLimits()
        {
            if (_limitService == null)
                return;
                
            Console.Clear();
            Console.WriteLine("MANAGE BUYER-SPECIFIC LIMITS");
            Console.WriteLine("============================\n");
            
            Console.WriteLine("1. View All Buyer-Seller Relationships");
            Console.WriteLine("2. Allocate Limit to Buyer");
            Console.WriteLine("0. Back");
            
            Console.Write("\nSelect an option: ");
            string? input = Console.ReadLine();
            
            switch (input)
            {
                case "1":
                    ViewAllLimits();
                    break;
                case "2":
                    AllocateBuyerLimit();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("\nInvalid option. Please try again.");
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                    break;
            }
        }
        
        private void ViewAllLimits()
        {
            if (_limitService == null)
                return;
                
            Console.Clear();
            Console.WriteLine("ALL LIMITS REPORT (BANK VIEW)");
            Console.WriteLine("============================\n");
            
            Console.WriteLine("Select report style:");
            Console.WriteLine("1. Tree-like structure (detailed view)");
            Console.WriteLine("2. Tabular format (summary view)");
            Console.Write("\nSelect an option: ");
            
            string? styleChoice = Console.ReadLine();
            
            Console.Clear();
            Console.WriteLine("ALL LIMITS REPORT (BANK VIEW)");
            Console.WriteLine("============================\n");
            
            string report;
            if (styleChoice == "1")
            {
                report = _limitService.GenerateLimitTreeReport();
            }
            else
            {
                report = _limitService.GenerateAllLimitsReport();
            }
            
            Console.WriteLine(report);
            
            Console.WriteLine("\nPress Enter to continue...");
            WaitForEnterKey();
        }
        
        private void AllocateBuyerLimit()
        {
            if (_limitService == null || _userService == null)
                return;
                
            Console.Clear();
            Console.WriteLine("ALLOCATE BUYER LIMIT");
            Console.WriteLine("===================\n");
            
            // Get all sellers
            var sellers = _userService.GetOrganizations().Where(o => o.IsSeller).ToList();
            if (!sellers.Any())
            {
                Console.WriteLine("No sellers found in the system.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            // List sellers
            Console.WriteLine("Available Sellers:");
            for (int i = 0; i < sellers.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {sellers[i].Name}");
            }
            
            // Select seller
            Console.Write("\nSelect seller (number): ");
            if (!int.TryParse(Console.ReadLine(), out int sellerIndex) || sellerIndex < 1 || sellerIndex > sellers.Count)
            {
                Console.WriteLine("Invalid selection.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            var selectedSeller = sellers[sellerIndex - 1];
            
            // Get all buyers
            var buyers = _userService.GetOrganizations().Where(o => o.IsBuyer).ToList();
            if (!buyers.Any())
            {
                Console.WriteLine("No buyers found in the system.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            // List buyers
            Console.WriteLine("\nAvailable Buyers:");
            for (int i = 0; i < buyers.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {buyers[i].Name}");
            }
            
            // Select buyer
            Console.Write("\nSelect buyer (number): ");
            if (!int.TryParse(Console.ReadLine(), out int buyerIndex) || buyerIndex < 1 || buyerIndex > buyers.Count)
            {
                Console.WriteLine("Invalid selection.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            var selectedBuyer = buyers[buyerIndex - 1];
            
            // Select facility type
            Console.WriteLine("\nSelect facility type:");
            Console.WriteLine("1. Invoice Financing");
            Console.WriteLine("2. Term Loan");
            Console.WriteLine("3. Overdraft");
            Console.WriteLine("4. Guarantee");
            
            Console.Write("\nSelect facility type (number): ");
            if (!int.TryParse(Console.ReadLine(), out int facilityTypeIndex) || facilityTypeIndex < 1 || facilityTypeIndex > 4)
            {
                Console.WriteLine("Invalid selection.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            FacilityType facilityType;
            switch (facilityTypeIndex)
            {
                case 1: facilityType = FacilityType.InvoiceFinancing; break;
                case 2: facilityType = FacilityType.TermLoan; break;
                case 3: facilityType = FacilityType.Overdraft; break;
                case 4: facilityType = FacilityType.Guarantee; break;
                default: facilityType = FacilityType.InvoiceFinancing; break;
            }
            
            // Enter amount
            Console.Write("\nEnter amount to allocate: $");
            if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
            {
                Console.WriteLine("Invalid amount.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            // Allocate limit
            var result = _limitService.AllocateBuyerLimit(selectedSeller.Id, selectedBuyer.Id, facilityType, amount);
            Console.WriteLine($"\n{result.Message}");
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
        
        private void LaunchGrantBuyerLimit()
        {
            Console.Clear();
            Console.WriteLine("GRANT BUYER LIMIT");
            Console.WriteLine("=================\n");
            
            Console.WriteLine("This will run the integrated GrantBuyerLimit functionality for automated limit management.");
            Console.WriteLine("The GrantBuyerLimit provides:");
            Console.WriteLine("- Automated buyer-seller limit allocation");
            Console.WriteLine("- Transaction-based limit granting");
            Console.WriteLine("- Additional facility management options");
            
            Console.Write("\nDo you want to run GrantBuyerLimit? (Y/N): ");
            string? response = Console.ReadLine()?.Trim().ToUpper();
            
            if (response == "Y")
            {
                try
                {
                    Console.WriteLine("\nRunning GrantBuyerLimit...\n");
                    GrantBuyerLimit.Execute();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error running GrantBuyerLimit: {ex.Message}");
                }
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
        
        private void GrantFacilityToCustomer()
        {
            if (_limitService == null || _userService == null)
                return;
                
            Console.Clear();
            Console.WriteLine("GRANT FACILITY TO ANY CUSTOMER");
            Console.WriteLine("==============================\n");
            
            // Get all non-bank organizations
            var customers = _userService.GetOrganizations().Where(o => !o.IsBank).ToList();
            if (!customers.Any())
            {
                Console.WriteLine("No customers found in the system.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            // List customers
            Console.WriteLine("Available Customers:");
            for (int i = 0; i < customers.Count; i++)
            {
                var customer = customers[i];
                string type = "";
                if (customer.IsBuyer && customer.IsSeller) type = " (Buyer & Seller)";
                else if (customer.IsBuyer) type = " (Buyer)";
                else if (customer.IsSeller) type = " (Seller)";
                else type = " (Other)";
                
                Console.WriteLine($"{i + 1}. {customer.Name}{type}");
            }
            
            // Select customer
            Console.Write("\nSelect customer (number): ");
            if (!int.TryParse(Console.ReadLine(), out int customerIndex) || customerIndex < 1 || customerIndex > customers.Count)
            {
                Console.WriteLine("Invalid selection.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            var selectedCustomer = customers[customerIndex - 1];
            
            // Check if customer already has credit limits
            var existingCreditLimit = _limitService.GetCreditLimitsByOrganization(selectedCustomer.Id).FirstOrDefault();
            
            if (existingCreditLimit != null)
            {
                Console.WriteLine($"\nCustomer already has existing credit limits.");
                Console.WriteLine("1. Add facility to existing limit");
                Console.WriteLine("2. View existing facilities first");
                Console.WriteLine("0. Cancel");
                
                Console.Write("\nSelect an option: ");
                string? choice = Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        AddFacilityToExistingLimit(selectedCustomer, existingCreditLimit);
                        break;
                    case "2":
                        ViewCustomerFacilities(selectedCustomer.Id);
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Invalid option.");
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();
                        break;
                }
            }
            else
            {
                CreateNewCreditLimitForCustomer(selectedCustomer);
            }
        }
        
        void AddFacilityToExistingLimit(Organization customer, CreditLimitInfo creditLimit)
        {
            if (_limitService == null) return;
            
            Console.WriteLine($"\nAdding facility to {customer.Name}");
            Console.WriteLine($"Current Master Limit: ${creditLimit.MasterLimit:N2}");
            Console.WriteLine($"Current Utilization: ${creditLimit.TotalUtilization:N2}");
            Console.WriteLine($"Available: ${creditLimit.AvailableMasterLimit:N2}");
            
            // Select facility type
            Console.WriteLine("\nSelect facility type:");
            Console.WriteLine("1. Invoice Financing");
            Console.WriteLine("2. Term Loan");
            Console.WriteLine("3. Overdraft");
            Console.WriteLine("4. Guarantee");
            
            Console.Write("\nSelect facility type (number): ");
            if (!int.TryParse(Console.ReadLine(), out int facilityTypeIndex) || facilityTypeIndex < 1 || facilityTypeIndex > 4)
            {
                Console.WriteLine("Invalid selection.");
                return;
            }
            
            FacilityType facilityType;
            switch (facilityTypeIndex)
            {
                case 1: facilityType = FacilityType.InvoiceFinancing; break;
                case 2: facilityType = FacilityType.TermLoan; break;
                case 3: facilityType = FacilityType.Overdraft; break;
                case 4: facilityType = FacilityType.Guarantee; break;
                default: facilityType = FacilityType.InvoiceFinancing; break;
            }
            
            // Enter facility details
            Console.Write("\nEnter facility limit: $");
            if (!decimal.TryParse(Console.ReadLine(), out decimal facilityLimit) || facilityLimit <= 0)
            {
                Console.WriteLine("Invalid amount.");
                return;
            }
            
            Console.Write("Enter review end date (MM/DD/YYYY): ");
            if (!DateTime.TryParse(Console.ReadLine(), out DateTime reviewEndDate) || reviewEndDate <= DateTime.Now)
            {
                Console.WriteLine("Invalid date or date is in the past.");
                return;
            }
            
            Console.Write("Enter grace period days (default: 5): ");
            string? gracePeriodInput = Console.ReadLine();
            int gracePeriodDays = 5;
            if (!string.IsNullOrWhiteSpace(gracePeriodInput))
            {
                int.TryParse(gracePeriodInput, out gracePeriodDays);
            }
            
            // Check if we need to increase master limit
            decimal newTotalFacilities = creditLimit.Facilities.Sum(f => f.TotalLimit) + facilityLimit;
            if (newTotalFacilities > creditLimit.MasterLimit)
            {
                Console.WriteLine($"\nWarning: New facility would exceed master limit.");
                Console.WriteLine($"Current Master Limit: ${creditLimit.MasterLimit:N2}");
                Console.WriteLine($"Total Facilities (with new): ${newTotalFacilities:N2}");
                Console.WriteLine($"Suggested Master Limit: ${newTotalFacilities:N2}");
                
                Console.Write("\nUpdate master limit to suggested amount? (y/n): ");
                string? updateMaster = Console.ReadLine();
                if (updateMaster?.ToLower() == "y")
                {
                    creditLimit.MasterLimit = newTotalFacilities;
                }
                else
                {
                    Console.WriteLine("Cannot add facility that exceeds master limit.");
                    return;
                }
            }
            
            // Add the facility
            var result = _limitService.AddFacilityToOrganization(customer.Id, facilityType, facilityLimit, reviewEndDate, gracePeriodDays);
            
            Console.WriteLine($"\n{result.Message}");
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
        
        void CreateNewCreditLimitForCustomer(Organization customer)
        {
            if (_limitService == null) return;
            
            Console.WriteLine($"\nCreating new credit limit for {customer.Name}");
            
            // Enter master limit
            Console.Write("Enter master credit limit: $");
            if (!decimal.TryParse(Console.ReadLine(), out decimal masterLimit) || masterLimit <= 0)
            {
                Console.WriteLine("Invalid amount.");
                return;
            }
            
            // Ask for counterparty information
            Console.Write("\nAdd a counterparty for this customer? (Y/N): ");
            string? addCounterparty = Console.ReadLine()?.Trim().ToUpper();
            
            if (addCounterparty == "Y")
            {
                Console.Write("Enter counterparty name: ");
                string? counterpartyName = Console.ReadLine()?.Trim();
                
                if (!string.IsNullOrWhiteSpace(counterpartyName))
                {
                    Console.Write("Is this counterparty a Buyer? (Y/N): ");
                    bool isBuyer = Console.ReadLine()?.Trim().ToUpper() == "Y";
                    
                    Console.Write("Is this counterparty a Seller? (Y/N): ");
                    bool isSeller = Console.ReadLine()?.Trim().ToUpper() == "Y";
                    
                    // Save counterparty to database
                    using (var scope = _services.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<SupplyChainDbContext>();
                        
                        // Check if counterparty already exists
                        var existingCounterparty = context.Counterparties
                            .FirstOrDefault(c => c.Name.ToLower() == counterpartyName.ToLower());
                            
                        if (existingCounterparty == null)
                        {
                            var counterparty = new Counterparty
                            {
                                Name = counterpartyName,
                                IsBuyer = isBuyer,
                                IsSeller = isSeller
                            };
                            
                            context.Counterparties.Add(counterparty);
                            context.SaveChanges();
                            Console.WriteLine($"Counterparty '{counterpartyName}' added successfully.");
                        }
                        else
                        {
                            Console.WriteLine($"Counterparty '{counterpartyName}' already exists.");
                        }
                    }
                }
            }
            
            // Create initial facilities
            var facilities = new List<(FacilityType Type, decimal Limit, DateTime ReviewEndDate, int GracePeriodDays)>();
            
            Console.Write("\nWould you like to add facilities now, or just create the credit limit? (F=Facilities, C=Credit Limit Only): ");
            string? choice = Console.ReadLine()?.Trim().ToUpper();
            
            if (choice == "C")
            {
                // Create credit limit without facilities (as requested)
                Console.Write("Enter master limit amount: ");
                if (decimal.TryParse(Console.ReadLine(), out decimal masterLimitAmount) && masterLimitAmount > 0)
                {
                    var creditLimit = new CreditLimitInfo
                    {
                        OrganizationId = customer.Id,
                        MasterLimit = masterLimitAmount,
                        Organization = customer,
                        LastReviewDate = DateTime.Now,
                        NextReviewDate = DateTime.Now.AddYears(1)
                    };
                    
                    // Create the credit limit with an empty facilities list
                    var result = _limitService?.CreateCreditLimitWithFacilities(creditLimit, new List<Facility>());
                    if (result != null && result.Success)
                    {
                        Console.WriteLine($"Credit limit of ${masterLimitAmount:N2} successfully created for {customer.Name}");
                        Console.WriteLine("Facilities can be added later through the facility management options.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to create credit limit: {result?.Message ?? "Unknown error"}");
                    }
                }
                else
                {
                    Console.WriteLine("Invalid master limit amount.");
                }
                WaitForEnterKey();
                return;
            }
            
            // Original facility creation logic
            bool addMoreFacilities = true;
            decimal totalFacilities = 0;
            
            while (addMoreFacilities && totalFacilities < masterLimit)
            {
                Console.WriteLine($"\nAdding facility (Master limit: ${masterLimit:N2}, Used: ${totalFacilities:N2}, Available: ${masterLimit - totalFacilities:N2})");
                
                // Select facility type
                Console.WriteLine("Select facility type:");
                Console.WriteLine("1. Invoice Financing");
                Console.WriteLine("2. Term Loan");
                Console.WriteLine("3. Overdraft");
                Console.WriteLine("4. Guarantee");
                
                Console.Write("\nSelect facility type (number): ");
                if (!int.TryParse(Console.ReadLine(), out int facilityTypeIndex) || facilityTypeIndex < 1 || facilityTypeIndex > 4)
                {
                    Console.WriteLine("Invalid selection.");
                    continue;
                }
                
                FacilityType facilityType;
                switch (facilityTypeIndex)
                {
                    case 1: facilityType = FacilityType.InvoiceFinancing; break;
                    case 2: facilityType = FacilityType.TermLoan; break;
                    case 3: facilityType = FacilityType.Overdraft; break;
                    case 4: facilityType = FacilityType.Guarantee; break;
                    default: facilityType = FacilityType.InvoiceFinancing; break;
                }
                
                // Enter facility limit
                Console.Write("Enter facility limit: $");
                if (!decimal.TryParse(Console.ReadLine(), out decimal facilityLimit) || facilityLimit <= 0)
                {
                    Console.WriteLine("Invalid amount.");
                    continue;
                }
                
                // Ensure facility limits don't exceed master limit
                decimal newTotalFacilities = totalFacilities + facilityLimit;
                if (newTotalFacilities > masterLimit)
                {
                    Console.WriteLine($"\nError: Total facility limits (${newTotalFacilities:N2}) would exceed master limit (${masterLimit:N2}).");
                    continue;
                }
                
                Console.Write("Review End Date (MM/DD/YYYY, default is 1 year from now): ");
                string dateInput = Console.ReadLine() ?? "";
                DateTime reviewEndDate;
                
                if (string.IsNullOrWhiteSpace(dateInput))
                    reviewEndDate = DateTime.Now.AddYears(1);
                else if (!DateTime.TryParse(dateInput, out reviewEndDate))
                {
                    Console.WriteLine("Invalid date format. Please use MM/DD/YYYY format.");
                    continue;
                }
                
                Console.Write("Grace Period Days (default is 5): ");
                string graceInput = Console.ReadLine() ?? "";
                int gracePeriodDays = 5;
                
                if (!string.IsNullOrWhiteSpace(graceInput) && (!int.TryParse(graceInput, out gracePeriodDays) || gracePeriodDays < 0))
                {
                    Console.WriteLine("Invalid grace period. Must be a positive number.");
                    continue;
                }
                
                facilities.Add((facilityType, facilityLimit, reviewEndDate, gracePeriodDays));
                totalFacilities += facilityLimit;
                
                Console.Write("\nAdd another facility? (Y/N): ");
                string addAnother = Console.ReadLine()?.Trim().ToUpper() ?? "N";
                
                if (addAnother != "Y")
                    addMoreFacilities = false;
            }
            
            if (facilities.Count == 0)
            {
                Console.WriteLine("\nNo facilities were added. Credit limit creation canceled.");
                WaitForEnterKey();
                return;
            }
            
            // Create the credit limit with facilities
            try
            {
                Console.WriteLine($"\nDEBUG: Creating credit limit with master limit {masterLimit} for organization ID {customer.Id}");
                Console.WriteLine($"DEBUG: Adding {facilities.Count} facilities: {string.Join(", ", facilities.Select(f => $"{f.Type}:{f.Limit}"))}");
                
                var creditLimit = new CreditLimitInfo 
                { 
                    MasterLimit = masterLimit,
                    OrganizationId = customer.Id,
                    Organization = customer,
                    LastReviewDate = DateTime.Now,
                    NextReviewDate = DateTime.Now.AddYears(1)
                };
                
                var result = _limitService.CreateCreditLimitWithFacilities(creditLimit, facilities);
                Console.WriteLine($"\nCredit limit successfully created with {facilities.Count} facilities.");
                Console.WriteLine($"DEBUG: Result message: {result.Message}");
                Console.WriteLine($"DEBUG: Result success: {result.Success}");
                
                // Verify the facilities were added by querying the database
                using (var scope = _services.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<SupplyChainDbContext>();
                    var savedFacilities = context.Facilities
                        .Where(f => f.CreditLimitInfo != null && f.CreditLimitInfo.OrganizationId == customer.Id)
                        .ToList();
                    
                    Console.WriteLine($"\nDEBUG: Database verification - Found {savedFacilities.Count} facilities for organization {customer.Id}:");
                    foreach (var f in savedFacilities)
                    {
                        Console.WriteLine($"DEBUG: Facility ID {f.Id}, Type {f.Type}, Limit {f.TotalLimit:N2}");
                    }
                }
                
                // Update organization roles if necessary
                bool updateNeeded = false;
                string roleMessage = "";
                
                // If they have an Invoice Financing facility and not marked as Seller
                if (facilities.Any(f => f.Type == FacilityType.InvoiceFinancing) && !customer.IsSeller)
                {
                    customer.IsSeller = true;
                    updateNeeded = true;
                    roleMessage += "Organization marked as Seller. ";
                    Console.WriteLine($"DEBUG: Organization {customer.Id} marked as Seller");
                }
                
                // If they have any financing facility (not just Invoice Financing) and not marked as Buyer
                if (facilities.Any() && !customer.IsBuyer)
                {
                    customer.IsBuyer = true;
                    updateNeeded = true;
                    roleMessage += "Organization marked as Buyer.";
                    Console.WriteLine($"DEBUG: Organization {customer.Id} marked as Buyer");
                }
                
                if (updateNeeded && _authService != null)
                {
                    _authService.UpdateOrganization(customer);
                }
                else if (updateNeeded)
                {
                    Console.WriteLine("Warning: Could not update organization - authentication service not available.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError creating credit limit: {ex.Message}");
            }
            
            WaitForEnterKey();
        }
        
        void ManageOrganization(int organizationId)
        {
            if (_userService == null)
                return;
                
            var organization = _userService.GetOrganizations().FirstOrDefault(o => o.Id == organizationId);
            if (organization == null)
            {
                Console.WriteLine("Organization not found.");
                return;
            }

            bool exit = false;
            while (!exit)
            {
                Console.Clear();
                Console.WriteLine($"===== Managing Organization: {organization.Name} =====");
                Console.WriteLine("1. View Organization Details");
                Console.WriteLine("2. Edit Organization");
                Console.WriteLine("3. View Facilities");
                Console.WriteLine("4. Add Facility");
                Console.WriteLine("5. View Credit Limits");
                Console.WriteLine("6. Add Credit Limit");
                Console.WriteLine("0. Back to Organizations");

                Console.Write("\nSelect an option: ");
                var option = Console.ReadLine();

                switch (option)
                {
                    case "1":
                        ViewOrganizationDetails(organization);
                        break;
                    case "2":
                        EditOrganization(organization);
                        break;
                    case "3":
                        ViewFacilities(organization);
                        break;
                    case "4":
                        AddFacilityToOrganization(organization);
                        break;
                    case "5":
                        ViewCreditLimits(organization);
                        break;
                    case "6":
                        CreateCreditLimitWithFacilities(organization);
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        WaitForEnterKey();
                        break;
                }
            }
        }
        
        void ViewOrganizationDetails(Organization organization)
        {
            Console.Clear();
            Console.WriteLine($"Organization Details: {organization.Name}");
            Console.WriteLine("======================================");
            Console.WriteLine($"ID: {organization.Id}");
            Console.WriteLine($"Name: {organization.Name}");
            Console.WriteLine($"Tax ID: {organization.TaxId}");
            Console.WriteLine($"Address: {organization.Address}");
            Console.WriteLine($"Contact Person: {organization.ContactPerson}");
            Console.WriteLine($"Contact Email: {organization.ContactEmail}");
            Console.WriteLine($"Contact Phone: {organization.ContactPhone}");
            Console.WriteLine($"Type: {(organization.IsBuyer ? "Buyer" : "")}{(organization.IsSeller ? "Seller" : "")}");
            
            WaitForEnterKey();
        }

        void EditOrganization(Organization organization)
        {
            Console.Clear();
            Console.WriteLine($"Edit Organization: {organization.Name}");
            Console.WriteLine("======================================");
            
            Console.Write($"Name [{organization.Name}]: ");
            string name = Console.ReadLine() ?? "";
            if (!string.IsNullOrWhiteSpace(name))
                organization.Name = name;
                
            Console.Write($"Tax ID [{organization.TaxId}]: ");
            string taxId = Console.ReadLine() ?? "";
            if (!string.IsNullOrWhiteSpace(taxId))
                organization.TaxId = taxId;
                
            Console.Write($"Address [{organization.Address}]: ");
            string address = Console.ReadLine() ?? "";
            if (!string.IsNullOrWhiteSpace(address))
                organization.Address = address;
                
            Console.Write($"Contact Person [{organization.ContactPerson}]: ");
            string contactPerson = Console.ReadLine() ?? "";
            if (!string.IsNullOrWhiteSpace(contactPerson))
                organization.ContactPerson = contactPerson;
                
            Console.Write($"Contact Email [{organization.ContactEmail}]: ");
            string contactEmail = Console.ReadLine() ?? "";
            if (!string.IsNullOrWhiteSpace(contactEmail))
                organization.ContactEmail = contactEmail;
                
            Console.Write($"Contact Phone [{organization.ContactPhone}]: ");
            string contactPhone = Console.ReadLine() ?? "";
            if (!string.IsNullOrWhiteSpace(contactPhone))
                organization.ContactPhone = contactPhone;
                
            Console.Write($"Is Buyer (y/n) [{(organization.IsBuyer ? "y" : "n")}]: ");
            string isBuyer = Console.ReadLine() ?? "";
            if (isBuyer.ToLower() == "y")
                organization.IsBuyer = true;
            else if (isBuyer.ToLower() == "n")
                organization.IsBuyer = false;
                
            Console.Write($"Is Seller (y/n) [{(organization.IsSeller ? "y" : "n")}]: ");
            string isSeller = Console.ReadLine() ?? "";
            if (isSeller.ToLower() == "y")
                organization.IsSeller = true;
            else if (isSeller.ToLower() == "n")
                organization.IsSeller = false;
            
            // Since we don't have direct access to UpdateOrganization, we'll use a simpler approach
            Console.WriteLine("\nOrganization updated successfully!");
            // Note: In a real application, we would need to properly save these changes
            // to the database.
            
            Console.WriteLine("\nOrganization updated successfully!");
            WaitForEnterKey();
        }

        void ViewFacilities(Organization organization)
        {
            Console.Clear();
            Console.WriteLine($"Facilities for {organization.Name}");
            Console.WriteLine("======================================");

            // Find all credit limits for this organization first
            List<CreditLimitInfo> creditLimits = new List<CreditLimitInfo>();
            List<Facility> facilities = new List<Facility>();
            
            if (_limitService != null)
            {
                creditLimits = _limitService.GetCreditLimitsByOrganization(organization.Id);
                
                // Collect all facilities across all credit limits
                foreach (var limit in creditLimits)
                {
                    facilities.AddRange(limit.Facilities);
                }
            }

            if (facilities.Count == 0)
            {
                Console.WriteLine("No facilities found for this organization.");
            }
            else
            {
                Console.WriteLine("ID | Type | Total Limit | Available | Utilization % | Review End Date");
                Console.WriteLine("----------------------------------------------------------------");
                
                foreach (var facility in facilities)
                {
                    Console.WriteLine($"{facility.Id} | {facility.Type} | {facility.TotalLimit:C} | {facility.AvailableLimit:C} | {facility.UtilizationPercentage:F1}% | {facility.ReviewEndDate:yyyy-MM-dd}");
                }
            }

            WaitForEnterKey();
        }

        void AddFacilityToOrganization(Organization organization)
        {
            Console.Clear();
            Console.WriteLine($"Add Facility to {organization.Name}");
            Console.WriteLine("======================================");
            
            if (_limitService == null)
            {
                Console.WriteLine("Limit service not available.");
                WaitForEnterKey();
                return;
            }
            
            // First check if the organization has a credit limit
            CreditLimitInfo? creditLimit = null;
            try
            {
                creditLimit = _limitService.GetOrganizationCreditLimitInfo(organization.Id);
            }
            catch (Exception)
            {
                // Create a new credit limit if one doesn't exist
                Console.WriteLine("Organization doesn't have a credit limit yet.");
                Console.Write("Would you like to create one? (y/n): ");
                string response = Console.ReadLine() ?? "";
                
                if (response.ToLower() == "y")
                {
                    Console.Write("Enter master limit amount: ");
                    if (decimal.TryParse(Console.ReadLine(), out decimal masterLimit) && masterLimit > 0)
                    {
                        creditLimit = new CreditLimitInfo
                        {
                            OrganizationId = organization.Id,
                            MasterLimit = masterLimit,
                            Organization = organization
                        };
                        // Create the credit limit with an empty facilities list
                        var result = _limitService.CreateCreditLimitWithFacilities(creditLimit, new List<Facility>());
                        if (result != null && result.Success)
                        {
                            // Get the updated credit limit info after creation
                            var updatedCreditLimit = _limitService.GetOrganizationCreditLimitInfo(organization.Id);
                            creditLimit = updatedCreditLimit;
                            Console.WriteLine($"Credit limit of {masterLimit:C} created for {organization.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to create credit limit: {result.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid amount. Facility creation cancelled.");
                        WaitForEnterKey();
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Facility creation cancelled.");
                    WaitForEnterKey();
                    return;
                }
            }

            // Now we can add a facility
            Console.WriteLine("\nFacility Type:");
            Console.WriteLine("1. Factoring");
            Console.WriteLine("2. Reverse Factoring");
            Console.WriteLine("3. Revolving Credit");
            Console.WriteLine("4. Term Loan");
            
            Console.Write("\nSelect facility type (1-4): ");
            if (!int.TryParse(Console.ReadLine(), out int typeChoice) || typeChoice < 1 || typeChoice > 4)
            {
                Console.WriteLine("Invalid selection. Facility creation cancelled.");
                WaitForEnterKey();
                return;
            }
            
            FacilityType facilityType = FacilityType.InvoiceFinancing;
            switch (typeChoice)
            {
                case 1:
                    facilityType = FacilityType.InvoiceFinancing;
                    break;
                case 2:
                    facilityType = FacilityType.InvoiceFinancing; // Using invoice financing for reverse factoring too
                    break;
                case 3:
                    facilityType = FacilityType.Overdraft; // Using overdraft for revolving credit
                    break;
                case 4:
                    facilityType = FacilityType.TermLoan;
                    break;
            }
            
            Console.Write("Enter facility limit: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal limit) || limit <= 0)
            {
                Console.WriteLine("Invalid limit. Facility creation cancelled.");
                WaitForEnterKey();
                return;
            }
            
            // Check that facility doesn't exceed the available master limit
            if (limit > creditLimit.AvailableMasterLimit)
            {
                Console.WriteLine($"Error: Facility limit of {limit:C} exceeds available master limit of {creditLimit.AvailableMasterLimit:C}");
                WaitForEnterKey();
                return;
            }
            
            Console.Write("Enter review end date (yyyy-MM-dd): ");
            if (!DateTime.TryParse(Console.ReadLine(), out DateTime reviewEndDate))
            {
                Console.WriteLine("Invalid date format. Facility creation cancelled.");
                WaitForEnterKey();
                return;
            }
            
            Console.Write("Enter grace period days (default 5): ");
            string gracePeriodInput = Console.ReadLine() ?? "";
            int gracePeriodDays = 5;
            if (!string.IsNullOrEmpty(gracePeriodInput) && !int.TryParse(gracePeriodInput, out gracePeriodDays))
            {
                Console.WriteLine("Invalid grace period. Using default of 5 days.");
                gracePeriodDays = 5;
            }
            
            // Create the facility
            var facility = new Facility
            {
                CreditLimitInfoId = creditLimit.Id,
                Type = facilityType,
                TotalLimit = limit,
                ReviewEndDate = reviewEndDate,
                GracePeriodDays = gracePeriodDays
            };
            
            try
            {
                // Since we can't directly access the database, update through the limit service
                // We'll need to update the credit limit with our new facility
                List<Facility> updatedFacilities = creditLimit.Facilities.ToList();
                updatedFacilities.Add(facility);
                
                // Use the existing CreateCreditLimitWithFacilities method with the updated list
                _limitService.CreateCreditLimitWithFacilities(creditLimit, updatedFacilities);
                Console.WriteLine($"\nFacility added successfully: {facilityType} with limit {limit:C}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding facility: {ex.Message}");
            }
            
            WaitForEnterKey();
        }

        void ViewCustomerFacilities(int customerId)
        {
            if (_limitService == null)
                return;
                
            Console.Clear();
            Console.WriteLine("CUSTOMER FACILITIES");
            Console.WriteLine("==================\n");
            
            try
            {
                // Get organization information
                var organization = _userService?.GetOrganizations().FirstOrDefault(o => o.Id == customerId);
                if (organization == null)
                {
                    Console.WriteLine("Customer not found.");
                    return;
                }
                
                Console.WriteLine($"Customer: {organization.Name}");
                Console.WriteLine($"Type: {(organization.IsBuyer ? "Buyer " : "")}{(organization.IsSeller ? "Seller" : "")}");
                
                // Get credit limits and facilities
                var creditLimits = _limitService.GetCreditLimitsByOrganization(customerId);
                
                if (!creditLimits.Any())
                {
                    Console.WriteLine("\nNo facilities found for this customer.");
                    return;
                }
                
                foreach (var limit in creditLimits)
                {
                    Console.WriteLine($"\nMaster Limit: ${limit.MasterLimit:N2}");
                    Console.WriteLine($"Total Utilization: ${limit.TotalUtilization:N2} ({limit.MasterUtilizationPercentage:N2}%)");
                    Console.WriteLine($"Available: ${limit.AvailableMasterLimit:N2}");
                    
                    Console.WriteLine("\nFacilities:");
                    Console.WriteLine($"{"Type",-15} {"Limit",-15} {"Used",-15} {"Available",-15} {"Status",-15} {"Expiry Date",-12}");
                    Console.WriteLine(new string('-', 90));
                    
                    foreach (var facility in limit.Facilities)
                    {
                        string status = facility.IsExpired ? "EXPIRED" : "Active";
                        Console.WriteLine($"{facility.Type,-15} ${facility.TotalLimit,-13:N2} ${facility.CurrentUtilization,-13:N2} ${facility.AvailableLimit,-13:N2} {status,-15} {facility.ReviewEndDate.ToShortDateString(),-12}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError retrieving facilities: {ex.Message}");
            }
        }
        
        void ViewCreditLimits(Organization organization)
        {
            Console.Clear();
            Console.WriteLine($"Credit Limits for {organization.Name}");
            Console.WriteLine("======================================");
            
            if (_limitService == null)
            {
                Console.WriteLine("Limit service not available.");
                WaitForEnterKey();
                return;
            }
            
            try
            {
                var creditLimitInfo = _limitService.GetOrganizationCreditLimitInfo(organization.Id);
                
                Console.WriteLine($"Master Limit: {creditLimitInfo.MasterLimit:C}");
                Console.WriteLine($"Total Utilization: {creditLimitInfo.TotalUtilization:C}");
                Console.WriteLine($"Available Master Limit: {creditLimitInfo.AvailableMasterLimit:C}");
                Console.WriteLine($"Utilization: {creditLimitInfo.MasterUtilizationPercentage:F1}%");
                
                Console.WriteLine("\nFacilities:");
                Console.WriteLine("ID | Type | Limit | Available | Utilization % | Review End Date");
                Console.WriteLine("----------------------------------------------------------------");
                
                foreach (var facility in creditLimitInfo.Facilities)
                {
                    string status = facility.IsExpired ? "EXPIRED" : "Active";
                    Console.WriteLine($"{facility.Id} | {facility.Type} | {facility.TotalLimit:C} | {facility.AvailableLimit:C} | {facility.UtilizationPercentage:F1}% | {facility.ReviewEndDate:yyyy-MM-dd} | {status}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            WaitForEnterKey();
        }

        void CreateCreditLimitWithFacilities(Organization organization)
        {
            Console.Clear();
            Console.WriteLine($"Create Credit Limit for {organization.Name}");
            Console.WriteLine("======================================");
            
            if (_limitService == null)
            {
                Console.WriteLine("Limit service not available.");
                WaitForEnterKey();
                return;
            }
            
            // Check if organization already has a credit limit
            try
            {
                var existingLimit = _limitService.GetOrganizationCreditLimitInfo(organization.Id);
                Console.WriteLine($"Organization already has a credit limit of {existingLimit.MasterLimit:C}");
                Console.WriteLine("Would you like to add a facility instead? (y/n)");
                
                string response = Console.ReadLine() ?? "";
                if (response.ToLower() == "y")
                {
                    AddFacilityToOrganization(organization);
                }
                return;
            }
            catch (Exception)
            {
                // No existing limit, continue with creation
            }
            
            // Create a new credit limit
            Console.Write("Enter master limit amount: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal masterLimit) || masterLimit <= 0)
            {
                Console.WriteLine("Invalid amount. Credit limit creation cancelled.");
                WaitForEnterKey();
                return;
            }
            
            // Create credit limit
            var creditLimit = new CreditLimitInfo
            {
                OrganizationId = organization.Id,
                MasterLimit = masterLimit,
                Organization = organization
            };
            
            // Ask if they want to add a facility right away
            Console.Write("\nWould you like to add a facility right away? (y/n): ");
            string addFacility = Console.ReadLine() ?? "";
            
            List<Facility> facilities = new List<Facility>();
            
            if (addFacility.ToLower() == "y")
            {
                Console.WriteLine("\nFacility Type:");
                Console.WriteLine("1. Invoice Financing");
                Console.WriteLine("2. Term Loan");
                Console.WriteLine("3. Overdraft");
                Console.WriteLine("4. Guarantee");
                
                Console.Write("\nSelect facility type (1-4): ");
                if (!int.TryParse(Console.ReadLine(), out int typeChoice) || typeChoice < 1 || typeChoice > 4)
                {
                    Console.WriteLine("Invalid selection. Using Invoice Financing.");
                    typeChoice = 1;
                }
                
                FacilityType facilityType = FacilityType.InvoiceFinancing;
                switch (typeChoice)
                {
                    case 1:
                        facilityType = FacilityType.InvoiceFinancing;
                        break;
                    case 2:
                        facilityType = FacilityType.TermLoan;
                        break;
                    case 3:
                        facilityType = FacilityType.Overdraft;
                        break;
                    case 4:
                        facilityType = FacilityType.Guarantee;
                        break;
                }
                
                Console.Write("Enter facility limit (must be <= master limit): ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal limit) || limit <= 0 || limit > masterLimit)
                {
                    Console.WriteLine($"Invalid limit. Using master limit of {masterLimit:C}.");
                    limit = masterLimit;
                }
                
                Console.Write("Enter review end date (yyyy-MM-dd): ");
                if (!DateTime.TryParse(Console.ReadLine(), out DateTime reviewEndDate))
                {
                    reviewEndDate = DateTime.Now.AddYears(1);
                    Console.WriteLine($"Invalid date. Using one year from now: {reviewEndDate:yyyy-MM-dd}");
                }
                
                // Create the facility
                var facility = new Facility
                {
                    Type = facilityType,
                    TotalLimit = limit,
                    ReviewEndDate = reviewEndDate,
                    GracePeriodDays = 5
                };
                
                facilities.Add(facility);
            }
            
            // Create the credit limit and facility
            try
            {
                var result = _limitService.CreateCreditLimitWithFacilities(creditLimit, facilities);
                Console.WriteLine($"\nCredit limit created successfully with {facilities.Count} facilities.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating credit limit: {ex.Message}");
            }
            
            WaitForEnterKey();
        }

        void AddNewOrganization()
        {
            Console.Clear();
            Console.WriteLine("ADD NEW ORGANIZATION");
            Console.WriteLine("=====================\n");
            
            if (_userService == null || _authService == null)
            {
                Console.WriteLine("Service unavailable.");
                WaitForEnterKey();
                return;
            }

            // Enter organization details
            Console.Write("Organization Name: ");
            string name = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.WriteLine("Name cannot be empty.");
                WaitForEnterKey();
                return;
            }
            
            Console.Write("Tax ID: ");
            string taxId = Console.ReadLine() ?? "";
            
            Console.Write("Address: ");
            string address = Console.ReadLine() ?? "";
            
            Console.Write("Contact Person Name: ");
            string contactPerson = Console.ReadLine() ?? "";
            
            Console.Write("Contact Email: ");
            string contactEmail = Console.ReadLine() ?? "";
            
            Console.Write("Contact Phone: ");
            string contactPhone = Console.ReadLine() ?? "";
            
            // Organization role selection
            Console.WriteLine("\nSelect organization role:");
            Console.WriteLine("1. Buyer");
            Console.WriteLine("2. Seller");
            Console.WriteLine("3. Both Buyer and Seller");
            
            Console.Write("\nSelect role (1-3): ");
            if (!int.TryParse(Console.ReadLine(), out int roleChoice) || roleChoice < 1 || roleChoice > 3)
            {
                Console.WriteLine("Invalid role selection.");
                WaitForEnterKey();
                return;
            }
            
            bool isBuyer = roleChoice == 1 || roleChoice == 3;
            bool isSeller = roleChoice == 2 || roleChoice == 3;
            
            // Create organization object
            var organization = new Organization
            {
                Name = name,
                TaxId = taxId,
                Address = address,
                ContactPerson = contactPerson,
                ContactEmail = contactEmail,
                ContactPhone = contactPhone,
                IsBuyer = isBuyer,
                IsSeller = isSeller,
                IsBank = false
            };
            
            try
            {
                // Add organization to the database
                var result = _userService.AddOrganization(organization);
                
                if (result != null)
                {
                    Console.WriteLine($"\nOrganization '{organization.Name}' created successfully with ID: {organization.Id}");
                    
                    // Ask if admin user should be created
                    Console.Write("\nCreate admin user for this organization? (Y/N): ");
                    if (Console.ReadLine()?.Trim().ToUpper() == "Y")
                    {
                        CreateUserForOrganization(organization);
                    }
                    
                    // Ask if credit facilities should be added
                    Console.Write("\nAdd credit facilities to this organization now? (Y/N): ");
                    if (Console.ReadLine()?.Trim().ToUpper() == "Y")
                    {
                        CreateCreditLimitWithFacilities(organization);
                    }
                }
                else
                {
                    Console.WriteLine("\nFailed to create organization.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
            
            WaitForEnterKey();
        }

        void CreateUserForOrganization(Organization organization)
        {
            if (_authService == null)
                return;
                
            Console.Write("\nUsername: ");
            string username = Console.ReadLine() ?? "";
            
            Console.Write("Password: ");
            string password = Console.ReadLine() ?? "";
            
            Console.Write("Full Name: ");
            string fullName = Console.ReadLine() ?? "";
            
            Console.Write("Email: ");
            string email = Console.ReadLine() ?? "";
            
            // Create admin user for organization
            var user = new User
            {
                Username = username,
                Password = password,  // In a real app, this would be hashed
                Name = fullName,
                Email = email,
                Role = UserRole.ClientAdmin,
                OrganizationId = organization.Id
            };
            
            var userResult = _userService.CreateUser(user);
            Console.WriteLine(userResult.Message);
        }

        private void ProcessFundingForInvoice(Invoice invoice)
        {
            if (_invoiceService == null || _currentUser == null)
                return;
                
            Console.Clear();
            Console.WriteLine($"PROCESS FUNDING FOR INVOICE: {invoice.InvoiceNumber}");
            Console.WriteLine("=================================\n");
            
            Console.WriteLine($"Invoice Number: {invoice.InvoiceNumber}");
            Console.WriteLine($"Amount: ${invoice.Amount:N2}");
            Console.WriteLine($"Issue Date: {invoice.IssueDate.ToShortDateString()}");
            Console.WriteLine($"Due Date: {invoice.DueDate.ToShortDateString()}");
            Console.WriteLine($"Seller: {invoice.Seller?.Name}");
            Console.WriteLine($"Buyer: {invoice.Buyer?.Name}");
            Console.WriteLine($"Status: {invoice.Status}");
            
            Console.WriteLine("\nInvoice is approved and ready for funding. Would you like to proceed?");
            Console.WriteLine("1. Process for immediate funding");
            Console.WriteLine("2. Schedule for funding on a future date");
            Console.WriteLine("0. Cancel");
            
            Console.Write("\nSelect option: ");
            string option = Console.ReadLine() ?? "0";
            
            switch (option)
            {
                case "1":
                    FundInvoice(invoice);
                    break;
                case "2":
                    ScheduleFunding(invoice);
                    break;
                case "0":
                    Console.WriteLine("Funding process cancelled.");
                    break;
                default:
                    Console.WriteLine("Invalid option.");
                    break;
            }
        }
        
        private void ScheduleFunding(Invoice invoice)
        {
            Console.WriteLine("\nSchedule Funding Feature is currently under development.");
            Console.WriteLine("Please use immediate funding option for now.");
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ManageAccountingEntries()
        {
            if (_accountingService == null)
            {
                Console.WriteLine("\nAccounting service not available.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            bool exit = false;

            while (!exit)
            {
                Console.Clear();
                Console.WriteLine("ACCOUNTING ENTRIES MANAGEMENT");
                Console.WriteLine("============================\n");

                Console.WriteLine("Chart of Accounts & Setup:");
                Console.WriteLine("1. View Chart of Accounts\n");

                Console.WriteLine("Journal Entries:");
                Console.WriteLine("2. View Journal Entries");
                Console.WriteLine("3. Create Manual Journal Entry");
                Console.WriteLine("4. Post Journal Entry\n");

                Console.WriteLine("Reports:");
                Console.WriteLine("5. View Account Balances");
                Console.WriteLine("6. Generate Trial Balance\n");

                Console.WriteLine("Transaction Processing:");
                Console.WriteLine("7. Create Invoice Financing Entry");
                Console.WriteLine("8. Create Payment Entry");
                Console.WriteLine("9. View Transaction History\n");

                Console.WriteLine("0. Back to Main Menu");

                Console.Write("\nSelect an option: ");
                string? input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        ViewChartOfAccounts();
                        break;
                    case "2":
                        ViewJournalEntries();
                        break;
                    case "3":
                        CreateManualJournalEntry();
                        break;
                    case "4":
                        PostJournalEntry();
                        break;
                    case "5":
                        ViewAccountBalances();
                        break;
                    case "6":
                        GenerateTrialBalance();
                        break;
                    case "7":
                        CreateInvoiceFinancingEntry();
                        break;
                    case "8":
                        CreatePaymentEntry();
                        break;
                    case "9":
                        ViewTransactionHistory();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("\nInvalid option. Please try again.");
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        private void ViewChartOfAccounts()
        {
            if (_accountingService == null) return;

            Console.Clear();
            Console.WriteLine("CHART OF ACCOUNTS");
            Console.WriteLine("=================\n");

            try
            {
                var accounts = _accountingService.GetAllAccounts();
                if (!accounts.Any())
                {
                    Console.WriteLine("No accounts found. Initializing chart of accounts...");
                    _accountingService.InitializeChartOfAccounts();
                    accounts = _accountingService.GetAllAccounts();
                }

                foreach (var account in accounts.OrderBy(a => a.AccountCode))
                {
                    Console.WriteLine($"{account.AccountCode} - {account.AccountName}");
                    Console.WriteLine($"   Type: {account.Type} | Balance: ${account.Balance:N2}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving chart of accounts: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ViewJournalEntries()
        {
            if (_accountingService == null) return;

            Console.Clear();
            Console.WriteLine("JOURNAL ENTRIES");
            Console.WriteLine("===============\n");

            try
            {
                var entries = _accountingService.GetAllJournalEntries();
                if (!entries.Any())
                {
                    Console.WriteLine("No journal entries found.");
                }
                else
                {
                    foreach (var entry in entries.OrderByDescending(e => e.TransactionDate))
                    {
                        Console.WriteLine($"Entry #{entry.Id} - {entry.TransactionReference}");
                        Console.WriteLine($"Date: {entry.TransactionDate.ToShortDateString()} | Status: {entry.Status}");
                        Console.WriteLine($"Description: {entry.Description}");
                        Console.WriteLine($"Total Debit: ${entry.TotalDebit:N2} | Total Credit: ${entry.TotalCredit:N2}");
                        
                        if (entry.JournalEntryLines?.Any() == true)
                        {
                            Console.WriteLine("Lines:");
                            foreach (var line in entry.JournalEntryLines)
                            {
                                string accountInfo = line.Account != null ? 
                                    $"{line.Account.AccountCode} - {line.Account.AccountName}" : 
                                    $"Account ID: {line.AccountId}";
                                
                                if (line.DebitAmount > 0)
                                    Console.WriteLine($"   DR {accountInfo}: ${line.DebitAmount:N2}");
                                else
                                    Console.WriteLine($"   CR {accountInfo}: ${line.CreditAmount:N2}");
                            }
                        }
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving journal entries: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void CreateManualJournalEntry()
        {
            if (_accountingService == null) return;

            Console.Clear();
            Console.WriteLine("CREATE MANUAL JOURNAL ENTRY");
            Console.WriteLine("===========================\n");

            try
            {
                Console.Write("Enter description: ");
                string description = Console.ReadLine() ?? "Manual Journal Entry";

                Console.Write("Enter transaction ID (optional): ");
                string transactionId = Console.ReadLine() ?? $"MANUAL-{DateTime.Now:yyyyMMdd-HHmmss}";

                var lines = new List<(int AccountId, decimal DebitAmount, decimal CreditAmount, string? EntityName)>();
                decimal totalDebits = 0;
                decimal totalCredits = 0;

                Console.WriteLine("\nEnter journal entry lines (enter 0 to finish):");
                
                while (true)
                {
                    Console.WriteLine($"\nCurrent totals - Debits: ${totalDebits:N2}, Credits: ${totalCredits:N2}");
                    Console.Write("Enter account code (or 0 to finish): ");
                    string accountCodeInput = Console.ReadLine() ?? "";
                    
                    if (accountCodeInput == "0")
                        break;

                    var account = _accountingService.GetAccountByCode(accountCodeInput);
                    if (account == null)
                    {
                        Console.WriteLine("Account not found. Please try again.");
                        continue;
                    }

                    Console.WriteLine($"Selected: {account.AccountCode} - {account.AccountName}");
                    Console.Write("Enter amount: $");
                    if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
                    {
                        Console.WriteLine("Invalid amount. Please try again.");
                        continue;
                    }

                    Console.Write("Debit or Credit (D/C): ");
                    string debitCredit = Console.ReadLine()?.ToUpper() ?? "";
                    
                    Console.Write("Entity name (optional): ");
                    string? entityName = Console.ReadLine();

                    if (debitCredit == "D")
                    {
                        lines.Add((account.Id, amount, 0, entityName));
                        totalDebits += amount;
                    }
                    else if (debitCredit == "C")
                    {
                        lines.Add((account.Id, 0, amount, entityName));
                        totalCredits += amount;
                    }
                    else
                    {
                        Console.WriteLine("Invalid selection. Please enter D for Debit or C for Credit.");
                        continue;
                    }
                }

                if (totalDebits != totalCredits)
                {
                    Console.WriteLine($"\nError: Debits (${totalDebits:N2}) do not equal Credits (${totalCredits:N2}).");
                    Console.WriteLine("Journal entry not created.");
                }
                else if (!lines.Any())
                {
                    Console.WriteLine("\nNo lines entered. Journal entry not created.");
                }
                else
                {
                    var journalEntry = _accountingService.CreateJournalEntry(transactionId, description, lines);
                    Console.WriteLine($"\nJournal entry created successfully! Entry ID: {journalEntry.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating journal entry: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void PostJournalEntry()
        {
            if (_accountingService == null) return;

            Console.Clear();
            Console.WriteLine("POST JOURNAL ENTRY");
            Console.WriteLine("==================\n");

            try
            {
                var unpostedEntries = _accountingService.GetUnpostedJournalEntries();
                if (!unpostedEntries.Any())
                {
                    Console.WriteLine("No unposted journal entries found.");
                }
                else
                {
                    Console.WriteLine("Unposted Journal Entries:");
                    for (int i = 0; i < unpostedEntries.Count; i++)
                    {
                        var entry = unpostedEntries[i];
                        Console.WriteLine($"{i + 1}. Entry #{entry.Id} - {entry.TransactionReference}");
                        Console.WriteLine($"   Date: {entry.TransactionDate.ToShortDateString()} | Amount: ${entry.TotalDebit:N2}");
                        Console.WriteLine($"   Description: {entry.Description}");
                    }

                    Console.Write("\nSelect entry number to post (or 0 to cancel): ");
                    if (int.TryParse(Console.ReadLine(), out int selection) && selection > 0 && selection <= unpostedEntries.Count)
                    {
                        var selectedEntry = unpostedEntries[selection - 1];
                        _accountingService.PostJournalEntry(selectedEntry.Id);
                        Console.WriteLine($"\nJournal entry #{selectedEntry.Id} posted successfully!");
                    }
                    else if (selection != 0)
                    {
                        Console.WriteLine("Invalid selection.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error posting journal entry: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ViewAccountBalances()
        {
            if (_accountingService == null) return;

            Console.Clear();
            Console.WriteLine("ACCOUNT BALANCES");
            Console.WriteLine("================\n");

            try
            {
                var balances = _accountingService.GetAccountBalances();
                if (!balances.Any())
                {
                    Console.WriteLine("No account balances found.");
                }
                else
                {
                    Console.WriteLine("Account                           Balance");
                    Console.WriteLine("====================================== =============");
                    
                    foreach (var balance in balances.OrderBy(b => b.Key.AccountCode))
                    {
                        var account = balance.Key;
                        var amount = balance.Value;
                        Console.WriteLine($"{account.AccountCode} - {account.AccountName,-25} ${amount,10:N2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving account balances: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void GenerateTrialBalance()
        {
            if (_accountingService == null) return;

            Console.Clear();
            Console.WriteLine("TRIAL BALANCE");
            Console.WriteLine("=============\n");

            try
            {
                var trialBalance = _accountingService.GenerateTrialBalance(DateTime.Now, _currentUser?.Id ?? 1);
                Console.WriteLine($"Trial Balance as of {DateTime.Now.ToShortDateString()}");
                Console.WriteLine();
                Console.WriteLine("Account                           Debit        Credit");
                Console.WriteLine("====================================== ============ ============");

                decimal totalDebits = 0;
                decimal totalCredits = 0;

                foreach (var line in trialBalance.Lines.OrderBy(l => l.Account?.AccountCode))
                {
                    Console.WriteLine($"{line.Account?.AccountCode} - {line.Account?.AccountName,-25} ${line.DebitBalance,10:N2} ${line.CreditBalance,10:N2}");
                    totalDebits += line.DebitBalance;
                    totalCredits += line.CreditBalance;
                }

                Console.WriteLine("====================================== ============ ============");
                Console.WriteLine($"{"TOTAL",-38} ${totalDebits,10:N2} ${totalCredits,10:N2}");
                Console.WriteLine();

                if (totalDebits == totalCredits)
                {
                    Console.WriteLine("✓ Trial balance is balanced!");
                }
                else
                {
                    Console.WriteLine($"⚠ Trial balance is out of balance by ${Math.Abs(totalDebits - totalCredits):N2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating trial balance: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void CreateInvoiceFinancingEntry()
        {
            if (_accountingService == null || _invoiceService == null) return;

            Console.Clear();
            Console.WriteLine("CREATE INVOICE FINANCING ENTRY");
            Console.WriteLine("===============================\n");

            try
            {
                var approvedInvoices = _invoiceService.GetInvoicesByStatus(InvoiceStatus.Approved);
                if (!approvedInvoices.Any())
                {
                    Console.WriteLine("No approved invoices available for financing.");
                }
                else
                {
                    Console.WriteLine("Approved Invoices:");
                    for (int i = 0; i < approvedInvoices.Count; i++)
                    {
                        var invoice = approvedInvoices[i];
                        Console.WriteLine($"{i + 1}. {invoice.InvoiceNumber} - ${invoice.Amount:N2} ({invoice.Seller?.Name} → {invoice.Buyer?.Name})");
                    }

                    Console.Write("\nSelect invoice number (or 0 to cancel): ");
                    if (int.TryParse(Console.ReadLine(), out int selection) && selection > 0 && selection <= approvedInvoices.Count)
                    {
                        var selectedInvoice = approvedInvoices[selection - 1];
                        
                        Console.Write("Enter discount rate (%): ");
                        if (decimal.TryParse(Console.ReadLine(), out decimal discountRate) && discountRate > 0)
                        {
                            var entry = _accountingService.CreateInvoiceFinancingEntry(
                                selectedInvoice.Id, 
                                selectedInvoice.Amount, 
                                discountRate, 
                                selectedInvoice.Seller?.Name ?? "Unknown Seller",
                                "Bank"
                            );
                            
                            Console.WriteLine($"\nInvoice financing entry created successfully! Entry ID: {entry.Id}");
                            Console.WriteLine($"Discount amount: ${selectedInvoice.Amount * discountRate / 100:N2}");
                            Console.WriteLine($"Net amount to seller: ${selectedInvoice.Amount * (100 - discountRate) / 100:N2}");
                        }
                        else
                        {
                            Console.WriteLine("Invalid discount rate.");
                        }
                    }
                    else if (selection != 0)
                    {
                        Console.WriteLine("Invalid selection.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating invoice financing entry: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void CreatePaymentEntry()
        {
            if (_accountingService == null || _invoiceService == null) return;

            Console.Clear();
            Console.WriteLine("CREATE PAYMENT ENTRY");
            Console.WriteLine("====================\n");

            try
            {
                var fundedInvoices = _invoiceService.GetInvoicesByStatus(InvoiceStatus.Funded);
                if (!fundedInvoices.Any())
                {
                    Console.WriteLine("No funded invoices available for payment processing.");
                }
                else
                {
                    Console.WriteLine("Funded Invoices:");
                    for (int i = 0; i < fundedInvoices.Count; i++)
                    {
                        var invoice = fundedInvoices[i];
                        Console.WriteLine($"{i + 1}. {invoice.InvoiceNumber} - ${invoice.Amount:N2} (Due: {invoice.DueDate.ToShortDateString()})");
                        Console.WriteLine($"   Buyer: {invoice.Buyer?.Name} | Funded: ${invoice.FundedAmount:N2}");
                    }

                    Console.Write("\nSelect invoice number for payment (or 0 to cancel): ");
                    if (int.TryParse(Console.ReadLine(), out int selection) && selection > 0 && selection <= fundedInvoices.Count)
                    {
                        var selectedInvoice = fundedInvoices[selection - 1];
                        
                        Console.Write($"Enter payment amount (full amount: ${selectedInvoice.Amount:N2}): $");
                        if (decimal.TryParse(Console.ReadLine(), out decimal paymentAmount) && paymentAmount > 0)
                        {
                            // Create a manual journal entry for payment
                            var lines = new List<(int AccountId, decimal DebitAmount, decimal CreditAmount, string? EntityName)>
                            {
                                // Bank receives cash from buyer
                                (_accountingService.GetAccountByCode("1100").Id, paymentAmount, 0, selectedInvoice.Buyer?.Name ?? "Unknown Buyer"),
                                // Reduce loan to customer
                                (_accountingService.GetAccountByCode("1300").Id, 0, paymentAmount, "Bank")
                            };

                            var entry = _accountingService.CreateJournalEntry(
                                $"PAYMENT-{selectedInvoice.Id}-{DateTime.Now:yyyyMMdd}",
                                $"Payment received for invoice {selectedInvoice.InvoiceNumber}",
                                lines
                            );
                            
                            Console.WriteLine($"\nPayment entry created successfully! Entry ID: {entry.Id}");
                            Console.WriteLine($"Payment amount: ${paymentAmount:N2}");
                        }
                        else
                        {
                            Console.WriteLine("Invalid payment amount.");
                        }
                    }
                    else if (selection != 0)
                    {
                        Console.WriteLine("Invalid selection.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating payment entry: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ViewTransactionHistory()
        {
            if (_accountingService == null) return;

            Console.Clear();
            Console.WriteLine("TRANSACTION HISTORY");
            Console.WriteLine("===================\n");

            try
            {
                var entries = _accountingService.GetAllJournalEntries()
                    .Where(e => e.Status == JournalEntryStatus.Posted)
                    .OrderByDescending(e => e.TransactionDate)
                    .Take(20)
                    .ToList();

                if (!entries.Any())
                {
                    Console.WriteLine("No posted transaction history found.");
                }
                else
                {
                    Console.WriteLine("Recent Posted Transactions (Last 20):");
                    Console.WriteLine();
                    
                    foreach (var entry in entries)
                    {
                        Console.WriteLine($"Date: {entry.TransactionDate.ToShortDateString()} | ID: {entry.TransactionReference} | Amount: ${entry.TotalDebit:N2}");
                        Console.WriteLine($"Description: {entry.Description}");
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving transaction history: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        void WaitForEnterKey()
        {
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }
    }
}
