"""
Database connection module (redirects to src/database.py)
This module ensures consistent database access from both root and src directories.
"""

import os
import sys
from pathlib import Path

# Add the project root to sys.path if needed
project_root = Path(__file__).parent
if str(project_root) not in sys.path:
    sys.path.insert(0, str(project_root))

# Import the Database class from src.database
from src.database import Database

# If this module is run directly
if __name__ == "__main__":
    db = Database()
    print("Connected to existing database successfully.")
    db.close()
