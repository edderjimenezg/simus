# 2. Arquitectura

De qué está hecho el sistema, qué hace cada pieza y dónde están las fronteras que no se cruzan.

Las cifras de este documento se obtienen del repositorio y del entorno en marcha —el contrato
publicado, el diario de migraciones y las dos suites de pruebas—, de modo que pueden recomprobarse.

---

## 2.1 Las tres piezas

| Pieza | Tecnología | Qué es |
|---|---|---|
| `pnmc-web/` | Angular 21 con señales, componentes autónomos y Tailwind 4 | El portal público, el espacio de la organización y la consola de gestión administrativa |
| `pnmc-api/` | .NET 10, Minimal APIs, EF Core | La única puerta a los datos: **310 operaciones** en 261 caminos |
| `pnmc-database/` | SQL Server, migraciones con DbUp | **113 tablas**, **102 migraciones** |

Alrededor: `scripts/` levanta y comprueba el entorno local, `infra/` guarda lo de despliegue y
`docs/` es lo que está leyendo.

## 2.2 Las fronteras que no se cruzan

**El frontend no habla con la base de datos.** Ni con un ORM, ni con una cadena de conexión, ni con
un servicio intermedio: todo pasa por el API.

**El API no crea estructura al arrancar.** Las tablas las crea DbUp aplicando los guiones de
`pnmc-database/schema/` en orden; una semilla nunca sustituye a una migración.

**Hay dos sesiones y no se mezclan.** La externa —la organización que registra sus procesos— y la
institucional —el equipo del PNMC—. Son dos cookies distintas y cada ruta declara cuál exige. Un
endpoint que aceptara cualquiera de las dos borraría la frontera entre los dos espacios.

**Los tres orígenes de un registro se guardan por separado**: qué entidad lo incorporó
(procedencia), qué cuenta ejecutó la acción, y qué organización responde por él. Que el PNMC haya
incorporado un Festival no lo convierte en su organización responsable.

## 2.3 Los tres espacios de la aplicación

| Espacio | Ruta | Quién entra |
|---|---|---|
| Portal público | `/` y sus secciones | cualquiera |
| Espacio de la organización | `/ecosistema/mi-panel`, `/gestion` | una organización con correo confirmado |
| Consola de gestión administrativa | `/administracion` | el equipo del PNMC, por rol |

Comparten componentes —la ficha del Festival es literalmente la misma en los tres, con distinto
envoltorio— pero no comparten sesión ni permisos.

## 2.4 Cómo se sostiene

- **Pruebas**: **1 125** en el API, de las que 59 corren contra SQL Server real, y **1 407** en el frontend.
- **Contrato**: `pnmc-api/openapi.yaml` se versiona y una prueba comprueba que corresponde al
  código. Se regenera con `ACTUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApi`.
- **Trinquetes**: **doce** métricas de interfaz y una de tipado, con techo sellado, y **87 pruebas
  propias** que fijan qué cuenta cada métrica y qué no. No dejan que la deuda suba sin que alguien lo
  decida. Viven en `pnmc-web/trinquete/`.
- **Puertas**: ver [`10-pruebas.md`](10-pruebas.md).

## 2.5 Lo que este documento no cubre

El detalle de cada módulo está en [`modulos.md`](06-modulos-y-funcionalidades.md); el del API en [`api.md`](08-api-e-integraciones.md); el
de la base en [`base-de-datos.md`](05-base-de-datos.md); el árbol de carpetas en
[`estructura-del-proyecto.md`](03-estructura-del-proyecto.md).

---

