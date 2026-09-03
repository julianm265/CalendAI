# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/CalendarioBackend.Api/CalendarioBackend.Api.csproj", "src/CalendarioBackend.Api/"]
COPY ["src/CalendarioBackend.Core/CalendarioBackend.Core.csproj", "src/CalendarioBackend.Core/"]
RUN dotnet restore "src/CalendarioBackend.Api/CalendarioBackend.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/CalendarioBackend.Api/CalendarioBackend.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CalendarioBackend.Api.dll"]
