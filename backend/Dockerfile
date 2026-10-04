# =========================================================================
# AssistLK ASP.NET Core Web API - Production Dockerfile
# =========================================================================

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY AssistLK.sln ./
COPY src/AssistLK.Domain/AssistLK.Domain.csproj src/AssistLK.Domain/
COPY src/AssistLK.Agents/AssistLK.Agents.csproj src/AssistLK.Agents/
COPY src/AssistLK.Application/AssistLK.Application.csproj src/AssistLK.Application/
COPY src/AssistLK.Infrastructure/AssistLK.Infrastructure.csproj src/AssistLK.Infrastructure/
COPY src/AssistLK.Api/AssistLK.Api.csproj src/AssistLK.Api/

# Restore dependencies
RUN dotnet restore src/AssistLK.Api/AssistLK.Api.csproj

# Copy all source files
COPY src/ src/

# Build and publish
WORKDIR /src/src/AssistLK.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for container health check
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Default port configuration for containerized deployment (e.g. Railway)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AssistLK.Api.dll"]
