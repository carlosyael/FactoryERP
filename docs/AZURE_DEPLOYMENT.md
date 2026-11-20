# Guía de Despliegue en Microsoft Azure y CI/CD — Génesis (FactoryERP)

**Génesis / FactoryERP** se encuentra arquitectado para operar en **Microsoft Azure** de forma 100% cloud-native, accesible vía **HTTPS** desde cualquier red tanto para operadores internos del ERP como para clientes y salones que visiten el catálogo público (`/catalogo`).

---

## Arquitectura Cloud en Producción (`canadacentral`)

```
Internet (HTTPS / TLS) ──▶ Azure Container Apps (app-factoryerp, 0.5 vCPU, 1 GiB, min/max=1)
                                  │  ├─ Blazor Interactive Server (WebSockets / SignalR estable)
                                  │  └─ Usuario no-root appuser, puerto 8080
                                  │
                                  ├──▶ Azure SQL Database (FactoryERP_DB, Basic 5 DTU)
                                  │      ├─ Tablas operacionales y contables (EF Core MigrateAsync)
                                  │      └─ Tabla DataProtectionKeys (sesiones/cookies persistentes)
                                  │
                                  ├──▶ Azure Blob Storage Privado (genesisfactoryerpst / uploads)
                                  │      └─ Logos y fotos servidas vía GET /uploads/{folder}/{file}
                                  │
                                  └──▶ Azure Container Registry (genesisfactoryerpacr.azurecr.io)
```

> **Nota sobre escalado horizontal:** En entornos Express de Azure Container Apps, las sticky sessions no están disponibles; por ello se fija `minReplicas = 1, maxReplicas = 1` para garantizar continuidad de los circuitos SignalR de Blazor Interactive Server. Si en el futuro se requiere escalar a múltiples réplicas, integrar **Azure SignalR Service** (`AddSignalR().AddAzureSignalR()`).

---

## Inventario de Recursos Provisionados (`rg-factoryerp-prod`)

| Recurso Azure | Nombre | Tipo / SKU | Propósito |
| :--- | :--- | :--- | :--- |
| Resource Group | `rg-factoryerp-prod` | `canadacentral` | Contenedor lógico de producción |
| Azure SQL Server | `genesisfactoryerp-sql` | SQL PaaS (`AllowAzureServices`) | Motor SQL gestionado |
| Azure SQL Database | `FactoryERP_DB` | `Basic` (5 DTU, 2 GB) | Base de datos transaccional y `DataProtectionKeys` |
| Storage Account | `genesisfactoryerpst` | `Standard_LRS` (`StorageV2`, acceso público deshabilitado) | Almacenamiento privado de imágenes en contenedor `uploads` |
| Container Registry | `genesisfactoryerpacr` | `Basic` | Repositorio privado de imágenes Docker |
| Container Apps Env | `genesisfactoryerp-env` | Consumption / Log Analytics | Entorno serverless administrado |
| Container App | `app-factoryerp` | Ingress HTTPS externo `:8080`, `min=1 / max=1` | Ejecución del contenedor `FactoryERP.Web` |

---

## Pipeline CI/CD Automatizado (`.github/workflows/ci.yml`)

Cada `push` o Pull Request a `main` ejecuta automáticamente el flujo completo con **autenticación OIDC federada sin contraseñas estáticas**:

1. **Job `build-and-test`:**
   - `dotnet restore FactoryERP.sln`
   - `dotnet build FactoryERP.sln --configuration Release --no-restore /warnaserror` (cero advertencias toleradas)
   - `dotnet test FactoryERP.sln --configuration Release --no-build --verbosity normal`
2. **Job `deploy-azure` (solo en `push` a `main` o `workflow_dispatch`):**
   - Inicia sesión en Azure con `azure/login@v2` usando OIDC (`sp-factoryerp-github-cd`, rol `Contributor` acotado únicamente a `rg-factoryerp-prod`, credencial federada para `repo:carlosyael/FactoryERP:ref:refs/heads/main`).
   - Obtiene token efímero de ACR (`az acr login --expose-token`) y publica la imagen Docker construída desde `src/Dockerfile` con tags `:${{ github.sha }}` y `:latest`.
   - Actualiza la revisión activa de `app-factoryerp` con `az containerapp update --image ...`.
   - Verifica en bucle la salud en vivo de `https://<fqdn>/health` esperando `200 OK` y `"database":"connected"`.

### Secretos y Variables Configurados en GitHub Actions (`carlosyael/FactoryERP`)

- **Secrets:** `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`
- **Variables:** `AZURE_RESOURCE_GROUP`, `AZURE_CONTAINERAPP_NAME`, `ACR_NAME`, `ACR_LOGIN_SERVER`

---

## Replicación desde Cero (`infra/deploy-azure.ps1`)

Para desplegar un nuevo comercio white-label en una suscripción limpia desde la raíz de `FactoryERP`:

```powershell
.\infra\deploy-azure.ps1 `
  -ResourceGroup "rg-factoryerp-prod" `
  -Location "canadacentral" `
  -UniquePrefix "genesisfactoryerp" `
  -SqlAdminPassword "<PASSWORD_SQL_SEGURO>" `
  -JwtSecretKey "<CLAVE_JWT_MIN_32_BYTES>" `
  -SeedAdminPassword "<PASSWORD_ADMIN_INICIAL>"
```

---

## Primeros Pasos Post-Despliegue

1. Verificar salud del servicio: `GET https://<fqdn>/health` → responde `200 OK` con `"platform": "Génesis"` y `"database": "connected"`.
2. Iniciar sesión en `/login` con `admin@factoryerp.local` y la contraseña inicial generada al aprovisionar (cambiar la contraseña o crear usuarios operativos en `/users`).
3. Ir a **`/settings` (Configuración)** para personalizar **Nombre del Comercio**, slogan, logotipo (persistido en Azure Blob Storage), color primario, RNC, teléfono, dirección y **WhatsApp de pedidos**.
4. Compartir el enlace público **`https://<fqdn>/catalogo`** con clientes y salones.
