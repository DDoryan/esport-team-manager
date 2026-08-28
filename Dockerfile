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

RUN apt-get update \
    && apt-get install -y --no-install-recommends util-linux \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER root

ENTRYPOINT ["/bin/sh", "-c", "set -eu; storage_root=\"${PrivateImageStorage__RootPath:-/app/App_Data/private-images}\"; mkdir -p \"$storage_root\"; chown -R \"$APP_UID:$APP_UID\" \"$storage_root\"; chmod 0750 \"$storage_root\"; umask 0027; exec setpriv --reuid=app --regid=app --init-groups --no-new-privs dotnet EsportTeamManager.Web.dll"]