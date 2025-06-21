FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/UnshackledWord.Tooling.Postgres.Migration/UnshackledWord.Tooling.Postgres.Migration.csproj", "src/UnshackledWord.Tooling.Postgres.Migration/"]
COPY ["src/UnshackledWord.Infrastructure/UnshackledWord.Infrastructure.csproj", "src/UnshackledWord.Infrastructure/"]
COPY ["src/UnshackledWord.Application/UnshackledWord.Application.csproj", "src/UnshackledWord.Application/"]
COPY ["src/UnshackledWord.Domain/UnshackledWord.Domain.csproj", "src/UnshackledWord.Domain/"]
COPY ["src/UnshackledWord.Persistence.Postgres/UnshackledWord.Persistence.Postgres.csproj", "src/UnshackledWord.Persistence.Postgres/"]
RUN dotnet restore "src/UnshackledWord.Tooling.Postgres.Migration/UnshackledWord.Tooling.Postgres.Migration.csproj"
#COPY . .
WORKDIR "/src/src/UnshackledWord.Tooling.Postgres.Migration"
RUN dotnet build "./UnshackledWord.Tooling.Postgres.Migration.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UnshackledWord.Tooling.Postgres.Migration.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UnshackledWord.Tooling.Postgres.Migration.dll"]
