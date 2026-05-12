FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/Ticketly.Api/Ticketly.Api.csproj", "src/Ticketly.Api/"]
RUN dotnet restore "src/Ticketly.Api/Ticketly.Api.csproj"

COPY . .
WORKDIR "/src/src/Ticketly.Api"
RUN dotnet publish "Ticketly.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Ticketly.Api.dll"]

