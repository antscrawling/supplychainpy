#!/usr/bin/env python3
"""
Database utility script for checking database status and performing basic operations.
Run this script to verify database connectivity and check table status.
"""

import os
import sys
from pathlib import Path

# Add project root directory to Python path
project_root = Path(__file__).parent
sys.path.insert(0, str(project_root))

from src.database import Database

def check_database():
    """Check database connectivity and basic information"""
    print("Checking database connection...")
    
    try:
        # Connect to database
        with Database() as db:
            # Check SQLite version
            db.execute("SELECT sqlite_version()")
            version = db.fetchone()[0]
            print(f"SQLite version: {version}")
            
            # Check database path
            import inspect
            db_path = os.path.abspath(inspect.getfile(db.__class__))
            db_dir = os.path.dirname(db_path)
            print(f"Database module location: {db_dir}")
            
            # Get list of tables
            db.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
            tables = db.fetchall()
            
            if not tables:
                print("No tables found in database.")
                return
            
            print(f"\nFound {len(tables)} tables:")
            for i, table in enumerate(tables, 1):
                table_name = table[0]
                print(f"  {i}. {table_name}")
                
                # Get record count for this table
                db.execute(f"SELECT COUNT(*) FROM [{table_name}]")
                count = db.fetchone()[0]
                print(f"     Records: {count}")
                
                # Get column information
                db.execute(f"PRAGMA table_info([{table_name}])")
                columns = db.fetchall()
                print(f"     Columns: {len(columns)}")
                
                # Print first 3 column names
                if columns:
                    col_names = [col[1] for col in columns[:3]]
                    more = "..." if len(columns) > 3 else ""
                    print(f"     Sample columns: {', '.join(col_names)}{more}")
                
    except Exception as e:
        print(f"Error accessing database: {e}")
        return False
        
    print("\nDatabase check completed successfully.")
    return True

if __name__ == "__main__":
    check_database()
