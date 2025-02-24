FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Trading.API/Trading.API.csproj", "Trading.API/"]
COPY ["src/Trading.Common/Trading.Common.csproj", "Trading.Common/"]
COPY ["src/Trading.Domain/Trading.Domain.csproj", "Trading.Domain/"]
COPY ["src/Trading.Infrastructure/Trading.Infrastructure.csproj", "Trading.Infrastructure/"]
RUN dotnet restore "Trading.API/Trading.API.csproj"
COPY src .
WORKDIR "/src/Trading.API"
RUN dotnet build "Trading.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Trading.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Trading.API.dll"]
