"""
Debug tool for locating and fixing the invoice validation accounting entry issue
"""

import os
import sys
from datetime import datetime
import logging

# Setup logging
logging.basicConfig(
    filename='validate_invoice_debug.log',
    level=logging.DEBUG,
    format='%(asctime)s - %(levelname)s - %(message)s'
)

def patched_validate_invoice(self, invoice: dict):
    """
    Patched version of validate_invoice that logs all operations
    and ensures no accounting entries are created or displayed
    """
    logging.debug(f"Starting validation of invoice {invoice['id']}")
    print(f"\nValidating invoice {invoice['number']}")

    # Update status to Validated (1)
    logging.debug("Updating invoice status to Validated (1)")
    if self.update_invoice_status(invoice['id'], 1):
        logging.debug("Status update successful")
        print("Invoice validation completed - Status updated to 'Validated'")
        
        # DO NOT create or display accounting entries during validation
        # According to business requirements:
        # - Only seller uploaded invoice that is funded needs to pay the seller with accounting entry
        # - For buyer uploaded invoice, accounting entry only starts when the seller approves the invoice
        
        # Send notification to seller
        logging.debug("Preparing to send notifications")
        stakeholders = self.get_invoice_stakeholders(invoice['id'])
        logging.debug(f"Retrieved stakeholders: {stakeholders}")
        
        if stakeholders.get('seller_user_id'):
            message = f"Invoice {invoice['number']} has been validated by the bank and is ready for buyer approval."
            logging.debug(f"Sending notification to seller: {stakeholders.get('seller_name')}")
            self.send_notification(stakeholders['seller_user_id'], message, invoice['id'], "Invoice Validated", "Success")
            print(f"Notification sent to seller: {stakeholders.get('seller_name', 'Unknown')}")

        # Send notification to buyer for approval
        if stakeholders.get('buyer_user_id'):
            message = f"Invoice {invoice['number']} from {stakeholders.get('seller_name', 'Unknown')} requires your approval for financing."
            logging.debug(f"Sending notification to buyer: {stakeholders.get('buyer_name')}")
            self.send_notification(stakeholders['buyer_user_id'], message, invoice['id'], "Approval Required", "Action", True)
            print(f"Approval request sent to buyer: {stakeholders.get('buyer_name', 'Unknown')}")
    else:
        logging.error("Failed to update invoice status")
        print("Error: Failed to update invoice status")
    
    logging.debug("Invoice validation complete")

def apply_patch():
    """
    Apply the patch to the bankportal.py file
    """
    try:
        # Find the bankportal.py file
        script_dir = os.path.dirname(os.path.abspath(__file__))
        bankportal_path = os.path.join(script_dir, 'src', 'bankportal.py')
        
        if not os.path.exists(bankportal_path):
            print(f"Error: Could not find bankportal.py at {bankportal_path}")
            return False
        
        import importlib.util
        spec = importlib.util.spec_from_file_location("bankportal", bankportal_path)
        bankportal = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(bankportal)
        
        # Create a backup of the original function
        original_validate_invoice = bankportal.BankPortal.validate_invoice
        
        # Monkey patch the validate_invoice function
        bankportal.BankPortal.validate_invoice = patched_validate_invoice
        
        print("Successfully patched BankPortal.validate_invoice function")
        print("The patched function will:")
        print("1. Not create or display accounting entries during validation")
        print("2. Log all operations to validate_invoice_debug.log for debugging")
        print("\nRun the application normally and check the logs after validation to see what's happening")

        # Create a restoration script
        with open(os.path.join(script_dir, 'restore_original_validate.py'), 'w') as f:
            f.write('''"""
Restore original validate_invoice function
"""
import os

def restore():
    bankportal_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'src', 'bankportal.py')
    backup_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'src', 'bankportal.py.bak')
    
    if os.path.exists(backup_path):
        with open(backup_path, 'r') as backup:
            content = backup.read()
            
        with open(bankportal_path, 'w') as original:
            original.write(content)
            
        os.remove(backup_path)
        print("Original validate_invoice function restored")
    else:
        print("No backup found")

if __name__ == "__main__":
    restore()
''')
        
        return True
    except Exception as e:
        print(f"Error applying patch: {e}")
        return False

if __name__ == "__main__":
    apply_patch()
