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

namespace ClientPortal
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

            var clientApp = new ClientApplication(services);
            clientApp.Run();
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

    class ClientApplication
    {
        private readonly IServiceProvider _services;
        private User? _currentUser;
        private Organization? _currentOrganization;

        private AuthService? _authService;
        private MyLimitService? _limitService;
        private InvoiceService? _invoiceService;
        private TransactionService? _transactionService;
        private UserService? _userService;

        public ClientApplication(IServiceProvider services)
        {
            _services = services;
        }

        public void Run()
        {
            Console.Clear();
            Console.WriteLine("===================================================");
            Console.WriteLine("   SUPPLY CHAIN FINANCE - CLIENT PORTAL");
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

                // Login
                if (Login())
                {
                    ShowMainMenu();
                }
            }

            Console.WriteLine("\nThank you for using the Supply Chain Finance system. Goodbye!");
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
                    if (_currentUser.Organization.IsBank)
                    {
                        Console.WriteLine("\nError: This portal is for clients only. Bank users must use the Bank Portal.");
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
                if (_currentOrganization.IsSeller)
                {
                    Console.WriteLine("1. Upload Invoice (as Seller)");
                }
                else if (_currentOrganization.IsBuyer)
                {
                    Console.WriteLine("1. Upload Invoice (as Buyer)");
                }
                Console.WriteLine("2. View My Invoices");
                Console.WriteLine("3. Check Credit Limits");
                Console.WriteLine("4. View Account Statement");
                Console.WriteLine("5. View Notifications");
                if (_currentOrganization.IsBuyer)
                {
                    Console.WriteLine("6. Make Payment");
                }
                Console.WriteLine("0. Logout");

                Console.Write("\nSelect an option: ");
                string? input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        if (_currentOrganization.IsSeller)
                            UploadSellerInvoice();
                        else if (_currentOrganization.IsBuyer)
                            UploadBuyerInvoice();
                        else
                            Console.WriteLine("\nYour organization type cannot upload invoices.");
                        break;
                    case "2":
                        ViewInvoices();
                        break;
                    case "3":
                        CheckCreditLimits();
                        break;
                    case "4":
                        ViewAccountStatement();
                        break;
                    case "5":
                        ViewNotifications();
                        break;
                    case "6":
                        if (_currentOrganization.IsBuyer)
                            MakePayment();
                        else
                            Console.WriteLine("\nInvalid option. Please try again.");
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

        private void UploadSellerInvoice()
        {
            if (_invoiceService == null || _userService == null || _currentOrganization == null)
                return;

            Console.Clear();
            Console.WriteLine("UPLOAD NEW INVOICE");
            Console.WriteLine("=================\n");

            // Get buyers
            var buyers = _userService.GetOrganizations().Where(o => o.IsBuyer).ToList();
            if (!buyers.Any())
            {
                Console.WriteLine("No buyers available in the system.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            // Display buyers
            Console.WriteLine("Available Buyers:");
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

            // Generate invoice number with format INV-YYYY-XXX
            // Get current count of invoices to determine next number
            var currentYear = DateTime.Now.Year;
            var existingInvoices = _invoiceService.GetInvoices(_currentOrganization.Id, false);
            var existingInvoiceCount = existingInvoices
                .Count(i => i.InvoiceNumber.StartsWith($"INV-{currentYear}-"));
            
            // Generate next invoice number
            var nextInvoiceNumber = existingInvoiceCount + 1;
            var invoiceNumber = $"INV-{currentYear}-{nextInvoiceNumber:D3}"; // e.g., INV-2025-003
            
            Console.WriteLine($"\nGenerated Invoice Number: {invoiceNumber}");
            
            Console.Write("Invoice Amount: $");
            if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
            {
                Console.WriteLine("Invalid amount.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.Write("Invoice Issue Date (MM/DD/YYYY): ");
            if (!DateTime.TryParse(Console.ReadLine(), out DateTime issueDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.Write("Invoice Due Date (MM/DD/YYYY): ");
            if (!DateTime.TryParse(Console.ReadLine(), out DateTime dueDate) || dueDate <= issueDate)
            {
                Console.WriteLine("Invalid date or due date is before issue date.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.Write("Description: ");
            string description = Console.ReadLine() ?? "";

            // Create invoice
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                IssueDate = issueDate,
                DueDate = dueDate,
                SellerId = _currentOrganization.Id,
                BuyerId = selectedBuyer.Id,
                Amount = amount,
                Description = description
            };

            // Upload invoice
            var result = _invoiceService.UploadInvoice(invoice);
            Console.WriteLine($"\n{result.Message}");
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void UploadBuyerInvoice()
        {
            if (_invoiceService == null || _userService == null || _currentOrganization == null)
                return;

            Console.Clear();
            Console.WriteLine("UPLOAD NEW INVOICE (AS BUYER)");
            Console.WriteLine("============================\n");

            // Get sellers
            var sellers = _userService.GetOrganizations().Where(o => o.IsSeller).ToList();
            if (!sellers.Any())
            {
                Console.WriteLine("No sellers available in the system.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            // Display sellers
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

            // Generate invoice number with format INV-YYYY-BXX (B for Buyer-initiated)
            var currentYear = DateTime.Now.Year;
            var existingInvoices = _invoiceService.GetInvoices(_currentOrganization.Id, true);
            var existingInvoiceCount = existingInvoices
                .Count(i => i.InvoiceNumber.StartsWith($"INV-{currentYear}-B"));
            
            // Generate next invoice number
            var nextInvoiceNumber = existingInvoiceCount + 1;
            var invoiceNumber = $"INV-{currentYear}-B{nextInvoiceNumber:D2}"; // e.g., INV-2025-B01
            
            Console.WriteLine($"\nGenerated Invoice Number: {invoiceNumber}");
            
            Console.Write("Invoice Amount: $");
            if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
            {
                Console.WriteLine("Invalid amount.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.Write("Invoice Issue Date (MM/DD/YYYY): ");
            if (!DateTime.TryParse(Console.ReadLine(), out DateTime issueDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.Write("Invoice Due Date (MM/DD/YYYY): ");
            if (!DateTime.TryParse(Console.ReadLine(), out DateTime dueDate) || dueDate <= issueDate)
            {
                Console.WriteLine("Invalid date or due date is before issue date.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.Write("Description: ");
            string description = Console.ReadLine() ?? "";

            // Create invoice with special BuyerUploaded status
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                IssueDate = issueDate,
                DueDate = dueDate,
                SellerId = selectedSeller.Id,
                BuyerId = _currentOrganization.Id,
                Amount = amount,
                Description = description,
                Status = InvoiceStatus.BuyerUploaded // New status
            };

            // Upload invoice
            var result = _invoiceService.UploadBuyerInvoice(invoice);
            Console.WriteLine($"\n{result.Message}");
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ViewInvoices()
        {
            if (_invoiceService == null || _currentOrganization == null)
                return;

            Console.Clear();
            Console.WriteLine("MY INVOICES");
            Console.WriteLine("==========\n");

            bool isBuyer = _currentOrganization.IsBuyer;
            var invoices = _invoiceService.GetInvoices(_currentOrganization.Id, isBuyer);

            if (!invoices.Any())
            {
                Console.WriteLine("No invoices found.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            // Display invoices
            Console.WriteLine($"{"ID",-5} {"Invoice #",-15} {"Date",-12} {"Due Date",-12} {"Amount",-15} {"Status",-15} {"Counterparty",-20}");
            Console.WriteLine(new string('-', 94));

            foreach (var invoice in invoices)
            {
                string counterparty = isBuyer ? invoice.Seller?.Name ?? "Unknown" : invoice.Buyer?.Name ?? "Unknown";
                string statusString = invoice.Status.ToString();
                
                // Add special indication for funded invoices
                if (invoice.Status == InvoiceStatus.Funded && invoice.FundedAmount.HasValue)
                {
                    statusString += $" (${invoice.FundedAmount:N2})";
                }
                
                Console.WriteLine($"{invoice.Id,-5} {invoice.InvoiceNumber,-15} {invoice.IssueDate.ToShortDateString(),-12} {invoice.DueDate.ToShortDateString(),-12} ${invoice.Amount,-13:N2} {statusString,-25} {counterparty,-20}");
            }

            // Invoice details option
            Console.Write("\nEnter invoice ID for details (or 0 to return): ");
            if (int.TryParse(Console.ReadLine(), out int invoiceId) && invoiceId > 0)
            {
                ViewInvoiceDetails(invoiceId);
            }
        }

        private void ViewInvoiceDetails(int invoiceId)
        {
            if (_invoiceService == null || _currentOrganization == null)
                return;

            try
            {
                var invoice = _invoiceService.GetInvoiceById(invoiceId);

                Console.Clear();
                Console.WriteLine("INVOICE DETAILS");
                Console.WriteLine("==============\n");

                Console.WriteLine($"Invoice Number: {invoice.InvoiceNumber}");
                Console.WriteLine($"Status: {invoice.Status}");
                Console.WriteLine($"Seller: {invoice.Seller?.Name}");
                Console.WriteLine($"Buyer: {invoice.Buyer?.Name}");
                Console.WriteLine($"Issue Date: {invoice.IssueDate.ToShortDateString()}");
                Console.WriteLine($"Due Date: {invoice.DueDate.ToShortDateString()}");
                Console.WriteLine($"Amount: ${invoice.Amount:N2}");
                
                if (invoice.Status == InvoiceStatus.Funded || invoice.Status == InvoiceStatus.PartiallyPaid || invoice.Status == InvoiceStatus.FullyPaid)
                {
                    Console.WriteLine($"Funding Date: {invoice.FundingDate?.ToShortDateString()}");
                    Console.WriteLine($"Funded Amount: ${invoice.FundedAmount:N2}");
                    Console.WriteLine($"Discount Rate: {invoice.DiscountRate:P2}");
                }
                
                if (invoice.Status == InvoiceStatus.PartiallyPaid || invoice.Status == InvoiceStatus.FullyPaid)
                {
                    Console.WriteLine($"Paid Amount: ${invoice.PaidAmount:N2}");
                    Console.WriteLine($"Payment Date: {invoice.PaymentDate?.ToShortDateString()}");
                }

                Console.WriteLine($"Description: {invoice.Description}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void CheckCreditLimits()
        {
            if (_limitService == null || _currentOrganization == null)
                return;

            Console.Clear();
            Console.WriteLine("CREDIT LIMITS");
            Console.WriteLine("============\n");
            
            Console.WriteLine("Select report style:");
            Console.WriteLine("1. Tree-like structure (detailed view)");
            Console.WriteLine("2. Tabular format (summary view)");
            Console.Write("\nSelect an option: ");
            
            string? styleChoice = Console.ReadLine();
            
            Console.Clear();
            Console.WriteLine("CREDIT LIMITS");
            Console.WriteLine("============\n");

            try
            {
                string report;
                if (styleChoice == "1")
                {
                    // Use the new tree method that respects buyer/seller visibility rules
                    report = _limitService.GenerateLimitTreeReportWithVisibilityRules(_currentOrganization.Id);
                }
                else
                {
                    // Use the tabular method that respects buyer/seller visibility rules
                    report = _limitService.GenerateLimitReportWithVisibilityRules(_currentOrganization.Id);
                }
                Console.WriteLine(report);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ViewAccountStatement()
        {
            if (_transactionService == null || _currentOrganization == null)
                return;

            Console.Clear();
            Console.WriteLine("ACCOUNT STATEMENT");
            Console.WriteLine("================\n");

            Console.Write("Start Date (MM/DD/YYYY, default: first day of current month): ");
            string? startDateInput = Console.ReadLine();
            DateTime startDate;
            
            if (string.IsNullOrWhiteSpace(startDateInput))
            {
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }
            else if (!DateTime.TryParse(startDateInput, out startDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            Console.Write("End Date (MM/DD/YYYY, default: today): ");
            string? endDateInput = Console.ReadLine();
            DateTime endDate;
            
            if (string.IsNullOrWhiteSpace(endDateInput))
            {
                endDate = DateTime.Now;
            }
            else if (!DateTime.TryParse(endDateInput, out endDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            if (startDate > endDate)
            {
                Console.WriteLine("Start date must be before end date.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            try
            {
                var statement = _transactionService.GenerateAccountStatement(_currentOrganization.Id, startDate, endDate);
                Console.WriteLine();
                Console.WriteLine(_transactionService.GenerateStatementReport(statement));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void ViewNotifications()
        {
            if (_authService == null || _currentUser == null)
                return;

            Console.Clear();
            Console.WriteLine("NOTIFICATIONS");
            Console.WriteLine("============\n");

            var notifications = _authService.GetUserNotifications(_currentUser.Id);

            if (!notifications.Any())
            {
                Console.WriteLine("No notifications found.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            // Display notifications
            for (int i = 0; i < notifications.Count; i++)
            {
                var n = notifications[i];
                Console.Write($"{i + 1}. ");
                if (n.IsRead)
                    Console.Write("[Read] ");
                else
                    Console.Write("[Unread] ");
                
                // Check if notification requires action
                if (n.RequiresAction && !n.ActionTaken)
                    Console.Write("[ACTION REQUIRED] ");
                
                Console.WriteLine($"{n.Type}: {n.Title}");
                Console.WriteLine($"   {n.CreatedDate.ToShortDateString()} - {n.Message}");
                Console.WriteLine();

                // Mark regular notifications as read, but not action notifications
                if (!n.IsRead && !n.RequiresAction)
                {
                    _authService.MarkNotificationAsRead(n.Id);
                }
            }
            
            // Ask if user wants to take action on any notification
            Console.Write("\nEnter notification number to act on (or 0 to return): ");
            if (int.TryParse(Console.ReadLine(), out int selection) && selection > 0 && selection <= notifications.Count)
            {
                var selectedNotification = notifications[selection - 1];
                
                // Process notification action based on type
                if (selectedNotification.RequiresAction && !selectedNotification.ActionTaken)
                {
                    if (selectedNotification.Type == "BuyerApprovalRequest" && _currentOrganization?.IsBuyer == true)
                    {
                        HandleBuyerApprovalRequest(selectedNotification);
                    }
                    else if (selectedNotification.Type == "SellerAcceptanceRequest" && _currentOrganization?.IsSeller == true)
                    {
                        HandleSellerAcceptanceRequest(selectedNotification);
                    }
                }
                else
                {
                    Console.WriteLine("\nThis notification doesn't require any action.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            else if (selection != 0)
            {
                Console.WriteLine("\nInvalid selection.");
            }
            Console.ReadKey();
        }

        private void MakePayment()
        {
            if (_invoiceService == null || _currentOrganization == null || !_currentOrganization.IsBuyer)
                return;

            Console.Clear();
            Console.WriteLine("MAKE PAYMENT");
            Console.WriteLine("===========\n");

            // Get invoices that can be paid
            var invoices = _invoiceService.GetInvoices(_currentOrganization.Id, true)
                .Where(i => i.Status == InvoiceStatus.Funded || i.Status == InvoiceStatus.PartiallyPaid)
                .ToList();

            if (!invoices.Any())
            {
                Console.WriteLine("No invoices available for payment.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            // Display invoices
            Console.WriteLine($"{"ID",-5} {"Invoice #",-15} {"Due Date",-12} {"Total Amount",-15} {"Amount Due",-15} {"Seller",-20}");
            Console.WriteLine(new string('-', 82));

            foreach (var invoice in invoices)
            {
                decimal amountDue = invoice.Amount - (invoice.PaidAmount ?? 0);
                Console.WriteLine($"{invoice.Id,-5} {invoice.InvoiceNumber,-15} {invoice.DueDate.ToShortDateString(),-12} ${invoice.Amount,-13:N2} ${amountDue,-13:N2} {invoice.Seller?.Name,-20}");
            }

            // Select invoice
            Console.Write("\nSelect invoice ID to pay: ");
            if (!int.TryParse(Console.ReadLine(), out int invoiceId))
            {
                Console.WriteLine("Invalid selection.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            var selectedInvoice = invoices.FirstOrDefault(i => i.Id == invoiceId);
            if (selectedInvoice == null)
            {
                Console.WriteLine("Invalid invoice ID.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            decimal amountDueOnSelected = selectedInvoice.Amount - (selectedInvoice.PaidAmount ?? 0);
            Console.WriteLine($"\nInvoice: {selectedInvoice.InvoiceNumber}");
            Console.WriteLine($"Amount Due: ${amountDueOnSelected:N2}");

            Console.Write("\nEnter payment amount: $");
            if (!decimal.TryParse(Console.ReadLine(), out decimal paymentAmount) || 
                paymentAmount <= 0 || 
                paymentAmount > amountDueOnSelected)
            {
                Console.WriteLine("Invalid payment amount.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }

            // Process payment
            var result = _invoiceService.ProcessPayment(selectedInvoice.Id, paymentAmount);
            Console.WriteLine($"\n{result.Message}");
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }

        private void HandleBuyerApprovalRequest(Notification notification)
        {
            if (_invoiceService == null || notification.InvoiceId == null)
                return;
                
            Console.Clear();
            Console.WriteLine("INVOICE LIABILITY TRANSFER APPROVAL");
            Console.WriteLine("==================================\n");
            
            // Fetch the invoice
            Invoice? invoice = null;
            try
            {
                invoice = _invoiceService.GetInvoice(notification.InvoiceId.Value);
            }
            catch (Exception)
            {
                Console.WriteLine("Invoice not found or no longer available.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            if (invoice == null || _currentUser == null || _currentOrganization == null)
                return;
            
            Console.WriteLine("The bank is requesting your approval to transfer payment liability for the following invoice:\n");
            Console.WriteLine($"Invoice Number: {invoice.InvoiceNumber}");
            Console.WriteLine($"Amount: ${invoice.Amount:N2}");
            Console.WriteLine($"Issue Date: {invoice.IssueDate.ToShortDateString()}");
            Console.WriteLine($"Due Date: {invoice.DueDate.ToShortDateString()}");
            Console.WriteLine($"Seller: {invoice.Seller?.Name}");
            Console.WriteLine($"Description: {invoice.Description}\n");
            
            Console.WriteLine("By approving, you agree that:");
            Console.WriteLine("1. The bank will pay the seller early");
            Console.WriteLine("2. You will pay the full invoice amount to the bank on the due date");
            Console.WriteLine("3. This transaction will be recorded in your books\n");
            
            Console.Write("Do you approve this invoice for financing? (Y/N): ");
            string response = Console.ReadLine() ?? "";
            
            if (response.ToUpper() == "Y")
            {
                var result = _invoiceService.BuyerApproveInvoice(invoice.Id, _currentUser.Id);
                Console.WriteLine($"\n{result.Message}");
            }
            else
            {
                Console.Write("\nPlease provide a reason for rejection: ");
                string reason = Console.ReadLine() ?? "No reason provided";
                
                var result = _invoiceService.BuyerRejectInvoice(invoice.Id, _currentUser.Id, reason);
                Console.WriteLine($"\n{result.Message}");
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
        
        private void HandleSellerAcceptanceRequest(Notification notification)
        {
            if (_invoiceService == null || notification.InvoiceId == null)
                return;
                
            Console.Clear();
            Console.WriteLine("EARLY PAYMENT OFFER");
            Console.WriteLine("==================\n");
            
            // Fetch the invoice
            Invoice? invoice = null;
            try
            {
                invoice = _invoiceService.GetInvoice(notification.InvoiceId.Value);
            }
            catch (Exception)
            {
                Console.WriteLine("Invoice not found or no longer available.");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                return;
            }
            
            if (invoice == null || _currentUser == null || _currentOrganization == null || !invoice.DiscountRate.HasValue)
                return;
            
            decimal discountAmount = invoice.Amount * (invoice.DiscountRate.Value / 100m);
            decimal fundedAmount = invoice.Amount - discountAmount;
            
            Console.WriteLine("The bank is offering early payment for the following invoice:\n");
            Console.WriteLine($"Invoice Number: {invoice.InvoiceNumber}");
            Console.WriteLine($"Original Amount: ${invoice.Amount:N2}");
            Console.WriteLine($"Original Due Date: {invoice.DueDate.ToShortDateString()}");
            Console.WriteLine($"Buyer: {invoice.Buyer?.Name}\n");
            
            Console.WriteLine("Early Payment Offer:");
            Console.WriteLine($"Discount Rate: {invoice.DiscountRate.Value}%");
            Console.WriteLine($"Discount Amount: ${discountAmount:N2}");
            Console.WriteLine($"Amount to be Received Immediately: ${fundedAmount:N2}");
            Console.WriteLine($"Effective Discount: ${invoice.Amount - fundedAmount:N2}\n");
            
            Console.Write("Do you accept this early payment offer? (Y/N): ");
            string response = Console.ReadLine() ?? "";
            
            if (response.ToUpper() == "Y")
            {
                var result = _invoiceService.SellerAcceptOffer(invoice.Id, _currentUser.Id);
                Console.WriteLine($"\n{result.Message}");
            }
            else
            {
                Console.Write("\nPlease provide a reason for rejection: ");
                string reason = Console.ReadLine() ?? "No reason provided";
                
                var result = _invoiceService.SellerRejectOffer(invoice.Id, _currentUser.Id, reason);
                Console.WriteLine($"\n{result.Message}");
            }
            
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
    }
}
