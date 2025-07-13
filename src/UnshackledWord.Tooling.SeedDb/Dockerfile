FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/UnshackledWord.Tooling.SeedDb/", "src/UnshackledWord.Tooling.SeedDb/"]
COPY ["src/UnshackledWord.Application/", "src/UnshackledWord.Application/"]
COPY ["src/UnshackledWord.Domain/", "src/UnshackledWord.Domain/"]
COPY ["src/UnshackledWord.Infrastructure/", "src/UnshackledWord.Infrastructure/"]
COPY ["src/UnshackledWord.Persistence.Postgres/", "src/UnshackledWord.Persistence.Postgres/"]
COPY ["assets/", "assets/"]
COPY ["Directory.Build.props", "Directory.Build.props"]
COPY ["Directory.Packages.props", "Directory.Packages.props"]

WORKDIR "/src/src/UnshackledWord.Tooling.SeedDb"
RUN dotnet restore "./UnshackledWord.Tooling.SeedDb.csproj"
RUN dotnet build "./UnshackledWord.Tooling.SeedDb.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UnshackledWord.Tooling.SeedDb.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UnshackledWord.Tooling.SeedDb.dll"]
