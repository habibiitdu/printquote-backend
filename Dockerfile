# PrintQuote API image.
#   docker build -t printquote-api .
# Listens on port 8080 inside the container. Configure it with environment
# variables (see deploy/.env.example), for example ConnectionStrings__Default.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, from the project files only, so this layer is cached until a
# package reference changes.
COPY nuget.config Directory.Build.props ./
COPY src/PrintQuote.Domain/PrintQuote.Domain.csproj src/PrintQuote.Domain/
COPY src/PrintQuote.Application/PrintQuote.Application.csproj src/PrintQuote.Application/
COPY src/PrintQuote.Infrastructure/PrintQuote.Infrastructure.csproj src/PrintQuote.Infrastructure/
COPY src/PrintQuote.Api/PrintQuote.Api.csproj src/PrintQuote.Api/
RUN dotnet restore src/PrintQuote.Api/PrintQuote.Api.csproj

COPY src/ src/
RUN dotnet publish src/PrintQuote.Api/PrintQuote.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# curl is only used by the container health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# The image's built-in non-root user.
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "PrintQuote.Api.dll"]
