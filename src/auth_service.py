import hashlib
from typing import Optional
import sqlite3
from database import Database

def hash_password(password: str) -> str:
    return hashlib.sha256(password.encode()).hexdigest()

def authenticate(username: str, password: str) -> Optional[dict]:
    db = Database()
    
    # Query user with organization information using correct schema
    query = """
        SELECT u.Id, u.Username, u.Name, u.Role, u.OrganizationId,
               o.Name as OrgName, o.IsSeller, o.IsBuyer, o.IsBank, u.Password
        FROM Users u
        LEFT JOIN Organizations o ON u.OrganizationId = o.Id
        WHERE u.Username = ?
    """
    
    db.cursor.execute(query, (username,))
    result = db.cursor.fetchone()
    db.close()
    
    if result:
        user_id, username, name, role, org_id, org_name, is_seller, is_buyer, is_bank, stored_password = result
        
        # Check password (handle both plain text and hashed)
        hashed_password = hash_password(password)
        if stored_password != password and stored_password != hashed_password:
            return None
        
        user_data = {
            "id": user_id,
            "username": username,
            "name": name,
            "role": role
        }
        
        # Add organization info if user belongs to one
        if org_id:
            user_data["organization"] = {
                "id": org_id,
                "name": org_name,
                "is_seller": bool(is_seller),
                "is_buyer": bool(is_buyer),
                "is_bank": bool(is_bank)
            }
        
        return user_data
    
    return None

def is_authorized(user: dict, allowed_roles: list) -> bool:
    return user["role"] in allowed_roles

def create_user(username: str, password: str, role: str):
    db = Database()
    hashed_password = hash_password(password)
    try:
        db.cursor.execute("INSERT INTO users (username, password, role) VALUES (?, ?, ?)", (username, hashed_password, role))
        db.connection.commit()
    except sqlite3.IntegrityError:
        print("Error: Username already exists.")
    finally:
        db.close()

# Example usage
if __name__ == "__main__":
    username = input("Enter username: ")
    password = input("Enter password: ")

    user = authenticate(username, password)
    if user:
        print(f"Welcome {user['username']}! Role: {user['role']}")
    else:
        print("Invalid credentials")
