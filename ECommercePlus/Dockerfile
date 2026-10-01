FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
RUN mkdir -p /app/App_Data/keys /app/App_Data/certs && chown -R $APP_UID /app/App_Data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_HTTPS_PORTS=8443 \
    ConnectionStrings__Default="Data Source=/app/App_Data/ecommerce.db" \
    DataProtection__KeysPath=/app/App_Data/keys \
    Https__SelfSignedCertificatePath=/app/App_Data/certs/selfsigned.pfx \
    AdminSeed__PasswordFilePath=/app/App_Data/initial-admin-password.txt
EXPOSE 8080 8443
VOLUME ["/app/App_Data"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ECommercePlus/ECommercePlus.csproj", "ECommercePlus/"]
RUN dotnet restore "ECommercePlus/ECommercePlus.csproj"
COPY ECommercePlus/ ECommercePlus/
WORKDIR "/src/ECommercePlus"
RUN dotnet publish "./ECommercePlus.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ECommercePlus.dll"]
