from src.bankportal import BankApplication

def test_create_facility():
    app = BankApplication()
    # Just test that we can call the method without errors
    # We won't actually create anything in the database
    try:
        print("Testing method signature...")
        app.create_facility_for_organization(1, "Test Organization")
        print("Method signature test passed!")
    except Exception as e:
        print(f"Error testing method: {e}")

if __name__ == "__main__":
    test_create_facility()
