FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/UnshackledWord.Tooling.BibleTagger/UnshackledWord.Tooling.BibleTagger.csproj", "src/UnshackledWord.Tooling.BibleTagger/"]
RUN dotnet restore "src/UnshackledWord.Tooling.BibleTagger/UnshackledWord.Tooling.BibleTagger.csproj"
COPY . .
WORKDIR "/src/src/UnshackledWord.Tooling.BibleTagger"
RUN dotnet build "./UnshackledWord.Tooling.BibleTagger.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UnshackledWord.Tooling.BibleTagger.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UnshackledWord.Tooling.BibleTagger.dll"]
