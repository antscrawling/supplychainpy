using System;
using Core.Models;
using Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Core.Services
{
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        
        public static ServiceResult Successful(string message = "Operation completed successfully")
        {
            return new ServiceResult { Success = true, Message = message };
        }
        
        public static ServiceResult Failed(string message)
        {
            return new ServiceResult { Success = false, Message = message };
        }
    }
    
    public class InvoiceService
    {
        private readonly SupplyChainDbContext _context;
        private readonly MyLimitService _limitService;
        private readonly TransactionService _transactionService;
        private readonly AccountingService _accountingService;

        public InvoiceService(
            SupplyChainDbContext context, 
            MyLimitService limitService,
            TransactionService transactionService)
        {
            _context = context;
            _limitService = limitService;
            _transactionService = transactionService;
            _accountingService = new AccountingService(context);
        }

        public List<Invoice> GetInvoices(int organizationId, bool isBuyer)
        {
            // This version is compatible with the existing database schema
            // (without the CounterpartyId column)
            var query = _context.Invoices
                .Include(i => i.Buyer)
                .Include(i => i.Seller)
                .Where(i => isBuyer ? i.BuyerId == organizationId : i.SellerId == organizationId);
            
            // Use AsEnumerable to finish the query with the SQL parts
            // before trying to access any new properties
            return query
                .AsEnumerable()
                .OrderByDescending(i => i.IssueDate)
                .ToList();
        }

        public Invoice GetInvoiceById(int id)
        {
            return _context.Invoices
                .Include(i => i.Buyer)
                .Include(i => i.Seller)
                .FirstOrDefault(i => i.Id == id)
                ?? throw new Exception($"Invoice with ID {id} not found");
        }

        public ServiceResult UploadInvoice(Invoice invoice, Counterparty? counterparty = null)
        {
            try
            {
                // Validate seller (must be a real customer)
                var seller = _context.Organizations.Find(invoice.SellerId);
                if (seller == null || !seller.IsSeller)
                    return ServiceResult.Failed("Seller must be a real customer.");

                // If buyer is not a customer, use/create counterparty
                Organization? buyer = null;
                if (invoice.BuyerId.HasValue)
                {
                    buyer = _context.Organizations.Find(invoice.BuyerId);
                }
                if (buyer == null && counterparty != null)
                {
                    // Find or create counterparty
                    var dbCounterparty = _context.Counterparties.FirstOrDefault(c => c.Name == counterparty.Name && c.TaxId == counterparty.TaxId);
                    if (dbCounterparty == null)
                    {
                        _context.Counterparties.Add(counterparty);
                        _context.SaveChanges();
                        dbCounterparty = counterparty;
                    }
                    invoice.CounterpartyId = dbCounterparty.Id;
                }

                invoice.Status = InvoiceStatus.Uploaded;
                _context.Invoices.Add(invoice);
                _context.SaveChanges();

                // If buyer is a counterparty, create dummy facility for risk tracking
                if (buyer == null && invoice.CounterpartyId.HasValue)
                {
                    var sellerCreditLimit = _context.CreditLimits.Include(cl => cl.Facilities)
                        .FirstOrDefault(cl => cl.OrganizationId == seller.Id);
                    if (sellerCreditLimit != null)
                    {
                        var sellerFacility = sellerCreditLimit.Facilities.FirstOrDefault(f => f.Type == FacilityType.InvoiceFinancing && f.RelatedPartyId == null);
                        if (sellerFacility != null)
                        {
                            var dummyFacility = sellerCreditLimit.Facilities.FirstOrDefault(f => f.Type == FacilityType.InvoiceFinancing && f.RelatedPartyId == invoice.CounterpartyId);
                            if (dummyFacility == null)
                            {
                                dummyFacility = new Facility
                                {
                                    CreditLimitInfoId = sellerCreditLimit.Id,
                                    Type = FacilityType.InvoiceFinancing,
                                    TotalLimit = invoice.Amount,
                                    AllocatedLimit = invoice.Amount,
                                    RelatedPartyId = invoice.CounterpartyId,
                                    ReviewEndDate = sellerFacility.ReviewEndDate
                                };
                                _context.Facilities.Add(dummyFacility);
                                _context.SaveChanges();
                            }
                        }
                    }
                }

                // Record the transaction
                _transactionService.RecordTransaction(new Transaction
                {
                    Type = TransactionType.InvoiceUpload,
                    FacilityType = FacilityType.InvoiceFinancing,
                    OrganizationId = invoice.SellerId ?? 0,
                    InvoiceId = invoice.Id,
                    Description = $"Invoice {invoice.InvoiceNumber} uploaded",
                    Amount = invoice.Amount,
                    TransactionDate = DateTime.Now,
                    MaturityDate = invoice.DueDate
                });

                return ServiceResult.Successful($"Invoice {invoice.InvoiceNumber} uploaded successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Failed to upload invoice: {ex.Message}");
            }
        }

        public ServiceResult UploadBuyerInvoice(Invoice invoice, Counterparty? counterparty = null)
        {
            try
            {
                // Validate buyer (must be a real customer)
                var buyer = _context.Organizations.Find(invoice.BuyerId);
                if (buyer == null || !buyer.IsBuyer)
                    return ServiceResult.Failed("Buyer must be a real customer.");

                // If seller is not a customer, use/create counterparty
                Organization? seller = null;
                if (invoice.SellerId.HasValue)
                {
                    seller = _context.Organizations.Find(invoice.SellerId);
                }
                if (seller == null && counterparty != null)
                {
                    // Find or create counterparty
                    var dbCounterparty = _context.Counterparties.FirstOrDefault(c => c.Name == counterparty.Name && c.TaxId == counterparty.TaxId);
                    if (dbCounterparty == null)
                    {
                        _context.Counterparties.Add(counterparty);
                        _context.SaveChanges();
                        dbCounterparty = counterparty;
                    }
                    invoice.CounterpartyId = dbCounterparty.Id;
                }

                invoice.Status = InvoiceStatus.BuyerUploaded;
                _context.Invoices.Add(invoice);
                _context.SaveChanges();

                // No dummy facility is created for the seller/counterparty

                // Create notification for seller to inform about the invoice
                var sellerUsers = _context.Users.Where(u => u.OrganizationId == invoice.SellerId).ToList();
                foreach (var user in sellerUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        Title = "New Invoice Received",
                        Message = $"Buyer {buyer.Name} has uploaded invoice {invoice.InvoiceNumber} for ${invoice.Amount:N2} due on {invoice.DueDate.ToShortDateString()}",
                        Type = "Info",
                        InvoiceId = invoice.Id,
                        RequiresAction = false
                    });
                }
                _context.SaveChanges();

                // Create notification for bank to inform about the new buyer-uploaded invoice
                var bankUsers = _context.Users
                    .Where(u => u.Organization != null && u.Organization.IsBank &&
                               (u.Role == UserRole.BankAdmin || u.Role == UserRole.BankUser))
                    .ToList();

                foreach (var user in bankUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        Title = "New Buyer-Uploaded Invoice",
                        Message = $"Buyer {buyer?.Name} has uploaded invoice {invoice.InvoiceNumber} for seller {seller?.Name} for ${invoice.Amount:N2}. You can now offer early payment to the seller.",
                        Type = "Info",
                        InvoiceId = invoice.Id,
                        RequiresAction = false
                    });
                }
                _context.SaveChanges();

                // Record transaction
                _transactionService.RecordTransaction(new Transaction
                {
                    Type = TransactionType.InvoiceUpload,
                    FacilityType = FacilityType.InvoiceFinancing,
                    OrganizationId = invoice.BuyerId ?? 0,
                    InvoiceId = invoice.Id,
                    Description = $"Invoice {invoice.InvoiceNumber} uploaded by buyer",
                    Amount = invoice.Amount,
                    TransactionDate = DateTime.Now,
                    MaturityDate = invoice.DueDate
                });

                return ServiceResult.Successful($"Invoice {invoice.InvoiceNumber} uploaded successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Failed to upload invoice: {ex.Message}");
            }
        }

        public List<Invoice> GetInvoicesByStatus(InvoiceStatus status)
        {
            return _context.Invoices
                .Include(i => i.Buyer)
                .Include(i => i.Seller)
                .Where(i => i.Status == status)
                .OrderByDescending(i => i.IssueDate)
                .ToList();
        }

        public ServiceResult ValidateInvoice(int invoiceId, int bankUserId)
        {
            try
            {
                var invoice = _context.Invoices
                    .FirstOrDefault(i => i.Id == invoiceId) 
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.Uploaded)
                    return ServiceResult.Failed($"Cannot validate invoice with status {invoice.Status}");

                invoice.Status = InvoiceStatus.Validated;
                _context.SaveChanges();

                // Create notification for seller
                var seller = _context.Organizations.Find(invoice.SellerId);
                if (seller != null)
                {
                    var sellerUsers = _context.Users.Where(u => u.OrganizationId == seller.Id).ToList();
                    foreach (var user in sellerUsers)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = user.Id,
                            Title = "Invoice Validated",
                            Message = $"Invoice {invoice.InvoiceNumber} has been validated and is pending approval.",
                            Type = "Info"
                        });
                    }
                    _context.SaveChanges();
                }

                return ServiceResult.Successful("Invoice validated successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error validating invoice: {ex.Message}");
            }
        }

        public ServiceResult ApproveInvoice(int invoiceId, int bankUserId)
        {
            try
            {
                var invoice = _context.Invoices
                    .FirstOrDefault(i => i.Id == invoiceId) 
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.Validated)
                    return ServiceResult.Failed($"Cannot approve invoice with status {invoice.Status}. Invoice must be validated first.");

                invoice.Status = InvoiceStatus.Approved;
                _context.SaveChanges();

                // Create notification for seller
                var seller = _context.Organizations.Find(invoice.SellerId);
                if (seller != null)
                {
                    var sellerUsers = _context.Users.Where(u => u.OrganizationId == seller.Id).ToList();
                    foreach (var user in sellerUsers)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = user.Id,
                            Title = "Invoice Approved",
                            Message = $"Invoice {invoice.InvoiceNumber} has been approved and is ready for funding.",
                            Type = "Info"
                        });
                    }
                    _context.SaveChanges();
                }

                return ServiceResult.Successful("Invoice approved successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error approving invoice: {ex.Message}");
            }
        }

        public ServiceResult FundInvoice(int invoiceId, int bankUserId, Core.Models.FundingDetails fundingDetails)
        {
            try
            {
                var invoice = _context.Invoices
                    .Include(i => i.Seller)
                    .Include(i => i.Buyer)
                    .FirstOrDefault(i => i.Id == invoiceId)
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                // Check if invoice is in the right status for funding
                bool canFund = invoice.Status == InvoiceStatus.Approved;
                
                if (invoice.Status == InvoiceStatus.SellerAcceptancePending && invoice.SellerAccepted)
                    canFund = true;
                
                if (invoice.Status == InvoiceStatus.BuyerApprovalPending && invoice.BuyerApproved)
                    canFund = true;
                
                if (!canFund)
                    return ServiceResult.Failed($"Cannot fund invoice with status {invoice.Status}. Invoice must be approved or have necessary approvals.");

                if (invoice.Seller == null || invoice.Buyer == null)
                    return ServiceResult.Failed("Invoice has missing seller or buyer information");

                // Calculate funding amount based on discount rate
                decimal discountAmount = invoice.Amount * (fundingDetails.FinalDiscountRate / 100m);
                decimal fundedAmount = invoice.Amount - discountAmount;

                // Update invoice with funding details
                invoice.Status = InvoiceStatus.Funded;
                invoice.FundingDate = fundingDetails.FundingDate;
                invoice.FundedAmount = fundedAmount;
                invoice.DiscountRate = fundingDetails.FinalDiscountRate;
                
                _context.SaveChanges();

                // Update facility utilization based on who is the bank's customer
                if (invoice.BuyerId.HasValue && IsBuyerBankCustomer(invoice.BuyerId.Value))
                {
                    _limitService.UpdateFacilityUtilization(
                        invoice.BuyerId.Value,
                        FacilityType.InvoiceFinancing,
                        invoice.Amount);
                }
                else if (invoice.SellerId.HasValue)
                {
                    _limitService.UpdateFacilityUtilization(
                        invoice.SellerId.Value,
                        FacilityType.InvoiceFinancing,
                        invoice.Amount);
                }

                // Record the funding transaction for the seller (discounted amount)
                if (invoice.SellerId.HasValue)
                {
                    _transactionService.RecordTransaction(new Transaction
                    {
                        Type = TransactionType.InvoiceFunding,
                        FacilityType = FacilityType.InvoiceFinancing,
                        OrganizationId = invoice.SellerId.Value,
                        InvoiceId = invoice.Id,
                    Description = $"Funding received for invoice {invoice.InvoiceNumber}",
                    Amount = fundedAmount,
                    InterestOrDiscountRate = fundingDetails.FinalDiscountRate,
                    TransactionDate = fundingDetails.FundingDate,
                    MaturityDate = invoice.DueDate
                    });
                }

                // Ensure the invoice is no longer in the 'for funding' list
                _context.SaveChanges();

                return ServiceResult.Successful($"Invoice {invoice.InvoiceNumber} funded successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error funding invoice: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates notifications for the buyer about invoice maturity dates and payment obligations
        /// </summary>
        private void NotifyBuyerOfMaturityDate(Invoice invoice)
        {
            if (invoice.BuyerId == null || invoice.Buyer == null)
                return;
                
            var buyerUsers = _context.Users.Where(u => u.OrganizationId == invoice.BuyerId).ToList();
            if (!buyerUsers.Any())
                return;
                
            string relationship = DetermineCustomerRelationship(invoice);
            string message;
                
            // Format message based on buyer-seller relationship with the bank
            if (relationship == "BuyerIsCustomer")
            {
                // When buyer is the bank's customer, they need to pay the full amount
                message = $"Invoice {invoice.InvoiceNumber} has been funded. As per your financing arrangement, " +
                          $"payment of ${invoice.Amount:N2} is due to the bank by {invoice.DueDate.ToShortDateString()}.";
            }
            else
            {
                // When seller is the bank's customer, buyer still needs to pay, but to the bank instead of seller
                message = $"Invoice {invoice.InvoiceNumber} has been financed through our bank. " +
                          $"Please make payment of ${invoice.Amount:N2} to our bank by {invoice.DueDate.ToShortDateString()}, " +
                          $"not directly to the seller {invoice.Seller?.Name}.";
            }
                
            // Create and save the notification
            foreach (var user in buyerUsers)
            {
                var notification = new Notification
                {
                    UserId = user.Id,
                    Title = "Invoice Payment Due - Important",
                    Message = message,
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    Type = "Warning"
                };
                
                _context.Notifications.Add(notification);
            }
        }
        
        /// <summary>
        /// Determines whether the invoice should be funded using buyer's facility or seller's facility
        /// </summary>
        private bool ShouldUseBuyerFacility(Invoice invoice)
        {
            if (invoice.BuyerId == null || invoice.SellerId == null)
                return false;
                
            // Check if buyer is a bank customer
            var buyerOrg = _context.Organizations
                .FirstOrDefault(o => o.Id == invoice.BuyerId);
                
            // Check if seller is a bank customer
            var sellerOrg = _context.Organizations
                .FirstOrDefault(o => o.Id == invoice.SellerId);
                
            if (buyerOrg == null || sellerOrg == null)
                return false;
                
            // If buyer is a bank customer and seller is not, use buyer's facility
            bool buyerHasLimit = _context.CreditLimits
                .Any(cl => cl.OrganizationId == invoice.BuyerId);
                
            bool sellerHasLimit = _context.CreditLimits
                .Any(cl => cl.OrganizationId == invoice.SellerId);
                
            // Use buyer's facility if:
            // 1. Buyer has a direct credit limit relationship with bank, and
            // 2. Either seller has no direct relationship OR buyer has specifically requested to use their facility
            return buyerHasLimit && (
                !sellerHasLimit || 
                invoice.Status == InvoiceStatus.BuyerApprovalPending && invoice.BuyerApproved
            );
        }

        /// <summary>
        /// Determines if the buyer is a bank's customer with invoice financing facility
        /// </summary>
        private bool IsBuyerBankCustomer(int buyerId)
        {
            // First, check if the buyer has any credit limits
            var buyerHasLimit = _context.CreditLimits
                .Any(cl => cl.OrganizationId == buyerId);
                
            if (!buyerHasLimit)
                return false;
                
            // Then, check if they specifically have invoice financing facility
            var hasInvoiceFinancing = _context.CreditLimits
                .Include(cl => cl.Facilities)
                .Any(cl => cl.OrganizationId == buyerId && 
                     cl.Facilities.Any(f => f.Type == FacilityType.InvoiceFinancing));
                     
            return hasInvoiceFinancing;
        }

        public ServiceResult RejectInvoice(int invoiceId, int bankUserId, string reason)
        {
            try
            {
                var invoice = _context.Invoices
                    .FirstOrDefault(i => i.Id == invoiceId)
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.Uploaded && invoice.Status != InvoiceStatus.Validated)
                    return ServiceResult.Failed($"Cannot reject invoice with status {invoice.Status}");

                invoice.Status = InvoiceStatus.Rejected;
                _context.SaveChanges();

                // Create notification for seller
                var seller = _context.Organizations.Find(invoice.SellerId);
                if (seller != null)
                {
                    var sellerUsers = _context.Users.Where(u => u.OrganizationId == seller.Id).ToList();
                    foreach (var user in sellerUsers)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId = user.Id,
                            Title = "Invoice Rejected",
                            Message = $"Invoice {invoice.InvoiceNumber} has been rejected. Reason: {reason}",
                            Type = "Warning"
                        });
                    }
                    _context.SaveChanges();
                }

                return ServiceResult.Successful("Invoice rejected successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error rejecting invoice: {ex.Message}");
            }
        }

        public ServiceResult ProcessPayment(int invoiceId, decimal amount)
        {
            try
            {
                var invoice = _context.Invoices.Find(invoiceId);
                if (invoice == null)
                    return ServiceResult.Failed($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.Funded && invoice.Status != InvoiceStatus.PartiallyPaid)
                    return ServiceResult.Failed($"Invoice must be funded before payment, current status: {invoice.Status}");

                // Validate amount
                if (amount <= 0)
                    return ServiceResult.Failed("Payment amount must be greater than zero");

                decimal totalPaid = (invoice.PaidAmount ?? 0) + amount;
                
                if (totalPaid > invoice.Amount)
                    return ServiceResult.Failed("Payment amount exceeds invoice amount");

                // Update invoice
                invoice.PaidAmount = totalPaid;
                
                if (totalPaid < invoice.Amount)
                {
                    invoice.Status = InvoiceStatus.PartiallyPaid;
                }
                else
                {
                    invoice.Status = InvoiceStatus.FullyPaid;
                    invoice.PaymentDate = DateTime.Now;
                    
                    // Release limit utilization
                    if (invoice.BuyerId.HasValue)
                    {
                        _limitService.ReleaseFacilityUtilization(invoice.BuyerId.Value, FacilityType.InvoiceFinancing, invoice.Amount);
                    }
                }
                
                _context.SaveChanges();

                // Record the transaction
                if (invoice.BuyerId.HasValue)
                {
                    _transactionService.RecordTransaction(new Transaction
                    {
                        Type = TransactionType.Payment,
                        FacilityType = FacilityType.InvoiceFinancing,
                        OrganizationId = invoice.BuyerId.Value,
                        InvoiceId = invoice.Id,
                    Description = $"Payment for invoice {invoice.InvoiceNumber}",
                    Amount = amount,
                    TransactionDate = DateTime.Now,
                    MaturityDate = DateTime.Now,
                    IsPaid = true,
                    PaymentDate = DateTime.Now
                    });
                }

                return ServiceResult.Successful($"Payment of ${amount:N2} processed successfully for invoice {invoice.InvoiceNumber}");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Failed to process payment: {ex.Message}");
            }
        }

        public ServiceResult RequestBuyerApproval(int invoiceId, int bankUserId)
        {
            try
            {
                var invoice = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Seller)
                    .FirstOrDefault(i => i.Id == invoiceId)
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.Approved)
                    return ServiceResult.Failed($"Cannot request buyer approval for invoice with status {invoice.Status}. Invoice must be approved first.");
                
                if (invoice.Buyer == null || invoice.Seller == null)
                    return ServiceResult.Failed("Invoice has missing buyer or seller information");

                // Update invoice status
                invoice.Status = InvoiceStatus.BuyerApprovalPending;
                _context.SaveChanges();

                // Create notification for buyer with action required
                var buyerUsers = _context.Users.Where(u => u.OrganizationId == invoice.BuyerId).ToList();
                foreach (var user in buyerUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        Title = "Liability Transfer Approval Required",
                        Message = $"The invoice {invoice.InvoiceNumber} from {invoice.Seller.Name} for ${invoice.Amount:N2} requires your approval to transfer payment obligation to the bank.",
                        Type = "BuyerApprovalRequest",
                        InvoiceId = invoice.Id,
                        RequiresAction = true
                    });
                }
                _context.SaveChanges();

                return ServiceResult.Successful("Buyer approval request sent successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error requesting buyer approval: {ex.Message}");
            }
        }

        public ServiceResult RequestSellerAcceptance(int invoiceId, int bankUserId, decimal proposedDiscountRate)
        {
            try
            {
                var invoice = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Seller)
                    .FirstOrDefault(i => i.Id == invoiceId)
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.BuyerApprovalPending && invoice.Status != InvoiceStatus.Approved)
                    return ServiceResult.Failed($"Cannot request seller acceptance for invoice with status {invoice.Status}. Invoice must be approved or have buyer approval pending.");
                
                if (invoice.Buyer == null || invoice.Seller == null)
                    return ServiceResult.Failed("Invoice has missing buyer or seller information");

                // Calculate discount amount
                decimal discountAmount = invoice.Amount * (proposedDiscountRate / 100m);
                decimal fundedAmount = invoice.Amount - discountAmount;

                // Update invoice status
                invoice.Status = InvoiceStatus.SellerAcceptancePending;
                invoice.DiscountRate = proposedDiscountRate;
                _context.SaveChanges();

                // Create notification for seller with action required
                var sellerUsers = _context.Users.Where(u => u.OrganizationId == invoice.SellerId).ToList();
                foreach (var user in sellerUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        Title = "Early Payment Offer",
                        Message = $"The bank is offering early payment for invoice {invoice.InvoiceNumber} at a discount rate of {proposedDiscountRate}%. " +
                                  $"If you accept, you will receive ${fundedAmount:N2} immediately instead of ${invoice.Amount:N2} on {invoice.DueDate.ToShortDateString()}.",
                        Type = "SellerAcceptanceRequest",
                        InvoiceId = invoice.Id,
                        RequiresAction = true
                    });
                }
                _context.SaveChanges();

                return ServiceResult.Successful("Seller acceptance request sent successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error requesting seller acceptance: {ex.Message}");
            }
        }

        public ServiceResult BuyerApproveInvoice(int invoiceId, int buyerUserId)
        {
            try
            {
                // Verify the user has rights to approve
                var user = _context.Users.FirstOrDefault(u => u.Id == buyerUserId)
                    ?? throw new Exception("User not found");
                
                var invoice = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Seller)
                    .FirstOrDefault(i => i.Id == invoiceId)
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.BuyerApprovalPending)
                    return ServiceResult.Failed($"Cannot approve invoice with status {invoice.Status}. Invoice must be pending buyer approval.");
                
                if (user.OrganizationId != invoice.BuyerId)
                    return ServiceResult.Failed("You don't have permission to approve this invoice");

                // Update invoice
                invoice.BuyerApproved = true;
                invoice.BuyerApprovalDate = DateTime.Now;
                invoice.BuyerApprovalUserId = buyerUserId;
                invoice.Status = InvoiceStatus.Approved; // Move back to approved state
                _context.SaveChanges();

                // Mark all notifications for this invoice as actioned
                var notifications = _context.Notifications
                    .Where(n => n.InvoiceId == invoiceId && n.UserId == buyerUserId && n.RequiresAction && !n.ActionTaken)
                    .ToList();

                foreach (var notification in notifications)
                {
                    notification.ActionTaken = true;
                    notification.ActionDate = DateTime.Now;
                    notification.ActionResponse = "Approved";
                    notification.IsRead = true;
                }
                _context.SaveChanges();

                // Notify bank
                var bankUsers = _context.Users
                    .Where(u => u.Organization != null && u.Organization.IsBank && 
                               (u.Role == UserRole.BankAdmin || u.Role == UserRole.BankUser))
                    .ToList();

                foreach (var bankUser in bankUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = bankUser.Id,
                        Title = "Buyer Approved Liability Transfer",
                        Message = $"Invoice {invoice.InvoiceNumber} from {invoice.Seller?.Name} to {invoice.Buyer?.Name} has been approved by the buyer for funding.",
                        Type = "Info"
                    });
                }
                _context.SaveChanges();

                return ServiceResult.Successful("Invoice approved successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error approving invoice: {ex.Message}");
            }
        }

        public ServiceResult BuyerRejectInvoice(int invoiceId, int buyerUserId, string reason)
        {
            try
            {
                // Verify the user has rights to reject
                var user = _context.Users.FirstOrDefault(u => u.Id == buyerUserId)
                    ?? throw new Exception("User not found");
                
                var invoice = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Seller)
                    .FirstOrDefault(i => i.Id == invoiceId)
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.BuyerApprovalPending)
                    return ServiceResult.Failed($"Cannot reject invoice with status {invoice.Status}. Invoice must be pending buyer approval.");
                
                if (user.OrganizationId != invoice.BuyerId)
                    return ServiceResult.Failed("You don't have permission to reject this invoice");

                // Update invoice
                invoice.Status = InvoiceStatus.Rejected;
                invoice.RejectionReason = reason;
                _context.SaveChanges();

                // Mark all notifications for this invoice as actioned
                var notifications = _context.Notifications
                    .Where(n => n.InvoiceId == invoiceId && n.UserId == buyerUserId && n.RequiresAction && !n.ActionTaken)
                    .ToList();

                foreach (var notification in notifications)
                {
                    notification.ActionTaken = true;
                    notification.ActionDate = DateTime.Now;
                    notification.ActionResponse = "Rejected: " + reason;
                    notification.IsRead = true;
                }
                _context.SaveChanges();

                // Notify bank
                var bankUsers = _context.Users
                    .Where(u => u.Organization != null && u.Organization.IsBank && 
                               (u.Role == UserRole.BankAdmin || u.Role == UserRole.BankUser))
                    .ToList();

                foreach (var bankUser in bankUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = bankUser.Id,
                        Title = "Buyer Rejected Liability Transfer",
                        Message = $"Invoice {invoice.InvoiceNumber} from {invoice.Seller?.Name} to {invoice.Buyer?.Name} has been rejected by the buyer. Reason: {reason}",
                        Type = "Warning"
                    });
                }
                _context.SaveChanges();

                return ServiceResult.Successful("Invoice rejected successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error rejecting invoice: {ex.Message}");
            }
        }

        public ServiceResult SellerAcceptOffer(int invoiceId, int sellerUserId)
        {
            try
            {
                // Verify the user has rights to accept
                var user = _context.Users.FirstOrDefault(u => u.Id == sellerUserId)
                    ?? throw new Exception("User not found");
                
                var invoice = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Seller)
                    .FirstOrDefault(i => i.Id == invoiceId)
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.SellerAcceptancePending)
                    return ServiceResult.Failed($"Cannot accept offer for invoice with status {invoice.Status}. Invoice must be pending seller acceptance.");
                
                if (user.OrganizationId != invoice.SellerId)
                    return ServiceResult.Failed("You don't have permission to accept this offer");

                // Update invoice
                invoice.SellerAccepted = true;
                invoice.SellerAcceptanceDate = DateTime.Now;
                invoice.SellerAcceptanceUserId = sellerUserId;
                invoice.Status = InvoiceStatus.Approved; // Move back to approved state
                _context.SaveChanges();

                // Mark all notifications for this invoice as actioned
                var notifications = _context.Notifications
                    .Where(n => n.InvoiceId == invoiceId && n.UserId == sellerUserId && n.RequiresAction && !n.ActionTaken)
                    .ToList();

                foreach (var notification in notifications)
                {
                    notification.ActionTaken = true;
                    notification.ActionDate = DateTime.Now;
                    notification.ActionResponse = "Accepted";
                    notification.IsRead = true;
                }
                _context.SaveChanges();

                // Notify bank
                var bankUsers = _context.Users
                    .Where(u => u.Organization != null && u.Organization.IsBank && 
                               (u.Role == UserRole.BankAdmin || u.Role == UserRole.BankUser))
                    .ToList();

                foreach (var bankUser in bankUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = bankUser.Id,
                        Title = "Seller Accepted Early Payment",
                        Message = $"Seller {invoice.Seller?.Name} has accepted the early payment offer for invoice {invoice.InvoiceNumber} at {invoice.DiscountRate}% discount rate.",
                        Type = "Info"
                    });
                }
                _context.SaveChanges();

                return ServiceResult.Successful("Early payment offer accepted successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error accepting early payment offer: {ex.Message}");
            }
        }

        public ServiceResult SellerRejectOffer(int invoiceId, int sellerUserId, string reason)
        {
            try
            {
                // Verify the user has rights to reject
                var user = _context.Users.FirstOrDefault(u => u.Id == sellerUserId)
                    ?? throw new Exception("User not found");
                
                var invoice = _context.Invoices
                    .Include(i => i.Buyer)
                    .Include(i => i.Seller)
                    .FirstOrDefault(i => i.Id == invoiceId)
                    ?? throw new Exception($"Invoice with ID {invoiceId} not found");

                if (invoice.Status != InvoiceStatus.SellerAcceptancePending)
                    return ServiceResult.Failed($"Cannot reject offer for invoice with status {invoice.Status}. Invoice must be pending seller acceptance.");
                
                if (user.OrganizationId != invoice.SellerId)
                    return ServiceResult.Failed("You don't have permission to reject this offer");

                // Update invoice - back to validated status
                invoice.Status = InvoiceStatus.Validated;
                invoice.RejectionReason = reason;
                _context.SaveChanges();

                // Mark all notifications for this invoice as actioned
                var notifications = _context.Notifications
                    .Where(n => n.InvoiceId == invoiceId && n.UserId == sellerUserId && n.RequiresAction && !n.ActionTaken)
                    .ToList();

                foreach (var notification in notifications)
                {
                    notification.ActionTaken = true;
                    notification.ActionDate = DateTime.Now;
                    notification.ActionResponse = "Rejected: " + reason;
                    notification.IsRead = true;
                }
                _context.SaveChanges();

                // Notify bank
                var bankUsers = _context.Users
                    .Where(u => u.Organization != null && u.Organization.IsBank && 
                               (u.Role == UserRole.BankAdmin || u.Role == UserRole.BankUser))
                    .ToList();

                foreach (var bankUser in bankUsers)
                {
                    _context.Notifications.Add(new Notification
                    {
                        UserId = bankUser.Id,
                        InvoiceId = invoiceId,
                        Message = $"Seller {invoice.Seller?.Name ?? "Unknown"} rejected early payment offer for invoice {invoice.InvoiceNumber}. Reason: {reason}",
                        CreatedDate = DateTime.Now,
                        RequiresAction = false,
                        Type = "Warning"
                    });
                }
                _context.SaveChanges();

                return ServiceResult.Successful("Early payment offer rejected successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error rejecting early payment offer: {ex.Message}");
            }
        }

        public Invoice? GetInvoice(int invoiceId)
        {
            return _context.Invoices
                .Include(i => i.Buyer)
                .Include(i => i.Seller)
                .FirstOrDefault(i => i.Id == invoiceId);
        }

        /// <summary>
        /// Determines the relationship between the bank, buyer, and seller for an invoice
        /// </summary>
        /// <returns>
        /// "BuyerIsCustomer" - When the buyer is the bank's customer and seller is a counterparty
        /// "SellerIsCustomer" - When the seller is the bank's customer and buyer is a counterparty 
        /// "BothAreCustomers" - When both buyer and seller are bank's customers (rare case)
        /// "NeitherIsCustomer" - When neither is a bank's customer (should not happen in normal operation)
        /// </returns>
        private string DetermineCustomerRelationship(Invoice invoice)
        {
            if (invoice.BuyerId == null || invoice.SellerId == null)
                return "Unknown";
                
            // Check if buyer has credit limits with the bank
            bool buyerIsCustomer = _context.CreditLimits
                .Any(cl => cl.OrganizationId == invoice.BuyerId);
                
            // Check if seller has credit limits with the bank
            bool sellerIsCustomer = _context.CreditLimits
                .Any(cl => cl.OrganizationId == invoice.SellerId);
                
            if (buyerIsCustomer && sellerIsCustomer)
                return "BothAreCustomers";
            else if (buyerIsCustomer)
                return "BuyerIsCustomer";
            else if (sellerIsCustomer)
                return "SellerIsCustomer";
            else
                return "NeitherIsCustomer";
        }

        /// <summary>
        /// Schedules reminders for invoice due dates
        /// In a real system, this would use a background service
        /// </summary>
        private void ScheduleDueDateReminder(Invoice invoice)
        {
            // In a real application, this would set up a scheduled task or use a background service
            // For now, we'll just create a notification immediately to simulate this
            
            if (invoice.BuyerId.HasValue && invoice.Buyer != null)
            {
                var buyerUsers = _context.Users.Where(u => u.OrganizationId == invoice.BuyerId).ToList();
                
                foreach (var user in buyerUsers)
                {
                    string relationship = "Unknown";
                    
                    // Check if buyer is a bank customer
                    bool buyerIsCustomer = _context.CreditLimits
                        .Any(cl => cl.OrganizationId == invoice.BuyerId);
                        
                    // Check if seller is a bank customer    
                    bool sellerIsCustomer = false;
                    if (invoice.SellerId.HasValue)
                    {
                        sellerIsCustomer = _context.CreditLimits
                            .Any(cl => cl.OrganizationId == invoice.SellerId);
                    }
                    
                    if (buyerIsCustomer && sellerIsCustomer)
                        relationship = "BothAreCustomers";
                    else if (buyerIsCustomer)
                        relationship = "BuyerIsCustomer";
                    else if (sellerIsCustomer)
                        relationship = "SellerIsCustomer";
                    else
                        relationship = "NeitherIsCustomer";
                    
                    string reminderMessage;
                    
                    if (relationship == "BuyerIsCustomer")
                    {
                        reminderMessage = $"REMINDER: Payment for invoice {invoice.InvoiceNumber} (${invoice.Amount:N2}) " +
                                       $"is due in 5 days on {invoice.DueDate.ToShortDateString()}. " +
                                       $"As per your financing arrangement, payment must be made to the bank.";
                    }
                    else
                    {
                        reminderMessage = $"REMINDER: Payment for invoice {invoice.InvoiceNumber} (${invoice.Amount:N2}) " +
                                       $"from {invoice.Seller?.Name} is due in 5 days on {invoice.DueDate.ToShortDateString()}.";
                    }
                    
                    // Create a "scheduled" notification for 5 days before due date
                    // (in a real app this would actually be scheduled)
                    _context.Notifications.Add(new Notification
                    {
                        UserId = user.Id,
                        Title = "Payment Due Soon",
                        Message = reminderMessage,
                        Type = "Warning",
                        CreatedDate = invoice.DueDate.AddDays(-5) // This simulates a scheduled date
                    });
                }
            }
        }
    }
}
