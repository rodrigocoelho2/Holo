# TcgStockBot.Website — the dashboard. Build context: the solution folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY TcgStockBot.Library/TcgStockBot.Library.csproj TcgStockBot.Library/
COPY TcgStockBot.Sdk/TcgStockBot.Sdk.csproj TcgStockBot.Sdk/
COPY TcgStockBot.Website/TcgStockBot.Website.csproj TcgStockBot.Website/
RUN dotnet restore TcgStockBot.Website/TcgStockBot.Website.csproj
COPY TcgStockBot.Library/ TcgStockBot.Library/
COPY TcgStockBot.Sdk/ TcgStockBot.Sdk/
COPY TcgStockBot.Website/ TcgStockBot.Website/
# Files that reach the build context through a Windows/SMB share or a git checkout can be readable by their owner
# only, and publish copies appsettings.json and wwwroot with those modes. The app runs as the non-root "app" user,
# so make everything readable (and folders traversable) for everyone.
RUN dotnet publish TcgStockBot.Website/TcgStockBot.Website.csproj -c Release -o /app --no-restore \
    && chmod -R a+rX /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# /keys keeps the login cookie keys, so "keep me signed in" survives updates. Owned by the non-root "app" user so a
# named volume inherits that ownership.
RUN mkdir -p /keys && chown "$APP_UID" /keys
VOLUME /keys

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Dashboard__KeysDirectory=/keys \
    TZ=Europe/Lisbon

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "TcgStockBot.Website.dll"]
