<#
.SYNOPSIS
    Despliega FactoryERP en Microsoft Azure (ACR + Azure App Service Linux + Azure SQL Database)
    con acceso público HTTPS desde cualquier red y WebSockets habilitados para Blazor Server.
#>
param(
    [Parameter(Mandatory = $true)] [string]$ResourceGroup,
    [Parameter(Mandatory = $true)] [string]$Location = "eastus",
    [Parameter(Mandatory = $true)] [string]$UniquePrefix,
    [Parameter(Mandatory = $true)] [string]$SqlAdminPassword,
    [Parameter(Mandatory = $true)] [string]$JwtSecretKey,
    [string]$SeedAdminPassword = "ChangeMeFirstLogin2026!"
)

$ErrorActionPreference = "Stop"

$acrName = ($UniquePrefix + "acr").ToLower() -replace "[^a-z0-9]", ""
$sqlServer = ($UniquePrefix + "-sql").ToLower()
$sqlDb = "FactoryERP_DB"
$appPlan = ($UniquePrefix + "-plan").ToLower()
$webApp = ($UniquePrefix + "-web").ToLower()

Write-Host "1. Creando Resource Group '$ResourceGroup' en '$Location'..."
az group create --name $ResourceGroup --location $Location | Out-Null

Write-Host "2. Creando Azure Container Registry '$acrName' y compilando imagen..."
az acr create --resource-group $ResourceGroup --name $acrName --sku Basic --admin-enabled true | Out-Null
az acr build --registry $acrName --image factoryerp-web:latest --file src/Dockerfile src

Write-Host "3. Creando Azure SQL Server y Database '$sqlDb'..."
az sql server create --name $sqlServer --resource-group $ResourceGroup --location $Location --admin-user factoryadmin --admin-password $SqlAdminPassword | Out-Null
az sql server firewall-rule create --resource-group $ResourceGroup --server $sqlServer --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 | Out-Null
az sql db create --resource-group $ResourceGroup --server $sqlServer --name $sqlDb --service-objective Basic | Out-Null

$connStr = "Server=tcp:$sqlServer.database.windows.net,1433;Initial Catalog=$sqlDb;Persist Security Info=False;User ID=factoryadmin;Password=$SqlAdminPassword;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$acrPass = az acr credential show --name $acrName --query "passwords[0].value" -o tsv

Write-Host "4. Creando App Service Plan (Linux) y Web App '$webApp'..."
az appservice plan create --name $appPlan --resource-group $ResourceGroup --is-linux --sku B1 | Out-Null
az webapp create --resource-group $ResourceGroup --plan $appPlan --name $webApp `
    --deployment-container-image-name "$acrName.azurecr.io/factoryerp-web:latest" | Out-Null

az webapp config container set --name $webApp --resource-group $ResourceGroup `
    --docker-custom-image-name "$acrName.azurecr.io/factoryerp-web:latest" `
    --docker-registry-server-url "https://$acrName.azurecr.io" `
    --docker-registry-server-user $acrName `
    --docker-registry-server-password $acrPass | Out-Null

Write-Host "5. Configurando variables de entorno, WebSockets y Affinity para Blazor Server..."
az webapp config set --resource-group $ResourceGroup --name $webApp --web-sockets-enabled true --always-on true | Out-Null
az webapp update --resource-group $ResourceGroup --name $webApp --client-affinity-enabled true --https-only true | Out-Null

az webapp config appsettings set --resource-group $ResourceGroup --name $webApp --settings `
    WEBSITES_PORT=8080 `
    WEBSITES_ENABLE_APP_SERVICE_STORAGE=true `
    ASPNETCORE_ENVIRONMENT=Production `
    DatabaseProvider=SqlServer `
    ConnectionStrings__DefaultConnection="$connStr" `
    JwtSettings__SecretKey="$JwtSecretKey" `
    SEED_ADMIN_PASSWORD="$SeedAdminPassword" `
    DATA_PROTECTION_KEYS_PATH="/home/dataprotection-keys" | Out-Null

$hostName = az webapp show --resource-group $ResourceGroup --name $webApp --query "defaultHostName" -o tsv
Write-Host "Despliegue completado! URL pública HTTPS: https://$hostName" -ForegroundColor Green
Write-Host "Catálogo público compartible: https://$hostName/catalogo" -ForegroundColor Green
Write-Host "Health check: https://$hostName/health" -ForegroundColor Green
