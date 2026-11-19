# Guía de Despliegue en Microsoft Azure — Génesis (FactoryERP)

Esta guía prepara el despliegue público de **Génesis / FactoryERP** en **Azure App Service for Containers (Linux)** + **Azure SQL Database**, quedando accesible vía **HTTPS** desde cualquier red para operadores del ERP y para clientes que visiten el catálogo público (`/catalogo`).

---

## Arquitectura Cloud Recomendada

```
Internet (HTTPS / TLS) ──▶ Azure App Service (Docker .NET 9, Puerto 8080, WebSockets ON)
                                  │
                                  ├──▶ Azure SQL Database (FactoryERP_DB, Migración automática)
                                  └──▶ Storage Persistente (/home/dataprotection-keys)
```

---

## Requisitos Previos
1. Cuenta activa de **Microsoft Azure**.
2. **Azure CLI (`az`)** instalado e iniciado con `az login`.
3. Claves seguras preparadas:
   - `SqlAdminPassword`: contraseña robusta para Azure SQL.
   - `JwtSecretKey`: llave aleatoria de al menos 32 caracteres.
   - `SeedAdminPassword`: contraseña inicial para el usuario `admin@factoryerp.local`.

---

## Despliegue Automatizado en 1 Comando (PowerShell)

Desde la raíz del repositorio `FactoryERP`:

```powershell
.\infra\deploy-azure.ps1 `
  -ResourceGroup "rg-factoryerp-prod" `
  -Location "eastus" `
  -UniquePrefix "factorycriss01" `
  -SqlAdminPassword "TuPasswordSQLSeguro2026!" `
  -JwtSecretKey "TuClaveJWTSeguraDeAlMenos32Caracteres2026!" `
  -SeedAdminPassword "TuClaveAdminInicial2026!"
```

### Configuraciones Clave Aplicadas por el Script
- **Acceso desde cualquier red:** HTTPS obligatorio (`--https-only true`), URL pública `https://<webapp>.azurewebsites.net`.
- **Blazor Server (SignalR):** `--web-sockets-enabled true` y `--client-affinity-enabled true` para estabilidad de circuitos.
- **DataProtection Persistente:** `DATA_PROTECTION_KEYS_PATH=/home/dataprotection-keys` con `WEBSITES_ENABLE_APP_SERVICE_STORAGE=true` para conservar sesiones entre reinicios.
- **Migración Automática Segura:** al arrancar en Producción con `DatabaseProvider=SqlServer`, `DbInitializer` ejecuta `Database.MigrateAsync()` y siembra roles, permisos, cuentas contables y configuración base.

---

## Primeros Pasos Post-Despliegue
1. Verificar salud del servicio: `GET https://<tu-app>.azurewebsites.net/health` → debe responder `200 OK` con `"database": "connected"`.
2. Iniciar sesión en `/login` con `admin@factoryerp.local` y tu `SeedAdminPassword` (cambia la contraseña o crea los usuarios del comercio en `/users`).
3. Ir a **`/settings` (Configuración)** y definir:
   - **Nombre del Comercio**, slogan, logotipo, color primario, RNC, teléfono, dirección y **WhatsApp de pedidos**.
4. Compartir el enlace público **`https://<tu-app>.azurewebsites.net/catalogo`** con clientes y salones.

> **Tip para imágenes en producción:** Para persistir imágenes subidas al catálogo en App Service Linux entre escalados horizontales, mantén `WEBSITES_ENABLE_APP_SERVICE_STORAGE=true` o configura Azure Files montado en `/app/wwwroot/uploads`.
