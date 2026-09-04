# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY TicketeraOnline.Api.csproj .
RUN dotnet restore TicketeraOnline.Api.csproj
COPY . .
RUN dotnet publish TicketeraOnline.Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
# Kestrel binds to 0.0.0.0:$PORT (Render injects PORT; fallback 10000).
# Shell form is required so $PORT is expanded at runtime, not build time.
CMD ["sh", "-c", "dotnet TicketeraOnline.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]