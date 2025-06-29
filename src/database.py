import sqlite3
import datetime

# Define adapter for datetime objects to fix the deprecation warning
def adapt_datetime(dt):
    return dt.strftime("%Y-%m-%d %H:%M:%S")

# Define converter for datetime strings
def convert_datetime(s):
    if s is None:
        return None
    try:
        return datetime.datetime.strptime(s.decode('utf-8'), "%Y-%m-%d %H:%M:%S")
    except ValueError:
        return s

# Register the adapter and converter
sqlite3.register_adapter(datetime.datetime, adapt_datetime)
sqlite3.register_converter("datetime", convert_datetime)

class Database:
    def __init__(self, db_name="supply_chain_finance.db"):
        """
        Initialize database connection, ensuring we always use the database in the src directory.
        
        Args:
            db_name: Name of the database file (default: "supply_chain_finance.db")
        """
        import os
        from pathlib import Path
        
        # Get the src directory path
        src_path = Path(os.path.dirname(os.path.abspath(__file__)))
        
        # Create the full path to the database
        db_path = src_path / db_name
        
        # Ensure database file exists
        if not db_path.exists():
            print(f"Warning: Database file not found at {db_path}")
            print(f"Creating connection anyway, which will create the file if operations are performed.")
        
        # Create the connection
        self.connection = sqlite3.connect(str(db_path), detect_types=sqlite3.PARSE_DECLTYPES|sqlite3.PARSE_COLNAMES)
        self.cursor = self.connection.cursor()
        # Don't create tables - use existing database structure

    def close(self):
        """Close the database connection"""
        if hasattr(self, 'connection') and self.connection:
            self.connection.close()
    
    def commit(self):
        """Commit changes to the database"""
        self.connection.commit()
        
    def execute(self, query, params=None):
        """Execute a query with optional parameters"""
        if params:
            self.cursor.execute(query, params)
        else:
            self.cursor.execute(query)
        return self.cursor
    
    def fetchone(self):
        """Fetch one row from the last query"""
        return self.cursor.fetchone()
    
    def fetchall(self):
        """Fetch all rows from the last query"""
        return self.cursor.fetchall()
    
    def __enter__(self):
        """Enable context manager pattern with 'with' statement"""
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """Auto-close when exiting context manager"""
        self.close()

# Example usage
if __name__ == "__main__":
    db = Database()
    print("Connected to existing database successfully.")
    
    # Example using the context manager pattern
    with Database() as db:
        db.execute("SELECT sqlite_version()")
        version = db.fetchone()[0]
        print(f"SQLite version: {version}")
    
    # The connection is automatically closed after the with block
