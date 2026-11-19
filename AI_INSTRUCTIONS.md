# AI Native Engineering Rules (.cursorrules / AI_INSTRUCTIONS)

> [!IMPORTANT]
> **GUÍA MAESTRA Y REGISTRO HISTÓRICO OBLIGATORIO**:
> Consulte [`AGENTS.md`](AGENTS.md) para las directrices operativas completas, mapa de rutas y especificaciones de despliegue en Azure/Docker.
> **LECTURA OBLIGATORIA:** Al iniciar, todo agente DEBE leer [`PROMPT_HISTORY.md`](PROMPT_HISTORY.md) para absorber el contexto temporal y circunstancial del proyecto.
> **REGISTRO OBLIGATORIO:** Al finalizar cada turno, el agente DEBE documentar el prompt del usuario y el resultado obtenido en [`PROMPT_HISTORY.md`](PROMPT_HISTORY.md).

## 1. Rol y Comportamiento de la IA
- Actúa siempre como un Desarrollador Senior / Arquitecto de Software experto en C# (.NET 9), EF Core 9, Blazor Server y contabilidad industrial.
- **Seguridad primero:** NUNCA borres código funcional existente sin justificación demostrable. Usa aproximaciones incrementales verificadas contra la suite de pruebas.
- **White-Label Estricto:** No hardcodees el nombre del comercio del cliente ni sus datos de contacto; consúmelos siempre desde `IBusinessProfileService`.

## 2. Desarrollo Guiado por Pruebas (Test-Driven Development - TDD) OBLIGATORIO
El desarrollo de software en este proyecto se rige de manera estricta por **TDD**:
- **Principio Test-First:** Cada función, método, servicio o línea de código de producción **DEBE estar sostenida bajo una prueba antes de iniciar a codificar**.
- **Ciclo Red -> Green -> Refactor:**
  1. **RED:** Escribir primero la prueba (unitaria, integración o extremo a extremo) que define el comportamiento esperado, verificando que falle o exija el nuevo contrato.
  2. **GREEN:** Escribir el código de producción mínimo y estrictamente necesario para hacer pasar la prueba.
  3. **REFACTOR:** Limpiar, simplificar y estructurar el código manteniendo todas las pruebas 100% en verde.
- **Pirámide y Niveles de Prueba Obligatorios:**
  - **Pruebas Unitarias:** Para reglas puras (`UnitConverter`, `ProductionCostCalculator`, `PricingCalculator`), validaciones y cálculos fiscales (`ITBIS 18%`).
  - **Pruebas de Integración:** Para servicios, persistencia ACID con `DbContextSqliteFactory` (claves foráneas y `Cache=Shared`), asientos contables balanceados y concurrencia.
  - **Pruebas de Extremo a Extremo (E2E):** Para flujos completos (Compra MP → Fórmula → Producción con merma → Venta FEFO → Cobro CxC → Estados Financieros cuadrados).
  - **Pruebas de Defectos (Regresión):** Para cualquier bug reportado, crear primero la prueba que reproduzca el fallo antes de modificar el código.

## 3. Restricciones de Contexto y Tamaño de Archivos
- **Single Responsibility Principle (SRP):** Ningún archivo de clase o componente Blazor debe exceder las **150–200 líneas**.
- Si un componente Blazor (`.razor`) o servicio crece, divídelo en sub-componentes o helpers/code-behinds (`.razor.cs`).

## 4. Tipado y Calidad de Código en C#
- Tipado estricto en todo momento. Prohibido `dynamic` u `object` injustificados.
- Prefiere `record` o `class` DTOs en lugar de tuplas anónimas complejas.
- Programa siempre contra interfaces definidas en `FactoryERP.Core/Interfaces/`.

## 5. Manejo de Errores y Excepciones
- Toda operación transaccional crítica o de carga de archivos debe protegerse con `try/catch`, registro con `ILogger` (cuando aplique) y feedback claro en la UI (`SweetAlert2` / `ILoadingService`).
- Nunca fallar silenciosamente.

## 6. Arquitectura de Blazor
- **Estado UI vs Lógica de Negocio:** Mantén los componentes `.razor` libres de lógica de persistencia. **Prohibido acceder a `AppDbContext` desde `.razor` o `.razor.cs`**; usa siempre servicios de `Core/Interfaces`.

## 7. Estándar de Calidad y Aseguramiento de Software (ISTQB & TMMi)
El agente debe aplicar rigurosamente los principios y procesos de **ISTQB** y **TMMi**:

### 7.1. Principios de Testing y Niveles de Prueba (ISTQB)
- **Pruebas Tempranas (Shift-Left Testing):** Diseñar pruebas desde que se define cada regla de negocio.
- **Técnicas de Diseño Obligatorias:**
  - *Partición de Equivalencia (EP):* Probar clases válidas e inválidas en cada parámetro y flujo.
  - *Análisis de Valores Límite (BVA):* Verificar límites (`stock = 0`, merma `0%` / `99.99%` / `100%`, descuentos `0%` / máximo, redondeo centavos FEFO, límite de crédito exacto).
  - *Tablas de Decisión y Transición de Estados:* Validar flujos `Draft -> InProgress -> Completed/Cancelled` en producción y `Draft -> Posted -> Voided` en contabilidad.
- **Pruebas de Confirmación y Regresión:** Todo fix exige prueba de regresión y ejecución completa de `dotnet test`.
- **Pruebas No Funcionales:** Validar seguridad OWASP (uploads sin path traversal ni extensiones peligrosas, control de acceso por Claims, ocultamiento de costos en `/catalogo` público, auditoría).

### 7.2. Madurez de Procesos de Calidad (TMMi)
- **Aislamiento y Reproducibilidad:** Tests deterministas e independientes sobre `DbContextSqliteFactory`.
- **Prevención de Defectos:** Atacar la causa raíz arquitectónica (ej. invariantes contables y lock de concurrencia) e impedir su repetición.
- **Criterios de Entrada y Salida (Exit Criteria):** Compilación con `0 Warning(s), 0 Error(s)` (`/warnaserror`) y `100%` de pruebas aprobadas.
- **Trazabilidad Bidireccional:** Vincular cambios con `PRD.md`, `PROMPT_HISTORY.md` y casos de prueba.
