# Build context: repository root (see docker-compose.yml / release workflow).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from the project file only, so the restore layer is cached
# until package references change.
COPY backend/DailyChallenges.csproj backend/
RUN dotnet restore backend/DailyChallenges.csproj

# Then copy the sources and publish.
COPY backend/ backend/
RUN dotnet publish backend/DailyChallenges.csproj -c Release -o /app --no-restore
# The release pipeline writes backend/release-info.json before building; make sure it ships.
RUN if [ -f backend/release-info.json ]; then cp backend/release-info.json /app/release-info.json; fi

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# SQLite lives in /data (named volume). Create it owned by the non-root app user
# ($APP_UID is provided by the aspnet base image) so the DB and its WAL/SHM files are writable.
RUN mkdir -p /data && chown "$APP_UID:$APP_UID" /data
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000
ENTRYPOINT ["dotnet", "DailyChallenges.dll"]
