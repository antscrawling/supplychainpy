"""
Supply Chain Finance - Client Portal
Python implementation of the client portal system
"""
import os
import sys
from datetime import datetime
from typing import Optional, List, Dict, Any

# Add the parent directory to the path to import our modules
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import auth_service
from database import Database


class ClientPortal:
    """Main client portal application"""
    
    def __init__(self):
        self.database = Database()
        self.current_user = None
        self.current_organization = None
    
    def run(self):
        """Main entry point for the client portal"""
        self.clear_screen()
        print("===================================================")
        print("   SUPPLY CHAIN FINANCE - CLIENT PORTAL")
        print("===================================================")
        print()
        
        if self.login():
            self.show_main_menu()
        
        print("\nThank you for using the Supply Chain Finance system. Goodbye!")
    
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
            
            user = auth_service.authenticate(username, password)
            
            if user:
                # Check if user is a client (not bank role 0 or 1)
                if user.get('role') in [0, 1]:  # Bank roles
                    print("\nError: This portal is for clients only. Bank users must use the Bank Portal.")
                    return False
                
                self.current_user = user
                # Use organization from database
                org = user.get('organization', {})
                if org:
                    self.current_organization = org
                else:
                    # Fallback if no organization
                    self.current_organization = {
                        'name': 'Unknown Organization',
                        'is_seller': False,
                        'is_buyer': False,
                        'is_bank': False
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
        """Display and handle the main menu"""
        exit_menu = False
        
        while not exit_menu and self.current_user and self.current_organization:
            # Check for notifications
            notifications = self.get_unread_notifications()
            if notifications:
                print(f"\nYou have {len(notifications)} unread notifications!")
            
            print("\nMAIN MENU")
            
            # Show upload option based on organization type
            if self.current_organization.get('is_seller', False):
                print("1. Upload Invoice (as Seller)")
            elif self.current_organization.get('is_buyer', False):
                print("1. Upload Invoice (as Buyer)")
            
            print("2. View My Invoices")
            print("3. Check Credit Limits")
            print("4. View Account Statement")
            print("5. View Notifications")
            
            if self.current_organization.get('is_buyer', False):
                print("6. Make Payment")
            
            print("0. Logout")
            
            choice = input("\nSelect an option: ").strip()
            
            if choice == "1":
                if self.current_organization.get('is_seller', False):
                    self.upload_seller_invoice()
                elif self.current_organization.get('is_buyer', False):
                    self.upload_buyer_invoice()
                else:
                    print("\nYour organization type cannot upload invoices.")
            elif choice == "2":
                self.view_invoices()
            elif choice == "3":
                self.check_credit_limits()
            elif choice == "4":
                self.view_account_statement()
            elif choice == "5":
                self.view_notifications()
            elif choice == "6":
                if self.current_organization.get('is_buyer', False):
                    self.make_payment()
                else:
                    print("\nInvalid option. Please try again.")
            elif choice == "0":
                exit_menu = True
            else:
                print("\nInvalid option. Please try again.")
    
    def save_invoice_to_database(self, invoice_data: dict) -> bool:
        """Save invoice to the database"""
        try:
            db = Database()
            
            # Insert invoice into database
            query = """
                INSERT INTO Invoices (
                    InvoiceNumber, IssueDate, DueDate, Amount, Description,
                    SellerId, BuyerId, CounterpartyId, Currency, Status, BuyerApproved, SellerAccepted
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """
            
            # Status: 0=New, 1=Validated, 2=Approved, 3=Funded, 4=Paid, 5=Rejected, 6=Funding sent for Seller Approval, 7=Pending Seller approval, 8=Seller Approved
            values = (
                invoice_data['invoice_number'],
                invoice_data['issue_date'].strftime('%d-%m-%Y') if hasattr(invoice_data['issue_date'], 'strftime') else invoice_data['issue_date'],
                invoice_data['due_date'].strftime('%d-%m-%Y') if hasattr(invoice_data['due_date'], 'strftime') else invoice_data['due_date'],
                str(invoice_data['amount']),  # Amount stored as TEXT in schema
                invoice_data['description'],
                invoice_data['seller_id'],
                invoice_data['buyer_id'],
                invoice_data.get('counterparty_id', None),  # Set counterparty properly
                'USD',  # Default currency
                0,  # Status: New
                0,  # BuyerApproved: False
                1   # SellerAccepted: True (seller uploaded it)
            )
            
            db.cursor.execute(query, values)
            db.connection.commit()
            db.close()
            
            return True
            
        except Exception as e:
            print(f"Error saving invoice to database: {e}")
            return False

    def get_user_invoices(self) -> List[Dict[str, Any]]:
        """Get invoices for the current user's organization"""
        if not self.current_user or not self.current_organization:
            return []
        
        try:
            db = Database()
            org_id = self.current_organization['id']
            
            # Get invoices where user's org is either buyer or seller
            query = """
                SELECT i.Id, i.InvoiceNumber, i.IssueDate, i.DueDate, i.Amount, 
                       i.Description, i.Status, i.Currency,
                       seller.Name as SellerName, buyer.Name as BuyerName,
                       i.SellerId, i.BuyerId
                FROM Invoices i
                LEFT JOIN Organizations seller ON i.SellerId = seller.Id
                LEFT JOIN Organizations buyer ON i.BuyerId = buyer.Id
                WHERE i.SellerId = ? OR i.BuyerId = ?
                ORDER BY i.IssueDate DESC
            """
            
            db.cursor.execute(query, (org_id, org_id))
            results = db.cursor.fetchall()
            db.close()
            
            invoices = []
            status_map = {
                0: 'New',
                1: 'Validated', 
                2: 'Approved', 
                3: 'Funded',
                4: 'Paid', 
                5: 'Rejected',
                6: 'Matured',
                7: 'Funding Sent for Seller Approval',
                8: 'Pending Seller Approval',
                9: 'Seller Approved'
            }
            
            for row in results:
                invoice = {
                    'id': row[0],
                    'number': row[1],
                    'issue_date': row[2],
                    'due_date': row[3],
                    'amount': float(row[4]) if row[4] else 0.0,
                    'description': row[5],
                    'status': status_map.get(row[6], 'Unknown'),
                    'currency': row[7],
                    'seller_name': row[8],
                    'buyer_name': row[9],
                    'seller_id': row[10],
                    'buyer_id': row[11]
                }
                invoices.append(invoice)
            
            return invoices
            
        except Exception as e:
            print(f"Error fetching invoices: {e}")
            return []

    def get_unread_notifications(self) -> List[Dict[str, Any]]:
        """Get unread notifications for current user"""
        if not self.current_user:
            return []
        
        try:
            # Placeholder implementation - would query database for notifications
            # where user_id = current_user.id and is_read = False
            return []
        except Exception as e:
            print(f"Error fetching notifications: {e}")
            return []
    
    def upload_seller_invoice(self):
        """Handle seller invoice upload"""
        self.clear_screen()
        print("UPLOAD NEW INVOICE (AS SELLER)")
        print("==============================\n")
        
        try:
            # Get available buyers
            buyers = self.get_buyer_organizations()
            if not buyers:
                print("No buyers available in the system.")
                input("\nPress Enter to continue...")
                return
            
            # Display buyers
            print("Available Buyers:")
            for i, buyer in enumerate(buyers, 1):
                print(f"{i}. {buyer.get('name', 'Unknown')}")
            
            # Select buyer
            try:
                buyer_choice = int(input("\nSelect buyer (number): ")) - 1
                if buyer_choice < 0 or buyer_choice >= len(buyers):
                    print("Invalid selection.")
                    input("\nPress Enter to continue...")
                    return
                
                selected_buyer = buyers[buyer_choice]
                
                # Get invoice details
                invoice_number = input("Invoice Number: ").strip()
                if not invoice_number:
                    print("Invoice number is required.")
                    input("\nPress Enter to continue...")
                    return
                
                try:
                    amount = float(input("Amount: $"))
                except ValueError:
                    print("Invalid amount format.")
                    input("\nPress Enter to continue...")
                    return
                
                description = input("Description: ").strip()
                
                # Get invoice start date (issue date)
                issue_date = input("Invoice Issue Date (DD-MM-YYYY): ").strip()
                try:
                    issue_date_obj = datetime.strptime(issue_date, '%d-%m-%Y')
                except ValueError:
                    print("Invalid issue date format. Please use DD-MM-YYYY.")
                    input("\nPress Enter to continue...")
                    return
                
                # Get due date
                due_date = input("Due Date (DD-MM-YYYY): ").strip()
                try:
                    due_date_obj = datetime.strptime(due_date, '%d-%m-%Y')
                except ValueError:
                    print("Invalid due date format. Please use DD-MM-YYYY.")
                    input("\nPress Enter to continue...")
                    return
                
                # Validate that due date is after issue date
                if due_date_obj <= issue_date_obj:
                    print("Due date must be after issue date.")
                    input("\nPress Enter to continue...")
                    return
                
                # Calculate loan tenor (days between issue and due date)
                tenor_days = (due_date_obj - issue_date_obj).days
                
                # Create invoice (placeholder)
                print(f"\nCreating invoice...")
                print(f"Invoice Number: {invoice_number}")
                print(f"Buyer: {selected_buyer.get('name')}")
                print(f"Amount: ${amount:.2f}")
                print(f"Description: {description}")
                print(f"Issue Date: {issue_date}")
                print(f"Due Date: {due_date}")
                print(f"Loan Tenor: {tenor_days} days")
                
                # TODO: Actually save to database with all fields including issue_date and tenor
                if self.save_invoice_to_database({
                    'invoice_number': invoice_number,
                    'issue_date': issue_date_obj,
                    'due_date': due_date_obj,
                    'amount': amount,
                    'description': description,
                    'seller_id': self.current_organization['id'],
                    'buyer_id': selected_buyer['id'],
                    'counterparty_id': selected_buyer['id']  # buyer is counterparty when seller uploads
                }):
                    print("\nInvoice uploaded successfully!")
                else:
                    print("\nFailed to upload invoice to database.")
                
            except ValueError:
                print("Invalid input. Please enter a number.")
                
        except Exception as e:
            print(f"Error uploading invoice: {e}")
        
        input("\nPress Enter to continue...")
    
    def upload_buyer_invoice(self):
        """Handle buyer invoice upload"""
        self.clear_screen()
        print("UPLOAD NEW INVOICE (AS BUYER)")
        print("=============================\n")
        
        try:
            # Get available sellers
            sellers = self.get_seller_organizations()
            if not sellers:
                print("No sellers available in the system.")
                input("\nPress Enter to continue...")
                return
            
            # Display sellers
            print("Available Sellers:")
            for i, seller in enumerate(sellers, 1):
                print(f"{i}. {seller.get('name', 'Unknown')}")
            
            # Select seller
            try:
                seller_choice = int(input("\nSelect seller (number): ")) - 1
                if seller_choice < 0 or seller_choice >= len(sellers):
                    print("Invalid selection.")
                    input("\nPress Enter to continue...")
                    return
                
                selected_seller = sellers[seller_choice]
                
                # Get invoice details (similar to seller upload)
                invoice_number = input("Invoice Number: ").strip()
                if not invoice_number:
                    print("Invoice number is required.")
                    input("\nPress Enter to continue...")
                    return
                
                try:
                    amount = float(input("Amount: $"))
                except ValueError:
                    print("Invalid amount format.")
                    input("\nPress Enter to continue...")
                    return
                
                description = input("Description: ").strip()
                
                # Get invoice start date (issue date)
                issue_date = input("Invoice Issue Date (DD-MM-YYYY): ").strip()
                try:
                    issue_date_obj = datetime.strptime(issue_date, '%d-%m-%Y')
                except ValueError:
                    print("Invalid issue date format. Please use DD-MM-YYYY.")
                    input("\nPress Enter to continue...")
                    return
                
                # Get due date
                due_date = input("Due Date (DD-MM-YYYY): ").strip()
                try:
                    due_date_obj = datetime.strptime(due_date, '%d-%m-%Y')
                except ValueError:
                    print("Invalid due date format. Please use DD-MM-YYYY.")
                    input("\nPress Enter to continue...")
                    return
                
                # Validate that due date is after issue date
                if due_date_obj <= issue_date_obj:
                    print("Due date must be after issue date.")
                    input("\nPress Enter to continue...")
                    return
                
                # Calculate loan tenor (days between issue and due date)
                tenor_days = (due_date_obj - issue_date_obj).days
                
                # Create invoice
                print(f"\nCreating invoice...")
                print(f"Invoice Number: {invoice_number}")
                print(f"Seller: {selected_seller.get('name')}")
                print(f"Amount: ${amount:.2f}")
                print(f"Description: {description}")
                print(f"Issue Date: {issue_date}")
                print(f"Due Date: {due_date}")
                print(f"Loan Tenor: {tenor_days} days")
                
                # TODO: Actually save to database with all fields
                if self.save_invoice_to_database({
                    'invoice_number': invoice_number,
                    'issue_date': issue_date_obj,
                    'due_date': due_date_obj,
                    'amount': amount,
                    'description': description,
                    'seller_id': selected_seller['id'],
                    'buyer_id': self.current_organization['id'],
                    'counterparty_id': selected_seller['id']  # seller is counterparty
                }):
                    print("\nInvoice uploaded successfully!")
                else:
                    print("\nFailed to upload invoice to database.")
                
            except ValueError:
                print("Invalid input. Please enter a number.")
                
        except Exception as e:
            print(f"Error uploading invoice: {e}")
        
        input("\nPress Enter to continue...")
    
    def view_invoices(self):
        """View invoices for current organization"""
        self.clear_screen()
        print("MY INVOICES")
        print("===========\n")
        
        try:
            # Get real invoices from database
            invoices = self.get_user_invoices()
            
            if invoices:
                print("Your invoices:")
                for i, invoice in enumerate(invoices, 1):
                    # Show counterparty name based on user's role
                    if invoice['seller_id'] == self.current_organization['id']:
                        # User is seller, show buyer name
                        counterparty = invoice['buyer_name'] or 'Unknown Buyer'
                        role = 'Seller'
                    else:
                        # User is buyer, show seller name
                        counterparty = invoice['seller_name'] or 'Unknown Seller'
                        role = 'Buyer'
                    
                    print(f"{i}. {invoice['number']} | {counterparty} | ${invoice['amount']:,.2f} | {invoice['status']} | Due: {invoice['due_date']} | ({role})")
                print()
                
                # Ask if they want to view details or go back
                choice = input("Enter invoice number to view details (or 0 to go back): ").strip()
                
                if choice == "0":
                    return
                    
                try:
                    index = int(choice) - 1
                    if 0 <= index < len(invoices):
                        self.view_invoice_details(invoices[index])
                    else:
                        print("Invalid invoice number.")
                except ValueError:
                    print("Invalid input. Please enter a number.")
            else:
                print("No invoices found.")
            
        except Exception as e:
            print(f"Error fetching invoices: {e}")
        
        input("\nPress Enter to continue...")
    
    def view_invoice_details(self, invoice: dict):
        """View detailed information about an invoice and provide actions based on status"""
        self.clear_screen()
        print(f"INVOICE DETAILS: {invoice['number']}")
        print("=" * (17 + len(invoice['number'])))
        print()
        
        # Display invoice details
        print(f"Invoice Number: {invoice['number']}")
        print(f"Amount: ${invoice['amount']:,.2f}")
        print(f"Issue Date: {invoice['issue_date']}")
        print(f"Due Date: {invoice['due_date']}")
        print(f"Status: {invoice['status']}")
        print(f"Seller: {invoice['seller_name']}")
        print(f"Buyer: {invoice['buyer_name']}")
        
        # If there's a funded amount and discount rate, show those details
        if invoice.get('funded_amount'):
            print(f"Funded Amount: ${invoice['funded_amount']:,.2f}")
            print(f"Discount Rate: {invoice['discount_rate']}%")
            print(f"Discount Amount: ${float(invoice['amount']) - float(invoice['funded_amount']):,.2f}")
        
        print()
        
        # If user is the seller and invoice status is "Pending Seller Approval"
        if (invoice['seller_id'] == self.current_organization['id'] and 
            invoice['status_code'] == 6):  # Pending Seller Approval
            
            print("This invoice has a pending early payment offer from the bank.")
            print(f"You can receive ${invoice['funded_amount']:,.2f} now instead of ${invoice['amount']:,.2f} on {invoice['due_date']}.")
            print(f"Discount rate: {invoice['discount_rate']}% (${float(invoice['amount']) - float(invoice['funded_amount']):,.2f} discount)")
            
            choice = input("\nDo you want to: (1) Accept early payment, (2) Reject offer, or (0) Decide later? ")
            
            if choice == "1":
                self.approve_early_payment_offer(invoice)
            elif choice == "2":
                self.reject_early_payment_offer(invoice)
        
        input("\nPress Enter to continue...")
    
    def approve_early_payment_offer(self, invoice: dict):
        """Approve an early payment offer for a buyer-uploaded invoice"""
        try:
            db = Database()
            
            # Update invoice status to "Seller Approved" (7)
            db.cursor.execute("""
                UPDATE Invoices 
                SET Status = 7 
                WHERE Id = ?
            """, (invoice['id'],))
            
            db.connection.commit()
            
            # Send notification to bank
            # In a real system, this would also notify the bank that the seller approved
            
            print("Early payment offer accepted successfully!")
            print("The bank will process your payment shortly.")
            
            db.close()
            
            # Create accounting entry for audit trail
            # In a real system, this would create an entry in the accounting system
            
            return True
        except Exception as e:
            print(f"Error approving early payment offer: {e}")
            return False
    
    def reject_early_payment_offer(self, invoice: dict):
        """Reject an early payment offer for a buyer-uploaded invoice"""
        try:
            db = Database()
            
            # In a real system, we would set a different status for rejected offers
            # For now, we'll just reset it to "Approved" (3) status
            db.cursor.execute("""
                UPDATE Invoices 
                SET Status = 3
                WHERE Id = ?
            """, (invoice['id'],))
            
            db.connection.commit()
            
            # Send notification to bank
            # In a real system, this would also notify the bank that the seller rejected
            
            print("Early payment offer rejected.")
            print("Invoice will be paid on the original due date.")
            
            db.close()
            return True
        except Exception as e:
            print(f"Error rejecting early payment offer: {e}")
            return False
    
    def check_credit_limits(self):
        """Check credit limits for current organization"""
        self.clear_screen()
        print("CREDIT LIMITS")
        print("=============\n")
        
        try:
            org_name = self.current_organization.get('name', 'Unknown')
            org_id = self.current_organization.get('id')
            
            if not org_id:
                print("Organization ID not found.")
                input("\nPress Enter to continue...")
                return
            
            db = Database()
            
            # Get credit facilities for current organization
            query = """
                SELECT f.Type, f.TotalLimit, f.CurrentUtilization 
                FROM Facilities f 
                JOIN CreditLimits cl ON f.CreditLimitInfoId = cl.Id
                WHERE cl.OrganizationId = ?
            """
            db.cursor.execute(query, (org_id,))
            facilities = db.cursor.fetchall()
            
            print(f"Organization: {org_name}")
            print()
            
            if not facilities:
                print("No credit facilities found for your organization.")
            else:
                total_limit = 0.0
                total_utilized = 0.0
                
                print(f"{'Facility Type':<20} {'Total Limit':<15} {'Used':<15} {'Available':<15} {'Utilization':<12}")
                print("-" * 85)
                
                for facility in facilities:
                    facility_type = "Invoice Finance" if facility[0] == 0 else f"Type {facility[0]}"
                    limit_amount = float(facility[1]) if facility[1] else 0.0
                    utilized_amount = float(facility[2]) if facility[2] else 0.0
                    available_amount = limit_amount - utilized_amount
                    utilization_pct = (utilized_amount / limit_amount * 100) if limit_amount > 0 else 0
                    
                    total_limit += limit_amount
                    total_utilized += utilized_amount
                    
                    print(f"{facility_type:<20} ${limit_amount:<14,.0f} ${utilized_amount:<14,.0f} ${available_amount:<14,.0f} {utilization_pct:<11.1f}%")
                
                if len(facilities) > 1:
                    print("-" * 85)
                    total_available = total_limit - total_utilized
                    total_utilization_pct = (total_utilized / total_limit * 100) if total_limit > 0 else 0
                    print(f"{'TOTAL':<20} ${total_limit:<14,.0f} ${total_utilized:<14,.0f} ${total_available:<14,.0f} {total_utilization_pct:<11.1f}%")
            
            db.close()
            
        except Exception as e:
            print(f"Error fetching credit limits: {e}")
        
        input("\nPress Enter to continue...")
    
    def view_account_statement(self):
        """View account statement"""
        self.clear_screen()
        print("ACCOUNT STATEMENT")
        print("=================\n")
        
        try:
            print("Recent Transactions:")
            print("Date       | Type     | Description      | Amount    | Balance")
            print("-" * 65)
            print("2024-01-15 | Credit   | Invoice Payment  | +$5,000   | $15,000")
            print("2024-01-10 | Debit    | Service Fee      | -$25      | $10,000")
            print("2024-01-05 | Credit   | Invoice Payment  | +$3,500   | $10,025")
            print("-" * 65)
            print("Current Balance: $15,000.00")
            
        except Exception as e:
            print(f"Error fetching account statement: {e}")
        
        input("\nPress Enter to continue...")
    
    def view_notifications(self):
        """View and manage notifications"""
        self.clear_screen()
        print("NOTIFICATIONS")
        print("=============\n")
        
        try:
            notifications = self.get_all_notifications()
            if not notifications:
                print("No notifications found.")
            else:
                for i, notification in enumerate(notifications, 1):
                    status = "READ" if notification.get('is_read') else "UNREAD"
                    print(f"{i}. [{status}] {notification.get('message', 'No message')}")
                    print(f"   Date: {notification.get('created_at', 'Unknown')}")
                    print()
            
        except Exception as e:
            print(f"Error fetching notifications: {e}")
        
        input("\nPress Enter to continue...")
    
    def make_payment(self):
        """Handle payment processing"""
        self.clear_screen()
        print("MAKE PAYMENT")
        print("============\n")
        
        try:
            # Get pending invoices for payment
            print("Pending Invoices for Payment:")
            print("1. INV-001 | ABC Corp | $5,000.00 | Due: 2024-02-15")
            print("3. INV-003 | DEF Inc  | $7,200.00 | Due: 2024-02-20")
            
            choice = input("\nSelect invoice to pay (number) or 0 to cancel: ").strip()
            
            if choice == "0":
                return
            elif choice in ["1", "3"]:
                amount = "5,000.00" if choice == "1" else "7,200.00"
                vendor = "ABC Corp" if choice == "1" else "DEF Inc"
                
                confirm = input(f"\nConfirm payment of ${amount} to {vendor}? (y/n): ").strip().lower()
                if confirm == 'y':
                    print(f"\nProcessing payment of ${amount} to {vendor}...")
                    print("Payment processed successfully!")
                else:
                    print("Payment cancelled.")
            else:
                print("Invalid selection.")
                
        except Exception as e:
            print(f"Error processing payment: {e}")
        
        input("\nPress Enter to continue...")
    
    def get_buyer_organizations(self) -> List[Dict[str, Any]]:
        """Get list of buyer organizations from database"""
        try:
            db = Database()
            query = "SELECT Id, Name FROM Organizations WHERE IsBuyer = 1"
            db.cursor.execute(query)
            results = db.cursor.fetchall()
            db.close()
            
            buyers = []
            for row in results:
                buyers.append({"id": row[0], "name": row[1], "is_buyer": True})
            
            return buyers
        except Exception as e:
            print(f"Error fetching buyer organizations: {e}")
            return []
    
    def get_seller_organizations(self) -> List[Dict[str, Any]]:
        """Get list of seller organizations from database"""
        try:
            db = Database()
            query = "SELECT Id, Name FROM Organizations WHERE IsSeller = 1"
            db.cursor.execute(query)
            results = db.cursor.fetchall()
            db.close()
            
            sellers = []
            for row in results:
                sellers.append({"id": row[0], "name": row[1], "is_seller": True})
            
            return sellers
        except Exception as e:
            print(f"Error fetching seller organizations: {e}")
            return []
    
    def get_all_notifications(self) -> List[Dict[str, Any]]:
        """Get all notifications for current user"""
        # TODO: Query database for all notifications for current user
        return [
            {"id": 1, "message": "New invoice INV-001 received", "is_read": False, "created_at": "2024-01-15 10:30"},
            {"id": 2, "message": "Payment processed for INV-002", "is_read": True, "created_at": "2024-01-10 14:15"},
            {"id": 3, "message": "Credit limit updated", "is_read": False, "created_at": "2024-01-08 09:45"}
        ]
    
    def save_invoice_to_database(self, invoice_data: dict) -> bool:
        """Save invoice to the database"""
        try:
            db = Database()
            
            # Insert invoice into database
            query = """
                INSERT INTO Invoices (
                    InvoiceNumber, IssueDate, DueDate, Amount, Description,
                    SellerId, BuyerId, CounterpartyId, Currency, Status, BuyerApproved, SellerAccepted
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """
            
            # Status: 0=Pending, 1=Approved, 2=Funded, 3=Paid, 4=Rejected
            values = (
                invoice_data['invoice_number'],
                invoice_data['issue_date'],
                invoice_data['due_date'],
                str(invoice_data['amount']),  # Amount stored as TEXT in schema
                invoice_data['description'],
                invoice_data['seller_id'],
                invoice_data.get('buyer_id', None),  # Optional buyer ID
                invoice_data.get('counterparty_id', None),  # Optional counterparty ID
                'USD',  # Default currency
                0,  # Status: Pending
                0,  # BuyerApproved: False
                1   # SellerAccepted: True (seller uploaded it)
            )
            
            db.cursor.execute(query, values)
            db.connection.commit()
            db.close()
            
            return True
            
        except Exception as e:
            print(f"Error saving invoice to database: {e}")
            return False

    def get_user_invoices(self) -> List[Dict[str, Any]]:
        """Get invoices for the current user's organization"""
        if not self.current_user or not self.current_organization:
            return []
        
        try:
            db = Database()
            org_id = self.current_organization['id']
            
            # Get invoices where user's org is either buyer or seller
            query = """
                SELECT i.Id, i.InvoiceNumber, i.IssueDate, i.DueDate, i.Amount, 
                       i.Description, i.Status, i.Currency,
                       seller.Name as SellerName, buyer.Name as BuyerName,
                       i.SellerId, i.BuyerId
                FROM Invoices i
                LEFT JOIN Organizations seller ON i.SellerId = seller.Id
                LEFT JOIN Organizations buyer ON i.BuyerId = buyer.Id
                WHERE i.SellerId = ? OR i.BuyerId = ?
                ORDER BY i.IssueDate DESC
            """
            
            db.cursor.execute(query, (org_id, org_id))
            results = db.cursor.fetchall()
            db.close()
            
            invoices = []
            status_map = {
                0: 'New',
                1: 'Validated', 
                2: 'Approved', 
                3: 'Funded',
                4: 'Paid', 
                5: 'Rejected',
                6: 'Matured',
                7: 'Funding Sent for Seller Approval',
                8: 'Pending Seller Approval',
                9: 'Seller Approved'
            }
            
            for row in results:
                invoice = {
                    'id': row[0],
                    'number': row[1],
                    'issue_date': row[2],
                    'due_date': row[3],
                    'amount': float(row[4]) if row[4] else 0.0,
                    'description': row[5],
                    'status': status_map.get(row[6], 'Unknown'),
                    'currency': row[7],
                    'seller_name': row[8],
                    'buyer_name': row[9],
                    'seller_id': row[10],
                    'buyer_id': row[11]
                }
                invoices.append(invoice)
            
            return invoices
            
        except Exception as e:
            print(f"Error fetching invoices: {e}")
            return []

def main():
    """Main entry point for the client portal"""
    try:
        client_portal = ClientPortal()
        client_portal.run()
    except KeyboardInterrupt:
        print("\n\nExiting client portal...")
    except Exception as e:
        print(f"\nAn error occurred: {e}")
        print("Please contact system administrator.")


if __name__ == "__main__":
    main()

