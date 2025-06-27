#!/usr/bin/env python3
"""
Test script to verify user logins work correctly
"""

from auth_service import authenticate

def test_login():
    """Test all user logins"""
    print("=== Supply Chain Finance Login Test ===\n")
    
    users = [
        ("bankadmin", "password", "Bank Portal"),
        ("selleradmin", "password", "Client Portal (Seller)"),
        ("buyeradmin", "password", "Client Portal (Buyer)")
    ]
    
    for username, password, portal in users:
        print(f"Testing {username}...")
        user = authenticate(username, password)
        if user:
            print(f"✓ Login successful for {username}")
            print(f"  Name: {user['name']}")
            print(f"  Role: {user['role']}")
            print(f"  Portal: {portal}")
        else:
            print(f"✗ Login failed for {username}")
        print()
    
    print("=== Test Complete ===")
    print("\nTo run the application:")
    print("python main.py")
    print("\nCredentials:")
    print("  bankadmin / password (for Bank Portal)")
    print("  selleradmin / password (for Client Portal)")
    print("  buyeradmin / password (for Client Portal)")

if __name__ == "__main__":
    test_login()
