# Product Requirements Document (PRD) — FactoryERP

> **Producto:** FactoryERP (Sistema Integral de Manufactura y Gestión Comercial Capilar)
> **Versión:** 1.0
> **Moneda e Impuestos:** Pesos Dominicanos (`RD$`) / ITBIS `18%`
> **Facturación Electrónica (DGII e-CF):** Preparada por interfaz extensible (`Pendiente de implementación`)

---

## 1. Visión General
FactoryERP organiza y provee la infraestructura operativa, productiva, comercial y contable para un fabricante de productos capilares, con identidad **100% configurable (White-Label)** desde el módulo de Configuración y lista para desplegar en la nube (**Microsoft Azure**).

## 2. Requerimientos Funcionales por Módulo

### 2.1. Configuración White-Label (`/settings`)
- Gestión centralizada de Nombre del Comercio, Slogan, RNC, Teléfono, WhatsApp, Email, Dirección, Logo (subida o URL) y Color Primario.
- Parámetros fiscales (`RD$`, `ITBIS 18%`, estado de facturación electrónica).
- Parámetros de producción (% margen de pérdida por defecto y tolerancia de merma anormal) y visibilidad del Catálogo Público.

### 2.2. Materias Primas, Suplidores y Compras (`/raw-materials`, `/suppliers`, `/purchases`)
- Gestión de ingredientes, bases, aceites, fragancias, envases y etiquetas con unidades (`g`, `kg`, `ml`, `L`, `unidad`).
- Órdenes de compra a suplidores con cálculo de ITBIS 18%, recálculo automático de **Costo Promedio Ponderado (CPP)**, cuentas por pagar (CxP) y abonos.

### 2.3. Preparación y Fabricación (`/formulas`, `/production`, `/batches`)
- **Fórmulas Versionadas (`ProductFormula`):** Definición de materiales requeridos por rendimiento base, margen de pérdida estándar (%) y costos indirectos por lote.
- **Órdenes de Producción (`ProductionOrder`):**
  - Simulación previa al iniciar: validación de disponibilidad de insumos, materiales escalados y costo unitario estimado.
  - Definición de **cantidad a producir**, **margen de pérdida estimado (%)** y **cantidad resultante esperada**.
  - Inicio (`InProgress`): descuento determinístico de materias primas y registro contable en Inventario en Proceso (WIP).
  - Cierre (`Completed`): registro de **cantidad resultante real**, cálculo de **merma real (%)**, separación de merma normal vs. anormal (NIC 2), cálculo de **costo unitario real**, generación automática de **Lote Terminado (`FinishedBatch`)** y actualización de CPP del producto.

### 2.4. Catálogo Interno y Público Compartible (`/catalog`, `/catalogo`)
- **Catálogo Interno (`/catalog`):** Gestión de línea capilar, presentación, foto, beneficios, ingredientes, modo de uso, costo real, precios Minorista/Salón/Mayorista, margen % y stock por lote.
- **Catálogo Público (`/catalogo`):** Vista comercial anónima con el branding del comercio, filtros por línea/presentación/disponibilidad, detalle del producto y carrito con envío de pedido directo por **WhatsApp**, sin exponer costos ni datos internos.

### 2.5. Clientes, Ventas y Cuentas por Cobrar (`/customers`, `/sales`)
- Clientes segmentados (`Minorista`, `Salón`, `Mayorista`) con tarifas automáticas, límite y días de crédito.
- Ventas con deducción **FEFO** de lotes terminados, cálculo de **ITBIS 18%**, prevención de stock negativo, control de crédito y registro de abonos CxC.

### 2.6. Contabilidad de Partida Doble (`/accounting/*`)
- Catálogo de cuentas industrial (incluyendo Materias Primas, Producción en Proceso WIP, Productos Terminados, Costo de Ventas y Merma Anormal).
- Asientos automáticos balanceados en compras, pagos a suplidores, inicio y cierre de producción, cancelaciones, ventas + COGS, cobros CxC y gastos operativos.
- Libro Diario, Balanza de Comprobación, Estado de Resultados (P&L) y Balance General en tiempo real.

### 2.7. Seguridad, Auditoría y Operación
- Roles (`Admin`, `Produccion`, `Ventas`, `Contabilidad`), permisos por Claims, menú dinámico y registro en `AuditLog`.
