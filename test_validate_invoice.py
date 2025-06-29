from src.bankportal import BankApplication
import sys

def test_validate_invoice():
    app = BankApplication()
    print("Testing invoice validation...")
    
    # Create a mock invoice for testing
    invoice = {
        'id': 1,
        'number': 'INV288383',
        'amount': 34567.00,
        'seller_name': 'Supply Solutions Ltd',
        'buyer_name': 'MegaCorp Industries',
        'status': 'New'
    }
    
    # Override any method we need to ensure the test runs
    app.update_invoice_status = lambda invoice_id, new_status: True
    app.get_invoice_stakeholders = lambda invoice_id: {'seller_user_id': 1, 'buyer_user_id': 2, 'seller_name': 'Supply Solutions Ltd', 'buyer_name': 'MegaCorp Industries'}
    app.send_notification = lambda *args, **kwargs: True
    app.wait_for_enter = lambda: None
    
    # Run the method we're testing
    app.validate_invoice(invoice)
    
    print("Test completed.")

if __name__ == "__main__":
    test_validate_invoice()
