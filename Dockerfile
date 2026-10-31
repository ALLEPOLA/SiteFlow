# Build the React frontend.
FROM node:22-bookworm-slim AS frontend-build

WORKDIR /src/frontend

COPY frontend/package.json frontend/package-lock.json ./

RUN npm ci

COPY frontend/ ./

RUN npm run build


# Build the .NET backend.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build

WORKDIR /src

COPY backend/SiteFlow.API/SiteFlow.API.csproj backend/SiteFlow.API/

RUN dotnet restore backend/SiteFlow.API/SiteFlow.API.csproj

COPY backend/SiteFlow.API/ backend/SiteFlow.API/

RUN dotnet publish backend/SiteFlow.API/SiteFlow.API.csproj \
    --configuration Release \
    --output /out \
    --no-restore \
    /p:UseAppHost=false


# Run the API and serve React from wwwroot.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=backend-build /out/ ./

COPY --from=frontend-build /src/frontend/dist/ ./wwwroot/

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

USER app

ENTRYPOINT ["dotnet", "SiteFlow.API.dll"]