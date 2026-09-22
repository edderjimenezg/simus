# 10. Pruebas

## 10.1 Qué se comprueba

| Suite | Qué cubre |
|---|---|
| Pruebas del API (.NET) | dominio, endpoints, persistencia y contrato; incluye pruebas contra SQL Server real |
| Pruebas del frontend (Angular) | componentes, servicios, guardas de ruta y comportamiento de pantalla |
| Pruebas de los guiones de operación | que los guiones de `scripts/` hacen lo que declaran |
| Verificación de estructura | que el repositorio conserva su forma y no reaparece material retirado |
| Trinquetes | límites cuantificados que no pueden empeorar |

## 10.2 Cómo se ejecutan

```bash
# Estructura del repositorio
./scripts/verificar-estructura.sh

# Guiones de operación
bash scripts/tests/run-script-tests.sh

# Frontend: lint, trinquetes, pruebas y compilación
cd pnmc-web && npm run lint && npm run trinquete && npm test && npm run build

# API
cd pnmc-api && dotnet build PNMC.Api.sln --configuration Release
dotnet test pnmc-api/tests/PNMC.Api.Tests/PNMC.Api.Tests.csproj
```

## 10.3 Pruebas contra SQL Server

Las pruebas que necesitan una base real están desactivadas por omisión. Para ejecutarlas hace falta
la base local en marcha y la variable de activación:

```bash
./scripts/local-db-up.sh
set -a && source .env && set +a
export PNMC_PRUEBAS_SQLSERVER=1
dotnet test pnmc-api/tests/PNMC.Api.Tests/PNMC.Api.Tests.csproj
```

Cada clase de prueba construye y destruye su propia base, de modo que no dependen del orden ni se
contaminan entre sí.

## 10.4 Los trinquetes

Un trinquete es un límite numérico que **puede bajar pero no subir**. Existen dos:

- **`trinquete-any`** cuenta los usos de `any` en TypeScript y falla si superan el techo vigente.
- **`trinquete-interfaz`** vigila doce métricas de interfaz —campos sin rótulo, rótulos escritos a
  mano, tipografías fuera de la escala y otras—.

Sirven para que la deuda no crezca en silencio: si un cambio la aumenta, la puerta lo dice en el
acto y el número queda a la vista.

## 10.5 Criterio de cierre

Un cambio se considera terminado cuando las cuatro comprobaciones de §10.2 pasan en verde, sin
pruebas omitidas, y cuando el trabajo de interfaz se ha comprobado además en navegador y en varias
resoluciones.
