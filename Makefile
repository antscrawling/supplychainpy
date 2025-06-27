.PHONY: help docker-build docker-push docker-run docker-compose-up docker-compose-down docker-logs docker-pull docker-stop docker-rm docker-clean dev-run dev-install dev-install-uv dev-install-pip dev-install-dev dev-format dev-lint shell attach db-backup db-restore deploy start stop restart main bank client test-accounting test-funding accounting-report test-invoices test-login db-status db-invoices db-accounts db-facilities db-facilities

help:
	@echo "Available targets:"
	@echo ""
	@echo "Docker Operations:"
	@echo "  docker-build      Build the Docker image"
	@echo "  docker-push       Push the Docker image to Docker Hub"
	@echo "  docker-pull       Pull the latest Docker image from Docker Hub"
	@echo "  docker-run        Run the Docker container"
	@echo "  docker-stop       Stop the running container"
	@echo "  docker-rm         Remove the container"
	@echo "  docker-logs       View container logs"
	@echo "  docker-compose-up Start the application using docker-compose"
	@echo "  docker-compose-down Stop and remove containers created by docker-compose"
	@echo "  docker-clean      Remove all containers and images related to this project"
	@echo ""
	@echo "Development:"
	@echo "  dev-run           Run application locally"
	@echo "  main              Run main.py (main application entry point)"
	@echo "  bank              Run bankportal.py (bank portal)"
	@echo "  client            Run clientportal.py (client portal)"
	@echo "  dev-install       Install dependencies (auto-detect uv/pip)"
	@echo "  dev-install-uv    Install with uv (fast)"
	@echo "  dev-install-pip   Install with pip (traditional)"
	@echo "  dev-install-dev   Install with development tools"
	@echo "  dev-format        Format Python code with black"
	@echo "  dev-lint          Lint Python code with flake8"
	@echo ""
	@echo "Container Interaction:"
	@echo "  shell             Open shell in running container"
	@echo "  attach            Attach to running container"
	@echo ""
	@echo "Database Management:"
	@echo "  db-backup         Create database backup"
	@echo "  db-restore        Show available backups"
	@echo "  db-status         Show database summary"
	@echo "  db-invoices       Show all invoices"
	@echo "  db-accounts       Show account balances"
	@echo "  db-facilities     Show credit facilities and utilization"
	@echo ""
	@echo "Testing:"
	@echo "  test-accounting   Run accounting entry tests"
	@echo "  test-funding      Run funding accounting tests"
	@echo "  test-invoices     Run invoice tests"
	@echo "  test-login        Run login tests"
	@echo "  accounting-report Generate comprehensive accounting report"
	@echo ""
	@echo "Database Queries:"
	@echo "  db-status         Show database status and invoice summary"
	@echo "  db-invoices       Show current invoices"
	@echo "  db-accounts       Show account balances"
	@echo "  db-facilities     Show credit facilities and utilization"
	@echo ""
	@echo "Convenience:"
	@echo "  deploy            Build and push image"
	@echo "  start             Start application"
	@echo "  stop              Stop application"
	@echo "  restart           Restart application"

docker-build:
	@echo "Building Docker image..."
	docker build -t antscrawlingjay/supplychainpy:latest .
	@echo "Docker image built successfully!"

docker-push:
	@echo "Pushing Docker image to Docker Hub..."
	docker push antscrawlingjay/supplychainpy:latest
	@echo "Docker image pushed successfully!"

docker-pull:
	@echo "Pulling latest Docker image..."
	docker pull antscrawlingjay/supplychainpy:latest
	@echo "Docker image pulled successfully!"

docker-run:
	@echo "Starting Docker container..."
	docker run -d --name supplychainpy -it antscrawlingjay/supplychainpy:latest
	@echo "Container started! Use 'docker attach supplychainpy' to interact with the application"

docker-stop:
	@echo "Stopping container..."
	docker stop supplychainpy || true
	@echo "Container stopped!"

docker-rm:
	@echo "Removing container..."
	docker rm supplychainpy || true
	@echo "Container removed!"

docker-logs:
	@echo "Showing container logs (Ctrl+C to exit)..."
	docker logs -f supplychainpy

docker-compose-up:
	@echo "Starting application with docker-compose..."
	docker compose up -d
	@echo "Application started! Use 'docker attach supplychainpy' to interact"

docker-compose-down:
	@echo "Stopping application..."
	docker compose down
	@echo "Application stopped!"

docker-clean:
	@echo "Cleaning up Docker resources..."
	docker stop supplychainpy || true
	docker rm supplychainpy || true
	docker rmi antscrawlingjay/supplychainpy:latest || true
	@echo "Cleanup complete!"

# Convenience targets
deploy: docker-build docker-push
	@echo "Deployment complete! Image pushed to Docker Hub."

start: docker-compose-up
	@echo "Application started! Use 'docker attach supplychainpy' to interact"

stop: docker-compose-down
	@echo "Application stopped!"

restart: stop start
	@echo "Application restarted!"

# Development targets
dev-run:
	@echo "Running application locally..."
	cd src && python3 main.py

bank:
	@echo "Starting Bank Portal..."
	cd src && python3 bankportal.py

client:
	@echo "Starting Client Portal..."
	cd src && python3 clientportal.py

main:
	@echo "Starting Main Application..."
	cd src && python3 main.py

dev-install:
	@echo "Installing dependencies..."
	@if command -v uv >/dev/null 2>&1; then \
		echo "Using uv for fast installation..."; \
		uv pip install --system .; \
	else \
		echo "uv not found, installing with pip..."; \
		pip install -r requirements.txt; \
	fi

dev-install-uv:
	@echo "Installing with uv (fast)..."
	pip install uv
	uv pip install --system .

dev-install-pip:
	@echo "Installing with pip (traditional)..."
	pip install -r requirements.txt

dev-install-dev:
	@echo "Installing with development dependencies..."
	@if command -v uv >/dev/null 2>&1; then \
		uv pip install --system ".[dev]"; \
	else \
		pip install -r requirements.txt; \
	fi

dev-format:
	@echo "Formatting Python code..."
	python -m black src/*.py || echo "Black not installed, run 'make dev-install-dev' first"

dev-lint:
	@echo "Linting Python code..."
	python -m flake8 src/*.py || echo "Flake8 not installed, run 'make dev-install-dev' first"

# Interactive container access
shell:
	@echo "Opening shell in running container..."
	docker exec -it supplychainpy /bin/bash

attach:
	@echo "Attaching to running container..."
	docker attach supplychainpy

# Database management
db-backup:
	@echo "Creating database backup..."
	@if [ -f src/supply_chain_finance.db ]; then \
		cp src/supply_chain_finance.db src/supply_chain_finance.db.backup.$(shell date +%Y%m%d_%H%M%S); \
		echo "Database backup created in src/"; \
	elif [ -f supply_chain_finance.db ]; then \
		cp supply_chain_finance.db supply_chain_finance.db.backup.$(shell date +%Y%m%d_%H%M%S); \
		echo "Database backup created in root/"; \
	else \
		echo "Database file not found in src/ or root directory"; \
	fi

db-restore:
	@echo "Available backups:"
	@ls -la src/*.db.backup.* 2>/dev/null || ls -la *.db.backup.* 2>/dev/null || echo "No backups found"

# Testing targets
test-accounting:
	@echo "Running accounting tests..."
	cd src && python3 test_accounting.py

test-funding:
	@echo "Running funding accounting tests..."
	cd src && python3 test_funding_accounting.py

accounting-report:
	@echo "Generating accounting report..."
	cd src && python3 accounting_report.py

test-invoices:
	@echo "Running invoice tests..."
	cd src && python3 test_invoices.py

test-login:
	@echo "Running login tests..."
	cd src && python3 test_login.py

# Database queries
db-status:
	@echo "Database status and invoice summary:"
	cd src && sqlite3 supply_chain_finance.db "SELECT 'Total Invoices:', COUNT(*) FROM Invoices; SELECT 'By Status:', Status, COUNT(*) FROM Invoices GROUP BY Status; SELECT 'Journal Entries:', COUNT(*) FROM JournalEntries; SELECT 'Journal Lines:', COUNT(*) FROM JournalEntryLines;"

db-invoices:
	@echo "Current invoices:"
	cd src && sqlite3 supply_chain_finance.db -header -column "SELECT Id, InvoiceNumber, Status, Amount, Description FROM Invoices ORDER BY Id;"

db-accounts:
	@echo "Account balances:"
	cd src && sqlite3 supply_chain_finance.db -header -column "SELECT AccountCode, AccountName, Balance FROM Accounts WHERE CAST(Balance AS REAL) != 0 ORDER BY AccountCode;"

db-facilities:
	@echo "Credit facilities and utilization:"
	cd src && sqlite3 supply_chain_finance.db -header -column "SELECT o.Name as Organization, cf.FacilityType, cf.Limit, cf.UtilizedAmount, (CAST(cf.Limit AS REAL) - CAST(cf.UtilizedAmount AS REAL)) as Available, cf.IsActive FROM CreditFacilities cf JOIN Organizations o ON cf.OrganizationId = o.Id ORDER BY o.Name;"