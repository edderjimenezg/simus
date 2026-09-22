# Migrador de esquema

`PNMC.Migrador` aplica en orden los guiones pendientes y registra cada ejecución en
`dbo.SchemaVersions`.

```bash
export PNMC_MIGRADOR_CONEXION='Server=...;Database=...;...'

dotnet run --project pnmc-api/src/PNMC.Migrador -- estado
dotnet run --project pnmc-api/src/PNMC.Migrador -- migrar
```

`estado` devuelve código 1 si encuentra pendientes. `baseline` marca guiones como aplicados
sin ejecutarlos y solo corresponde a una base preexistente cuyo esquema se comprobó antes.

El migrador también ofrece `divipola-mgn-2025` y `divipola-mgn-2025-si-falta`; ambas
operaciones consultan el servicio institucional configurado en el código e informan un fallo
antes de modificar la base cuando la respuesta no coincide con el corte esperado.

Para uso local, `scripts/schema-local.sh` arma la cadena desde `.env` y exige confirmación
explícita antes de ejecutar `baseline`.

