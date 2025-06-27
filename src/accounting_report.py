#!/usr/bin/env python3
"""
Comprehensive accounting report to show all entries and verify they're working
"""

import sqlite3
from database import Database

def accounting_report():
    """Generate comprehensive accounting report"""
    print("=" * 60)
    print("         SUPPLY CHAIN FINANCE - ACCOUNTING REPORT")
    print("=" * 60)
    
    try:
        db = Database()
        
        # 1. Journal Entries Summary
        print("\n1. JOURNAL ENTRIES SUMMARY")
        print("-" * 40)
        
        db.cursor.execute("""
            SELECT je.Id, je.TransactionReference, je.TransactionDate, je.Description,
                   i.InvoiceNumber, je.Status
            FROM JournalEntries je
            LEFT JOIN Invoices i ON je.InvoiceId = i.Id
            ORDER BY je.Id
        """)
        
        journal_entries = db.cursor.fetchall()
        
        for entry in journal_entries:
            print(f"JE #{entry[0]}: {entry[1]}")
            print(f"   Date: {entry[2][:10]}")
            print(f"   Invoice: {entry[4] if entry[4] else 'N/A'}")
            print(f"   Description: {entry[3]}")
            print(f"   Status: {'Posted' if entry[5] == 1 else 'Draft'}")
            print()
        
        # 2. Journal Entry Lines Detail
        print("\n2. JOURNAL ENTRY LINES DETAIL")
        print("-" * 40)
        
        for entry in journal_entries:
            je_id = entry[0]
            je_ref = entry[1]
            
            print(f"\nJournal Entry: {je_ref}")
            
            db.cursor.execute("""
                SELECT jel.Id, acc.AccountCode, acc.AccountName, 
                       jel.DebitAmount, jel.CreditAmount, jel.Description
                FROM JournalEntryLines jel
                JOIN Accounts acc ON jel.AccountId = acc.Id
                WHERE jel.JournalEntryId = ?
                ORDER BY jel.Id
            """, (je_id,))
            
            lines = db.cursor.fetchall()
            
            total_debit = 0.0
            total_credit = 0.0
            
            for line in lines:
                debit = float(line[3]) if line[3] and line[3] != '0' else 0.0
                credit = float(line[4]) if line[4] and line[4] != '0' else 0.0
                
                total_debit += debit
                total_credit += credit
                
                if debit > 0:
                    print(f"   Dr  {line[1]} - {line[2]:<25} ${debit:>10,.2f}")
                elif credit > 0:
                    print(f"       Cr  {line[1]} - {line[2]:<23} ${credit:>10,.2f}")
                else:
                    print(f"   Memo: {line[5]}")
            
            print(f"   {'='*50}")
            print(f"   Total Debits:  ${total_debit:>10,.2f}")
            print(f"   Total Credits: ${total_credit:>10,.2f}")
            print(f"   Difference:    ${abs(total_debit - total_credit):>10,.2f}")
            print()
        
        # 3. Chart of Accounts with Balances
        print("\n3. CHART OF ACCOUNTS")
        print("-" * 40)
        
        db.cursor.execute("""
            SELECT AccountCode, AccountName, Type, Balance
            FROM Accounts
            ORDER BY AccountCode
        """)
        
        accounts = db.cursor.fetchall()
        
        account_types = {
            0: "Asset",
            1: "Liability", 
            2: "Equity",
            3: "Revenue",
            4: "Expense"
        }
        
        current_type = None
        type_totals = {0: 0, 1: 0, 2: 0, 3: 0, 4: 0}
        
        for account in accounts:
            acc_type = account[2]
            balance = float(account[3]) if account[3] else 0.0
            type_totals[acc_type] += balance
            
            if current_type != acc_type:
                if current_type is not None:
                    print()
                print(f"\n{account_types[acc_type].upper()}S:")
                current_type = acc_type
            
            balance_str = f"${balance:,.2f}" if balance != 0 else "$0.00"
            print(f"   {account[0]} - {account[1]:<30} {balance_str:>15}")
        
        # 4. Balance Sheet Summary
        print("\n\n4. BALANCE SHEET SUMMARY")
        print("-" * 40)
        
        assets = type_totals[0]
        liabilities = type_totals[1] 
        equity = type_totals[2]
        revenues = type_totals[3]
        expenses = type_totals[4]
        
        print(f"Total Assets:      ${assets:>15,.2f}")
        print(f"Total Liabilities: ${liabilities:>15,.2f}")
        print(f"Total Equity:      ${equity:>15,.2f}")
        print(f"Total Revenues:    ${revenues:>15,.2f}")
        print(f"Total Expenses:    ${expenses:>15,.2f}")
        print("-" * 40)
        print(f"Net Worth:         ${assets - liabilities:>15,.2f}")
        print(f"Net Income:        ${revenues - expenses:>15,.2f}")
        
        # 5. Transaction History
        print("\n\n5. TRANSACTION HISTORY")
        print("-" * 40)
        
        try:
            db.cursor.execute("""
                SELECT t.Id, i.InvoiceNumber, t.Amount, t.TransactionDate, t.Type, t.Description
                FROM Transactions t
                LEFT JOIN Invoices i ON t.InvoiceId = i.Id
                ORDER BY t.Id
            """)
            
            transactions = db.cursor.fetchall()
            
            # Transaction type mapping
            type_names = {
                1: "Funding",
                2: "Payment", 
                3: "Fee",
                4: "Interest"
            }
            
            for txn in transactions:
                amount = float(txn[2]) if txn[2] else 0.0
                txn_type = type_names.get(txn[4], f"Type {txn[4]}")
                print(f"Transaction #{txn[0]}")
                print(f"   Invoice: {txn[1] if txn[1] else 'N/A'}")
                print(f"   Amount: ${amount:,.2f}")
                print(f"   Date: {txn[3]}")
                print(f"   Type: {txn_type}")
                print(f"   Description: {txn[5]}")
                print()
                
        except Exception as e:
            print(f"Error retrieving transactions: {e}")
        
        db.close()
        
    except Exception as e:
        print(f"Error generating report: {e}")

if __name__ == "__main__":
    accounting_report()
