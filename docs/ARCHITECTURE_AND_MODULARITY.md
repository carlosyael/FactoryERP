# Génesis (FactoryERP) — Arquitectura, Modularidad y Guía de Desarrollo

## 1. Introducción y Propósito del Proyecto
**FactoryERP** (vertical de manufactura del ecosistema **Génesis**) es una plataforma ERP white-label diseñada para fabricantes de productos capilares, optimizada para operar en República Dominicana (**RD$**, **ITBIS 18%**) y preparada para desplegar en **Microsoft Azure**:
- **Preparación y Fabricación:** Materias primas e insumos, fórmulas versionadas, simulación de materiales/costos, margen de pérdida estándar/esperado, cantidad resultante real, merma normal vs. anormal (NIC 2) y trazabilidad de lotes (`FinishedBatch`).
- **Compras y Suplidores:** Recepción con ITBIS 18%, Costo Promedio Ponderado (CPP) y cuentas por pagar.
- **Catálogo Interno y Público:** Control interno de costos/márgenes/precios por segmento y vitrina pública compartible (`/catalogo`) con pedidos por WhatsApp.
- **Clientes y Ventas:** Segmentación (`Retail`, `Salon`, `Wholesale`), líneas de crédito, abonos CxC y despacho FEFO.
- **Contabilidad en Partida Doble:** Plan de cuentas industrial (`MP`, `WIP`, `Terminados`, `COGS`, `Merma Anormal`), asientos automáticos, Libro Diario, Balanza, Estado de Resultados y Balance General.

---

## 2. Arquitectura de Software (.NET Clean Architecture)

```text
FactoryERP.sln
│
├── src/FactoryERP.Core/             # Dominio (Entidades, Enums, Constantes, Calculadores puros, DTOs, Interfaces)
├── src/FactoryERP.Infrastructure/   # Infraestructura (AppDbContext, Migraciones, AuditInterceptor, Servicios)
├── src/FactoryERP.Web/              # Presentación (Blazor Web App InteractiveServer, Layouts, Páginas, Seguridad)
└── src/FactoryERP.Tests/            # Pruebas Unitarias, Integración ACID, Concurrencia y E2E (xUnit + SQLite)
```

---

## 3. Reglas de Modularidad para Agentes e Ingenieros
1. **SRP y Límite de 200 Líneas:** Ningún servicio, helper o componente `.razor`/`.razor.cs` debe superar las **200 líneas**.
2. **Programación contra Interfaces:** Toda interacción de la UI o entre módulos debe resolverse mediante interfaces en `FactoryERP.Core/Interfaces/`.
3. **Integridad Transaccional y Concurrencia:** Las operaciones que mutan inventario (`PurchaseOrderService`, `ProductionOrderService`, `SaleService`) deben ordenarse determinísticamente por ID, ejecutarse en transacciones ACID y evitar stock negativo bajo concurrencia.
4. **Criterio de Salida:** `dotnet build FactoryERP.sln --configuration Release /warnaserror` (`0 Warning(s), 0 Error(s)`) y `100%` de pruebas aprobadas en `dotnet test`.
