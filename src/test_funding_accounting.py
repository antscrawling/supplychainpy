#!/usr/bin/env python3
"""
Test funding accounting entries
"""

from database import Database
from bankportal import BankApplication

def test_funding_accounting():
    """Test funding accounting entry creation"""
    print("Testing Funding Accounting Entries...")
    print("=" * 40)
    
    try:
        # Create bank app instance
        app = BankApplication()
        
        # Mock current user (bank user)
        app.current_user = {'id': 1, 'name': 'Bank Admin'}
        app.current_organization = {'id': 1, 'name': 'Supply Chain Finance Bank'}
        
        # Get a validated invoice to fund
        validated_invoices = app.get_real_invoices_by_status(1)  # Status 1 = Validated
        
        if validated_invoices:
            invoice = validated_invoices[0]
            print(f"Testing funding for invoice: {invoice['number']}")
            print(f"Invoice amount: ${invoice['amount']:,.2f}")
            
            # Simulate funding parameters
            base_rate = 5.0
            margin = 3.0
            final_rate = base_rate + margin
            invoice_amount = invoice['amount']
            discount_amount = invoice_amount * (final_rate / 100)
            funded_amount = invoice_amount - discount_amount
            
            print(f"Funding amount: ${funded_amount:,.2f}")
            print(f"Discount amount: ${discount_amount:,.2f}")
            
            # Test funding accounting entry
            stakeholders = app.get_invoice_stakeholders(invoice['id'])
            
            # 1. Test funding entry
            result1 = app.create_accounting_entry(
                "FUNDING", 
                funded_amount, 
                invoice['id'], 
                f"Test funding for invoice {invoice['number']}", 
                stakeholders.get('seller_org_id'),
                stakeholders.get('buyer_org_id')
            )
            
            # 2. Test interest income entry
            result2 = app.create_accounting_entry(
                "INTEREST_INCOME", 
                discount_amount, 
                invoice['id'], 
                f"Test discount income from invoice {invoice['number']}", 
                stakeholders.get('seller_org_id'),
                stakeholders.get('buyer_org_id')
            )
            
            if result1 and result2:
                print("✓ Both funding accounting entries created successfully!")
                
                # Check the results in database
                db = Database()
                db.cursor.execute("SELECT COUNT(*) FROM JournalEntries")
                total_entries = db.cursor.fetchone()[0]
                print(f"Total journal entries now: {total_entries}")
                
                # Check account balances
                db.cursor.execute("""
                    SELECT AccountCode, AccountName, Balance 
                    FROM Accounts 
                    WHERE CAST(Balance AS REAL) != 0 
                    ORDER BY AccountCode
                """)
                accounts = db.cursor.fetchall()
                
                print("\nUpdated Account Balances:")
                for account in accounts:
                    balance = float(account[2]) if account[2] else 0.0
                    print(f"   {account[0]} - {account[1]}: ${balance:,.2f}")
                
                db.close()
            else:
                print("✗ Failed to create one or both accounting entries")
        else:
            print("No validated invoices found to test with.")
            
    except Exception as e:
        print(f"Error during funding test: {e}")

if __name__ == "__main__":
    test_funding_accounting()
