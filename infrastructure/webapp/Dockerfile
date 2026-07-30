# ==========================================
# STAGE 1: Build React frontend
# Build context = repository root (Koyeb / monorepo).
# ==========================================
FROM node:22-alpine AS frontend-build
WORKDIR /app/frontend

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# ==========================================
# STAGE 2: Build .NET host
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /app/backend

COPY backend/NuGet.config ./
COPY backend/Directory.Build.props ./
COPY backend/Directory.Packages.props ./
COPY backend/AgentForge.sln ./
COPY backend/src/AgentForge.Core/AgentForge.Core.csproj ./src/AgentForge.Core/
COPY backend/src/AgentForge.Areas.Abstractions/AgentForge.Areas.Abstractions.csproj ./src/AgentForge.Areas.Abstractions/
COPY backend/src/AgentForge.Host/AgentForge.Host.csproj ./src/AgentForge.Host/
COPY backend/src/Areas/AgentForge.Areas.Agents/AgentForge.Areas.Agents.csproj ./src/Areas/AgentForge.Areas.Agents/

RUN dotnet restore src/AgentForge.Host/AgentForge.Host.csproj

COPY backend/ ./
RUN dotnet publish src/AgentForge.Host/AgentForge.Host.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:SkipWebBuild=true

# ==========================================
# STAGE 3: Runtime
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Required when Areas:Agents:Workspace:Enabled=true
RUN apk add --no-cache git

COPY --from=backend-build /app/publish .
COPY --from=frontend-build /app/frontend/dist ./wwwroot

ENTRYPOINT ["dotnet", "AgentForge.Host.dll"]
