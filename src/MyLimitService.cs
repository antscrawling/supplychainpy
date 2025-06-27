using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Core.Services
{
    public class MyLimitService
    {
        private readonly SupplyChainDbContext _context;

        public MyLimitService(SupplyChainDbContext context)
        {
            _context = context;
        }

        public CreditLimitInfo GetOrganizationCreditLimitInfo(int organizationId)
        {
            var creditLimitInfo = _context.CreditLimits
                .Include(c => c.Facilities)
                .Include(c => c.Organization)
                .FirstOrDefault(c => c.OrganizationId == organizationId);

            if (creditLimitInfo == null)
            {
                throw new Exception($"No credit limits defined for organization ID {organizationId}");
            }

            return creditLimitInfo;
        }

        public ServiceResult CheckFacilityLimit(int organizationId, FacilityType facilityType, decimal amount)
        {
            var creditLimitInfo = _context.CreditLimits
                .Include(c => c.Facilities)
                .FirstOrDefault(c => c.OrganizationId == organizationId);

            if (creditLimitInfo == null)
                return ServiceResult.Failed($"No credit limits defined for organization {organizationId}");
            
            var facility = creditLimitInfo.Facilities.FirstOrDefault(f => f.Type == facilityType);
            if (facility == null)
                return ServiceResult.Failed($"No {facilityType} facility defined for organization {organizationId}");
            
            // Check if facility is expired (beyond grace period)
            if (facility.IsExpired)
                return ServiceResult.Failed($"The {facilityType} facility has expired. Facility in excess by ${facility.InExcess:N2}");
            
            // Check facility limit
            if (facility.CurrentUtilization + amount > facility.TotalLimit)
                return ServiceResult.Failed($"Insufficient {facilityType} limit. Available: ${facility.AvailableLimit:N2}, Required: ${amount:N2}");
            
            // Also check master limit
            var totalUtilization = creditLimitInfo.Facilities.Sum(f => f.CurrentUtilization);
            if (totalUtilization + amount > creditLimitInfo.MasterLimit)
                return ServiceResult.Failed($"Transaction exceeds master limit. Available: ${creditLimitInfo.MasterLimit - totalUtilization:N2}, Required: ${amount:N2}");
            
            return ServiceResult.Successful();
        }

        public void UpdateFacilityUtilization(int organizationId, FacilityType facilityType, decimal amount)
        {
            var facility = _context.Facilities
                .Include(f => f.CreditLimitInfo)
                .FirstOrDefault(f => f.CreditLimitInfo != null && f.CreditLimitInfo.OrganizationId == organizationId && f.Type == facilityType);

            if (facility != null)
            {
                facility.CurrentUtilization += amount;
            }
            
            // Ensure changes to facility utilization are saved
            _context.SaveChanges();
        }

        public void ReleaseFacilityUtilization(int organizationId, FacilityType facilityType, decimal amount)
        {
            var facility = _context.Facilities
                .Include(f => f.CreditLimitInfo)
                .FirstOrDefault(f => f.CreditLimitInfo != null && f.CreditLimitInfo.OrganizationId == organizationId && f.Type == facilityType);

            if (facility != null)
            {
                facility.CurrentUtilization = Math.Max(0, facility.CurrentUtilization - amount);
                
                // Ensure changes are saved to the database
                _context.SaveChanges();
                Console.WriteLine($"DEBUG: Released {amount} from facility {facility.Id}, new utilization: {facility.CurrentUtilization}");
            }
            else
            {
                Console.WriteLine($"DEBUG: No facility found for release - org {organizationId}, type {facilityType}");
            }
        }

        public string GenerateLimitReport(int organizationId)
        {
            var creditInfo = GetOrganizationCreditLimitInfo(organizationId);
            
            var report = new System.Text.StringBuilder();
            report.AppendLine($"LIMIT REPORT FOR: {creditInfo.Organization?.Name} (ID: {organizationId})");
            report.AppendLine($"Generated on: {DateTime.Now}\n");
            
            report.AppendLine($"MASTER LIMIT: ${creditInfo.MasterLimit:N2}");
            report.AppendLine($"TOTAL UTILIZED: ${creditInfo.TotalUtilization:N2} ({creditInfo.MasterUtilizationPercentage:N2}%)");
            report.AppendLine($"AVAILABLE MASTER LIMIT: ${creditInfo.AvailableMasterLimit:N2}\n");
            
            report.AppendLine("FACILITY BREAKDOWN:");
            report.AppendLine("-----------------------------------");
            
            foreach (var facility in creditInfo.Facilities)
            {
                report.AppendLine($"{facility.Type} Facility:");
                report.AppendLine($"  Total Limit: ${facility.TotalLimit:N2}");
                report.AppendLine($"  Utilized: ${facility.CurrentUtilization:N2} ({facility.UtilizationPercentage:N2}%)");
                report.AppendLine($"  Available: ${facility.AvailableLimit:N2}");
                report.AppendLine($"  Review Date: {facility.ReviewEndDate.ToShortDateString()}");
                if (facility.IsExpired)
                {
                    report.AppendLine($"  Status: EXPIRED - FACILITY IN EXCESS");
                    report.AppendLine($"  In Excess: ${facility.InExcess:N2}");
                }
                else if (DateTime.Now > facility.ReviewEndDate)
                {
                    var daysLeft = facility.ReviewEndDate.AddDays(facility.GracePeriodDays).Subtract(DateTime.Now).Days;
                    report.AppendLine($"  Status: IN GRACE PERIOD ({daysLeft} days left)");
                }
                report.AppendLine();
            }
            
            // List active transactions
            var activeTransactions = _context.Transactions
                .Where(t => t.OrganizationId == organizationId && !t.IsPaid)
                .ToList();
            
            if (activeTransactions.Any())
            {
                report.AppendLine("ACTIVE TRANSACTIONS:");
                report.AppendLine("-----------------------------------");
                
                foreach (var transaction in activeTransactions)
                {
                    report.AppendLine($"ID: {transaction.Id} - {transaction.Description}");
                    report.AppendLine($"  Type: {transaction.FacilityType}");
                    report.AppendLine($"  Amount: ${transaction.Amount:N2}");
                    if (transaction.InterestOrDiscountRate.HasValue)
                    {
                        report.AppendLine($"  Rate: {transaction.InterestOrDiscountRate.Value:P2}");
                    }
                    report.AppendLine($"  Date: {transaction.TransactionDate.ToShortDateString()}");
                    report.AppendLine($"  Maturity: {transaction.MaturityDate.ToShortDateString()}");
                    report.AppendLine();
                }
            }
            
            return report.ToString();
        }

        public string GenerateLimitInqReport(int organizationId)
        {
            var creditInfo = GetOrganizationCreditLimitInfo(organizationId);
            var org = creditInfo.Organization;
            var report = new System.Text.StringBuilder();
            report.AppendLine($"LIMIT INQUIRY REPORT FOR: {org?.Name} (ID: {organizationId})");
            report.AppendLine($"Generated on: {DateTime.Now}\n");

            // If buyer only (not seller), show only their granted InvoiceFinancing facility, no master limit or other facilities
            if (org != null && org.IsBuyer && !org.IsSeller)
            {
                var buyerFacilities = creditInfo.Facilities.Where(f => f.RelatedPartyId == org.Id && f.Type == FacilityType.InvoiceFinancing).ToList();
                if (buyerFacilities.Any())
                {
                    var facility = buyerFacilities.First();
                    report.AppendLine($"INVOICE FINANCING LIMIT GRANTED: ${facility.TotalLimit:N2}");
                    report.AppendLine($"Utilized: ${facility.CurrentUtilization:N2} ({facility.UtilizationPercentage:N2}%)");
                    report.AppendLine($"Available: ${facility.AvailableLimit:N2}");
                    report.AppendLine($"Valid until {facility.ReviewEndDate.ToShortDateString()}");
                }
                else
                {
                    report.AppendLine("No invoice financing facility granted.");
                }
            }
            else
            {
                // Seller or dual-role: show all their facilities (including buyer allocations)
                report.AppendLine("MASTER LIMIT");
                report.AppendLine($"├── Total: ${creditInfo.MasterLimit:N2}");
                report.AppendLine($"├── Utilized: ${creditInfo.TotalUtilization:N2} ({creditInfo.MasterUtilizationPercentage:N2}%)");
                report.AppendLine($"└── Available: ${creditInfo.AvailableMasterLimit:N2}\n");
                report.AppendLine("FACILITIES");
                var facilitiesToShow = creditInfo.Facilities.OrderBy(f => f.Type.ToString()).ToList();
                for (int i = 0; i < facilitiesToShow.Count; i++)
                {
                    var facility = facilitiesToShow[i];
                    bool isLast = i == facilitiesToShow.Count - 1;
                    string prefix = isLast ? "└── " : "├── ";
                    string childPrefix = isLast ? "    " : "│   ";
                    report.AppendLine($"{prefix}{facility.Type} Facility");
                    string reviewStatus = facility.IsExpired 
                        ? "EXPIRED - FACILITY IN EXCESS" 
                        : (DateTime.Now > facility.ReviewEndDate 
                            ? $"IN GRACE PERIOD ({facility.ReviewEndDate.AddDays(facility.GracePeriodDays).Subtract(DateTime.Now).Days} days left)" 
                            : $"Valid until {facility.ReviewEndDate.ToShortDateString()}");
                    report.AppendLine($"{childPrefix}├── Review: {reviewStatus}");
                    report.AppendLine($"{childPrefix}├── Total: ${facility.TotalLimit:N2}");
                    report.AppendLine($"{childPrefix}├── Utilized: ${facility.CurrentUtilization:N2} ({facility.UtilizationPercentage:N2}%)");
                    report.AppendLine($"{childPrefix}├── Available: ${facility.AvailableLimit:N2}");
                    if (facility.IsExpired)
                    {
                        report.AppendLine($"{childPrefix}└── In Excess: ${facility.InExcess:N2}");
                    }
                    else
                    {
                        report.AppendLine($"{childPrefix}└── In Excess: $0.00");
                    }
                    // If seller, show which buyer this allocation is for
                    if (org != null && org.IsSeller && facility.RelatedPartyId.HasValue)
                    {
                        var buyer = _context.Organizations.FirstOrDefault(o => o.Id == facility.RelatedPartyId);
                        if (buyer != null)
                            report.AppendLine($"{childPrefix}    (Allocated to Buyer: {buyer.Name})");
                    }
                }
            }

            // Get active transactions for this organization
            var activeTransactions = _context.Transactions
                .Where(t => t.OrganizationId == organizationId && !t.IsPaid)
                .ToList();
            
            if (activeTransactions.Any())
            {
                report.AppendLine("\nACTIVE TRANSACTIONS");
                
                for (int i = 0; i < activeTransactions.Count; i++)
                {
                    var transaction = activeTransactions[i];
                    bool isLast = i == activeTransactions.Count - 1;
                    string prefix = isLast ? "└── " : "├── ";
                    string childPrefix = isLast ? "    " : "│   ";
                    
                    report.AppendLine($"{prefix}ID: {transaction.Id} - {transaction.Description}");
                    report.AppendLine($"{childPrefix}├── Type: {transaction.FacilityType}");
                    report.AppendLine($"{childPrefix}├── Amount: ${transaction.Amount:N2}");
                    
                    if (transaction.InterestOrDiscountRate.HasValue)
                    {
                        report.AppendLine($"{childPrefix}├── Rate: {transaction.InterestOrDiscountRate.Value:P2}");
                    }
                    
                    report.AppendLine($"{childPrefix}├── Date: {transaction.TransactionDate.ToShortDateString()}");
                    report.AppendLine($"{childPrefix}└── Maturity: {transaction.MaturityDate.ToShortDateString()}");
                }
            }
            
            return report.ToString();
        }

        public List<CreditLimitInfo> GetCreditLimitsByOrganization(int organizationId)
        {
            return _context.CreditLimits
                .Include(c => c.Facilities)
                .Include(c => c.Organization)
                .Where(c => c.OrganizationId == organizationId)
                .ToList();
        }

        public List<Facility> GetAllFacilities()
        {
            return _context.Facilities
                .Include(f => f.CreditLimitInfo)
                    .ThenInclude(cli => cli!.Organization) // Using null-forgiving operator
                .Include(f => f.RelatedParty)
                .ToList();
        }
        
        public ServiceResult AllocateBuyerLimit(int sellerId, int buyerId, FacilityType facilityType, decimal amount)
        {
            try
            {
                // Verify seller and buyer
                var seller = _context.Organizations.Find(sellerId);
                var buyer = _context.Organizations.Find(buyerId);
                
                if (seller == null)
                    return ServiceResult.Failed("Seller organization not found");
                    
                if (buyer == null)
                    return ServiceResult.Failed("Buyer organization not found");
                    
                if (!seller.IsSeller)
                    return ServiceResult.Failed("First organization must be a seller");
                    
                if (!buyer.IsBuyer)
                    return ServiceResult.Failed("Second organization must be a buyer");
                
                // Get seller's credit limit info
                var creditLimit = _context.CreditLimits
                    .Include(cl => cl.Facilities)
                    .FirstOrDefault(cl => cl.OrganizationId == sellerId);
                    
                if (creditLimit == null)
                    return ServiceResult.Failed("Seller has no credit facilities");
                
                // For seller-bank relationship, we specifically want the invoice financing facility
                var sellerFacility = creditLimit.Facilities
                    .FirstOrDefault(f => f.Type == facilityType && f.RelatedPartyId == null);
                    
                if (sellerFacility == null)
                    return ServiceResult.Failed($"Seller has no {facilityType} facility");
                
                // Check if seller has enough available limit
                if (sellerFacility.AvailableLimit < amount)
                    return ServiceResult.Failed($"Seller doesn't have enough available limit. Available: ${sellerFacility.AvailableLimit:N2}, Requested: ${amount:N2}");
                
                // Check if buyer allocation already exists
                var existingAllocation = _context.Facilities
                    .FirstOrDefault(f => f.CreditLimitInfoId == creditLimit.Id && 
                                        f.Type == facilityType && 
                                        f.RelatedPartyId == buyerId);
                
                if (existingAllocation != null)
                {
                    // Update existing allocation
                    existingAllocation.AllocatedLimit += amount;
                    
                    // Note: When seller is the bank's customer, buyer is not the bank's customer directly
                    // The limit is shared with the seller, and the buyer can only see this allocation
                    return ServiceResult.Successful($"Added ${amount:N2} to buyer's existing allocation. New allocation: ${existingAllocation.AllocatedLimit:N2}");
                }
                
                // Create new buyer allocation
                var buyerAllocation = new Facility
                {
                    CreditLimitInfoId = creditLimit.Id,
                    Type = facilityType,
                    TotalLimit = amount,
                    AllocatedLimit = amount,
                    RelatedPartyId = buyerId,
                    ReviewEndDate = sellerFacility.ReviewEndDate // Same expiry as parent facility
                };
                
                _context.Facilities.Add(buyerAllocation);
                
                // Adjust the seller's facility if needed to reflect shared limit
                // For invoice financing specifically, the limit is shared between seller and buyer
                if (facilityType == FacilityType.InvoiceFinancing)
                {
                    // This allocation shares the seller's limit and doesn't create additional limit
                    // We don't modify sellerFacility.TotalLimit since it's already accounting for this
                }
                
                return ServiceResult.Successful($"Successfully allocated ${amount:N2} limit from {seller.Name} to {buyer.Name} for {facilityType}");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Error allocating limit: {ex.Message}");
            }
        }
        
        public string GenerateAllLimitsReport()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("ALL LIMITS REPORT - BANK VIEW");
            sb.AppendLine("===============================================");
            
            // Get all organizations
            var organizations = _context.Organizations.ToList();
            var creditLimits = _context.CreditLimits
                .Include(cl => cl.Organization)
                .Include(cl => cl.Facilities)
                .ToList();
            
            // Group by organization type
            var sellers = organizations.Where(o => o.IsSeller).ToList();
            var buyers = organizations.Where(o => o.IsBuyer).ToList();
            
            // Show seller facilities
            sb.AppendLine("\nSELLER FACILITIES:");
            sb.AppendLine($"{"Seller",-20} {"Facility Type",-15} {"Total Limit",-15} {"Used",-15} {"Available",-15} {"% Used",-10}");
            sb.AppendLine(new string('-', 90));
            
            foreach (var seller in sellers)
            {
                var creditLimit = creditLimits.FirstOrDefault(cl => cl.OrganizationId == seller.Id);
                if (creditLimit == null)
                    continue;
                
                foreach (var facility in creditLimit.Facilities.Where(f => f.RelatedPartyId == null))
                {
                    sb.AppendLine($"{seller.Name,-20} {facility.Type,-15} ${facility.TotalLimit,-13:N2} ${facility.CurrentUtilization,-13:N2} ${facility.AvailableLimit,-13:N2} {facility.UtilizationPercentage,-10:N2}%");
                }
            }
            
            // Show buyer facilities (non-allocated)
            sb.AppendLine("\nBUYER FACILITIES:");
            sb.AppendLine($"{"Buyer",-20} {"Facility Type",-15} {"Total Limit",-15} {"Used",-15} {"Available",-15} {"% Used",-10}");
            sb.AppendLine(new string('-', 90));
            
            foreach (var buyer in buyers.Where(b => !b.IsSeller)) // Only pure buyers, not seller-buyers
            {
                var creditLimit = creditLimits.FirstOrDefault(cl => cl.OrganizationId == buyer.Id);
                if (creditLimit == null)
                    continue;
                
                foreach (var facility in creditLimit.Facilities)
                {
                    sb.AppendLine($"{buyer.Name,-20} {facility.Type,-15} ${facility.TotalLimit,-13:N2} ${facility.CurrentUtilization,-13:N2} ${facility.AvailableLimit,-13:N2} {facility.UtilizationPercentage,-10:N2}%");
                }
            }
            
            // Show buyer allocations
            sb.AppendLine("\nBUYER ALLOCATIONS:");
            sb.AppendLine($"{"Buyer",-20} {"Seller",-20} {"Facility Type",-15} {"Allocated",-15} {"Used",-15} {"Available",-15}");
            sb.AppendLine(new string('-', 100));
            
            foreach (var creditLimit in creditLimits)
            {
                var allocatedFacilities = creditLimit.Facilities.Where(f => f.RelatedPartyId.HasValue).ToList();
                foreach (var facility in allocatedFacilities)
                {
                    var buyer = organizations.FirstOrDefault(o => o.Id == facility.RelatedPartyId);
                    if (buyer != null)
                    {
                        sb.AppendLine($"{buyer.Name,-20} {creditLimit.Organization?.Name,-20} {facility.Type,-15} ${facility.AllocatedLimit,-13:N2} ${facility.CurrentUtilization,-13:N2} ${facility.AvailableLimit,-13:N2}");
                    }
                }
            }
            
            return sb.ToString();
        }

        public string GenerateLimitTreeReport()
        {
            Console.WriteLine($"DEBUG: Generating LimitTreeReport at {DateTime.Now}");
            
            var report = new System.Text.StringBuilder();
            report.AppendLine("BANK MASTER LIMIT REPORT");
            report.AppendLine("==============================================");
            report.AppendLine($"Generated on: {DateTime.Now}\n");
            
            var organizations = _context.Organizations
                .Where(o => !o.IsBank)
                .OrderBy(o => o.IsSeller ? 0 : 1)  // Sort sellers first then buyers
                .ThenBy(o => o.Name)
                .ToList();
            
            Console.WriteLine($"DEBUG: Found {organizations.Count} non-bank organizations");
                
            var creditLimits = _context.CreditLimits
                .Include(cl => cl.Organization)
                .Include(cl => cl.Facilities)
                .ToList();
                
            Console.WriteLine($"DEBUG: Found {creditLimits.Count} credit limits");
            
            // Check if we have any facilities in the database
            var allFacilities = _context.Facilities.ToList();
            Console.WriteLine($"DEBUG: Total facilities in database: {allFacilities.Count}");
            foreach (var facility in allFacilities)
            {
                Console.WriteLine($"DEBUG: Facility ID {facility.Id}, Type {facility.Type}, Limit {facility.TotalLimit:N2}, CreditLimitInfoId {facility.CreditLimitInfoId}");
            }
                
            foreach (var org in organizations)
            {
                var creditInfo = creditLimits.FirstOrDefault(cl => cl.OrganizationId == org.Id);
                if (creditInfo == null) 
                {
                    report.AppendLine($"ORGANIZATION: {org.Name} (ID: {org.Id}) - {(org.IsBuyer ? "Buyer" : "")}{(org.IsSeller ? "Seller" : "")}");
                    report.AppendLine("└── No credit limits defined\n");
                    continue;
                }
                
                report.AppendLine($"ORGANIZATION: {org.Name} (ID: {org.Id}) - {(org.IsBuyer ? "Buyer" : "")}{(org.IsSeller ? "Seller" : "")}");
                report.AppendLine($"├── MASTER LIMIT: ${creditInfo.MasterLimit:N2}");
                report.AppendLine($"├── UTILIZED: ${creditInfo.TotalUtilization:N2} ({creditInfo.MasterUtilizationPercentage:N2}%)");
                report.AppendLine($"└── AVAILABLE: ${creditInfo.AvailableMasterLimit:N2}\n");
                
                report.AppendLine("FACILITIES:");
                var facilitiesToShow = creditInfo.Facilities.OrderBy(f => f.Type.ToString()).ToList();
                for (int i = 0; i < facilitiesToShow.Count; i++)
                {
                    var facility = facilitiesToShow[i];
                    bool isLast = i == facilitiesToShow.Count - 1;
                    string prefix = isLast ? "└── " : "├── ";
                    string childPrefix = isLast ? "    " : "│   ";
                    
                    report.AppendLine($"{prefix}{facility.Type} Facility");
                    string reviewStatus = facility.IsExpired 
                        ? "EXPIRED - FACILITY IN EXCESS" 
                        : (DateTime.Now > facility.ReviewEndDate 
                            ? $"IN GRACE PERIOD ({facility.ReviewEndDate.AddDays(facility.GracePeriodDays).Subtract(DateTime.Now).Days} days left)" 
                            : $"Valid until {facility.ReviewEndDate.ToShortDateString()}");
                            
                    report.AppendLine($"{childPrefix}├── Review: {reviewStatus}");
                    report.AppendLine($"{childPrefix}├── Total: ${facility.TotalLimit:N2}");
                    report.AppendLine($"{childPrefix}├── Utilized: ${facility.CurrentUtilization:N2} ({facility.UtilizationPercentage:N2}%)");
                    report.AppendLine($"{childPrefix}├── Available: ${facility.AvailableLimit:N2}");
                    
                    if (facility.IsExpired)
                    {
                        report.AppendLine($"{childPrefix}└── In Excess: ${facility.InExcess:N2}");
                    }
                    else
                    {
                        report.AppendLine($"{childPrefix}└── In Excess: $0.00");
                    }
                    
                    // If seller facility is allocated to a buyer, show which buyer
                    if (facility.RelatedPartyId.HasValue)
                    {
                        var relatedParty = _context.Organizations.FirstOrDefault(o => o.Id == facility.RelatedPartyId);
                        if (relatedParty != null)
                            report.AppendLine($"{childPrefix}    (Allocated to Buyer: {relatedParty.Name})");
                    }
                    
                    // If buyer facility is allocated from a seller, show which seller
                    if (org.IsBuyer && !org.IsSeller)
                    {
                        var sellerCredits = _context.CreditLimits
                            .Include(cl => cl.Organization)
                            .Include(cl => cl.Facilities)
                            .Where(cl => cl.Organization!.IsSeller)
                            .ToList();
                            
                        foreach (var sellerCredit in sellerCredits)
                        {
                            var allocations = sellerCredit.Facilities
                                .Where(f => f.RelatedPartyId == org.Id)
                                .ToList();
                                
                            if (allocations.Any())
                            {
                                report.AppendLine($"{childPrefix}    (Allocated from: {sellerCredit.Organization?.Name})");
                            }
                        }
                    }
                }
                
                report.AppendLine();
            }
            
            return report.ToString();
        }

        public ServiceResult CreateCreditLimitWithFacilities(CreditLimitInfo creditLimit, List<Facility> facilities)
        {
            _context.CreditLimits.Add(creditLimit);
            
            foreach (var facility in facilities)
            {
                facility.CreditLimitInfoId = creditLimit.Id;
                _context.Facilities.Add(facility);
            }
            
            return ServiceResult.Successful($"Credit limit created successfully with ID {creditLimit.Id}");
        }

        public ServiceResult CreateCreditLimitWithFacilities(CreditLimitInfo creditLimit, List<(FacilityType Type, decimal Limit, DateTime ReviewEndDate, int GracePeriodDays)> facilityDetails)
        {
            try
            {
                Console.WriteLine($"DEBUG: CreateCreditLimitWithFacilities called for org {creditLimit.OrganizationId}, master limit {creditLimit.MasterLimit}");
                Console.WriteLine($"DEBUG: Number of facilities to create: {facilityDetails.Count}");
                
                // Add the credit limit
                _context.CreditLimits.Add(creditLimit);
                _context.SaveChanges();
                Console.WriteLine($"DEBUG: CreditLimitInfo created with ID {creditLimit.Id}");
                
                // Create facilities
                foreach (var detail in facilityDetails)
                {
                    var facility = new Facility
                    {
                        CreditLimitInfoId = creditLimit.Id,
                        Type = detail.Type,
                        TotalLimit = detail.Limit,
                        ReviewEndDate = detail.ReviewEndDate,
                        GracePeriodDays = detail.GracePeriodDays
                    };
                    
                    Console.WriteLine($"DEBUG: Adding facility: Type={detail.Type}, Limit={detail.Limit}, ReviewEndDate={detail.ReviewEndDate}");
                    _context.Facilities.Add(facility);
                }
                
                _context.SaveChanges();
                Console.WriteLine($"DEBUG: All facilities saved successfully");
                
                return ServiceResult.Successful("Credit limit with facilities created successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Failed to create credit limit: {ex.Message}");
            }
        }

        public List<Facility> GetVisibleFacilitiesForOrganization(int organizationId)
        {
            // Get the organization details first to know if it's a buyer or seller
            var organization = _context.Organizations.Find(organizationId);
            if (organization == null)
                return new List<Facility>();
                
            // This will hold all facilities the organization can see
            var visibleFacilities = new List<Facility>();
            
            // Case 1: If organization is a bank customer (direct relationship)
            var directFacilities = _context.Facilities
                .Include(f => f.CreditLimitInfo)
                    .ThenInclude(cli => cli!.Organization)
                .Include(f => f.RelatedParty)
                .Where(f => f.CreditLimitInfo != null && f.CreditLimitInfo.OrganizationId == organizationId)
                .ToList();
                
            visibleFacilities.AddRange(directFacilities);
            
            // Case 2: If organization is a buyer with allocated facilities from sellers
            if (organization.IsBuyer)
            {
                var allocatedFacilities = _context.Facilities
                    .Include(f => f.CreditLimitInfo)
                        .ThenInclude(cli => cli!.Organization)
                    .Include(f => f.RelatedParty)
                    .Where(f => f.RelatedPartyId == organizationId)
                    .ToList();
                    
                // For non-bank-customer buyers, they can only see invoice financing facilities allocated to them
                visibleFacilities.AddRange(allocatedFacilities);
            }
            
            return visibleFacilities;
        }
        
        public string GenerateLimitReportWithVisibilityRules(int organizationId)
        {
            var organization = _context.Organizations.Find(organizationId);
            if (organization == null)
                return "Organization not found.";
                
            var visibleFacilities = GetVisibleFacilitiesForOrganization(organizationId);
            
            var report = new System.Text.StringBuilder();
            report.AppendLine($"LIMIT REPORT FOR: {organization.Name} (ID: {organizationId})");
            report.AppendLine($"Generated on: {DateTime.Now}\n");
            
            // Group facilities by credit limit info and type
            var groupedFacilities = visibleFacilities
                .GroupBy(f => f.CreditLimitInfoId)
                .ToList();
                
            foreach (var group in groupedFacilities)
            {
                var creditLimitInfo = _context.CreditLimits
                    .Include(c => c.Organization)
                    .FirstOrDefault(c => c.Id == group.Key);
                    
                if (creditLimitInfo == null)
                    continue;
                    
                // If this is the organization's own credit limit
                if (creditLimitInfo.OrganizationId == organizationId)
                {
                    report.AppendLine($"MASTER LIMIT: ${creditLimitInfo.MasterLimit:N2}");
                    report.AppendLine($"TOTAL UTILIZED: ${creditLimitInfo.TotalUtilization:N2} ({creditLimitInfo.MasterUtilizationPercentage:N2}%)");
                    report.AppendLine($"AVAILABLE MASTER LIMIT: ${creditLimitInfo.AvailableMasterLimit:N2}\n");
                }
                else
                {
                    // For buyers viewing seller-granted facilities
                    var limitProvider = creditLimitInfo.Organization?.Name ?? "Unknown Provider";
                    report.AppendLine($"Credit limits provided by: {limitProvider}\n");
                }
                
                report.AppendLine("FACILITIES:");
                report.AppendLine("----------------------------------------------------------");
                
                foreach (var facility in group)
                {
                    string relationshipInfo = "";
                    
                    // Show relationship info only if applicable
                    if (facility.RelatedPartyId.HasValue && facility.RelatedParty != null)
                    {
                        if (creditLimitInfo.OrganizationId == organizationId)
                        {
                            // Seller viewing buyer allocation
                            relationshipInfo = $" (Allocated to: {facility.RelatedParty.Name})";
                        }
                        else
                        {
                            // Buyer viewing its allocation from seller
                            relationshipInfo = $" (Allocated from: {creditLimitInfo.Organization?.Name})";
                        }
                    }
                    
                    report.AppendLine($"TYPE: {facility.Type}{relationshipInfo}");
                    report.AppendLine($"LIMIT: ${facility.TotalLimit:N2}");
                    report.AppendLine($"UTILIZED: ${facility.CurrentUtilization:N2} ({facility.UtilizationPercentage:N2}%)");
                    report.AppendLine($"AVAILABLE: ${facility.AvailableLimit:N2}");
                    report.AppendLine($"EXPIRY: {facility.ReviewEndDate:MM/dd/yyyy} (Grace: {facility.GracePeriodDays} days)");
                    
                    if (facility.IsExpired)
                    {
                        report.AppendLine("STATUS: EXPIRED - NO NEW TRANSACTIONS ALLOWED");
                        report.AppendLine($"AMOUNT IN EXCESS: ${facility.InExcess:N2}");
                    }
                    
                    report.AppendLine("----------------------------------------------------------");
                }
                
                report.AppendLine();
            }
            
            return report.ToString();
        }

        public string GenerateLimitTreeReportWithVisibilityRules(int organizationId)
        {
            var organization = _context.Organizations.Find(organizationId);
            if (organization == null)
                return "Organization not found.";
                
            var visibleFacilities = GetVisibleFacilitiesForOrganization(organizationId);
            
            var report = new System.Text.StringBuilder();
            report.AppendLine($"LIMIT REPORT FOR: {organization.Name} (ID: {organizationId})");
            report.AppendLine("==============================================");
            report.AppendLine($"Generated on: {DateTime.Now}\n");
            
            // Group facilities by credit limit info
            var groupedFacilities = visibleFacilities
                .GroupBy(f => f.CreditLimitInfoId)
                .ToList();
                
            foreach (var group in groupedFacilities)
            {
                var creditLimitInfo = _context.CreditLimits
                    .Include(c => c.Organization)
                    .FirstOrDefault(c => c.Id == group.Key);
                    
                if (creditLimitInfo == null)
                    continue;
                    
                // If this is the organization's own credit limit
                if (creditLimitInfo.OrganizationId == organizationId)
                {
                    report.AppendLine($"ORGANIZATION: {organization.Name} (ID: {organizationId}) - {(organization.IsBuyer ? "Buyer" : "")}{(organization.IsSeller ? "Seller" : "")}");
                    report.AppendLine($"├── MASTER LIMIT: ${creditLimitInfo.MasterLimit:N2}");
                    report.AppendLine($"├── UTILIZED: ${creditLimitInfo.TotalUtilization:N2} ({creditLimitInfo.MasterUtilizationPercentage:N2}%)");
                    report.AppendLine($"└── AVAILABLE: ${creditLimitInfo.AvailableMasterLimit:N2}\n");
                }
                else
                {
                    // For buyers viewing seller-granted facilities
                    var limitProvider = creditLimitInfo.Organization?.Name ?? "Unknown Provider";
                    report.AppendLine($"CREDIT PROVIDER: {limitProvider}");
                    report.AppendLine($"├── PROVIDER TYPE: {(creditLimitInfo.Organization?.IsSeller == true ? "Seller" : "Other")}");
                    report.AppendLine($"└── FACILITIES PROVIDED TO: {organization.Name}\n");
                }
                
                report.AppendLine("FACILITIES:");
                var facilitiesToShow = group.OrderBy(f => f.Type.ToString()).ToList();
                for (int i = 0; i < facilitiesToShow.Count; i++)
                {
                    var facility = facilitiesToShow[i];
                    bool isLast = i == facilitiesToShow.Count - 1;
                    string prefix = isLast ? "└── " : "├── ";
                    string childPrefix = isLast ? "    " : "│   ";
                    
                    string relationshipInfo = "";
                    if (facility.RelatedPartyId.HasValue && facility.RelatedParty != null)
                    {
                        if (creditLimitInfo.OrganizationId == organizationId)
                        {
                            // Seller viewing buyer allocation
                            relationshipInfo = $" (Allocated to: {facility.RelatedParty.Name})";
                        }
                        else
                        {
                            // Buyer viewing its allocation from seller
                            relationshipInfo = $" (Allocated from: {creditLimitInfo.Organization?.Name})";
                        }
                    }
                    
                    report.AppendLine($"{prefix}{facility.Type} Facility{relationshipInfo}");
                    
                    string reviewStatus = facility.IsExpired 
                        ? "EXPIRED - FACILITY IN EXCESS" 
                        : (DateTime.Now > facility.ReviewEndDate 
                            ? $"IN GRACE PERIOD ({facility.ReviewEndDate.AddDays(facility.GracePeriodDays).Subtract(DateTime.Now).Days} days left)" 
                            : $"Valid until {facility.ReviewEndDate.ToShortDateString()}");
                            
                    report.AppendLine($"{childPrefix}├── Review: {reviewStatus}");
                    report.AppendLine($"{childPrefix}├── Total: ${facility.TotalLimit:N2}");
                    report.AppendLine($"{childPrefix}├── Utilized: ${facility.CurrentUtilization:N2} ({facility.UtilizationPercentage:N2}%)");
                    report.AppendLine($"{childPrefix}├── Available: ${facility.AvailableLimit:N2}");
                    
                    if (facility.IsExpired)
                    {
                        report.AppendLine($"{childPrefix}└── In Excess: ${facility.InExcess:N2}");
                    }
                    else
                    {
                        report.AppendLine($"{childPrefix}└── In Excess: $0.00");
                    }
                }
                
                report.AppendLine();
            }
            
            if (!groupedFacilities.Any())
            {
                report.AppendLine("└── No credit limits or facilities available to view\n");
            }
            
            return report.ToString();
        }

        public ServiceResult AddFacilityToOrganization(int organizationId, FacilityType facilityType, decimal facilityLimit, DateTime reviewEndDate, int gracePeriodDays)
        {
            try
            {
                Console.WriteLine($"DEBUG: AddFacilityToOrganization called for org {organizationId}, facility type {facilityType}, limit {facilityLimit}");
                
                // Get or create credit limit for the organization
                var creditLimit = _context.CreditLimits
                    .FirstOrDefault(c => c.OrganizationId == organizationId);
                
                Console.WriteLine($"DEBUG: Found creditLimit: {(creditLimit != null ? "Yes, ID=" + creditLimit.Id : "No")}");
                
                if (creditLimit == null)
                {
                    // Create a new credit limit with the same amount as the facility
                    creditLimit = new CreditLimitInfo
                    {
                        OrganizationId = organizationId,
                        MasterLimit = facilityLimit,
                        Organization = _context.Organizations.Find(organizationId),
                        LastReviewDate = DateTime.Now,
                        NextReviewDate = DateTime.Now.AddYears(1)
                    };
                    
                    Console.WriteLine($"DEBUG: Adding new CreditLimitInfo for org {organizationId} with master limit {facilityLimit}");
                    _context.CreditLimits.Add(creditLimit);
                    _context.SaveChanges();
                    Console.WriteLine($"DEBUG: New CreditLimitInfo created with ID {creditLimit.Id}");
                }
                
                // Check if the facility limit exceeds the available master limit
                if (facilityLimit > creditLimit.AvailableMasterLimit)
                {
                    Console.WriteLine($"DEBUG: Facility limit {facilityLimit} exceeds available master limit {creditLimit.AvailableMasterLimit}");
                    return ServiceResult.Failed($"Facility limit of {facilityLimit:C} exceeds available master limit of {creditLimit.AvailableMasterLimit:C}");
                }
                
                // Create new facility
                var facility = new Facility
                {
                    CreditLimitInfoId = creditLimit.Id,
                    Type = facilityType,
                    TotalLimit = facilityLimit,
                    ReviewEndDate = reviewEndDate,
                    GracePeriodDays = gracePeriodDays
                };
                
                Console.WriteLine($"DEBUG: Adding new Facility with type {facilityType}, limit {facilityLimit}, for CreditLimitInfoId {creditLimit.Id}");
                _context.Facilities.Add(facility);
                _context.SaveChanges();
                Console.WriteLine($"DEBUG: New Facility created with ID {facility.Id}");
                
                return ServiceResult.Successful($"Facility added successfully: {facilityType} with limit {facilityLimit:C}");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failed($"Failed to add facility: {ex.Message}");
            }
        }
    }
}
