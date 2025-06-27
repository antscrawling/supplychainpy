import sqlite3

class Database:
    def __init__(self, db_name="supply_chain_finance.db"):
        self.connection = sqlite3.connect(db_name)
        self.cursor = self.connection.cursor()
        # Don't create tables - use existing database structure

    def close(self):
        self.connection.close()

# Example usage
if __name__ == "__main__":
    db = Database()
    print("Connected to existing database successfully.")
    db.close()
