<#
.SYNOPSIS
    Despliega Génesis / FactoryERP en Microsoft Azure (ACR + Azure Container Apps + Azure SQL Database + Azure Blob Storage)
    con acceso público HTTPS desde cualquier red, persistencia DataProtection en SQL y archivos subidos en Blob privado.
.NOTES
    Regiones válidas habituales en Azure for Students: canadacentral, westus2, brazilsouth, francecentral, spaincentral.
#>
param(
    [Parameter(Mandatory = $true)] [string]$ResourceGroup,
    [string]$Location = "canadacentral",
    [Parameter(Mandatory = $true)] [string]$UniquePrefix,
    [Parameter(Mandatory = $true)] [string]$SqlAdminPassword,
    [Parameter(Mandatory = $true)] [string]$JwtSecretKey,
    [Parameter(Mandatory = $true)] [string]$SeedAdminPassword,
    [switch]$SkipInitialAcrBuild
)

$ErrorActionPreference = "Stop"

$cleanPrefix = $UniquePrefix.ToLower() -replace "[^a-z0-9]", ""
$acrName = "${cleanPrefix}acr"
$storageName = "${cleanPrefix}st"
$sqlServer = "$($UniquePrefix.ToLower())-sql"
$sqlDb = "FactoryERP_DB"
$caEnv = "$($UniquePrefix.ToLower())-env"
$containerApp = "app-$($UniquePrefix.ToLower())"

Write-Host "0. Verificando registro de proveedores Microsoft.Sql, Microsoft.App y Microsoft.Storage..."
az provider register --namespace Microsoft.Sql --wait | Out-Null
az provider register --namespace Microsoft.App --wait | Out-Null
az provider register --namespace Microsoft.Storage --wait | Out-Null

Write-Host "1. Creando Resource Group '$ResourceGroup' en '$Location'..."
az group create --name $ResourceGroup --location $Location -o none

Write-Host "2. Creando Azure Container Registry '$acrName'..."
az acr create --resource-group $ResourceGroup --name $acrName --location $Location --sku Basic --admin-enabled true -o none
$imageTag = if ($SkipInitialAcrBuild) {
    "mcr.microsoft.com/k8se/quickstart:latest"
} else {
    az acr build --registry $acrName --image factoryerp-web:latest --file src/Dockerfile src
    "$acrName.azurecr.io/factoryerp-web:latest"
}

Write-Host "3. Creando Azure SQL Server '$sqlServer' y Database '$sqlDb' (Basic)..."
az sql server create --name $sqlServer --resource-group $ResourceGroup --location $Location --admin-user factoryadmin --admin-password $SqlAdminPassword -o none
az sql server firewall-rule create --resource-group $ResourceGroup --server $sqlServer --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 -o none
az sql db create --resource-group $ResourceGroup --server $sqlServer --name $sqlDb --service-objective Basic --max-size 2GB -o none

Write-Host "4. Creando Storage Account '$storageName' y contenedor Blob privado 'uploads'..."
az storage account create --name $storageName --resource-group $ResourceGroup --location $Location --sku Standard_LRS --kind StorageV2 --allow-blob-public-access false --min-tls-version TLS1_2 -o none
$storageKey = az storage account keys list --resource-group $ResourceGroup --account-name $storageName --query "[0].value" -o tsv
$storageConnStr = az storage account show-connection-string --resource-group $ResourceGroup --name $storageName --query "connectionString" -o tsv
az storage container create --name uploads --account-name $storageName --account-key $storageKey --public-access off -o none

Write-Host "5. Creando Azure Container Apps Environment '$caEnv'..."
az containerapp env create --name $caEnv --resource-group $ResourceGroup --location $Location -o none

$connStr = "Server=tcp:$sqlServer.database.windows.net,1433;Initial Catalog=$sqlDb;Persist Security Info=False;User ID=factoryadmin;Password=$SqlAdminPassword;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$acrPass = az acr credential show --name $acrName --query "passwords[0].value" -o tsv

Write-Host "6. Creando Container App '$containerApp' (instancia única estable para Blazor Server + HTTPS + ACR)..."
az containerapp create --name $containerApp --resource-group $ResourceGroup --environment $caEnv `
    --image $imageTag --ingress external --target-port 8080 --transport auto `
    --cpu 0.5 --memory 1.0Gi --min-replicas 1 --max-replicas 1 `
    --secrets "sql-conn-str=$connStr" "jwt-secret-key=$JwtSecretKey" "seed-admin-password=$SeedAdminPassword" "storage-conn-str=$storageConnStr" `
    --env-vars "ASPNETCORE_ENVIRONMENT=Production" "ASPNETCORE_URLS=http://+:8080" "DatabaseProvider=SqlServer" `
        "ConnectionStrings__DefaultConnection=secretref:sql-conn-str" "JwtSettings__SecretKey=secretref:jwt-secret-key" `
        "SEED_ADMIN_PASSWORD=secretref:seed-admin-password" "AZURE_STORAGE_CONNECTION_STRING=secretref:storage-conn-str" `
        "AZURE_STORAGE_CONTAINER_NAME=uploads" -o none

az containerapp registry set --resource-group $ResourceGroup --name $containerApp `
    --server "$acrName.azurecr.io" --username $acrName --password $acrPass -o none

$fqdn = az containerapp show --resource-group $ResourceGroup --name $containerApp --query "properties.configuration.ingress.fqdn" -o tsv
Write-Host "Despliegue completado! URL pública HTTPS: https://$fqdn" -ForegroundColor Green
Write-Host "Catálogo público compartible: https://$fqdn/catalogo" -ForegroundColor Green
Write-Host "Health check: https://$fqdn/health" -ForegroundColor Green
