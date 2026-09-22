# 11. Operación y mantenimiento

## 11.1 Operación corriente

| Tarea | Cómo |
|---|---|
| Levantar el entorno | `./scripts/dev-up.sh` |
| Comprobar que responde | `./scripts/dev-check.sh` |
| Detener sin perder datos | `./scripts/dev-down.sh` |
| Reiniciar aplicando migraciones | `./scripts/dev-refresh.sh` |
| Aplicar migraciones pendientes | `./scripts/schema-local.sh migrar` |

`dev-down.sh` detiene el contenedor de SQL Server y **conserva su volumen**: ninguna operación
corriente borra datos.

## 11.2 Estado del sistema

El API expone dos comprobaciones: `/health/live` responde si el proceso está en pie y `/health/ready`
si además puede atender, incluida la conexión con la base. La consola tiene una sección de **Salud
del sistema** que las consulta y muestra el resultado.

## 11.3 Trazabilidad

Cada acción con efecto sobre un registro queda en la bitácora de auditoría, identificada con el par
**módulo + identificador de registro**, la cuenta que la ejecutó y su fecha. La consola la expone en
**Auditoría y trazabilidad**, y cada ficha tiene además su propio historial, que reúne de dónde vino
el registro, qué versiones se han publicado y qué le ha ocurrido.

Un registro retirado no desaparece de la bitácora: la trazabilidad sobrevive al registro.

## 11.4 Datos personales

El sistema trata datos personales bajo la **Ley 1581 de 2012**. El principio que ordena la
implementación es que una autorización vale como prueba solo si puede demostrarse **qué texto leyó el
titular** al otorgarla; por eso el texto de cada política se versiona y se **copia dentro** de la
autorización en lugar de referenciarse.


#### Base de datos

`dbo.PoliticasDatos` — el texto versionado que sirve el servidor. Una sola vigente por clave, y lo
garantiza un **índice único filtrado** (`WHERE Vigente = 1`), no el código: con dos filas vigentes
de la misma clave, la respuesta del servidor dependería del orden de lectura y dos personas podrían
aceptar textos distintos creyendo que aceptan el mismo.

`dbo.AutorizacionesDatos` — cada autorización, con el texto **copiado dentro**. Copiado y no
referenciado: una referencia dice a qué fila apuntó; la copia dice qué leyó. Solo la segunda sirve
como prueba. El titular puede ser una cuenta **o** solo un correo (CHECK exige al menos uno), que es
por lo que el boletín había tenido que montarse su propio mecanismo aparte. Revocar **no borra la
fila**: le pone fecha.

#### Las finalidades

Son tres: `tratamiento`, `terminos` y `boletin`. El sistema **solo pide autorización para
finalidades que ejerce**: una lista de permisos que no se corresponde con lo que de verdad ocurre
con los datos del titular no cumple su función.

#### API

- `GET /api/v1/publico/politicas` y `/publico/politicas/{clave}` — **anónimas a propósito**: el art.
  12 obliga a informar *antes* de pedir la autorización, y quien va a registrarse todavía no tiene
  cuenta.
- `GET /api/v1/externo/mis-autorizaciones` y `POST .../{finalidad}/revocar` — con sesión.
- `RegistroDeAutorizaciones` — el **único** punto por el que se otorga o se retira una autorización.
  Cualquier puerta nueva pasa por ahí.

#### Frontend

- **Registro**: una casilla por finalidad vigente, con el **texto entero** desplegable, su versión a
  la vista y el enlace al documento del Ministerio. El boletín aparece marcado **«Opcional»**: que
  se pueda completar el alta sin él es lo que lo convierte en una autorización libre y no en un
  peaje.
- **`/politicas/{clave}`**: el texto que el sistema publica, leíble sin cuenta. Enlazado desde el pie
  del sitio, donde «Política de privacidad» y «Términos y condiciones» salían a `mincultura.gov.co`:
  documentos institucionales legítimos, pero no lo que **este** sistema le dijo a esta persona.
- **«Mis autorizaciones»**, dentro de *Cuenta y seguridad*: qué autorizó, cuándo, en qué versión, y
  el texto exacto. Lo retirado se sigue enseñando —tachado y con su fecha—, porque una lista que
  solo muestra lo vigente no permite comprobar que algo se retiró de verdad.

#### Qué se puede retirar, y qué no

`boletin` se retira en el acto, y al hacerlo la suscripción pasa a baja: dejarla activa haría que el
sistema siguiera escribiéndole a quien acaba de decir que no.

`tratamiento` y `terminos` no se retiran por separado. El art. 9 del decreto 1377 lo admite cuando
el titular tiene un deber legal o contractual de permanecer en la base, y quien responde por una
organización con procesos inscritos está en ese caso. **Lo que no se hace es poner el botón y que
conteste que no**: cuando no procede, el botón no está y en su lugar va el motivo escrito.

Una autorización **nunca se borra**: revocarla le pone fecha de retiro y la fila permanece, que es lo
que permite demostrar después tanto que se otorgó como que se retiró.

## 11.5 Retención de importaciones asistidas

Las previsualizaciones de una importación son material temporal y se depuran automáticamente al
expirar su plazo. Las cabeceras de los lotes aplicados, las decisiones humanas y la auditoría se
conservan: son evidencia administrativa, y su eliminación no se automatiza mientras la Tabla de
Retención Documental institucional no defina serie, tiempo y disposición.

La depuración es irreversible dentro de la base operativa. Una previsualización expirada se
reconstruye volviendo a cargar el archivo fuente.

## 11.6 Mantener el código

cómo se nombran y dónde viven las cosas en SIMUS. Es la referencia para escribir
código nuevo y para decidir si algo que llega de otro desarrollo puede entrar tal cual.


### El idioma es el español

Lo que es nuestro se escribe en español: tablas, columnas, endpoints, ficheros, clases, métodos,
variables, comentarios y textos de interfaz. Lo que llega de un desarrollo de referencia se traduce
al entrar, o se justifica por escrito por qué no.

Hay dos excepciones, y solo dos:

- **Lo que impone una plataforma o una biblioteca**: `ngOnInit`, `HttpClient`, `IActionResult`,
  los nombres de las clases de Tailwind, los verbos HTTP.
- **Lo que ya está publicado y romperlo tiene coste**: una ruta pública que alguien pueda tener
  guardada. En ese caso se documenta la deuda en vez de dejarla sin nombrar.

Quedan en el código nombres en inglés heredados de los desarrollos de origen —`selectedWorkItem`,
`fetchAgendaEvents`, `workQueueFilter`—. No se renombran en masa: se traducen cuando se toca la
pieza, para no convertir cada corrección en una migración.

### Nomenclatura por tipo de elemento

| Elemento | Forma | Ejemplo |
|---|---|---|
| Fichero de componente Angular | `kebab-case.component.ts` | `ficha-en-revision.component.ts` |
| Selector de componente | `app-kebab-case` | `app-ficha-en-revision` |
| Servicio | `kebab-case.service.ts` | `festivales-publicos.service.ts` |
| Clase, interfaz, tipo | `PascalCase` | `FichaDeFestivalEnRevision` |
| Señal, método, variable | `camelCase` | `unifiedWorkQueue`, `contarPublicados` |
| Constante de módulo | `MAYUSCULAS_CON_GUION_BAJO` | `CATEGORIAS_ECOSISTEMA` |
| Migración SQL | `VAAAAMMDD_NN__descripcion_en_espanol.sql` | `V20260913_01__ediciones_en_revision.sql` |
| Tabla y columna | `PascalCase` en español | `dbo.Festivales`, `FechaEnvioRevision` |
| Ruta del API | `/api/v1/{ambito}/{recurso}` en español | `/api/v1/publico/agenda` |
| Documento de producto | `kebab-case.md` | `base-de-datos.md` |

### Los cuatro ámbitos del API

Toda ruta declara para quién es, y eso decide qué sesión exige:

| Ámbito | Quién entra | Ejemplo |
|---|---|---|
| `/api/v1/publico/` | cualquiera, sin sesión | `/publico/festivales` |
| `/api/v1/externo/` | una organización con su sesión externa | `/externo/festivales/{id}` |
| `/api/v1/institucional/` | el equipo del PNMC, con sesión administrativa | `/institucional/festivales` |
| `/api/v1/admin/` | operación y gobierno de la consola | `/admin/auditoria` |

Son **dos cookies distintas** —externa e institucional— y no se mezclan. Un endpoint que sirva a
dos ámbitos se declara dos veces antes que aceptar cualquiera de las dos sesiones.

### Dónde va cada cosa

```text
pnmc-web/src/app/
  core/            servicios, contratos, http, navegación y utilidades compartidas
  shared/          componentes de interfaz reutilizables y directivas
  features/        una carpeta por área funcional, con sus páginas y sus componentes
pnmc-web/tools/    utilidades del proyecto que no se publican con la aplicación
pnmc-web/trinquete/ los trinquetes y sus techos sellados
pnmc-api/src/      Api (endpoints), Application, Domain, Infrastructure
pnmc-database/     esquema DbUp y datos de referencia
scripts/           arranque, comprobación y mantenimiento del entorno local
```

Un componente que usan dos áreas vive en `shared/`; uno que usa una sola, dentro de su área. Un
servicio que habla con el API vive en `core/services/`, nunca dentro de una página.

### Estilo del código

- **Los comentarios explican por qué, no qué.** Un comentario que repite lo que dice la línea de
  abajo sobra; uno que explica por qué la pieza es así y qué alternativas no funcionan evita que alguien
  deshaga la decisión dentro de tres meses.
- **Nada de CSS minificado a mano.** Una regla duplicada dentro de una línea de miles de caracteres
  líneas de hasta 4 712 caracteres, y en las cuatro ganaba la copia equivocada.
- **Los colores se nombran por su papel**, no con su hexadecimal: el trinquete `colores_literales`
  lo vigila. Los papeles del texto están en `pnmc-web/src/styles.css`.
- **Los tamaños salen de la escala tipográfica** (`--text-dato`, `--text-cuerpo`, `--text-lectura`,
  `--text-destacado`, `--text-titulo`), con 12 px como suelo.
- **Los controles salen de `app-boton` y de la escala de áreas de toque** (`--spacing-control*`).
