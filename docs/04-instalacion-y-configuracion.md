# 4. Instalación y configuración

La orden habitual es `./scripts/dev-up.sh`. El script carga `.env`, comprueba Docker,
levanta la base, migra el esquema, completa DIVIPOLA cuando falta e inicia API y web.

| Script | Uso |
|---|---|
| `dev-up.sh` | iniciar el entorno completo |
| `dev-check.sh` | comprobar web, API y conectividad |
| `dev-down.sh` | detener procesos y contenedor sin borrar datos |
| `dev-refresh.sh` | reiniciar el conjunto |
| `local-db-up.sh` | iniciar y validar únicamente SQL Server |
| `schema-local.sh` | consultar, aplicar o inicializar historial DbUp |
| `api-local.sh` | iniciar el API contra SQL local |
| `base-limpia-local.sh` | reconstruir una base local desde cero |
| `actualizar-divipola-mgn-2025.sh` | actualizar el catálogo territorial desde DANE |

Los archivos bajo `tmp/`, así como `bin/`, `obj/`, `dist/` y `.angular/`, son resultados
locales regenerables. No contienen código fuente.

