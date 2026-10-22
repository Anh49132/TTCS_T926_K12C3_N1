FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY QL_PhongTro/QL_PhongTro.csproj QL_PhongTro/
RUN dotnet restore QL_PhongTro/QL_PhongTro.csproj
COPY QL_PhongTro/ QL_PhongTro/
COPY docs/sql/ docs/sql/
COPY deployment/ReleasePublish.targets deployment/ReleasePublish.targets
RUN dotnet publish QL_PhongTro/QL_PhongTro.csproj -c Release --no-restore -o /publish \
    -p:CustomAfterMicrosoftCommonTargets=/src/deployment/ReleasePublish.targets \
    -p:UseAppHost=false
RUN test -f /publish/Microsoft.Data.Sqlite.dll \
    && test -f /publish/Microsoft.EntityFrameworkCore.Sqlite.dll \
    && test -f /publish/Npgsql.dll \
    && test -f /publish/AWSSDK.S3.dll

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=10000 \
    DatabaseProvider=PostgreSql \
    DatabasePath=/tmp/nhatot/cache-scope \
    IdentityImagePath=/tmp/nhatot/identity \
    RoomImagesPath=/tmp/nhatot/rooms \
    Deployment__RenderProxy=true
COPY --from=build /publish/ ./
USER $APP_UID
EXPOSE 10000
ENTRYPOINT ["dotnet", "QL_PhongTro.dll"]
