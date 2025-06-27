"""
Bank Portal for Supply Chain Finance Management System
Converted from C# BankApplication class
"""

import os
import sys
from typing import Optional, List
from datetime import datetime
from database import Database
from auth_service import authenticate, is_authorized


class BankApplication:
    """Main Bank Portal Application Class"""
    
    def __init__(self):
        self.db = Database()
        self.current_user = None
        self.current_organization = None
        self.auth_service = None
        self.limit_service = None
        self.invoice_service = None
        self.transaction_service = None
        self.user_service = None
        self.accounting_service = None

    def run(self):
        """Main application entry point"""
        self.clear_screen()
        print("=" * 51)
        print("   SUPPLY CHAIN FINANCE - BANK PORTAL")
        print("=" * 51)
        print()
        
        if self.login():
            self.show_main_menu()
        
        print("\nThank you for using the Supply Chain Finance Bank Portal. Goodbye!")

    def clear_screen(self):
        """Clear the console screen"""
        os.system('cls' if os.name == 'nt' else 'clear')

    def login(self) -> bool:
        """Handle user login with 3 attempts"""
        attempts = 0
        while attempts < 3:
            print("Please login to continue:")
            username = input("Username: ").strip()
            password = input("Password: ").strip()
            
            # Use auth_service for authentication
            user = authenticate(username, password)
            
            if user:
                # Check if user has bank organization or bank role (role 0 = bank admin)
                org = user.get('organization', {})
                if not org.get('is_bank', False) and user.get('role') != 0:
                    print("\nError: This portal is for bank users only. Clients must use the Client Portal.")
                    return False
                
                self.current_user = user
                self.current_organization = org if org else {
                    'name': 'Global Finance Bank',
                    'is_bank': True,
                    'is_buyer': False,
                    'is_seller': False
                }
                self.clear_screen()
                print(f"\nWelcome, {user.get('name', username)}!")
                print(f"Organization: {self.current_organization['name']}")
                print()
                return True
            else:
                attempts += 1
                print(f"\nInvalid username or password. {3 - attempts} attempts remaining.\n")
        
        print("\nToo many failed login attempts. Exiting...")
        return False

    def show_main_menu(self):
        """Display and handle main menu options"""
        exit_menu = False
        
        while not exit_menu and self.current_user and self.current_organization:
            # Check for notifications (placeholder)
            notifications = self.get_user_notifications()
            if notifications:
                print(f"\nYou have {len(notifications)} unread notifications!")
            
            print("\nMAIN MENU")
            print("1. Manage Organizations")
            print("2. Manage Credit Facilities")
            print("3. Review Invoices")
            print("4. Process Funding")
            print("5. Process Payments")
            print("6. View Transactions")
            print("7. View Reports")
            print("8. View Notifications")
            print("9. Manage Accounting Entries")
            print("0. Logout")
            
            choice = input("\nSelect an option: ").strip()
            
            if choice == "1":
                self.manage_organizations()
            elif choice == "2":
                self.manage_credit_facilities()
            elif choice == "3":
                self.review_invoices()
            elif choice == "4":
                self.process_funding()
            elif choice == "5":
                self.process_payments()
            elif choice == "6":
                self.view_transactions()
            elif choice == "7":
                self.view_reports()
            elif choice == "8":
                self.view_notifications()
            elif choice == "9":
                self.manage_accounting_entries()
            elif choice == "0":
                exit_menu = True
            else:
                print("\nInvalid option. Please try again.")

    def get_user_notifications(self) -> List:
        """Get unread notifications for current user"""
        # Placeholder - implement actual notification retrieval
        return []

    def manage_organizations(self):
        """Manage customer organizations"""
        self.clear_screen()
        print("MANAGE ORGANIZATIONS")
        print("=" * 19)
        print()
        
        try:
            # Get all non-bank organizations
            organizations = self.get_organizations()
            
            print("Organizations:")
            for i, org in enumerate(organizations, 1):
                org_type = []
                if org.get('is_buyer', False):
                    org_type.append("Buyer")
                if org.get('is_seller', False):
                    org_type.append("Seller")
                type_str = " & ".join(org_type) if org_type else "Unknown"
                print(f"{org['id']:3d}. {org['name']} ({type_str})")
            
            print("\n1. View Organization Details")
            print("2. Add New Organization")
            print("0. Back to Main Menu")
            
            choice = input("\nSelect an option: ").strip()
            
            if choice == "1":
                org_id = input("\nEnter Organization ID: ").strip()
                try:
                    org_id = int(org_id)
                    org = next((o for o in organizations if o['id'] == org_id), None)
                    if org:
                        self.manage_organization(org_id)
                    else:
                        print("\nOrganization not found.")
                        self.wait_for_enter()
                except ValueError:
                    print("\nInvalid ID format.")
                    self.wait_for_enter()
            elif choice == "2":
                self.add_new_organization()
            elif choice == "0":
                return
            else:
                print("\nInvalid option.")
                self.wait_for_enter()
                
        except Exception as e:
            print(f"Error managing organizations: {e}")
            self.wait_for_enter()

    def get_organizations(self) -> List:
        """Get all non-bank organizations from database"""
        try:
            db = Database()
            query = "SELECT Id, Name, IsBuyer, IsSeller FROM Organizations WHERE IsBank = 0"
            db.cursor.execute(query)
            results = db.cursor.fetchall()
            db.close()
            
            organizations = []
            for row in results:
                organizations.append({
                    'id': row[0],
                    'name': row[1],
                    'is_buyer': bool(row[2]),
                    'is_seller': bool(row[3])
                })
            
            return organizations
            
        except Exception as e:
            print(f"Error fetching organizations: {e}")
            return []

    def manage_organization(self, org_id: int):
        """Manage specific organization details"""
        print(f"\nManaging organization {org_id}")
        print("This feature is under development.")
        self.wait_for_enter()

    def add_new_organization(self):
        """Add a new organization"""
        self.clear_screen()
        print("ADD NEW ORGANIZATION")
        print("=" * 19)
        print()
        
        try:
            name = input("Organization Name: ").strip()
            if not name:
                print("Organization name is required.")
                self.wait_for_enter()
                return
            
            tax_id = input("Tax ID: ").strip()
            address = input("Address: ").strip()
            contact_person = input("Contact Person: ").strip()
            contact_email = input("Contact Email: ").strip()
            contact_phone = input("Contact Phone: ").strip()
            
            print("\nOrganization Type:")
            print("1. Buyer only")
            print("2. Seller only") 
            print("3. Both Buyer and Seller")
            
            role_choice = input("Select type (1-3): ").strip()
            
            is_buyer = role_choice in ["1", "3"]
            is_seller = role_choice in ["2", "3"]
            
            # Create organization record
            org_data = {
                'name': name,
                'tax_id': tax_id,
                'address': address,
                'contact_person': contact_person,
                'contact_email': contact_email,
                'contact_phone': contact_phone,
                'is_buyer': is_buyer,
                'is_seller': is_seller,
                'is_bank': False
            }
            
            org_id = self.create_organization(org_data)
            print(f"\nOrganization '{name}' created successfully with ID: {org_id}")
            
            # Ask if admin user should be created
            create_user = input("\nCreate admin user for this organization? (Y/N): ").strip().upper()
            if create_user == "Y":
                self.create_user_for_organization(org_id)
            
            # Ask if credit facilities should be added
            add_credit = input("\nAdd credit facilities to this organization now? (Y/N): ").strip().upper()
            if add_credit == "Y":
                self.add_facility_to_organization(org_id)
                
        except Exception as e:
            print(f"Error creating organization: {e}")
        
        self.wait_for_enter()

    def create_organization(self, org_data: dict) -> int:
        """Create organization in database"""
        # Placeholder - implement actual database insertion
        return 999  # Mock ID

    def create_user_for_organization(self, org_id: int):
        """Create admin user for organization"""
        print(f"\nCreating user for organization {org_id}")
        print("This feature is under development.")

    def add_facility_to_organization(self, org_id: int):
        """Add credit facility to organization"""
        print(f"\nAdding credit facility to organization {org_id}")
        print("This feature is under development.")

    def manage_credit_facilities(self):
        """Manage credit facilities"""
        self.clear_screen()
        print("MANAGE CREDIT FACILITIES")
        print("=" * 23)
        print()
        
        print("1. View All Limits (Bank View)")
        print("2. Grant Facility to Any Customer")
        print("3. Manage Buyer-Specific Limits")
        print("4. Launch GrantBuyerLimit Program")
        print("0. Back")
        
        choice = input("\nSelect an option: ").strip()
        
        if choice == "1":
            self.view_all_limits()
        elif choice == "2":
            self.grant_facility_to_customer()
        elif choice == "3":
            self.manage_buyer_limits()
        elif choice == "4":
            self.launch_grant_buyer_limit()
        elif choice == "0":
            return
        else:
            print("\nInvalid option. Please try again.")
            input("\nPress any key to continue...")

    def view_all_limits(self):
        """View all credit limits with utilization"""
        self.clear_screen()
        print("ALL LIMITS REPORT (BANK VIEW)")
        print("=" * 28)
        print()
        
        try:
            db = Database()
            
            # Get all credit facilities with organization info
            query = """
                SELECT o.Name, cf.FacilityType, cf.Limit, cf.UtilizedAmount, cf.IsActive,
                       cf.CreatedDate, o.Id as OrgId
                FROM CreditFacilities cf
                JOIN Organizations o ON cf.OrganizationId = o.Id
                ORDER BY o.Name, cf.FacilityType
            """
            
            db.cursor.execute(query)
            facilities = db.cursor.fetchall()
            
            if not facilities:
                print("No credit facilities found.")
            else:
                print(f"{'Organization':<25} {'Type':<15} {'Limit':<15} {'Used':<15} {'Available':<15} {'Status':<8}")
                print("-" * 100)
                
                for facility in facilities:
                    org_name = facility[0]
                    facility_type = "Invoice Finance" if facility[1] == 0 else f"Type {facility[1]}"
                    limit_amount = float(facility[2]) if facility[2] else 0.0
                    utilized_amount = float(facility[3]) if facility[3] else 0.0
                    available_amount = limit_amount - utilized_amount
                    status = "Active" if facility[4] else "Inactive"
                    
                    print(f"{org_name:<25} {facility_type:<15} ${limit_amount:<14,.0f} ${utilized_amount:<14,.0f} ${available_amount:<14,.0f} {status:<8}")
            
            db.close()
            
        except Exception as e:
            print(f"Error retrieving credit facilities: {e}")
        
        self.wait_for_enter()

    def generate_limit_tree_report(self) -> str:
        # 1. Organization
        #    |-Limit Amount : 100,000
        #       |-Invoice Financing Limit: 50,000
        #          |-Utiliozation : 20,000
        #          |-Available Limit: 30,000
        
        
        
        
        """Generate tree-style limits report"""
        return "Tree-style limits report (placeholder)"

    def generate_all_limits_report(self) -> str:
        """Generate tabular limits report"""
        return "Tabular limits report (placeholder)"

    def grant_facility_to_customer(self):
        """Grant facility to any customer"""
        print("\nGrant Facility to Customer")
        print("This feature is under development.")
        self.wait_for_enter()

    def manage_buyer_limits(self):
        """Manage buyer-specific limits"""
        print("\nManage Buyer Limits")
        print("This feature is under development.")
        self.wait_for_enter()

    def launch_grant_buyer_limit(self):
        """Launch grant buyer limit program"""
        print("\nLaunch Grant Buyer Limit Program")
        print("This feature is under development.")
        self.wait_for_enter()

    def review_invoices(self):
        """Review and process invoices"""
        self.clear_screen()
        print("REVIEW INVOICES")
        print("=" * 14)
        print()
        
        print("1. Review New Invoices (Seller Uploaded)")
        print("2. Review Validated Invoices")
        print("3. Review Buyer Uploaded Invoices")
        print("4. Review Approval Pending")
        print("0. Back to Main Menu")
        
        choice = input("\nSelect an option: ").strip()
        
        if choice == "1":
            self.review_invoices_by_status("uploaded", "New Invoices")
        elif choice == "2":
            self.review_invoices_by_status("validated", "Validated Invoices")
        elif choice == "3":
            self.review_invoices_by_status("buyer_uploaded", "Buyer Uploaded")
        elif choice == "4":
            self.review_invoices_by_status("approval_pending", "Approval Pending")
        elif choice == "0":
            return
        else:
            print("\nInvalid option.")
            self.wait_for_enter()

    def review_invoices_by_status(self, status: str, status_name: str):
        """Review invoices by specific status"""
        self.clear_screen()
        print(f"REVIEW {status_name.upper()}")
        print("=" * (7 + len(status_name)))
        print()
        
        invoices = self.get_invoices_by_status(status)
        
        if not invoices:
            print(f"No {status_name.lower()} found.")
            self.wait_for_enter()
            return
        
        print(f"{status_name}:")
        for i, invoice in enumerate(invoices, 1):
            print(f"{i}. Invoice #{invoice['number']} | Amount: ${invoice['amount']:,.2f} | "
                  f"Seller: {invoice.get('seller_name', 'Unknown')} | "
                  f"Buyer: {invoice.get('buyer_name', 'Unknown')}")
        
        print("0. Back")
        
        selection = input(f"\nSelect invoice to review (1-{len(invoices)} or 0): ").strip()
        
        try:
            selection = int(selection)
            if 1 <= selection <= len(invoices):
                self.review_invoice(invoices[selection - 1])
            elif selection != 0:
                print("Invalid selection.")
                self.wait_for_enter()
        except ValueError:
            print("Invalid input.")
            self.wait_for_enter()

    def get_invoices_by_status(self, status: str) -> List:
        """Get invoices by status"""
        # Map status names to database status codes
        status_map = {
            "uploaded": 0,
            "validated": 1, 
            "approved": 2,
            "funded": 3,
            "paid": 4,
            "rejected": 5,
            "matured": 6,
            "discount_approved": 7,
            "buyer_uploaded": 0,  # Same as uploaded but we can filter by buyer
            "approval_pending": 1  # Same as validated - waiting for buyer approval
        }
        
        status_code = status_map.get(status, 0)
        return self.get_real_invoices_by_status(status_code)

    def review_invoice(self, invoice: dict):
        """Review individual invoice"""
        self.clear_screen()
        print(f"REVIEW INVOICE: {invoice['number']}")
        print("=" * (15 + len(invoice['number'])))
        print()
        
        print(f"Invoice Number: {invoice['number']}")
        print(f"Amount: ${invoice['amount']:,.2f}")
        print(f"Seller: {invoice.get('seller_name', 'Unknown')}")
        print(f"Buyer: {invoice.get('buyer_name', 'Unknown')}")
        print(f"Status: {invoice.get('status', 'Unknown')}")
        
        print("\nAvailable Actions:")
        print("1. Validate Invoice")
        print("2. Approve for Funding")
        print("3. Request Buyer Approval")
        print("4. Make Early Payment Offer")
        print("5. Reject Invoice")
        print("0. Back")
        
        choice = input("\nSelect action: ").strip()
        
        if choice == "1":
            self.validate_invoice(invoice)
        elif choice == "2":
            self.approve_invoice(invoice)
        elif choice == "3":
            self.request_buyer_approval(invoice)
        elif choice == "4":
            self.make_early_payment_offer(invoice)
        elif choice == "5":
            self.reject_invoice(invoice)
        elif choice == "0":
            return
        else:
            print("\nInvalid option.")
            self.wait_for_enter()

    def validate_invoice(self, invoice: dict):
        """Validate an invoice"""
        print(f"\nValidating invoice {invoice['number']}")
        
        # Update status to Validated (1)
        if self.update_invoice_status(invoice['id'], 1):
            print("Invoice validation completed - Status updated to 'Validated'")
            
            # Create validation accounting entry (for audit trail)
            stakeholders = self.get_invoice_stakeholders(invoice['id'])
            self.create_accounting_entry(
                "VALIDATION", 
                0.0, 
                invoice['id'], 
                f"Invoice validation completed - {invoice['number']}", 
                stakeholders.get('seller_org_id'),
                stakeholders.get('buyer_org_id')
            )
            
            # Send notification to seller
            if stakeholders.get('seller_user_id'):
                message = f"Invoice {invoice['number']} has been validated by the bank and is ready for buyer approval."
                self.send_notification(stakeholders['seller_user_id'], message, invoice['id'], "Invoice Validated", "Success")
                print(f"Notification sent to seller: {stakeholders.get('seller_name', 'Unknown')}")
            
            # Send notification to buyer for approval
            if stakeholders.get('buyer_user_id'):
                message = f"Invoice {invoice['number']} from {stakeholders.get('seller_name', 'Unknown')} requires your approval for financing."
                self.send_notification(stakeholders['buyer_user_id'], message, invoice['id'], "Approval Required", "Action", True)
                print(f"Approval request sent to buyer: {stakeholders.get('buyer_name', 'Unknown')}")
        else:
            print("Error: Failed to update invoice status")
        
        self.wait_for_enter()

    def approve_invoice(self, invoice: dict):
        """Approve invoice for funding"""
        print(f"\nApproving invoice {invoice['number']} for funding")
        
        # Update status to Approved (2)
        if self.update_invoice_status(invoice['id'], 2):
            print("Invoice approved successfully - Status updated to 'Approved'")
            print("Invoice is now ready for funding.")
            
            # Create approval accounting entry (for audit trail)
            stakeholders = self.get_invoice_stakeholders(invoice['id'])
            self.create_accounting_entry(
                "APPROVAL", 
                0.0, 
                invoice['id'], 
                f"Invoice approved for funding - {invoice['number']}", 
                stakeholders.get('seller_org_id'),
                stakeholders.get('buyer_org_id')
            )
            
            # Send notification to seller
            if stakeholders.get('seller_user_id'):
                message = f"Great news! Invoice {invoice['number']} has been approved for funding by the bank."
                self.send_notification(stakeholders['seller_user_id'], message, invoice['id'], "Invoice Approved", "Success")
                print(f"Approval notification sent to seller: {stakeholders.get('seller_name', 'Unknown')}")
            
            # Send notification to buyer
            if stakeholders.get('buyer_user_id'):
                message = f"Invoice {invoice['number']} has been approved for funding. You will be notified when payment is due."
                self.send_notification(stakeholders['buyer_user_id'], message, invoice['id'], "Invoice Approved", "Info")
                print(f"Status update sent to buyer: {stakeholders.get('buyer_name', 'Unknown')}")
        else:
            print("Error: Failed to update invoice status")
        
        self.wait_for_enter()

    def request_buyer_approval(self, invoice: dict):
        """Request buyer approval for liability transfer"""
        print(f"\nRequesting buyer approval for invoice {invoice['number']}")
        
        # Send notification to buyer requesting approval
        stakeholders = self.get_invoice_stakeholders(invoice['id'])
        if stakeholders.get('buyer_user_id'):
            message = f"URGENT: Please approve invoice {invoice['number']} from {stakeholders.get('seller_name', 'Unknown')} for supply chain financing. Amount: ${invoice['amount']:,.2f}"
            
            if self.send_notification(stakeholders['buyer_user_id'], message, invoice['id']):
                print("Buyer approval request sent successfully.")
                print(f"Notification sent to: {stakeholders.get('buyer_name', 'Unknown')}")
                
                # Also notify seller that buyer approval was requested
                if stakeholders.get('seller_user_id'):
                    seller_message = f"Buyer approval has been requested for invoice {invoice['number']}. You will be notified once the buyer responds."
                    self.send_notification(stakeholders['seller_user_id'], seller_message, invoice['id'])
                    print(f"Status update sent to seller: {stakeholders.get('seller_name', 'Unknown')}")
            else:
                print("Error: Failed to send buyer approval request")
        else:
            print("Error: Could not find buyer contact information")
        
        self.wait_for_enter()

    def make_early_payment_offer(self, invoice: dict):
        """Make early payment offer to seller"""
        print(f"\nMaking early payment offer for invoice {invoice['number']}")
        
        try:
            discount_rate = float(input("Enter discount rate (%): "))
            if discount_rate <= 0:
                print("Invalid discount rate.")
                self.wait_for_enter()
                return
            
            discount_amount = invoice['amount'] * (discount_rate / 100)
            net_amount = invoice['amount'] - discount_amount
            
            print(f"\nDiscount Rate: {discount_rate:.2f}%")
            print(f"Discount Amount: ${discount_amount:,.2f}")
            print(f"Net Payment to Seller: ${net_amount:,.2f}")
            
            confirm = input("\nSend offer to seller? (Y/N): ").strip().upper()
            if confirm == "Y":
                # Send notification to seller with early payment offer
                stakeholders = self.get_invoice_stakeholders(invoice['id'])
                
                if stakeholders.get('seller_user_id'):
                    offer_message = f"Early payment offer for invoice {invoice['number']}: Receive ${net_amount:,.2f} now (discount: {discount_rate:.2f}%) instead of waiting until due date. Please respond to accept or decline."
                    
                    if self.send_notification(stakeholders['seller_user_id'], offer_message, invoice['id']):
                        print("Early payment offer sent to seller.")
                        print(f"Offer sent to: {stakeholders.get('seller_name', 'Unknown')}")
                        
                        # Also notify buyer about the offer
                        if stakeholders.get('buyer_user_id'):
                            buyer_message = f"Early payment offer has been made to seller for invoice {invoice['number']}. Amount: ${net_amount:,.2f}"
                            self.send_notification(stakeholders['buyer_user_id'], buyer_message, invoice['id'])
                    else:
                        print("Error: Failed to send offer to seller")
                else:
                    print("Error: Could not find seller contact information")
            else:
                print("Offer cancelled.")
                
        except ValueError:
            print("Invalid discount rate format.")
        
        self.wait_for_enter()

    def reject_invoice(self, invoice: dict):
        """Reject an invoice"""
        reason = input(f"\nEnter rejection reason for invoice {invoice['number']}: ").strip()
        if reason:
            # Update status to Rejected (5)
            if self.update_invoice_status(invoice['id'], 5):
                print("Invoice rejected successfully.")
                
                # Update rejection reason in database
                try:
                    db = Database()
                    db.cursor.execute("UPDATE Invoices SET RejectionReason = ? WHERE Id = ?", (reason, invoice['id']))
                    db.connection.commit()
                    db.close()
                except Exception as e:
                    print(f"Error updating rejection reason: {e}")
                
                # Create rejection accounting entry (for audit trail)
                stakeholders = self.get_invoice_stakeholders(invoice['id'])
                self.create_accounting_entry(
                    "REJECTION", 
                    0.0, 
                    invoice['id'], 
                    f"Invoice rejected - {invoice['number']} - Reason: {reason}", 
                    stakeholders.get('seller_org_id'),
                    stakeholders.get('buyer_org_id')
                )
                
                # Send notifications to seller and buyer
                if stakeholders.get('seller_user_id'):
                    seller_message = f"Invoice {invoice['number']} has been rejected by the bank. Reason: {reason}"
                    self.send_notification(stakeholders['seller_user_id'], seller_message, invoice['id'])
                    print(f"Rejection notification sent to seller: {stakeholders.get('seller_name', 'Unknown')}")
                
                if stakeholders.get('buyer_user_id'):
                    buyer_message = f"Invoice {invoice['number']} has been rejected by the bank and will not require payment."
                    self.send_notification(stakeholders['buyer_user_id'], buyer_message, invoice['id'])
                    print(f"Status update sent to buyer: {stakeholders.get('buyer_name', 'Unknown')}")
            else:
                print("Error: Failed to update invoice status")
        else:
            print("Rejection cancelled - no reason provided.")
        self.wait_for_enter()

    def process_funding(self):
        """Process invoice funding"""
        self.clear_screen()
        print("PROCESS INVOICE FUNDING")
        print("=" * 22)
        print()
        
        approved_invoices = self.get_approved_invoices()
        
        if not approved_invoices:
            print("No invoices approved and ready for funding.")
            self.wait_for_enter()
            return
        
        print("Approved Invoices Ready for Funding:")
        for i, invoice in enumerate(approved_invoices, 1):
            print(f"{i}. Invoice #{invoice['number']} | Amount: ${invoice['amount']:,.2f} | "
                  f"Seller: {invoice.get('seller_name', 'Unknown')} | "
                  f"Buyer: {invoice.get('buyer_name', 'Unknown')}")
        
        print("0. Back")
        
        selection = input(f"\nSelect invoice to fund (1-{len(approved_invoices)} or 0): ").strip()
        
        try:
            selection = int(selection)
            if 1 <= selection <= len(approved_invoices):
                self.fund_invoice(approved_invoices[selection - 1])
            elif selection != 0:
                print("Invalid selection.")
                self.wait_for_enter()
        except ValueError:
            print("Invalid input.")
            self.wait_for_enter()

    def get_approved_invoices(self) -> List:
        """Get approved invoices ready for funding"""
        return self.get_real_invoices_by_status(2)  # Status 2 = Approved

    def fund_invoice(self, invoice: dict):
        """Fund an individual invoice"""
        self.clear_screen()
        print(f"FUND INVOICE: {invoice['number']}")
        print("=" * (13 + len(invoice['number'])))
        print()
        
        print(f"Invoice Number: {invoice['number']}")
        print(f"Amount: ${invoice['amount']:,.2f}")
        print(f"Seller: {invoice.get('seller_name', 'Unknown')}")
        print(f"Buyer: {invoice.get('buyer_name', 'Unknown')}")
        
        print("\nPlease enter the funding details:")
        
        try:
            base_rate = float(input("Base Rate (%): "))
            if base_rate < 0:
                print("Invalid base rate. Funding cancelled.")
                self.wait_for_enter()
                return
            
            margin = float(input("Department Margin (%): "))
            if margin < 0:
                print("Invalid margin. Funding cancelled.")
                self.wait_for_enter()
                return
            
            final_rate = base_rate + margin
            discount_amount = invoice['amount'] * (final_rate / 100)
            funded_amount = invoice['amount'] - discount_amount
            
            print(f"\nFinal Discount Rate: {final_rate:.2f}%")
            print(f"Invoice Face Value: ${invoice['amount']:,.2f}")
            print(f"Discount Amount: ${discount_amount:,.2f} (deducted from face value)")
            print(f"Amount to be Credited to Seller: ${funded_amount:,.2f}")
            print(f"Buyer will pay full face value of ${invoice['amount']:,.2f} at maturity")
            
            confirm = input("\nConfirm funding (Y/N)? ").strip().upper()
            
            if confirm == "Y":
                # Get stakeholders for credit checks
                stakeholders = self.get_invoice_stakeholders(invoice['id'])
                seller_org_id = stakeholders.get('seller_org_id')
                buyer_org_id = stakeholders.get('buyer_org_id')
                
                # Check credit availability for both seller and buyer
                if not self.check_credit_availability(seller_org_id, invoice['amount']):
                    print(f"Error: Seller does not have sufficient credit limit for ${invoice['amount']:,.2f}")
                    self.wait_for_enter()
                    return
                
                if not self.check_credit_availability(buyer_org_id, invoice['amount']):
                    print(f"Error: Buyer does not have sufficient credit limit for ${invoice['amount']:,.2f}")
                    self.wait_for_enter()
                    return
                
                # Update invoice status to Funded (3) and record funding details
                if self.update_invoice_status(invoice['id'], 3):
                    # Update funding details in database
                    db = Database()
                    try:
                        db.cursor.execute("""
                            UPDATE Invoices 
                            SET FundedAmount = ?, DiscountRate = ? 
                            WHERE Id = ?
                        """, (str(funded_amount), str(final_rate), invoice['id']))
                        db.connection.commit()
                        db.close()
                        
                        print("\nFunding processed successfully!")
                        print("Invoice status updated to 'Funded'")
                        
                        # Update credit utilization for both seller and buyer
                        if self.update_credit_utilization(seller_org_id, invoice['amount'], True):
                            print(f"Seller credit utilization updated")
                        
                        if self.update_credit_utilization(buyer_org_id, invoice['amount'], True):
                            print(f"Buyer credit utilization updated")
                        
                        # Send notifications to all parties
                        stakeholders = self.get_invoice_stakeholders(invoice['id'])
                        
                        # Notify seller
                        if stakeholders.get('seller_user_id'):
                            seller_message = f"Funding completed! ${funded_amount:,.2f} has been credited to your account for invoice {invoice['number']}. Discount rate: {final_rate:.2f}%"
                            self.send_notification(stakeholders['seller_user_id'], seller_message, invoice['id'])
                            print(f"Funding notification sent to seller: {stakeholders.get('seller_name', 'Unknown')}")
                        
                        # Notify buyer
                        if stakeholders.get('buyer_user_id'):
                            buyer_message = f"Invoice {invoice['number']} has been funded. You will need to pay ${invoice['amount']:,.2f} at maturity. Due date: {invoice.get('due_date', 'TBD')}"
                            self.send_notification(stakeholders['buyer_user_id'], buyer_message, invoice['id'])
                            print(f"Payment reminder sent to buyer: {stakeholders.get('buyer_name', 'Unknown')}")
                        
                        # Record funding transaction
                        self.record_funding(invoice, base_rate, margin, final_rate, funded_amount, discount_amount)
                        
                    except Exception as e:
                        print(f"Error updating funding details: {e}")
                else:
                    print("Error: Failed to update invoice status")
            else:
                print("Funding cancelled.")
                
        except ValueError:
            print("Invalid rate format. Funding cancelled.")
        
        self.wait_for_enter()

    def record_funding(self, invoice: dict, base_rate: float, margin: float, 
                      final_rate: float, funded_amount: float, discount_amount: float):
        """Record funding transaction"""
        try:
            db = Database()
            timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            
            # Insert transaction record
            query = """
                INSERT INTO Transactions (Type, FacilityType, OrganizationId, InvoiceId, Description, Amount, 
                                        InterestOrDiscountRate, TransactionDate, MaturityDate, IsPaid)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """
            
            description = f"Invoice funding - Base rate: {base_rate}%, Margin: {margin}%, Final rate: {final_rate}%, Discount: ${discount_amount:,.2f}"
            
            # Get invoice due date and seller organization ID for the transaction
            invoice_query = "SELECT DueDate, SellerId FROM Invoices WHERE Id = ?"
            db.cursor.execute(invoice_query, (invoice['id'],))
            invoice_details = db.cursor.fetchone()
            maturity_date = invoice_details[0] if invoice_details and invoice_details[0] else timestamp
            seller_org_id = invoice_details[1] if invoice_details and invoice_details[1] else 1
            
            db.cursor.execute(query, (
                1,  # Type: 1 for Funding
                0,  # FacilityType: 0 (based on existing data)
                seller_org_id,  # Organization that received the funding
                invoice['id'],
                description,
                str(funded_amount),
                str(final_rate),
                timestamp,
                maturity_date,
                0  # IsPaid: False initially
            ))
            
            db.connection.commit()
            db.close()
            
            print("Funding transaction recorded in database.")
            
            # Create accounting entries for funding
            stakeholders = self.get_invoice_stakeholders(invoice['id'])
            
            # 1. Record the funding transaction (loan advance)
            self.create_accounting_entry(
                "FUNDING", 
                funded_amount, 
                invoice['id'], 
                f"Invoice funding advance - {invoice['number']}", 
                stakeholders.get('seller_org_id'),
                stakeholders.get('buyer_org_id')
            )
            
            # 2. Record the discount/fee income
            if discount_amount > 0:
                self.create_accounting_entry(
                    "INTEREST_INCOME", 
                    discount_amount, 
                    invoice['id'], 
                    f"Discount income from invoice {invoice['number']}", 
                    stakeholders.get('seller_org_id'),
                    stakeholders.get('buyer_org_id')
                )
            
            # 3. Automatically credit the seller (this completes the seller side)
            self.create_accounting_entry(
                "SELLER_PAYMENT", 
                funded_amount, 
                invoice['id'], 
                f"Payment to seller for invoice {invoice['number']}", 
                stakeholders.get('seller_org_id'),
                stakeholders.get('buyer_org_id')
            )
            
            print("Accounting entries created for funding transaction.")
            print(f"Seller automatically credited ${funded_amount:,.2f}")
            
        except Exception as e:
            print(f"Error recording funding transaction: {e}")

    def process_payments(self):
        """Process payments for funded invoices (buyer payments only)"""
        self.clear_screen()
        print("PROCESS PAYMENTS")
        print("=" * 15)
        print()
        
        # Get funded invoices (status 3) - these need buyer payment
        funded_invoices = self.get_real_invoices_by_status(3)
        
        if not funded_invoices:
            print("No funded invoices requiring buyer payment found.")
            print("Note: Sellers are automatically credited when invoices are funded.")
            self.wait_for_enter()
            return
        
        print("Funded Invoices Awaiting Buyer Payment:")
        print("-" * 80)
        for i, invoice in enumerate(funded_invoices, 1):
            print(f"{i:2d}. Invoice #{invoice['number']:15} | Amount: ${invoice['amount']:>10,.2f} | "
                  f"Seller: {invoice.get('seller_name', 'Unknown'):20} | "
                  f"Buyer: {invoice.get('buyer_name', 'Unknown'):20}")
        
        print("\nNote: Sellers have already been credited. These are buyer payments to the bank.")
        print("\n0. Back to Main Menu")
        
        try:
            choice = int(input("\nSelect invoice to process payment (enter number): "))
            if choice == 0:
                return
            elif 1 <= choice <= len(funded_invoices):
                selected_invoice = funded_invoices[choice - 1]
                self.record_invoice_payment(selected_invoice)
            else:
                print("Invalid selection.")
                self.wait_for_enter()
        except ValueError:
            print("Invalid input.")
            self.wait_for_enter()

    def record_invoice_payment(self, invoice: dict):
        """Record payment received from buyer for an invoice"""
        self.clear_screen()
        print(f"RECORD BUYER PAYMENT: {invoice['number']}")
        print("=" * (21 + len(invoice['number'])))
        print()
        
        print(f"Invoice Number: {invoice['number']}")
        print(f"Original Amount: ${invoice['amount']:,.2f}")
        print(f"Seller: {invoice.get('seller_name', 'Unknown')} (Already Credited)")
        print(f"Buyer: {invoice.get('buyer_name', 'Unknown')} (Payment Due)")
        print()
        
        try:
            payment_amount = float(input("Enter buyer payment amount received: $"))
            
            if payment_amount <= 0:
                print("Invalid payment amount.")
                self.wait_for_enter()
                return
            
            payment_date = input("Enter payment date (YYYY-MM-DD) or press Enter for today: ").strip()
            if not payment_date:
                payment_date = datetime.now().strftime('%Y-%m-%d')
            
            print(f"\nPayment Details:")
            print(f"Amount: ${payment_amount:,.2f}")
            print(f"Date: {payment_date}")
            
            confirm = input("\nConfirm payment recording (Y/N)? ").strip().upper()
            
            if confirm == "Y":
                # Update invoice status to Paid (4)
                if self.update_invoice_status(invoice['id'], 4):
                    # Record payment transaction
                    try:
                        db = Database()
                        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
                        
                        # Insert payment transaction
                        query = """
                            INSERT INTO Transactions (Type, FacilityType, OrganizationId, InvoiceId, Description, Amount, 
                                                    TransactionDate, MaturityDate, IsPaid, PaymentDate)
                            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                        """
                        
                        description = f"Payment received for invoice {invoice['number']}"
                        payment_timestamp = payment_date + " " + datetime.now().strftime('%H:%M:%S')
                        
                        db.cursor.execute(query, (
                            2,  # Type: 2 for Payment
                            0,  # FacilityType: 0 (based on existing data)
                            stakeholders.get('buyer_org_id', 1),  # Organization that made the payment
                            invoice['id'],
                            description,
                            str(payment_amount),
                            payment_timestamp,
                            payment_timestamp,  # Maturity date same as payment date since it's paid
                            1,  # IsPaid: True
                            payment_timestamp
                        ))
                        
                        db.connection.commit()
                        db.close()
                        
                        print("\nPayment recorded successfully!")
                        print("Invoice status updated to 'Paid'")
                        
                        # Restore credit utilization for both seller and buyer
                        stakeholders = self.get_invoice_stakeholders(invoice['id'])
                        seller_org_id = stakeholders.get('seller_org_id')
                        buyer_org_id = stakeholders.get('buyer_org_id')
                        
                        if self.update_credit_utilization(seller_org_id, invoice['amount'], False):
                            print(f"Seller credit limit restored")
                        
                        if self.update_credit_utilization(buyer_org_id, invoice['amount'], False):
                            print(f"Buyer credit limit restored")
                        
                        # Send notifications
                        stakeholders = self.get_invoice_stakeholders(invoice['id'])
                        
                        # Notify seller
                        if stakeholders.get('seller_user_id'):
                            seller_message = f"Buyer payment of ${payment_amount:,.2f} has been received for invoice {invoice['number']}. The financing cycle is now complete."
                            self.send_notification(stakeholders['seller_user_id'], seller_message, invoice['id'], "Buyer Payment Received", "Success")
                            print(f"Payment notification sent to seller: {stakeholders.get('seller_name', 'Unknown')}")
                        
                        # Notify buyer
                        if stakeholders.get('buyer_user_id'):
                            buyer_message = f"Thank you for your payment of ${payment_amount:,.2f} for invoice {invoice['number']}. Your invoice financing obligation is now complete."
                            self.send_notification(stakeholders['buyer_user_id'], buyer_message, invoice['id'], "Payment Confirmed", "Success")
                            print(f"Payment confirmation sent to buyer: {stakeholders.get('buyer_name', 'Unknown')}")
                        
                        # Create accounting entries for buyer payment
                        self.create_accounting_entry(
                            "PAYMENT", 
                            payment_amount, 
                            invoice['id'], 
                            f"Buyer payment received for invoice {invoice['number']}", 
                            stakeholders.get('seller_org_id'),
                            stakeholders.get('buyer_org_id')
                        )
                        
                        print("Accounting entries created for payment.")
                        
                    except Exception as e:
                        print(f"Error recording payment: {e}")
                else:
                    print("Error: Failed to update invoice status")
            else:
                print("Payment recording cancelled.")
                
        except ValueError:
            print("Invalid amount format.")
        
        self.wait_for_enter()

    def view_transactions(self):
        """View transaction history"""
        print("\nVIEW TRANSACTIONS")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def view_reports(self):
        """View various reports"""
        print("\nVIEW REPORTS")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def view_notifications(self):
        """View user notifications"""
        self.clear_screen()
        print("NOTIFICATIONS")
        print("=" * 13)
        print()
        
        notifications = self.get_user_notifications()
        
        if not notifications:
            print("No notifications found.")
        else:
            for i, notification in enumerate(notifications, 1):
                print(f"{i}. {notification.get('message', 'No message')}")
                print(f"   Date: {notification.get('date', 'Unknown')}")
                print(f"   Status: {'Read' if notification.get('is_read', False) else 'Unread'}")
                print()
        
        self.wait_for_enter()

    def manage_accounting_entries(self):
        """Manage accounting entries"""
        self.clear_screen()
        print("ACCOUNTING ENTRIES MANAGEMENT")
        print("=" * 28)
        print()
        
        exit_menu = False
        while not exit_menu:
            print("Chart of Accounts & Setup:")
            print("1. View Chart of Accounts")
            print()
            print("Journal Entries:")
            print("2. View Journal Entries")
            print("3. Create Manual Journal Entry")
            print("4. Post Journal Entry")
            print()
            print("Reports:")
            print("5. View Account Balances")
            print("6. Generate Trial Balance")
            print()
            print("Transaction Processing:")
            print("7. Create Invoice Financing Entry")
            print("8. Create Payment Entry")
            print("9. View Transaction History")
            print()
            print("0. Back to Main Menu")
            
            choice = input("\nSelect an option: ").strip()
            
            if choice == "1":
                self.view_chart_of_accounts()
            elif choice == "2":
                self.view_journal_entries()
            elif choice == "3":
                self.create_manual_journal_entry()
            elif choice == "4":
                self.post_journal_entry()
            elif choice == "5":
                self.view_account_balances()
            elif choice == "6":
                self.generate_trial_balance()
            elif choice == "7":
                self.create_invoice_financing_entry()
            elif choice == "8":
                self.create_payment_entry()
            elif choice == "9":
                self.view_transaction_history()
            elif choice == "0":
                exit_menu = True
            else:
                print("\nInvalid option. Please try again.")
                input("\nPress any key to continue...")
                self.clear_screen()
                print("ACCOUNTING ENTRIES MANAGEMENT")
                print("=" * 28)
                print()

    def view_chart_of_accounts(self):
        """View chart of accounts"""
        print("\nVIEW CHART OF ACCOUNTS")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def view_journal_entries(self):
        """View journal entries"""
        print("\nVIEW JOURNAL ENTRIES")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def create_manual_journal_entry(self):
        """Create manual journal entry"""
        print("\nCREATE MANUAL JOURNAL ENTRY")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def post_journal_entry(self):
        """Post journal entry"""
        print("\nPOST JOURNAL ENTRY")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def view_account_balances(self):
        """View account balances"""
        print("\nVIEW ACCOUNT BALANCES")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def generate_trial_balance(self):
        """Generate trial balance"""
        print("\nGENERATE TRIAL BALANCE")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def create_invoice_financing_entry(self):
        """Create invoice financing entry"""
        print("\nCREATE INVOICE FINANCING ENTRY")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def create_payment_entry(self):
        """Create payment entry"""
        print("\nCREATE PAYMENT ENTRY")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def view_transaction_history(self):
        """View transaction history"""
        print("\nVIEW TRANSACTION HISTORY")
        print("Feature not implemented yet.")
        self.wait_for_enter()

    def wait_for_enter(self):
        """Wait for user to press Enter"""
        input("\nPress Enter to continue...")

    def update_invoice_status(self, invoice_id: int, new_status: int, notes: str = "") -> bool:
        """Update invoice status in database"""
        try:
            db = Database()
            
            # Status mapping: 0=Uploaded, 1=Validated, 2=Approved, 3=Funded, 4=Paid, 5=Rejected, 6=Matured, 7=Discount_Approved
            query = "UPDATE Invoices SET Status = ? WHERE Id = ?"
            db.cursor.execute(query, (new_status, invoice_id))
            
            # Add status change timestamp based on status
            timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            
            if new_status == 3:  # Funded
                db.cursor.execute("UPDATE Invoices SET FundingDate = ? WHERE Id = ?", (timestamp, invoice_id))
            elif new_status == 4:  # Paid
                db.cursor.execute("UPDATE Invoices SET PaymentDate = ? WHERE Id = ?", (timestamp, invoice_id))
            elif new_status == 2:  # Approved (buyer approval)
                db.cursor.execute("UPDATE Invoices SET BuyerApproved = 1, BuyerApprovalDate = ? WHERE Id = ?", (timestamp, invoice_id))
            
            db.connection.commit()
            db.close()
            return True
            
        except Exception as e:
            print(f"Error updating invoice status: {e}")
            return False

    def send_notification(self, user_id: int, message: str, invoice_id: int = None, title: str = "Invoice Update", notification_type: str = "Info", requires_action: bool = False) -> bool:
        """Send notification to user"""
        try:
            db = Database()
            
            timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            
            # Insert notification with all required fields
            query = """
                INSERT INTO Notifications (UserId, Title, Message, CreatedDate, IsRead, Type, InvoiceId, RequiresAction, ActionTaken)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """
            
            values = (
                user_id,
                title,
                message,
                timestamp,
                0,  # IsRead = False
                notification_type,
                invoice_id,
                1 if requires_action else 0,
                0   # ActionTaken = False
            )
            
            db.cursor.execute(query, values)
            db.connection.commit()
            db.close()
            return True
            
        except Exception as e:
            print(f"Error sending notification: {e}")
            return False

    def get_invoice_stakeholders(self, invoice_id: int) -> dict:
        """Get seller and buyer user IDs for an invoice"""
        try:
            db = Database()
            
            query = """
                SELECT i.SellerId, i.BuyerId, i.InvoiceNumber,
                       seller_users.Id as SellerUserId, buyer_users.Id as BuyerUserId,
                       seller_users.Name as SellerName, buyer_users.Name as BuyerName
                FROM Invoices i
                LEFT JOIN Users seller_users ON seller_users.OrganizationId = i.SellerId AND seller_users.Role = 2
                LEFT JOIN Users buyer_users ON buyer_users.OrganizationId = i.BuyerId AND buyer_users.Role = 2
                WHERE i.Id = ?
            """
            
            db.cursor.execute(query, (invoice_id,))
            result = db.cursor.fetchone()
            db.close()
            
            if result:
                return {
                    'seller_org_id': result[0],
                    'buyer_org_id': result[1],
                    'invoice_number': result[2],
                    'seller_user_id': result[3],
                    'buyer_user_id': result[4],
                    'seller_name': result[5],
                    'buyer_name': result[6]
                }
            return {}
            
        except Exception as e:
            print(f"Error getting invoice stakeholders: {e}")
            return {}

    def get_real_invoices_by_status(self, status: int) -> List:
        """Get real invoices from database by status"""
        try:
            db = Database()
            
            query = """
                SELECT i.Id, i.InvoiceNumber, i.Amount, i.Description, i.IssueDate, i.DueDate,
                       seller.Name as SellerName, buyer.Name as BuyerName, i.Status
                FROM Invoices i
                LEFT JOIN Organizations seller ON i.SellerId = seller.Id
                LEFT JOIN Organizations buyer ON i.BuyerId = buyer.Id
                WHERE i.Status = ?
                ORDER BY i.IssueDate DESC
            """
            
            db.cursor.execute(query, (status,))
            results = db.cursor.fetchall()
            db.close()
            
            invoices = []
            for row in results:
                invoices.append({
                    'id': row[0],
                    'number': row[1],
                    'amount': float(row[2]) if row[2] else 0.0,
                    'description': row[3],
                    'issue_date': row[4],
                    'due_date': row[5],
                    'seller_name': row[6],
                    'buyer_name': row[7],
                    'status': row[8]
                })
            
            return invoices
            
        except Exception as e:
            print(f"Error fetching invoices by status: {e}")
            return []

    def create_accounting_entry(self, transaction_type: str, amount: float, invoice_id: int, description: str, seller_org_id: int = None, buyer_org_id: int = None) -> bool:
        """Create accounting journal entries for different transaction types"""
        try:
            db = Database()
            timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            
            # Generate transaction reference
            trans_ref = f"{transaction_type.upper()}-{datetime.now().strftime('%Y%m%d-%H%M%S')}"
            
            # Create journal entry header
            je_query = """
                INSERT INTO JournalEntries (TransactionReference, TransactionDate, Description, 
                                          OrganizationId, InvoiceId, Status, PostedDate, PostedByUserId)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """
            
            db.cursor.execute(je_query, (
                trans_ref,
                timestamp,
                description,
                1,  # Bank organization ID
                invoice_id,
                1,  # Status: Posted
                timestamp,
                self.current_user['id']
            ))
            
            journal_entry_id = db.cursor.lastrowid
            
            # Create journal entry lines based on transaction type
            if transaction_type in ["VALIDATION", "APPROVAL"]:
                # No monetary entries for validation/approval - just documentation
                # Add a memo entry
                self.create_journal_line(db, journal_entry_id, 1, "0", "0", f"Memo: {description}", seller_org_id)
                
            elif transaction_type == "FUNDING":
                # Dr: Loans to Customers (1300)    Cr: Cash (1100)
                self.create_journal_line(db, journal_entry_id, 3, str(amount), "0", f"Loan advance for invoice funding - {description}", seller_org_id)  # Debit
                self.create_journal_line(db, journal_entry_id, 1, "0", str(amount), f"Cash disbursement for invoice funding - {description}", seller_org_id)  # Credit
                
            elif transaction_type == "PAYMENT":
                # Dr: Cash (1100)    Cr: Loans to Customers (1300)
                self.create_journal_line(db, journal_entry_id, 1, str(amount), "0", f"Payment received - {description}", buyer_org_id)  # Debit
                self.create_journal_line(db, journal_entry_id, 3, "0", str(amount), f"Loan repayment - {description}", buyer_org_id)  # Credit
                
            elif transaction_type == "SELLER_PAYMENT":
                # Dr: Cash (1100)    Cr: Cash (1100) - This represents the actual payment to seller
                # This is essentially a memo entry since the cash was already debited in FUNDING
                self.create_journal_line(db, journal_entry_id, 1, "0", "0", f"Memo: Seller payment processed - {description}", seller_org_id)
                
            elif transaction_type == "FEE_INCOME":
                # Dr: Cash (1100)    Cr: Fee Income (4200)
                self.create_journal_line(db, journal_entry_id, 1, str(amount), "0", f"Fee income earned - {description}", 1)  # Debit
                self.create_journal_line(db, journal_entry_id, 14, "0", str(amount), f"Fee income earned - {description}", 1)  # Credit
                
            elif transaction_type == "INTEREST_INCOME":
                # Dr: Accounts Receivable (1200)    Cr: Interest Income (4100)
                self.create_journal_line(db, journal_entry_id, 2, str(amount), "0", f"Interest income accrued - {description}", seller_org_id)  # Debit
                self.create_journal_line(db, journal_entry_id, 13, "0", str(amount), f"Interest income accrued - {description}", seller_org_id)  # Credit
            
            elif transaction_type == "REJECTION":
                # Memo entry for rejections
                self.create_journal_line(db, journal_entry_id, 1, "0", "0", f"Memo: {description}", seller_org_id)
                
            elif transaction_type == "MATURITY":
                # Handle matured invoices - may need special accounting
                self.create_journal_line(db, journal_entry_id, 1, "0", "0", f"Memo: {description}", buyer_org_id)
            
            db.connection.commit()
            db.close()
            
            print(f"Accounting entry created: {trans_ref}")
            return True
            
        except Exception as e:
            print(f"Error creating accounting entry: {e}")
            return False

    def create_journal_line(self, db, journal_entry_id: int, account_id: int, debit_amount: str, credit_amount: str, description: str, org_id: int = None):
        """Create individual journal entry line"""
        line_query = """
            INSERT INTO JournalEntryLines (JournalEntryId, AccountId, DebitAmount, CreditAmount, Description, OrganizationId)
            VALUES (?, ?, ?, ?, ?, ?)
        """
        
        db.cursor.execute(line_query, (
            journal_entry_id,
            account_id,
            debit_amount,
            credit_amount,
            description,
            org_id
        ))

    def update_account_balance(self, account_id: int, amount: float, is_debit: bool) -> bool:
        """Update account balance based on debit/credit"""
        try:
            # Use the existing database connection passed in create_accounting_entry
            # This function should be called within a transaction context
            return True  # Balance updates will be handled in a batch at period end
            
        except Exception as e:
            print(f"Error updating account balance: {e}")
            return False

    def update_credit_utilization(self, organization_id: int, amount: float, is_utilization: bool) -> bool:
        """Update credit utilization for an organization"""
        try:
            db = Database()
            
            # Get current facility info
            query = """
                SELECT cf.Id, cf.Limit, cf.UtilizedAmount 
                FROM CreditFacilities cf 
                WHERE cf.OrganizationId = ? AND cf.IsActive = 1
            """
            db.cursor.execute(query, (organization_id,))
            facility = db.cursor.fetchone()
            
            if not facility:
                print(f"No active credit facility found for organization {organization_id}")
                db.close()
                return False
            
            facility_id, limit, current_utilization = facility
            current_utilization = float(current_utilization) if current_utilization else 0.0
            limit_amount = float(limit) if limit else 0.0
            
            if is_utilization:
                # Increase utilization (when funding)
                new_utilization = current_utilization + amount
                if new_utilization > limit_amount:
                    print(f"Credit limit exceeded! Available: ${limit_amount - current_utilization:,.2f}, Requested: ${amount:,.2f}")
                    db.close()
                    return False
            else:
                # Decrease utilization (when payment received)
                new_utilization = max(0, current_utilization - amount)
            
            # Update utilization
            update_query = "UPDATE CreditFacilities SET UtilizedAmount = ? WHERE Id = ?"
            db.cursor.execute(update_query, (str(new_utilization), facility_id))
            db.connection.commit()
            
            print(f"Credit utilization updated: ${current_utilization:,.2f} → ${new_utilization:,.2f}")
            print(f"Available credit: ${limit_amount - new_utilization:,.2f}")
            
            db.close()
            return True
            
        except Exception as e:
            print(f"Error updating credit utilization: {e}")
            return False

    def check_credit_availability(self, organization_id: int, required_amount: float) -> bool:
        """Check if organization has sufficient credit available"""
        try:
            db = Database()
            
            query = """
                SELECT cf.Limit, cf.UtilizedAmount 
                FROM CreditFacilities cf 
                WHERE cf.OrganizationId = ? AND cf.IsActive = 1
            """
            db.cursor.execute(query, (organization_id,))
            facility = db.cursor.fetchone()
            
            if not facility:
                db.close()
                return False
            
            limit_amount = float(facility[0]) if facility[0] else 0.0
            utilized_amount = float(facility[1]) if facility[1] else 0.0
            available = limit_amount - utilized_amount
            
            db.close()
            return available >= required_amount
            
        except Exception as e:
            print(f"Error checking credit availability: {e}")
            return False

def main():
    """Main function to run the bank portal"""
    try:
        app = BankApplication()
        app.run()
    except KeyboardInterrupt:
        print("\n\nApplication interrupted by user. Goodbye!")
    except Exception as e:
        print(f"\nAn error occurred: {e}")
        print("Please contact system administrator.")


if __name__ == "__main__":
    main()