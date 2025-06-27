# Use Python 3.13 to match pyproject.toml requirements
FROM python:3.13-slim

# Set working directory
WORKDIR /app

# Install system dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

# Create a non-root user
RUN useradd -m -u 1000 appuser

# Copy project files
COPY pyproject.toml ./
COPY uv.lock ./

# Install uv for fast Python package management
RUN pip install --no-cache-dir uv

# Install Python dependencies
RUN uv pip install --system --no-cache .

# Copy the application code
COPY src/ ./src/

# Copy database files if they exist
# Note: Database files will be created at runtime if they don't exist

# Create data directory for SQLite database
RUN mkdir -p /app/data && \
    chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Set environment variables
ENV PYTHONPATH="/app/src"
ENV PYTHONUNBUFFERED=1

# Health check for the application
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD python -c "import sys; sys.path.append('/app/src'); import sqlite3; sqlite3.connect('/app/src/supply_chain_finance.db').close()" || exit 1

# Run the main application
WORKDIR /app/src
CMD ["python", "main.py"]