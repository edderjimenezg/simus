# Base de datos de SIMUS

Artefactos de SQL Server y Azure SQL.

## Estructura

- `schema/`: cambios estructurales inmutables, aplicados por DbUp.
- `seed/`: datos de referencia o demostración, separados del esquema.
- `scripts/`: validaciones y auditorías SQL.
- `local/`: documentación del modelo y del entorno local.

La carpeta `migrations/` fue retirada: contenía dos guiones baseline fuera del flujo real.
`schema/` es la única fuente de cambios y `dbo.SchemaVersions` conserva su historial.

## Convención

Un cambio nuevo usa `VAAAAMMDD_NN__descripcion_en_espanol.sql`. No se modifica un archivo
que ya pudo aplicarse. Los guiones no contienen credenciales y declaran las opciones de
sesión que necesiten.

```bash
./scripts/schema-local.sh estado
./scripts/schema-local.sh migrar
```

El detalle operativo está en
[`docs/05-base-de-datos.md`](../docs/05-base-de-datos.md).

