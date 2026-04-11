FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Build.props"]
COPY ["Directory.Packages.props", "Directory.Packages.props"]
COPY ["src/UnshackledWord.Tooling.WebApi/", "src/UnshackledWord.Tooling.WebApi/"]
COPY ["src/UnshackledWord.Persistence.Postgres/", "src/UnshackledWord.Persistence.Postgres/"]
COPY ["src/UnshackledWord.Infrastructure/", "src/UnshackledWord.Infrastructure/"]
COPY ["src/UnshackledWord.Application/", "src/UnshackledWord.Application/"]
COPY ["src/UnshackledWord.Domain/", "src/UnshackledWord.Domain/"]

WORKDIR "/src/src/UnshackledWord.Tooling.WebApi"
RUN dotnet restore "./UnshackledWord.Tooling.WebApi.csproj"
RUN dotnet build "./UnshackledWord.Tooling.WebApi.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UnshackledWord.Tooling.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UnshackledWord.Tooling.WebApi.dll"]
