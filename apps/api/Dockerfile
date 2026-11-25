FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/Tms.Domain/Tms.Domain.csproj src/Tms.Domain/
COPY src/Tms.Infrastructure/Tms.Infrastructure.csproj src/Tms.Infrastructure/
COPY src/Tms.Api/Tms.Api.csproj src/Tms.Api/
RUN dotnet restore src/Tms.Api/Tms.Api.csproj

COPY src/ src/
RUN dotnet publish src/Tms.Api/Tms.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish/ ./

# Render monta Secret Files como grupo 1000; o processo app precisa lê-los
# para validar a CA do PostgreSQL com VerifyFull.
USER root
RUN usermod -a -G 1000 app
USER app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
ENTRYPOINT ["sh", "-c", "exec dotnet Tms.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
