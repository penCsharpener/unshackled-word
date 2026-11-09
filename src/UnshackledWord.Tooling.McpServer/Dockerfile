FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "Directory.Build.props"]
COPY ["Directory.Packages.props", "Directory.Packages.props"]
COPY ["src/UnshackledWord.Tooling.McpServer/", "src/UnshackledWord.Tooling.McpServer/"]
COPY ["src/UnshackledWord.Persistence.Postgres/", "src/UnshackledWord.Persistence.Postgres/"]
COPY ["src/UnshackledWord.Infrastructure/", "src/UnshackledWord.Infrastructure/"]
COPY ["src/UnshackledWord.Application/", "src/UnshackledWord.Application/"]
COPY ["src/UnshackledWord.Domain/", "src/UnshackledWord.Domain/"]
COPY ["src/UnshackledWord.Domain.WebApi/", "src/UnshackledWord.Domain.WebApi/"]

WORKDIR "/src/src/UnshackledWord.Tooling.McpServer"
RUN dotnet restore "./UnshackledWord.Tooling.McpServer.csproj"
RUN dotnet build "./UnshackledWord.Tooling.McpServer.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UnshackledWord.Tooling.McpServer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UnshackledWord.Tooling.McpServer.dll"]
