# ---------------------------------------------------------------
# AssistLK — Quotation & Booking Agent (Component 3)
# Python FastAPI service with LangGraph orchestration
# ---------------------------------------------------------------
FROM python:3.12-slim

# Prevent Python from writing .pyc files and enable unbuffered output
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

# Working directory inside the container
WORKDIR /app

# Install system dependencies required by some Python packages
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# Copy requirements first to leverage Docker layer caching
COPY requirements.txt .

# Install Python dependencies
RUN pip install --upgrade pip \
 && pip install -r requirements.txt

# Copy the application code
COPY app/ ./app/

# Environment defaults (can be overridden by docker run -e ...)
ENV BACKEND_API_URL=http://host.docker.internal:5012 \
    SERVICE_BEARER_TOKEN="" \
    PORT=8002

# Expose the FastAPI port
EXPOSE 8002

# Health check using the /health endpoint
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8002/health || exit 1

# Start the service
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8002"]