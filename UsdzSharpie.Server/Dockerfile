# docker build -f UsdzSharpie.Server/Dockerfile -t usdz-renderer .
# docker run -d --name usdz-server -p 8080:8080 usdz-renderer

# Assimp build stage
FROM debian:bookworm-slim AS assimp-builder

# Install build dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    git \
    wget \
    libz-dev \
    && rm -rf /var/lib/apt/lists/*

# Build Assimp 6.0.0 with USDZ support
WORKDIR /tmp
RUN wget https://github.com/assimp/assimp/archive/refs/tags/v5.4.3.tar.gz && \
    tar -xzf v5.4.3.tar.gz && \
    cd assimp-5.4.3 && \
    cmake -S . -B build \
        -DCMAKE_BUILD_TYPE=Release \
        -DBUILD_SHARED_LIBS=ON \
        -DASSIMP_BUILD_ZLIB=ON \
        -DASSIMP_WARNINGS_AS_ERRORS=OFF \
        -DASSIMP_BUILD_TESTS=OFF \
        -DASSIMP_BUILD_ALL_IMPORTERS_BY_DEFAULT=OFF \
        -DASSIMP_BUILD_USD_IMPORTER=ON \
        -DASSIMP_BUILD_OBJ_IMPORTER=ON \
        -DASSIMP_BUILD_FBX_IMPORTER=ON \
        -DASSIMP_BUILD_GLTF_IMPORTER=ON \
        -DCMAKE_CXX_FLAGS="-Wno-error=nonnull -Wno-error=maybe-uninitialized" && \
    cmake --build build --config Release

# .NET Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["UsdzSharpie.Server/UsdzSharpie.Server.csproj", "UsdzSharpie.Server/"]
COPY ["UsdzSharpie/UsdzSharpie.csproj", "UsdzSharpie/"]
RUN dotnet restore "UsdzSharpie.Server/UsdzSharpie.Server.csproj"

# Copy everything else and build
COPY ["UsdzSharpie.Server/", "UsdzSharpie.Server/"]
COPY ["UsdzSharpie/", "UsdzSharpie/"]
WORKDIR "/src/UsdzSharpie.Server"
RUN dotnet build "UsdzSharpie.Server.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish

# Detect architecture and create runtime directory structure
RUN ARCH=$(uname -m) && \
    if [ "$ARCH" = "x86_64" ]; then \
        RID="linux-x64"; \
    elif [ "$ARCH" = "aarch64" ]; then \
        RID="linux-arm64"; \
    else \
        echo "Unsupported architecture: $ARCH" && exit 1; \
    fi && \
    mkdir -p /app/publish/runtimes/$RID/native

# Copy Assimp libraries from builder
COPY --from=assimp-builder /tmp/assimp-5.4.3/build/bin/libassimp.so* /tmp/assimp/

# Install into runtime directory
RUN ARCH=$(uname -m) && \
    if [ "$ARCH" = "x86_64" ]; then \
        RID="linux-x64"; \
    elif [ "$ARCH" = "aarch64" ]; then \
        RID="linux-arm64"; \
    else \
        RID="linux-x64"; \
    fi && \
    cp /tmp/assimp/libassimp.so.5 /app/publish/runtimes/$RID/native/ && \
    echo "Installed Assimp to runtimes/$RID/native/"

# Publish .NET application
RUN dotnet publish "UsdzSharpie.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install OSMesa, Assimp dependencies, and required libraries
RUN apt-get update && apt-get install -y \
    libosmesa6 \
    libosmesa6-dev \
    mesa-common-dev \
    libfontconfig1 \
    libfreetype6 \
    libice6 \
    libsm6 \
    libx11-6 \
    libxext6 \
    libz-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .

# Display installed Assimp version if available
RUN ARCH=$(uname -m) && \
    if [ "$ARCH" = "x86_64" ]; then \
        RID="linux-x64"; \
    elif [ "$ARCH" = "aarch64" ]; then \
        RID="linux-arm64"; \
    else \
        RID="linux-x64"; \
    fi && \
    if [ -f "runtimes/$RID/native/libassimp.so.5" ]; then \
        echo "Assimp library found at runtimes/$RID/native/libassimp.so.5"; \
        ls -lh "runtimes/$RID/native/libassimp.so.5"; \
    else \
        echo "Warning: Assimp library not found"; \
    fi

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "UsdzSharpie.Server.dll"]
