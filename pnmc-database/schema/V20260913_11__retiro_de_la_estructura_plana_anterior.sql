/*
 * SE RETIRA LA ESTRUCTURA EDITORIAL ANTERIOR.
 *
 * QUE SE RETIRA Y POR QUE. `RecursosEditoriales` y `RecursosEditorialesArchivos` son el modelo plano
 * que precedió al Catálogo Editorial: guardaban la autoría y las palabras clave como TEXTO LIBRE y
 * los identificadores como columnas sueltas —`ISBN`, `ISMN`, `ISSN`, `DOI`—, es decir, todo lo que
 * el modelo normalizado resuelve hoy con `AgentesEditoriales`, `CreditosEditoriales`,
 * `IdentificadoresEditoriales` y `PalabrasClaveEditoriales`.
 *
 * LA AUDITORIA QUE LO SOSTIENE, y está hecha antes de escribir esto:
 *   · CERO filas en las dos tablas.
 *   · NINGUNA referencia en el código: no aparecen en `pnmc-api/src` ni en `pnmc-web/src`; solo en
 *     dos migraciones antiguas y en el guion de auditoría de legado.
 *   · Son una isla: la única clave foránea que las toca es la que une una con la otra. Ninguna
 *     tabla viva apunta a ellas.
 *

 *
 * LA COMPROBACION SE REPITE AQUI, EN LA BASE QUE SE MIGRE. La auditoría se hizo sobre el desarrollo
 * local; otra instalación podría tener filas. Si las hay, este guion FALLA a propósito en vez de
 * borrarlas: es preferible una migración detenida que un dato perdido en silencio.
 */
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.RecursosEditorialesArchivos', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.RecursosEditorialesArchivos)
    BEGIN
        THROW 50001, N'RecursosEditorialesArchivos tiene filas. Exporte y concilie antes de retirarla.', 1;
    END;
END;
GO

IF OBJECT_ID(N'dbo.RecursosEditoriales', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.RecursosEditoriales)
    BEGIN
        THROW 50002, N'RecursosEditoriales tiene filas. Exporte y concilie antes de retirarla.', 1;
    END;
END;
GO

/* El orden importa: primero la que apunta, después la apuntada. */
IF OBJECT_ID(N'dbo.RecursosEditorialesArchivos', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.RecursosEditorialesArchivos;
END;
GO

IF OBJECT_ID(N'dbo.RecursosEditoriales', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.RecursosEditoriales;
END;
GO
