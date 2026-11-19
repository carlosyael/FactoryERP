# FactoryERP — ERP de Manufactura y Gestión Comercial Capilar

Plataforma ERP construida en **.NET 9 (Blazor Server + Clean Architecture + EF Core 9)** para fabricantes de productos capilares en República Dominicana (**RD$**, **ITBIS 18%**), con identidad **100% configurable (White-Label)** y lista para desplegar en **Microsoft Azure**.

## Módulos Incluidos (v1.0)
- **Configuración White-Label (`/settings`):** Nombre del comercio, slogan, logo, color de marca, datos de contacto, WhatsApp, parámetros de merma, ITBIS 18% y estado de catálogo público.
- **Preparación y Fabricación (`/raw-materials`, `/formulas`, `/production`, `/batches`):** Materias primas y empaques, fórmulas versionadas, simulación de producción, cálculo de margen de pérdida, cantidad resultante real, costeo por lote terminado y trazabilidad FEFO.
- **Compras y Suplidores (`/suppliers`, `/purchases`):** Recepción de materias primas con recálculo de Costo Promedio Ponderado (CPP) y cuentas por pagar.
- **Catálogo Interno y Público (`/catalog`, `/catalogo`):** Gestión de línea capilar con márgenes internos y vitrina pública compartible con pedidos directos por WhatsApp.
- **Clientes y Ventas (`/customers`, `/sales`):** Segmentación (`Minorista`, `Salón`, `Mayorista`), líneas de crédito, CxC/abonos, ventas FEFO con ITBIS 18% y comprobantes con branding configurado.
- **Contabilidad en Partida Doble (`/accounting/*`):** Catálogo industrial (Materias Primas, WIP, Productos Terminados, COGS, Merma Anormal), asientos automáticos en cada evento, Libro Diario, Balanza de Comprobación, Estado de Resultados y Balance General.
- **Seguridad y Roles (`/users`, `/roles`):** Control de acceso por roles y permisos granulares.

> **Nota Fiscal:** La integración de Facturación Electrónica (DGII e-CF) queda desacoplada vía `ITaxDocumentProvider` y marcada como **Pendiente de implementación** para la siguiente fase.

## Ejecución Local Rápida
```bash
dotnet restore FactoryERP.sln
dotnet test FactoryERP.sln
dotnet run --project src/FactoryERP.Web/FactoryERP.Web.csproj
```
Por defecto en `Development` utiliza SQLite local (`factoryerp_dev.db`) para arrancar de inmediato sin dependencias externas. Para **SQL Server / Docker / Azure**, consulta [`docs/AZURE_DEPLOYMENT.md`](docs/AZURE_DEPLOYMENT.md).
