# 1. Descripción general

## 1.1 Qué es SIMUS

**SIMUS —Sistema de Información de la Música— organiza y gobierna la información del sector musical
colombiano con alcance sectorial.** Reúne en un solo lugar los procesos del ecosistema musical
—festivales, mercados musicales, escuelas y los demás—, las organizaciones que los desarrollan y los
contenidos con que el sector se informa y se encuentra.

El sistema tiene dos caras. Hacia afuera, un **portal público** donde cualquiera consulta los
procesos, su distribución territorial, la agenda, las noticias y el catálogo editorial. Hacia
adentro, dos espacios de trabajo: el **espacio de las organizaciones**, donde cada entidad registra y
mantiene sus propios procesos, y el **espacio de gestión administrativa**, donde el equipo
institucional revisa, decide, publica y administra.

## 1.2 Su relación con el Plan Nacional de Música para la Convivencia

El **Plan Nacional de Música para la Convivencia (PNMC)** es el programa del Ministerio de las
Culturas, las Artes y los Saberes que orienta la política pública de música en Colombia. SIMUS es la
infraestructura de información que lo sirve, y la distinción entre ambos es estructural para entender
el sistema:

- El **Entorno Virtual del PNMC** es la experiencia digital con la que el Plan comunica contenidos,
  orienta la participación y hace visible lo que le es relevante al sector.
- **SIMUS** tiene una responsabilidad más amplia: **provee una infraestructura que sirve al Plan sin
  agotarse en él.**

La consecuencia práctica es la que define el alcance del sistema: **un festival privado, una escuela
comunitaria o una organización independiente que nunca haya participado en una acción del Ministerio
forman parte del ecosistema musical y, por tanto, pueden estar en SIMUS.** El Plan orienta y cobija
institucionalmente el sistema, pero la vocación informacional de SIMUS excede el registro de las
acciones del propio Plan.

De ahí se sigue una regla que atraviesa todo el modelo de datos: **todo registro guarda de dónde
viene**, y eso es distinto de quién responde por él. Que el equipo del PNMC haya incorporado un
festival no lo convierte en su organización responsable.

## 1.3 El modelo conceptual

Cuatro conceptos sostienen el sistema. Entenderlos es entender SIMUS.

### La organización

**La organización es el único actor que registra y administra procesos.** No hay personas naturales
registrando festivales a título propio ni «agrupaciones» como tipo de entidad aparte: quien registra
es siempre una organización, y quien la representa es una cuenta de usuario vinculada a ella.

Una organización se da de alta desde el espacio externo, confirma su correo electrónico y, a partir
de ahí, puede registrar procesos.

### El proceso

**Un proceso es una manifestación del ecosistema musical con existencia continuada**: un festival, un
mercado musical, una escuela. Es la entidad permanente, la que existe año tras año, y pertenece a una
organización responsable.

Cada tipo de proceso es un módulo del sistema con su propio modelo, pero todos comparten la misma
arquitectura: el mismo ciclo de revisión, la misma forma de guardar sus relaciones, la misma
procedencia y la misma trazabilidad.

### La edición

**Una edición es cada realización concreta de un proceso**: el festival de este año, el mercado de la
temporada pasada. Pertenece a un proceso y no existe sin él.

La jerarquía no es negociable: **el proceso es la entidad principal y la edición es un subelemento
suyo**, nunca un módulo de navegación equivalente. Lo que cambia de una realización a otra —fechas,
programación, financiación, director— vive en la edición; lo que identifica al proceso vive en el
proceso.

### La procedencia

**Todo registro guarda tres cosas que nunca se confunden:**

| Dimensión | Qué responde |
|---|---|
| Procedencia | qué entidad lo incorporó al sistema |
| Usuario ejecutor | qué cuenta concreta realizó la acción, y cuándo |
| Organización responsable | quién gestiona o desarrolla realmente el proceso |

Un registro creado desde el espacio administrativo tiene como procedencia al PNMC, porque quien lo
incorporó pertenece institucionalmente al Programa; pero su organización responsable puede seguir
siendo una fundación independiente. Sin esas tres dimensiones separadas, la pregunta «¿quién
respondió por este dato, y desde dónde entró?» deja de tener respuesta en cuanto el registro cambia
de manos.

## 1.4 Cómo circula la información

El recorrido es el mismo para cualquier proceso del ecosistema:

1. Una organización **registra** su proceso desde el espacio externo, o el equipo del PNMC lo
   **incorpora** desde la consola administrativa.
2. El registro nace en **borrador**, visible solo para quien lo trabaja.
3. Cuando está completo, se **envía a revisión institucional**.
4. El equipo lo **revisa campo por campo**: aprueba, o devuelve con observaciones puntuales.
5. Aprobado, el registro se **publica** y aparece en el portal.
6. Sobre lo ya publicado no se edita directamente: se **propone un cambio**, que vuelve a pasar por
   decisión institucional.

Ese circuito —y no el formulario— es lo que define el producto. El detalle está en
[`06-modulos-y-funcionalidades.md`](06-modulos-y-funcionalidades.md).

## 1.5 Alcance de esta documentación

| Documento | Contenido |
|---|---|
| [`02-arquitectura.md`](02-arquitectura.md) | las piezas técnicas y las fronteras entre ellas |
| [`03-estructura-del-proyecto.md`](03-estructura-del-proyecto.md) | qué contiene cada carpeta |
| [`04-instalacion-y-configuracion.md`](04-instalacion-y-configuracion.md) | requisitos, puesta en marcha y variables |
| [`05-base-de-datos.md`](05-base-de-datos.md) | el modelo de datos y las migraciones |
| [`06-modulos-y-funcionalidades.md`](06-modulos-y-funcionalidades.md) | los módulos y los flujos de revisión y publicación |
| [`07-roles-y-permisos.md`](07-roles-y-permisos.md) | quién puede hacer qué, y cómo se concede |
| [`08-api-e-integraciones.md`](08-api-e-integraciones.md) | el API y los servicios externos |
| [`09-despliegue.md`](09-despliegue.md) | instalación en un ambiente servido |
| [`10-pruebas.md`](10-pruebas.md) | qué se comprueba y cómo se ejecuta |
| [`11-operacion-y-mantenimiento.md`](11-operacion-y-mantenimiento.md) | operación corriente, datos personales y retención |
