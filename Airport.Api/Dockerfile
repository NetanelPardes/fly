FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Airport.Api/Airport.Api.csproj", "Airport.Api/"]
RUN dotnet restore "Airport.Api/Airport.Api.csproj"
COPY . .
WORKDIR "/src/Airport.Api"
RUN dotnet publish "Airport.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Airport.Api.dll"]
