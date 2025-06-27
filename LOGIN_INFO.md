# Supply Chain Finance System - Login Summary

## Database File
The system now uses the correct database file: `src/supply_chain_finance.db`

## User Credentials
All users have the password: **password**

### Available Users:

#### Bank Portal Users:
- **Username:** `bankadmin`
- **Password:** `password`
- **Name:** Bank Administrator
- **Organization:** Global Finance Bank
- **Access:** Full bank portal functionality

#### Client Portal Users:

##### Buyer Users:
- **Username:** `buyeradmin`
- **Password:** `password`
- **Name:** Buyer Administrator
- **Organization:** MegaCorp Industries
- **Type:** Buyer organization

##### Seller Users:
- **Username:** `selleradmin`
- **Password:** `password`
- **Name:** Seller Administrator
- **Organization:** Supply Solutions Ltd
- **Type:** Seller organization

## How to Run:

### Using Makefile:
```bash
make dev-run
```

### Direct execution:
```bash
cd src && python3 main.py
```

## Portal Access:

1. **Bank Portal:** Select option 1, login with `bankadmin/password`
2. **Client Portal:** Select option 2, login with `buyeradmin/password` or `selleradmin/password`

## Features Working:
- ✅ User authentication with existing database
- ✅ Organization-based access control
- ✅ Bank portal with full menu system
- ✅ Client portal with buyer/seller specific options
- ✅ Database integration with proper schema
- ✅ Role-based permissions (Bank=0/1, Admin=2, User=3)

## Database Schema:
- Users table with Pascal case columns (Username, Password, Name, Role, OrganizationId)
- Organizations table with buyer/seller/bank flags
- Roles: 0=Bank Admin, 1=Bank User, 2=Organization Admin, 3=Organization User
