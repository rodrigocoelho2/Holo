# TcgStockBot.WebApi — the bot (background monitor + JSON API). Build context: the solution folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY TcgStockBot.Library/TcgStockBot.Library.csproj TcgStockBot.Library/
COPY TcgStockBot.WebApi/TcgStockBot.WebApi.csproj TcgStockBot.WebApi/
RUN dotnet restore TcgStockBot.WebApi/TcgStockBot.WebApi.csproj
COPY TcgStockBot.Library/ TcgStockBot.Library/
COPY TcgStockBot.WebApi/ TcgStockBot.WebApi/
RUN dotnet publish TcgStockBot.WebApi/TcgStockBot.WebApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# /data holds bot-config.json, alert-state.json and alert-history.json. Created owned by the non-root
# "app" user so a named volume inherits that ownership (same layout as v2, so existing volumes keep working).
RUN mkdir -p /data && chown "$APP_UID" /data
VOLUME /data

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    TcgStockBot__DataDirectory=/data \
    TZ=Europe/Lisbon

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TcgStockBot.WebApi.dll"]
