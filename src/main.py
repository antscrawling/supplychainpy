from bankportal import main as bank_main
from clientportal import main as client_main

def main():
    print("Welcome to the Supply Chain Finance System")
    print("1. Bank Portal")
    print("2. Client Portal")
    print("3. Exit")
    
    print("\nAvailable Users:")
    print("  bankadmin / password (Bank Portal)")
    print("  buyeradmin / password (Client Portal - Buyer)")
    print("  selleradmin / password (Client Portal - Seller)")
    print()
    
    choice = input("Please select an option (1-3): ").strip()
    
    if choice == '1':
        bank_main()
    elif choice == '2':
        client_main()
    elif choice == '3':
        print("Exiting the system. Goodbye!")
        return
    else:
        print("Invalid choice. Please try again.")
        main()  # Restart the menu


if __name__ == "__main__":
    main()
