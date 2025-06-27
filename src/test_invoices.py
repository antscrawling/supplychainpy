#!/usr/bin/env python3
"""
Quick test to check invoice viewing functionality
"""

import sys
import os
sys.path.append('/Users/joseibay/Documents/PythonPrograms/supplychain/src')

from clientportal import ClientPortal
import auth_service

def test_invoice_view():
    print("=== Testing Invoice View ===")
    
    # Test as buyer admin
    print("\n1. Testing as Buyer Admin...")
    user = auth_service.authenticate('buyeradmin', 'password')
    if user:
        portal = ClientPortal()
        portal.current_user = user
        portal.current_organization = user['organization']
        
        invoices = portal.get_user_invoices()
        print(f"Found {len(invoices)} invoices for {user['name']}")
        
        for invoice in invoices:
            print(f"  - {invoice['number']}: ${invoice['amount']} ({invoice['status']})")
    
    # Test as seller admin  
    print("\n2. Testing as Seller Admin...")
    user = auth_service.authenticate('selleradmin', 'password')
    if user:
        portal = ClientPortal()
        portal.current_user = user
        portal.current_organization = user['organization']
        
        invoices = portal.get_user_invoices()
        print(f"Found {len(invoices)} invoices for {user['name']}")
        
        for invoice in invoices:
            print(f"  - {invoice['number']}: ${invoice['amount']} ({invoice['status']})")

if __name__ == "__main__":
    test_invoice_view()
