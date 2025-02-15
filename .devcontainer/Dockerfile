# Use the amd64 version so that the linux-x64 package works on your M1/M4 Mac
ARG VARIANT=8.0
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:${VARIANT}

# Create non-root user
RUN groupadd -g 1000 vscode && \
    useradd -u 1000 -g vscode -s /bin/bash -m vscode

# Install prerequisites
RUN apt-get update && export DEBIAN_FRONTEND=noninteractive && \
    apt-get -y install --no-install-recommends \
      curl \
      gnupg \
      lsb-release \
      wget \
      unzip \
      procps

# Install AZ CLI (if needed)
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash

# --- Install Azure Functions Core Tools ---
# Download the Linux x64 zip package (version 4.0.6821 in this example)
RUN wget -q https://github.com/Azure/azure-functions-core-tools/releases/download/4.0.6821/Azure.Functions.Cli.linux-x64.4.0.6821.zip && \
    unzip -q Azure.Functions.Cli.linux-x64.4.0.6821.zip -d /azure-functions && \
    rm Azure.Functions.Cli.linux-x64.4.0.6821.zip && \
    chmod +x /azure-functions/func && \
    ln -s /azure-functions/func /usr/local/bin/func

# Create azure-functions directory and set permissions
RUN mkdir -p /azure-functions && \
    chown -R vscode:vscode /azure-functions && \
    chmod -R 755 /azure-functions

# Clean up apt caches
RUN apt-get clean && rm -rf /var/lib/apt/lists/*

# Switch to non-root user
USER vscode

ENV PATH $PATH:/home/vscode/.dotnet:/home/vscode/.dotnet/tools
