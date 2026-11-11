# Build context: repository root (needs ../shared). See docker-compose.yml: build.context = .
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["shared/LastMile.Contracts/LastMile.Contracts.csproj", "shared/LastMile.Contracts/"]
COPY ["shared/LastMile.Messaging/LastMile.Messaging.csproj", "shared/LastMile.Messaging/"]
COPY ["ShipmentService/ShipmentService.csproj", "ShipmentService/"]
RUN dotnet restore "ShipmentService/ShipmentService.csproj"

COPY shared/ shared/
COPY ShipmentService/ ShipmentService/
RUN dotnet publish "ShipmentService/ShipmentService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ShipmentService.dll"]
