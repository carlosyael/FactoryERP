# FactoryERP — Especificación Técnica del Stack Tecnológico

| Capa | Tecnología | Uso en FactoryERP |
| :--- | :--- | :--- |
| **Runtime & Lenguaje** | `.NET 9.0` / `C# 13` | Backend, lógica de dominio y servidor Kestrel |
| **Arquitectura** | Clean Architecture (4 proyectos) | `Core` → `Infrastructure` → `Web` + `Tests` |
| **UI Web** | Blazor Web App (`InteractiveServer`) | SPA reactiva en tiempo real sobre SignalR + Dark/Light Mode |
| **Estilos & Alertas** | Bootstrap 5.3 + Bootstrap Icons + SweetAlert2 | Diseño responsivo, tokens CSS dinámicos por marca y confirmaciones |
| **ORM & Base de Datos** | EF Core 9 (`SQL Server / Azure SQL` + `SQLite`) | Migraciones Code-First en Azure SQL / SQL Server y SQLite en desarrollo/tests |
| **Auditoría & Concurrencia** | `AuditInterceptor` + `IDbContextFactory` | Trazabilidad automática y transacciones seguras |
| **Seguridad** | ASP.NET Core Identity + Claims + JWT + Rate Limiting | Roles, políticas por permiso, cabeceras OWASP y DataProtection |
| **Testing** | xUnit + FluentAssertions + Moq + SQLite `:memory:` | TDD estricto con llaves foráneas y transacciones ACID reales |
| **Cloud / Contenedores** | Docker Multi-Stage + Azure App Service / Container Apps + Azure SQL | Despliegue público HTTPS accesible desde cualquier red |
