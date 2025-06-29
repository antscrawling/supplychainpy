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

        # First try to get a validated invoice to fund (Status 1 = Validated)
        validated_invoices = app.get_real_invoices_by_status(1)
        
        # Initialize invoice
        invoice = None
        
        if not validated_invoices:
            print("No validated invoices found, trying to use a non-validated invoice...")
            # Try to use a new invoice (Status 0 = New) and validate it first
            new_invoices = app.get_real_invoices_by_status(0)
            
            if not new_invoices:
                print("No invoices found to test with.")
                return
                
            invoice = new_invoices[0]
            print(f"Using non-validated invoice: {invoice['number']}")
            
            # Validate the invoice first
            print("Validating invoice before funding...")
            if app.update_invoice_status(invoice['id'], 1):
                print(f"Successfully validated invoice {invoice['number']}")
                # Refresh invoice data to ensure we have the updated status
                validated_invoices = app.get_real_invoices_by_status(1)
                for inv in validated_invoices:
                    if inv['number'] == invoice['number']:
                        invoice = inv
                        break
            else:
                print(f"Failed to validate invoice {invoice['number']}")
                return
        else:
            invoice = validated_invoices[0]
            print(f"Using already validated invoice: {invoice['number']}")

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

        if not (result1 and result2):
            print("✗ Failed to create one or both accounting entries")
            return

        print("✓ Both funding accounting entries created successfully!")

        # Check the results in database
        db = Database()
        try:
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
        finally:
            db.close()

    except Exception as e:
        print(f"Error during funding test: {e}")

if __name__ == "__main__":
    test_funding_accounting()
