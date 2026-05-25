FROM python:3.11-slim

# Install system dependencies (for torch and llama-cpp)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        build-essential \
        git \
        curl && \
    rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy project files
COPY . /app

# Install Python dependencies
RUN pip install --no-cache-dir \
    autogen \
    torch \
    transformers \
    "llama-cpp-python>=0.2.0" \
    jinja2

# Expose any needed ports (none for CLI)
# ENTRYPOINT will be the executor CLI
ENTRYPOINT ["python", "-m", "bundle_executor.executor"]
