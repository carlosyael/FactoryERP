# Bitácora de Desarrollo (PROMPT_HISTORY) — FactoryERP

## [2026-11-19] — Inicialización de Arquitectura, Desarrollo TDD y Entrega v1.0 Lista para Azure
- **Requerimiento:** Crear un ERP para fabricante de productos capilares basado en el stack de `DomPharma` (`.NET 9`, `Blazor Web App InteractiveServer`, `EF Core 9`, `SQL Server / SQLite`, `Identity`, `Bootstrap 5.3`, `SweetAlert2`, `xUnit`, `FluentAssertions`, `Moq`), bajo metodologías ágiles, Clean Code (SRP ≤ 200 líneas) y TDD estricto, con:
  - Identidad del comercio 100% configurable desde `/settings` (white-label).
  - Módulo de preparación y fabricación (materias primas/empaques, fórmulas versionadas, simulación de materiales y costos, margen de pérdida, cantidad resultante real, merma normal vs. anormal bajo NIC 2, lotes terminados FEFO y recálculo CPP).
  - Contabilidad de partida doble integrada (catálogo industrial con Materias Primas `1104`, WIP `1105`, Terminados `1106`, COGS `5101`, Merma Anormal `5201`; asientos automáticos y manuales, gastos, Libro Diario, Balanza, Estado de Resultados y Balance General).
  - Compras a suplidores (CPP e ITBIS 18%, CxP y abonos).
  - Clientes (Minorista/Salón/Mayorista, crédito, CxC y abonos), pedidos/ventas FEFO en RD$ con ITBIS 18%, y facturación electrónica DGII desacoplada vía `ITaxDocumentProvider` (marcada como *Pendiente de implementación*).
  - Catálogo interno (costos/márgenes/subida de fotos) y catálogo público compartible (`/catalogo`) con pedidos por WhatsApp.
  - Despliegue preparado para **Microsoft Azure** accesible desde cualquier red.
- **Resultado Técnico Verificado:**
  - **Migración EF Core SQL Server:** `20261119205633_InitialCreate` generada limpiamente en `src/FactoryERP.Infrastructure/Migrations/`.
  - **Compilación Release estricta (`/warnaserror`):** `0 Warning(s), 0 Error(s)`.
  - **Suite TDD (`dotnet test --configuration Release`):** **49/49 pruebas aprobadas** (dominio puro, configuración white-label, carga segura de imágenes, partida doble completa y liquidación WIP = 0, compras CPP, fórmulas con conversión de unidades, producción con mermas/cancelación/concurrencia, ventas FEFO/crédito/concurrencia, catálogo interno/público y KPIs).
  - **Auditoría Clean Code (SRP):** 0 archivos de producción superan las 185 líneas.
  - **Smoke Test en vivo (`FactoryERP.Web.exe`, `Development` SQLite):** `/health` HTTP 200 (`connected`), `/catalogo` HTTP 200 (muestra comercio configurado y producto sembrado sin exponer costos), `/login` HTTP 200, autenticación Anti-CSRF `POST /login` y acceso a `/` (*Panel Principal*) HTTP 200.
  - **Contenedores / Azure:** `src/Dockerfile`, `docker-compose.yml`, `.github/workflows/ci.yml`, `infra/deploy-azure.ps1` y `docs/AZURE_DEPLOYMENT.md` listos (`docker build` local omitido porque el daemon de Docker Desktop no estaba iniciado en el host).
