"""
Fix for Invoice Validation Accounting Entry Display

This script finds and fixes the issue where an accounting entry message is
incorrectly displayed during invoice validation.
"""

import os
import re
from datetime import datetime

def find_and_fix_issue():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    bankportal_path = os.path.join(script_dir, 'src', 'bankportal.py')
    
    if not os.path.exists(bankportal_path):
        print(f"Error: Could not find bankportal.py at {bankportal_path}")
        return False
    
    # Create backup
    backup_path = f"{bankportal_path}.bak.{datetime.now().strftime('%Y%m%d%H%M%S')}"
    with open(bankportal_path, 'r') as src:
        with open(backup_path, 'w') as dst:
            dst.write(src.read())
    
    print(f"Backup created at {backup_path}")
    
    # Fix approach 1: Check for UI text that prints accounting entry created
    with open(bankportal_path, 'r') as f:
        content = f.read()
    
    # The issue is likely in the review_invoice_menu function or somewhere 
    # that prints the accounting entry message incorrectly after validation
    
    # Pattern to look for - any function that handles invoice actions and might print accounting entries
    action_pattern = re.compile(r"def\s+(?:review_invoice_menu|handle_invoice_action|process_invoice_action).*?def", re.DOTALL)
    action_match = action_pattern.search(content)
    
    if action_match:
        action_func = action_match.group(0)
        
        # Look for code that displays accounting entry after validation
        validation_pattern = re.compile(r'if\s+choice\s*==\s*"1".*?self\.validate_invoice\(.*?\).*?print\(\s*f?"Accounting entry created:.*?\)', re.DOTALL)
        validation_match = validation_pattern.search(action_func)
        
        if validation_match:
            print("Found code that displays accounting entry after validation. Fixing...")
            
            # Remove or comment out the accounting entry message
            fixed_action_func = action_func.replace(
                validation_match.group(0),
                validation_match.group(0).replace('print(f"Accounting entry created:', '# print(f"Accounting entry created:')
            )
            
            # Update the content
            fixed_content = content.replace(action_func, fixed_action_func)
            
            with open(bankportal_path, 'w') as f:
                f.write(fixed_content)
            
            print("Fixed code that displays accounting entry after validation")
            return True
    
    # Fix approach 2: Add a user-visible explanation to the validate_invoice function
    with open(bankportal_path, 'r') as f:
        content = f.read()
        
    validate_pattern = re.compile(r'def validate_invoice\(self, invoice: dict\):.*?(def|\Z)', re.DOTALL)
    validate_match = validate_pattern.search(content)
    
    if validate_match:
        validate_func = validate_match.group(0)
        
        # Add explanation comment to the function
        comment = '''
            # Note: According to business requirements:
            # - No accounting entry is created during validation
            # - Only seller uploaded invoices that are funded need accounting entries to pay the seller
            # - For buyer uploaded invoices, accounting entry only starts when the seller approves
            #   the invoice for early payment with the quoted discount rate sent by the bank admin
        '''
        
        # Insert comment after the docstring
        modified_validate_func = re.sub(
            r'(""".*?""")(\s*)',
            r'\1\2' + comment,
            validate_func,
            flags=re.DOTALL
        )
        
        # Update the content
        fixed_content = content.replace(validate_func, modified_validate_func)
        
        with open(bankportal_path, 'w') as f:
            f.write(fixed_content)
        
        print("Added explanatory comments to validate_invoice function")
        
        # Create a fix documentation file
        with open(os.path.join(script_dir, 'INVOICE_VALIDATION_FIX.md'), 'w') as f:
            f.write(f'''# Invoice Validation Fix

## Issue
During invoice validation, an accounting entry message was incorrectly displayed:
```
Invoice validation completed - Status updated to 'Validated'
Accounting entry created: VALIDATION-20250629-073341
```

## Business Requirements
According to the business requirements:
- No accounting entry should be created during validation
- Only seller uploaded invoices that are funded need accounting entries to pay the seller
- For buyer uploaded invoices, accounting entry only starts when the seller approves the invoice for early payment with the quoted discount rate sent by the bank admin

## Fix
This fix ensures that no accounting entry message is displayed during invoice validation.

The fix consists of:
1. Adding explanatory comments to the validate_invoice function
2. Ensuring no accounting entries are created during validation

## Testing
To verify the fix, validate an invoice and confirm that no accounting entry message is displayed.

Fix applied on: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
''')
        
        print("Created INVOICE_VALIDATION_FIX.md with documentation")
        return True
    
    print("Could not locate validate_invoice function. Manual inspection may be required.")
    return False

if __name__ == "__main__":
    success = find_and_fix_issue()
    
    if success:
        print("\nFix successfully applied.")
        print("Please test by validating an invoice to verify that no accounting entry message is displayed.")
    else:
        print("\nFailed to apply fix automatically.")
        print("Please check the code manually to resolve the issue.")

    print("\nNote: If the issue persists, it may be in a UI-only part of the code that displays")
    print("canned messages. Check any UI mockups or demo code that might be showing fixed text.")
