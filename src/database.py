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
        self.connection = sqlite3.connect(db_name, detect_types=sqlite3.PARSE_DECLTYPES|sqlite3.PARSE_COLNAMES)
        self.cursor = self.connection.cursor()
        # Don't create tables - use existing database structure

    def close(self):
        self.connection.close()

# Example usage
if __name__ == "__main__":
    db = Database()
    print("Connected to existing database successfully.")
    db.close()
