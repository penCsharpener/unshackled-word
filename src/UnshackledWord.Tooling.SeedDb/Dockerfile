# Use the official .NET 9 SDK image for building the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["UnshackledWord.Tooling.SeedDb/UnshackledWord.Tooling.SeedDb.csproj", "UnshackledWord.Tooling.SeedDb/"]
RUN dotnet restore "UnshackledWord.Tooling.SeedDb/UnshackledWord.Tooling.SeedDb.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/UnshackledWord.Tooling.SeedDb"
RUN dotnet publish "UnshackledWord.Tooling.SeedDb.csproj" -c Release -o /app/publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UnshackledWord.Tooling.SeedDb.dll"]