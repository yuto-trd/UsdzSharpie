# docker build -f UsdzSharpie.Server/Dockerfile -t usdz-renderer .
# docker run -d --name usdz-server -p 8080:8080 usdz-renderer
# Build stage
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
RUN dotnet publish "UsdzSharpie.Server.csproj" -c Debug -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install OSMesa and required dependencies
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
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "UsdzSharpie.Server.dll"]
