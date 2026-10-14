# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY RakRao.sln ./
COPY src/ src/
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then \
      SSL_CERT_FILE=/run/secrets/proxy_ca dotnet restore src/RakRao.Api/RakRao.Api.csproj; \
    else dotnet restore src/RakRao.Api/RakRao.Api.csproj; fi
RUN dotnet publish src/RakRao.Api/RakRao.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Staging
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec dotnet RakRao.Api.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]
