# ==========================================
# STAGE 1: Build der React Frontend Applikation
# ==========================================
FROM node:22-alpine AS frontend-build
WORKDIR /app/frontend

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# ==========================================
# STAGE 2: Build der .NET Backend Applikation
# Build context = repository root (Koyeb / monorepo).
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /app/backend

COPY backend/Service.slnx ./
COPY backend/src/Core/*.csproj ./src/Core/
COPY backend/src/Service/*.csproj ./src/Service/
COPY backend/src/Module.AI/*.csproj ./src/Module.AI/
COPY backend/src/Module.Demo/*.csproj ./src/Module.Demo/
COPY backend/tests/Core.Unit/*.csproj ./tests/Core.Unit/
COPY backend/tests/Service.Unit/*.csproj ./tests/Service.Unit/
COPY backend/tests/Service.Integration/*.csproj ./tests/Service.Integration/
RUN dotnet restore Service.slnx

COPY backend/ ./
RUN dotnet publish src/Service/Service.csproj -c Release -o /app/publish --no-restore

# ==========================================
# STAGE 3: Finaler Runtime Aggregations-Container
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Kopiert die kompilierten .NET Binaries
COPY --from=backend-build /app/publish .

# Integriert die statischen Frontend-Assets direkt in das wwwroot von .NET
COPY --from=frontend-build /app/frontend/dist ./wwwroot

ENTRYPOINT ["dotnet", "Service.dll"]
