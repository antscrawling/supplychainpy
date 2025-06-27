#!/usr/bin/env python3
"""
Test script to verify accounting entries are being created properly
"""

import sqlite3
from database import Database

def test_accounting_entries():
    """Test if accounting entries are being created"""
    print("Testing Accounting Entries...")
    print("=" * 40)
    
    try:
        db = Database()
        
        # Check journal entries
        print("\n1. Journal Entries:")
        db.cursor.execute("SELECT COUNT(*) FROM JournalEntries")
        count = db.cursor.fetchone()[0]
        print(f"   Total Journal Entries: {count}")
        
        if count > 0:
            db.cursor.execute("""
                SELECT Id, TransactionReference, TransactionDate, Description, Status 
                FROM JournalEntries 
                ORDER BY Id DESC LIMIT 5
            """)
            recent_entries = db.cursor.fetchall()
            print("\n   Recent Journal Entries:")
            for entry in recent_entries:
                print(f"   ID: {entry[0]}, Ref: {entry[1]}, Date: {entry[2][:10]}, Desc: {entry[3][:50]}")
        
        # Check journal entry lines
        print("\n2. Journal Entry Lines:")
        db.cursor.execute("SELECT COUNT(*) FROM JournalEntryLines")
        count = db.cursor.fetchone()[0]
        print(f"   Total Journal Entry Lines: {count}")
        
        if count > 0:
            db.cursor.execute("""
                SELECT jel.Id, jel.JournalEntryId, acc.AccountCode, acc.AccountName, 
                       jel.DebitAmount, jel.CreditAmount, jel.Description
                FROM JournalEntryLines jel
                JOIN Accounts acc ON jel.AccountId = acc.Id
                ORDER BY jel.Id DESC LIMIT 10
            """)
            recent_lines = db.cursor.fetchall()
            print("\n   Recent Journal Entry Lines:")
            for line in recent_lines:
                print(f"   JE: {line[1]}, Account: {line[2]} - {line[3]}, Dr: {line[4]}, Cr: {line[5]}")
        
        # Check account balances
        print("\n3. Account Balances:")
        db.cursor.execute("""
            SELECT AccountCode, AccountName, Balance 
            FROM Accounts 
            WHERE CAST(Balance AS REAL) != 0 
            ORDER BY AccountCode
        """)
        accounts_with_balance = db.cursor.fetchall()
        
        if accounts_with_balance:
            print("   Accounts with Non-Zero Balances:")
            for account in accounts_with_balance:
                balance = float(account[2]) if account[2] else 0.0
                print(f"   {account[0]} - {account[1]}: ${balance:,.2f}")
        else:
            print("   No accounts with non-zero balances found.")
        
        # Check transactions
        print("\n4. Transactions:")
        db.cursor.execute("SELECT COUNT(*) FROM Transactions")
        count = db.cursor.fetchone()[0]
        print(f"   Total Transactions: {count}")
        
        if count > 0:
            db.cursor.execute("""
                SELECT Id, InvoiceId, Amount, TransactionDate, Type, Description
                FROM Transactions 
                ORDER BY Id DESC LIMIT 5
            """)
            recent_txns = db.cursor.fetchall()
            print("\n   Recent Transactions:")
            for txn in recent_txns:
                type_names = {1: "Funding", 2: "Payment", 3: "Fee", 4: "Interest"}
                txn_type = type_names.get(txn[4], f"Type {txn[4]}")
                print(f"   ID: {txn[0]}, Invoice: {txn[1]}, Amount: ${float(txn[2]):,.2f}, Type: {txn_type}")
        
        # Check invoices by status
        print("\n5. Invoice Status Summary:")
        db.cursor.execute("""
            SELECT Status, COUNT(*) as Count
            FROM Invoices 
            GROUP BY Status
            ORDER BY Status
        """)
        status_summary = db.cursor.fetchall()
        
        status_names = {
            0: "Uploaded",
            1: "Validated", 
            2: "Approved",
            3: "Funded",
            4: "Paid",
            5: "Rejected",
            6: "Matured",
            7: "Discount_Approved"
        }
        
        for status, count in status_summary:
            status_name = status_names.get(status, f"Unknown ({status})")
            print(f"   {status_name}: {count}")
        
        db.close()
        
    except Exception as e:
        print(f"Error: {e}")

def simulate_invoice_validation():
    """Simulate invoice validation to test accounting entry creation"""
    print("\n" + "=" * 50)
    print("SIMULATING INVOICE VALIDATION")
    print("=" * 50)
    
    try:
        from bankportal import BankApplication
        
        # Create bank app instance (without running the UI)
        app = BankApplication()
        
        # Mock current user (bank user)
        app.current_user = {'id': 1, 'name': 'Bank Admin'}
        app.current_organization = {'id': 1, 'name': 'Supply Chain Finance Bank'}
        
        # Get an uploaded invoice to validate
        uploaded_invoices = app.get_real_invoices_by_status(0)  # Status 0 = Uploaded
        
        if uploaded_invoices:
            invoice = uploaded_invoices[0]
            print(f"Testing validation for invoice: {invoice['number']}")
            
            # Test accounting entry creation directly
            stakeholders = app.get_invoice_stakeholders(invoice['id'])
            result = app.create_accounting_entry(
                "VALIDATION", 
                0.0, 
                invoice['id'], 
                f"Test validation for invoice {invoice['number']}", 
                stakeholders.get('seller_org_id'),
                stakeholders.get('buyer_org_id')
            )
            
            if result:
                print("✓ Accounting entry created successfully!")
            else:
                print("✗ Failed to create accounting entry")
        else:
            print("No uploaded invoices found to test with.")
            
    except Exception as e:
        print(f"Error during simulation: {e}")

if __name__ == "__main__":
    test_accounting_entries()
    simulate_invoice_validation()
