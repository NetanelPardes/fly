FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Airport.Api/Airport.Api.csproj", "Airport.Api/"]
COPY ["Airport.FlightUpdates.Consumer/Airport.FlightUpdates.Consumer.csproj", "Airport.FlightUpdates.Consumer/"]
RUN dotnet restore "Airport.FlightUpdates.Consumer/Airport.FlightUpdates.Consumer.csproj"
COPY . .
WORKDIR "/src/Airport.FlightUpdates.Consumer"
RUN dotnet publish "Airport.FlightUpdates.Consumer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Airport.FlightUpdates.Consumer.dll"]
