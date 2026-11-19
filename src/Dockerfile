FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["FactoryERP.Web/FactoryERP.Web.csproj", "FactoryERP.Web/"]
COPY ["FactoryERP.Core/FactoryERP.Core.csproj", "FactoryERP.Core/"]
COPY ["FactoryERP.Infrastructure/FactoryERP.Infrastructure.csproj", "FactoryERP.Infrastructure/"]
RUN dotnet restore "FactoryERP.Web/FactoryERP.Web.csproj"

COPY . .
WORKDIR "/src/FactoryERP.Web"
RUN dotnet publish "FactoryERP.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser \
    && mkdir -p /app/dataprotection-keys /app/wwwroot/uploads \
    && chown -R appuser:appgroup /app

USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"
ENV DATA_PROTECTION_KEYS_PATH="/app/dataprotection-keys"

ENTRYPOINT ["dotnet", "FactoryERP.Web.dll"]
