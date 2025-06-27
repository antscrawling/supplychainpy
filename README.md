# Supply Chain Finance Management System

A comprehensive supply chain finance application with both Python and C# components for managing invoices, credit facilities, and financial transactions.

## Features

- **Bank Portal**: Manage organizations, credit facilities, invoice reviews, and funding operations
- **Client Portal**: Upload invoices, view account statements, manage transactions
- **Database Management**: SQLite-based data persistence
- **Docker Support**: Containerized deployment and development

## Quick Start

### Local Development

1. **Install Dependencies**:
   ```bash
   make dev-install
   ```

2. **Run Application**:
   ```bash
   make dev-run
   ```

### Docker Deployment

1. **Build and Run**:
   ```bash
   make docker-build
   make docker-run
   ```

2. **Interact with Application**:
   ```bash
   make attach
   ```

3. **Stop Application**:
   ```bash
   make docker-stop
   ```

### Using Docker Compose

1. **Start Services**:
   ```bash
   make start
   # or
   docker compose up -d
   ```

2. **With Database Backup**:
   ```bash
   docker compose --profile backup up -d
   ```

3. **Stop Services**:
   ```bash
   make stop
   ```

## Available Commands

Run `make help` to see all available commands:

### Docker Operations
- `docker-build` - Build the Docker image
- `docker-run` - Run the Docker container
- `docker-stop` - Stop the container
- `docker-clean` - Clean up Docker resources

### Development
- `dev-run` - Run application locally
- `dev-install` - Install dependencies
- `dev-format` - Format Python code
- `dev-lint` - Lint Python code

### Database Management
- `db-backup` - Create database backup
- `db-restore` - Show available backups

## Project Structure

```
├── main.py              # Main application entry point
├── bank_portal.py       # Bank portal functionality
├── client_portal.py     # Client portal functionality
├── auth_service.py      # Authentication service
├── database.py          # Database management
├── transaction_service.py # Transaction handling
├── Dockerfile           # Container configuration
├── docker-compose.yml   # Multi-container setup
├── Makefile            # Build and deployment automation
├── pyproject.toml      # Python project configuration
└── *.db                # SQLite database files
```

## Database

The application uses SQLite for data persistence. Database files are automatically created and can be backed up using the provided commands.

## Development

### Code Formatting
```bash
make dev-format
```

### Linting
```bash
make dev-lint
```

### Interactive Development
For interactive development with the containerized application:
```bash
make docker-run
make attach
```

## Deployment

### Local Registry
```bash
make deploy
```

### Custom Registry
```bash
docker tag antscrawlingjay/supplychainpy:latest your-registry/supplychainpy:latest
docker push your-registry/supplychainpy:latest
```

## Troubleshooting

### Container Issues
- Check logs: `make docker-logs`
- Access shell: `make shell`
- Clean restart: `make docker-clean && make docker-build && make docker-run`

### Database Issues
- Create backup: `make db-backup`
- Check database connection in container health check

## License

This project is part of a supply chain finance management system.
