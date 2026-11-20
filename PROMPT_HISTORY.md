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

## [2026-11-20] — Despliegue en Microsoft Azure Container Apps y Pipeline CI/CD OIDC
- **Requerimiento:** Desplegar **Génesis / FactoryERP** públicamente en Microsoft Azure accesible vía HTTPS desde cualquier red y automatizar entregas continuas desde GitHub Actions.
- **Arquitectura Cloud-Native Implementada:**
  - **Persistencia sin volumen SMB (compatible con Container Apps Express):**
    - Claves de ASP.NET Core DataProtection persistidas en Azure SQL mediante `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (`AppDbContext` + migración `20261120131146_AddDataProtectionKeys`).
    - Carga y entrega segura de logotipos e imágenes (`/uploads/{folder}/{fileName}`) respaldadas en **Azure Blob Storage** privado (`AzureBlobImageStore` + `FileUploadService` + `MapExternalUploadsEndpoint`), manteniendo compatibilidad local con `wwwroot/uploads` o `UPLOADS_PATH`.
  - **Recursos en `rg-factoryerp-prod` (`canadacentral`):** `genesisfactoryerp-sql` + `FactoryERP_DB` (`Basic`), `genesisfactoryerpacr` (`Basic`), `genesisfactoryerpst` (`Standard_LRS`, contenedor privado `uploads`), `genesisfactoryerp-env`, y `app-factoryerp` (instancia única `min=1 / max=1` para estabilidad de SignalR Blazor Server, HTTPS externo `https://app-factoryerp.braveflower-c95c4a85.canadacentral.azurecontainerapps.io`).
  - **Pipeline CI/CD (`.github/workflows/ci.yml`):** Compilación estricta (`/warnaserror`) + pruebas TDD (`build-and-test`), seguida de login federado **OIDC** (`sp-factoryerp-github-cd`, sin secretos estáticos), build/push Docker a ACR, parche atómico de registro + revisión en Container Apps y verificación automática de `/health`.
- **Verificación Local:** `dotnet build --configuration Release /warnaserror` (`0 Warning(s), 0 Error(s)`), `dotnet ef migrations has-pending-model-changes` sin cambios pendientes, `90/90` pruebas TDD aprobadas y todos los archivos `.cs` propios ≤ 199 líneas.

## [2026-11-20] — Dominio Corto en Cloudflare (`https://factory.zetaflowlab.com`)
- **Requerimiento:** Acortar la URL larga de Azure Container Apps usando el dominio del usuario en Cloudflare.
- **Solución:** Desplegado Cloudflare Worker `factoryerp-proxy` en la zona `zetaflowlab.com` con dominio personalizado `factory.zetaflowlab.com` apuntando a `app-factoryerp.braveflower-c95c4a85.canadacentral.azurecontainerapps.io`, preservando redirecciones/cookies (`redirect: "manual"`), reescritura de `Origin`/`Referer`/`Location` y WebSockets passthrough para Blazor Interactive Server.
- **Verificación en Vivo:** `GET /health` (`200 OK`, `"platform":"Génesis"`, `"database":"connected"`), `GET /catalogo` (`200 OK`), flujo completo `GET/POST /login` autenticado (`200 OK` en `/`) y `POST /_blazor/negotiate` (`200 OK`, `WebSockets`) sobre `https://factory.zetaflowlab.com`.

## [2026-11-20] — Auditoría Integral End-to-End, CRUD de Roles/Usuarios y Correcciones por Módulo (rama `fix/full-audit-roles-and-modules`)
- **Requerimiento:** Auditar e inspeccionar de inicio a fin todas las funcionalidades implementadas de FactoryERP (Génesis) empezando por la gestión y definición de roles, corregir brechas detectadas, corroborar su ejecución con pruebas y hacer push exclusivamente a una rama distinta de `main` para evitar un despliegue automático.
- **Cambios en Roles, Usuarios, Seguridad y Auditoría (Fase 1):**
  - `Permissions.cs`, `IdentityModels.cs`, `IRoleManagementService.cs`, `IUserManagementService.cs`: metadatos legibles en español y agrupación por módulo para los 14 permisos, DTOs y contratos completos de gestión de roles y usuarios.
  - `RoleManagementService.cs` y `UserManagementService.cs`: CRUD completo de roles con sincronización de claims, validación de permisos duplicados/desconocidos, protección del rol `Admin` (no renombrable, no eliminable, conserva `Users.Manage`/`Settings.Manage`) y bloqueo de borrado de roles con usuarios asignados; creación/edición multi-rol de usuarios, activación/desactivación y reseteo de contraseña protegiendo al último `Admin` activo; refresco de `SecurityStamp`.
  - `Roles.razor/.cs` y `Users.razor/.cs`: UI CRUD completa con permisos agrupados por módulo, selector por grupo, bloqueo visual en permisos críticos de `Admin`, asignación multi-rol y modales de edición/reseteo/eliminación.
  - `AuditInterceptor.cs`, `CurrentUserService.cs`, `MainLayout.razor`, `ServiceRegistrationExtensions.cs`, `SecuritySetupExtensions.cs`: exclusión de `DataProtectionKey` del `AuditLog`, propagación de `ICircuitUserAccessor` en circuitos Blazor Interactive Server para registrar el `UserId` real en auditoría y ajuste de `SecurityStampValidatorOptions.ValidationInterval` a 1 minuto.
  - `Routes.razor`, `NavMenu.razor`, `Dashboard.razor`: vista amigable de `"Acceso restringido"` para usuarios autenticados sin permiso, visualización de múltiples roles en barra lateral y resguardo de enlaces/acciones del dashboard con `AuthorizeView`.
- **Cambios en Configuración, Operación e Inventario (Fase 2):**
  - `BusinessProfileService.cs`, `Settings.razor`: validación de `BusinessName` requerido y rangos numéricos (`DefaultTaxRate` `[0,1]`, `MaxDiscountPercentage` `[0,100]`, `DefaultLossMarginPercent` `[0,99.99]`, `AbnormalLossTolerancePercent` `[0,50]`), más previsualización de logotipo actual.
  - `RawMaterialService.cs`, `RawMaterials.razor`, `Suppliers.razor/.cs`, `PurchaseOrderService.cs`, `Purchases.razor/.cs`: validación de `Sku` obligatorio, autorización en acciones de materias primas, columna `Email` y modal `"Historial"` de pagos en proveedores, `table-responsive`, recarga de materiales tras compra, costo unitario automático y vista previa SubTotal/ITBIS/Total en compras, más resguardo contable para compras de costo `0m`.
  - `Formulas.razor/.cs`, `ProductionExecutionHelper.cs`, `ProductionOrderService.cs`, `ProductionOrders.razor.cs`, `FinishedBatches.razor`: margen de merma por defecto leído desde `IBusinessProfileService`, consulta segura con `TryGetValue`, resguardo contable en órdenes/lotes con costo `0m`, estado activo/inactivo en fórmulas y filtros de búsqueda/existencia en lotes terminados.
- **Cambios en Comercial y Contabilidad (Fase 3):**
  - `InternalCatalog.razor/.cs`, `PublicCatalog.razor`, `Customers.razor/.cs`, `SaleService.cs`, `Sales.razor/.cs`: símbolo de moneda dinámico desde perfil de negocio, previsualización y confirmación visual al subir foto, subtotal estimado en carrito público, columna `Email` y modal `"Historial"` de pagos en clientes, hidratación de `FinishedBatch` en `SaleService.GetAllAsync`, resguardo contable en ventas con total y COGS `0m`, y detalle ampliado (`Descuento`, `Lote`, `P. Unit`) en recibo de venta.
  - `ChartOfAccountsService.cs`, `ChartOfAccounts.razor`, `JournalEntries.razor/.cs`, `FinancialReportService.cs`, `TrialBalance.razor`, `IncomeStatement.razor`, `BalanceSheet.razor`: validación de código/nombre obligatorio y modal de edición en catálogo contable; resguardo `AuthorizeView` y soporte de guardado como borrador junto a manejo de errores al anular asientos; fechas por defecto en UTC (`DateTime.UtcNow.Date`); y corrección en `FinancialReportService.GetIncomeStatementAsync` para sumar todas las cuentas de `AccountGroup.Costos` en la utilidad bruta, neta y cuadre del balance general.
- **Verificación:** `dotnet build FactoryERP.sln --configuration Release /warnaserror` (`0 Warning(s), 0 Error(s)`), `dotnet test FactoryERP.sln --configuration Release --no-build --verbosity minimal` (`102 / 102` pruebas aprobadas), `dotnet ef migrations has-pending-model-changes` sin cambios pendientes, límite SRP <= 200 líneas por archivo `.cs` propio (máximo 199 líneas) y prueba HTTP end-to-end en servidor aislado (`200 OK` en `/health`, `/login` y las 19 rutas del sistema).

## [2026-11-20] — Menús Laterales Plegados al Iniciar Sesión (rama `fix/ux-refinements`)
- **Requerimiento:** Hacer que todos los grupos del menú lateral inicien plegados al iniciar sesión en lugar de mostrarse desplegados por defecto.
- **Solución:**
  - `src/FactoryERP.Web/Components/Layout/NavMenu.razor`: inicializado `_expandedGroups` vacío (`new()`), capturada la variable local `groupKey` en el bucle y añadido `aria-expanded` en los botones `.nav-group-toggle`.
  - `src/FactoryERP.Web/wwwroot/app.css`: añadido cursor puntero, borde redondeado y estado hover suave en `.nav-group-toggle`.
- **Verificación:** `dotnet build FactoryERP.sln --configuration Release /warnaserror` (`0 Warning(s), 0 Error(s)`) y `dotnet test FactoryERP.sln --configuration Release --no-build --verbosity minimal` (`102 / 102` pruebas aprobadas).

## [2026-11-20] — Bloque A Fabricación: Consumo Real, Clonado de Fórmulas, Edición de Borradores y Hoja Imprimible (rama `fix/ux-refinements`)
- **Requerimiento:** Cerrar las brechas operativas y de integridad detectadas en el módulo de fabricación (`RawMaterials`, `Formulas`, `ProductionOrders`, `FinishedBatches`) con enfoque TDD.
- **Cambios en Dominio, Servicios y Contabilidad:**
  - `ProductionModels.cs`, `IFormulaService.cs`, `IProductionOrderService.cs`, `IManufacturingAccountingService.cs`: nuevo contrato `UpdateDraftAsync` (`UpdateProductionOrderRequest`), `CloneAsNewVersionAsync` y soporte de liquidación contable con variación de consumo real de materiales (`wipMaterialCleared`).
  - `RawMaterialService.cs`: bloqueo en `UpdateAsync` si se intenta cambiar `UnitOfMeasure` hacia una dimensión incompatible con fórmulas existentes.
  - `FormulaService.cs` + `FormulaValidationHelper.cs`: protección de `UpdateAsync` cuando la fórmula ya tiene órdenes no canceladas (solo permite editar `Instructions` e `IsActive`) y clonación automática a siguiente versión (`CloneAsNewVersionAsync`).
  - `ProductionPlannerHelper.cs`, `ProductionExecutionHelper.cs`, `ProductionOrderService.cs`, `ManufacturingAccountingService.cs`: rechazo de simulación/órdenes sobre fórmulas o materiales inactivos, edición completa de borradores (`UpdateDraftAsync`), reconciliación real de materiales al cerrar (`ActualMaterialQuantities` con descuento extra o devolución de sobrante a almacén + variación exacta contra `RawMaterialInventory` manteniendo WIP en `0`), y reemplazo de acceso directo a diccionario por `TryGetValue` en `CancelOrderAsync`.
- **Cambios en UI:**
  - Nuevo componente `ProductionOrderDetailModal.razor` + soporte `@media print` (`.print-modal`) para visualizar e imprimir la Hoja de Producción (Batch Record).
  - `ProductionOrders.razor/.cs`: buscador y filtro por estado, botón Detalle/Impresión, edición de órdenes en borrador (`PlannedDate`, `ExpirationDate`, `Notes`), tabla editable de consumo real por insumo al cerrar la orden y moneda dinámica (`CurrencySymbol`).
  - `Formulas.razor/.cs`, `RawMaterials.razor/.cs`, `FinishedBatches.razor`: filtros por texto/producto/categoría/stock bajo/inactivos, botón **"Nueva versión"**, visualización de instrucciones de receta, semáforo de vencimiento en lotes (`Vencido`, `Por vencer`, `Vigente`) y moneda dinámica desde `IBusinessProfileService`.
- **Verificación:** `dotnet build FactoryERP.sln --configuration Release /warnaserror` (`0 Warning(s), 0 Error(s)`), `dotnet test FactoryERP.sln --configuration Release --no-build --verbosity minimal` (`106 / 106` pruebas aprobadas), `dotnet ef migrations has-pending-model-changes` sin cambios pendientes y todos los `.cs` propios <= 199 líneas.

## [2026-11-20] — Auditoría Profunda Bloque A: Validaciones en Inicio, Fechas Calendario y Unicidad de Fórmulas (rama `fix/ux-refinements`)
- **Correcciones Aplicadas:**
  - `ProductionOrderService.cs`, `ProductionExecutionHelper.cs`, `ProductionPlannerHelper.cs`: validación en `StartOrderAsync` contra fórmulas o insumos desactivados tras crear el borrador, bloqueo de consumo adicional (`deltaQty > 0m`) sobre insumos inactivos permitiendo devolución de sobrantes, y truncamiento seguro de `Notes` a 400 caracteres (`TruncateNotes`).
  - `FormulaValidationHelper.cs`: verificación de cambio de `Name` en `HasStructuralChanges` para fórmulas con órdenes activas/históricas y exigencia de `Version` no vacía ni duplicada por producto.
  - `ProductionOrders.razor/.cs`, `ProductionOrderDetailModal.razor`, `FinishedBatches.razor`, `Formulas.razor/.cs`: eliminación de `.ToLocalTime()` en fechas calendario (`PlannedDate`, `ExpirationDate`) para evitar desfase por zona horaria, preservación en selector de materiales/fórmulas inactivas al editar y recálculo automático al cambiar cantidad o merma.
- **Verificación:** `dotnet build --configuration Release /warnaserror` (`0 Warning(s), 0 Error(s)`), `107 / 107` pruebas aprobadas, `has-pending-model-changes` sin cambios y máximo `199` líneas en `.cs` propios.

## [2026-11-20] — Bloque B: Control de Calidad (QC / Cuarentena), Bloqueo Comercial y Bajas/Ajustes de Lotes (rama `fix/ux-refinements`)
- **Requerimiento:** Incorporar liberación de Control de Calidad en lotes terminados, impedir la venta/oferta de lotes en cuarentena, rechazados o vencidos, y soportar bajas/ajustes contables de existencias de producto terminado.
- **Cambios en Core, Infraestructura y Contabilidad:**
  - `BatchQualityStatus.cs`, `FinishedBatch.cs`: estados `Approved`, `Quarantine`, `Rejected`, trazabilidad (`QualityNotes`, `QualityCheckedAt`, `QualityCheckedBy`) y reglas de dominio `IsExpired` / `IsSellable`.
  - `SettingKeys.ProductionRequireQualityApproval`, `BusinessProfileDto`, `DbInitializer.cs`, `BusinessProfileService.cs`: parámetro global configurable para enviar nuevos lotes producidos automáticamente a Cuarentena.
  - `ProductionOrderService.cs` + `ProductionExecutionHelper.cs`: asignación automática de `Quarantine` o `Approved` al completar orden según configuración.
  - `IBatchQualityService`, `BatchQualityService.cs`, `AccountingEntryPoster.cs`, `ManufacturingAccountingService.cs` (`PostFinishedBatchAdjustmentAsync`), `ServiceRegistrationExtensions.cs`: flujo completo para dictamen de calidad y ajustes/bajas de inventario terminado con asiento automático balanceado (`5102` vs `1103-03` o `1103-03` vs `3102`).
  - `SaleService.cs`, `FefoBatchAllocator.cs`, `CatalogService.cs`, `DashboardService.cs`: filtrado consistente por `IsSellable(utcToday)` en asignación FEFO, catálogos interno/público y alertas de stock bajo.
  - Migración EF Core `20261120183623_AddFinishedBatchQualityControl` (`QualityStatus` por defecto `0`, `QualityNotes`, `QualityCheckedAt`, `QualityCheckedBy`, índice en `QualityStatus`).
- **Cambios en UI y Pruebas:**
  - `Settings.razor/.cs`, `Sales.razor`, `FinishedBatches.razor/.cs`: interruptor QC en configuración, stock vendible en selector de ventas, resumen superior (Vendibles, En Cuarentena, Rechazados, Por vencer, Vencidos), filtro por calidad y modales `Calidad` y `Ajustar / Baja`.
  - Nuevas pruebas en `BatchQualityServiceTests.cs` y ampliación en `BusinessProfileServiceTests.cs`.
- **Verificación:** `dotnet build --configuration Release /warnaserror` (`0 Warning(s), 0 Error(s)`), `110 / 110` pruebas aprobadas, `has-pending-model-changes` sin cambios, máximo `199` líneas en `.cs` propios y smoke test HTTP autenticado (`200 OK` en `/raw-materials`, `/formulas`, `/production`, `/batches`, `/settings`, `/sales`, `/catalogo`).





