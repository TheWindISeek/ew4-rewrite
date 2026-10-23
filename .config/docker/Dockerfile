# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Ew4.sln ./
COPY src/ src/
RUN dotnet restore src/Ew4.Backend.Web/Ew4.Backend.Web.csproj
RUN dotnet publish src/Ew4.Backend.Web/Ew4.Backend.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish/ ./
RUN mkdir -p /app/install/mods /app/install/user && chown -R "$APP_UID:$APP_UID" /app/install
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_NOLOGO=1 \
    EW4_HOME=/app/install
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Ew4.Backend.Web.dll", "--root", "/app", "--home", "/app/install", "--urls", "http://0.0.0.0:8080"]
