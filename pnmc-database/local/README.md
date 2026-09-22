# Base local de SIMUS

```bash
cp .env.example .env
# definir PNMC_LOCAL_SA_PASSWORD
./scripts/local-db-up.sh
./scripts/schema-local.sh migrar
```

La conexión predeterminada es:

- Servidor: `127.0.0.1,14344`.
- Base: `PNMC_LOCAL`.
- Usuario: `sa`.
- Contraseña: valor local de `PNMC_LOCAL_SA_PASSWORD`.
- Certificado del servidor: confiar en entorno local.

El contenedor se llama `simus-desarrollo-sqlserver`. El puerto se enlaza únicamente a
`127.0.0.1`. `./scripts/dev-down.sh` lo detiene sin borrar el volumen.

Las semillas amplias y de demostración son opcionales. Antes de aplicarlas, revise su
contenido y confirme que la base admite cambios destructivos o datos sintéticos.

