# Guía Maestra y Directrices de Operación — FactoryERP

> [!IMPORTANT]
> **REGLA DE LECTURA OBLIGATORIA AL INICIAR**:
> Todo agente de IA o desarrollador que interactúe con este repositorio **DEBE LEER** `PROMPT_HISTORY.md` antes de proponer o ejecutar cambios para conocer el contexto histórico del proyecto.
>
> **REGLA DE ACTUALIZACIÓN AL FINALIZAR**:
> Al completar cada requerimiento, se **DEBE REGISTRAR** una entrada en `PROMPT_HISTORY.md` con fecha, requerimiento, alcance y resultado técnico verificado.

---

## 1. Rol y Principios de Ingeniería

1. **Perfil:** Desarrollador Senior / Arquitecto de Software especializado en **C# (.NET 9)**, **Entity Framework Core 9**, **Blazor Web App (InteractiveServer)**, manufactura cosmética capilar y contabilidad en partida doble bajo marco dominicano (**RD$**, **ITBIS 18%**).
2. **White-Label Estricto:** Ningún nombre comercial del cliente debe quedar hardcodeado en el código fuente. Toda la identidad del comercio (nombre, slogan, logo, color primario, RNC, dirección, teléfono, WhatsApp, email) se gestiona desde el módulo de Configuración (`/settings`).
3. **Clean Architecture Estricta:**
   - `FactoryERP.Core`: Entidades, enums, constantes, calculadores de dominio puros, DTOs (`record`) e interfaces. **Cero dependencias externas** (salvo `Microsoft.Extensions.Identity.Stores`).
   - `FactoryERP.Infrastructure`: `AppDbContext`, migraciones EF Core, interceptores de auditoría, semilla y servicios de aplicación/persistencia.
   - `FactoryERP.Web`: Componentes Blazor, layouts, endpoints HTTP y servicios de UI. **Prohibido acceder a `AppDbContext` directamente desde `.razor` o `.razor.cs`**.
   - `FactoryERP.Tests`: Pruebas unitarias, de integración (SQLite ACID en memoria con FKs habilitadas) y de concurrencia.

---

## 2. Reglas de Desarrollo (Ágil + Clean Code + TDD)

1. **TDD Obligatorio (Red -> Green -> Refactor):**
   - Toda lógica de negocio, cálculo o servicio se respalda con pruebas en `FactoryERP.Tests` antes de escribir el código de producción.
2. **Estándares ISTQB & TMMi:**
   - Partición de equivalencia (EP) y Análisis de Valores Límite (BVA) en mermas (`0%`, tolerancia, anormal, `100%`), stock cero, redondeo ITBIS 18%, límites de crédito y balance de asientos (`Σ Débito == Σ Crédito`).
   - Criterio de salida: `dotnet build --configuration Release` con `0 Warning(s), 0 Error(s)` y `dotnet test` con 100% de pruebas en verde.
3. **Límite de Responsabilidad (SRP):**
   - Ninguna clase de servicio o componente Razor debe superar las **200 líneas**. Usar subcomponentes `.razor` y code-behind `.razor.cs`.
4. **Concurrencia y Transacciones:**
   - Ordenar determinísticamente insumos por `RawMaterialId` antes de descontar inventario y productos por `ProductId` en ventas.
   - Usar `IDbContextFactory<AppDbContext>` para operaciones transaccionales y aislar cada ciclo de vida.
   - Prevenir stock negativo en materias primas y en lotes terminados (`FinishedBatch`) mediante asignación FEFO.
5. **Seguridad OWASP & Azure Readiness:**
   - Validación de subida de imágenes (extensiones permitidas, límite 5 MB, nombres GUID, sin path traversal).
   - Cabeceras de seguridad HTTP, Rate Limiting, Anti-CSRF y DataProtection persistente.
