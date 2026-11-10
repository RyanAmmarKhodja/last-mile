# Build context: repository root (needs ../shared). See docker-compose.yml: build.context = .
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["shared/LastMile.Contracts/LastMile.Contracts.csproj", "shared/LastMile.Contracts/"]
COPY ["shared/LastMile.Messaging/LastMile.Messaging.csproj", "shared/LastMile.Messaging/"]
COPY ["FleetService/FleetService.csproj", "FleetService/"]
RUN dotnet restore "FleetService/FleetService.csproj"

COPY shared/ shared/
COPY FleetService/ FleetService/
RUN dotnet publish "FleetService/FleetService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "FleetService.dll"]
