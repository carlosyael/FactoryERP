# Instrucciones de Ingeniería para Agentes — FactoryERP

1. Leer `PROMPT_HISTORY.md` al iniciar cualquier tarea.
2. Seguir estrictamente el ciclo **Red → Green → Refactor** con pruebas `xUnit + FluentAssertions + DbContextSqliteFactory` (SQLite ACID con `PRAGMA foreign_keys = ON`).
3. Mantener archivos concisos (≤ 200 líneas), tipado estricto, manejo explícito de errores con `ILogger` y feedback claro con `SweetAlert2` en la UI.
4. No hardcodear nombres de empresa ni credenciales.
5. Verificar siempre `dotnet build FactoryERP.sln --configuration Release` (`0 Warning(s), 0 Error(s)`) y `dotnet test FactoryERP.sln` antes de dar por cerrada una iteración.
6. Registrar el avance en `PROMPT_HISTORY.md`.
