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

## [2026-11-19] — Perfil de Arranque Local (`launchSettings.json`)
- **Requerimiento:** Corregir fallo al ejecutar `dotnet run --project .\src\FactoryERP.Web\FactoryERP.Web.csproj` sin variables de entorno manuales (`JWT_SECRET_KEY debe configurarse con al menos 32 bytes`).
- **Diagnóstico:** Faltaba `src/FactoryERP.Web/Properties/launchSettings.json`, por lo que `dotnet run` arrancaba en entorno `Production` por defecto en lugar de `Development` (donde reside `appsettings.Development.json` con SQLite y la clave JWT local).
- **Resultado:** Añadido `src/FactoryERP.Web/Properties/launchSettings.json` con perfiles `http` (`http://localhost:5210`) y `https` (`https://localhost:7210;http://localhost:5210`) en `Development`. Verificado arranque directo con `dotnet run` respondiendo `200 OK` en `http://localhost:5210/health`, `0 Warning(s) / 0 Error(s)` y `49/49` pruebas aprobadas.

## [2026-11-19] — Auditoría Integral de Módulos, Correcciones UI/Dominio, Paridad Agéntica y Expansión TDD
- **Requerimiento:** Corregir ícono faltante en *Fórmulas y Recetas*, cambiar el nombre inicial del comercio a `FactoryERP` y proponer nombres de marca trascendentales, auditar todos los módulos y pruebas, y alinear las directrices agénticas con `DomPharma`.
- **Hallazgos y Correcciones Aplicadas:**
  - **UI e Iconografía:** Reemplazados íconos no existentes en Bootstrap Icons 1.11.3 (`bi-flask` → `bi-eyedropper` en `AppMenus` y `Settings.razor`, `bi-scale` → `bi-calculator` en `AppMenus`) con reparación automática en `DbInitializer.SeedMenusAsync`; corregido render literal `v@f.Version` → `v@(f.Version)` en `Formulas.razor` y `ProductionOrders.razor`.
  - **Identidad por Defecto:** Actualizado nombre comercial inicial a `FactoryERP` (con migración automática del placeholder previo) manteniendo edición libre en `/settings`.
  - **Integridad Contable y Reglas de Negocio:** Corregido reparto residual de descuento/ITBIS en `FefoBatchAllocator.cs` para garantizar cuadre al centavo en entregas multi-lote; agregado `PostRawMaterialAdjustmentAsync` en `IManufacturingAccountingService`/`RawMaterialService.AdjustStockAsync`; endurecidas validaciones en `FormulaService`, `ProductionOrderService` (`BatchCode` único), `ProductService`, `CustomerService` y `SupplierService`.
  - **Mejoras Operativas en UI:** Costo teórico por fórmula en `Formulas.razor/.cs`; auto-simulación y confirmación `Swal` en `ProductionOrders.razor/.cs`; carrito persistente entre filtros en `PublicCatalog.razor/.cs`; resumen en vivo en `Sales.razor/.cs`; filtros y acciones Mayorizar/Anular en `JournalEntries.razor/.cs`; activación/desactivación protegida de usuarios en `Users.razor`.
  - **Paridad Agéntica:** Actualizados `AGENTS.md`, `AI_INSTRUCTIONS.md`, `.cursorrules` y creado `docs/ARCHITECTURE_AND_MODULARITY.md` replicando el estándar y flujo de 9 pasos de `DomPharma`.
- **Verificación de Salida:**
  - `dotnet build FactoryERP.sln --configuration Release /warnaserror`: `0 Warning(s), 0 Error(s)`.
  - `dotnet test FactoryERP.sln --configuration Release --no-build`: **86 / 86 pruebas aprobadas** (incluyendo flujo completo E2E manufactura + contabilidad, concurrencia, FEFO centavo exacto, EP/BVA, `DbInitializer`, `AppMenuService` y `AuditInterceptor`).
  - Smoke test aislado (`http://127.0.0.1:5219`): `/health`, `/catalogo`, `/login` y `/formulas` autenticados en `200 OK` verificando `FactoryERP` y `v1.0` sin `v@f.Version`, `bi-flask` ni `bi-scale`.

## [2026-11-19] — Adopción de la Marca Madre del Ecosistema: **Génesis**
- **Requerimiento:** Nombrar formalmente el ecosistema de sistemas ERP como **Génesis**, integrando la identidad en `FactoryERP` sin alterar el nombre white-label configurable por comercio (`/settings`).
- **Cambios:** Actualizados `<PageTitle>` (`MainLayout.razor`, `EmptyLayout.razor`), pie de acceso en `Login.razor` (`Powered by Génesis`), `/health` (`platform: "Génesis"`), subtítulo por defecto en `DbInitializer`/`BusinessProfileService`, y documentación (`README.md`, `PRD.md`, `TECH_STACK.md`, `AGENTS.md`, `docs/ARCHITECTURE_AND_MODULARITY.md`, `docs/AZURE_DEPLOYMENT.md`).
- **Verificación:** `dotnet build --configuration Release /warnaserror` (`0 Warning(s), 0 Error(s)`) y `dotnet test --configuration Release` (`86/86` pruebas aprobadas).



