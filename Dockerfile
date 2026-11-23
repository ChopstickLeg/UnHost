FROM node:24-bookworm-slim AS node

FROM mcr.microsoft.com/dotnet/sdk:11.0-resolute-aot AS build
WORKDIR /src

COPY --from=node /usr/local/ /usr/local/
COPY unhost-ui/package.json unhost-ui/package-lock.json unhost-ui/
RUN npm ci --prefix unhost-ui

COPY . .
RUN dotnet publish Unhost.API/Unhost.API.csproj -c Release -o /publish -p:ShouldRunNpmInstall=false

FROM mcr.microsoft.com/dotnet/runtime-deps:11.0-resolute
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_CONTENTROOT=/app
EXPOSE 8080

COPY --from=build /publish/ /app/
RUN mkdir /data && chown "$APP_UID" /data
WORKDIR /data
USER $APP_UID

ENTRYPOINT ["/app/Unhost.API"]
