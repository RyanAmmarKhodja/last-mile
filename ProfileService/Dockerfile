# Build context: repository root (needs ../shared). See docker-compose.yml: build.context = .
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["shared/LastMile.Contracts/LastMile.Contracts.csproj", "shared/LastMile.Contracts/"]
COPY ["shared/LastMile.Messaging/LastMile.Messaging.csproj", "shared/LastMile.Messaging/"]
COPY ["ProfileService/ProfileService.csproj", "ProfileService/"]
RUN dotnet restore "ProfileService/ProfileService.csproj"

COPY shared/ shared/
COPY ProfileService/ ProfileService/
RUN dotnet publish "ProfileService/ProfileService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ProfileService.dll"]
