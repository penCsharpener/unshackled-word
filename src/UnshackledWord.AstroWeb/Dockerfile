# Use Node 22 (LTS) for modern Astro 5 features
FROM node:22-slim

# Install git for source control inside the container
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*

WORKDIR /workspaces/web

# Keep the container running so VS Code can attach to it
CMD ["sleep", "infinity"]
