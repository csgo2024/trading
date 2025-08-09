FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Directory.Packages.props .

COPY ["src/Trading.API/Trading.API.csproj", "Trading.API/"]
COPY ["src/Trading.Common/Trading.Common.csproj", "Trading.Common/"]
COPY ["src/Trading.Domain/Trading.Domain.csproj", "Trading.Domain/"]
COPY ["src/Trading.Exchange.Abstraction/Trading.Exchange.Abstraction.csproj", "Trading.Exchange.Abstraction/"]
COPY ["src/Trading.Exchange.Binance/Trading.Exchange.Binance.csproj", "Trading.Exchange.Binance/"]
COPY ["src/Trading.Infrastructure/Trading.Infrastructure.csproj", "Trading.Infrastructure/"]
COPY ["src/Trading.Application/Trading.Application.csproj", "Trading.Application/"]
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
