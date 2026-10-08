# Stage 1: Build .NET 10 Solution (Clean Architecture)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy MSBuild central configs first (to optimize Docker caching)
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

# Copy project files for optimal layer caching
COPY ["src/DocumentMDConverter.Domain/DocumentMDConverter.Domain.csproj", "src/DocumentMDConverter.Domain/"]
COPY ["src/DocumentMDConverter.Application/DocumentMDConverter.Application.csproj", "src/DocumentMDConverter.Application/"]
COPY ["src/DocumentMDConverter.Infrastructure/DocumentMDConverter.Infrastructure.csproj", "src/DocumentMDConverter.Infrastructure/"]
COPY ["src/DocumentMDConverter.Web/DocumentMDConverter.Web.csproj", "src/DocumentMDConverter.Web/"]

RUN dotnet restore "src/DocumentMDConverter.Web/DocumentMDConverter.Web.csproj"

# Copy full source and publish
COPY . .
WORKDIR "/src/src/DocumentMDConverter.Web"
RUN dotnet publish "DocumentMDConverter.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Environment with Node.js and anydoc
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Instalar Node.js 20 LTS y anydoc CLI globalmente
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ca-certificates \
    gnupg \
    && mkdir -p /etc/apt/keyrings \
    && curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg \
    && echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_20.x nodistro main" | tee /etc/apt/sources.list.d/nodesource.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g @firecrawl/anydoc \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copiar binarios compilados de la aplicación
COPY --from=build /app/publish .

# Variables de entorno para Cloud Run
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
ENV ANYDOC_BIN_PATH=/usr/bin/anydoc

EXPOSE 8080

ENTRYPOINT ["dotnet", "DocumentMDConverter.Web.dll"]
