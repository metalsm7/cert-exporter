# docker build --rm --no-cache -t mparang/cert-exporter:latest .

# builder
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /cert_exporter_builder

COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /cert_exporter
COPY --from=build-env /cert_exporter_builder/out .
ENTRYPOINT ["dotnet", "CertExporter.dll"]

# runtime for alpine
#FROM docker.io/library/alpine:latest
#WORKDIR /cert_exporter
#COPY --from=build-env /cert_exporter_builder/out .
#CMD ["sh", "-c", "tail -f /dev/null"]