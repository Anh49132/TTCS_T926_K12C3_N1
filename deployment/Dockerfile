# Build context: the clean directory returned by Prepare-Online.ps1.
# The Release build runs locally so build licenses never enter the image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    DatabasePath=/data/nhatot.sqlite \
    IdentityImagePath=/data/identity-images \
    RoomImagesPath=/data/rooms \
    DataProtectionKeysPath=/data/keys
COPY app/ ./
COPY start.sh /app/start.sh
EXPOSE 8080
ENTRYPOINT ["/bin/sh", "/app/start.sh"]
