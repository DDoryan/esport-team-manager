FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY ["EsportTeamManager.Web.csproj", "./"]
COPY ["EsportTeamManager.Application/EsportTeamManager.Application.csproj", "EsportTeamManager.Application/"]
COPY ["EsportTeamManager.Domain/EsportTeamManager.Domain.csproj", "EsportTeamManager.Domain/"]
COPY ["EsportTeamManager.Infrastructure/EsportTeamManager.Infrastructure.csproj", "EsportTeamManager.Infrastructure/"]
COPY ["EsportTeamManager.Infrastructure.PostgreSql.Migrations/EsportTeamManager.Infrastructure.PostgreSql.Migrations.csproj", "EsportTeamManager.Infrastructure.PostgreSql.Migrations/"]

RUN dotnet restore "EsportTeamManager.Web.csproj"

COPY . .

RUN dotnet publish "EsportTeamManager.Web.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "EsportTeamManager.Web.dll"]