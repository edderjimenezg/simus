# 5. Base de datos

El motor es **SQL Server**, y el modelo está escrito íntegramente en español: tablas, columnas y
restricciones. Hay dos líneas de migraciones, una por base:

- la base propia de la plataforma, administrada con **DbUp** a través de `PNMC.Migrador`
  (`pnmc-database/schema/`);
- la base **SIMUS del Ministerio**, administrada con **Flyway** (`pnmc-database/simus-ministerio/`),
  que lleva el módulo de Festivales heredado al mismo modelo. Se describe en el apartado 5.5.

Las dos producen la misma estructura en las tablas que comparten, y
`scripts/validar-simus-ministerio.sh --comparar-con-desarrollo` lo comprueba.

## 5.1 Migraciones

El esquema se modifica exclusivamente mediante archivos nuevos en
`pnmc-database/schema/`. `PNMC.Migrador` descubre los guiones, los ordena por nombre y
registra cada aplicación en `dbo.SchemaVersions`.

```bash
export PNMC_MIGRADOR_CONEXION='Server=...;Database=...;...'
dotnet run --project pnmc-api/src/PNMC.Migrador -- estado
dotnet run --project pnmc-api/src/PNMC.Migrador -- migrar
```

`baseline` solo marca guiones como aplicados y se reserva para incorporar una base
preexistente cuyo esquema ya fue comprobado. No debe usarse en una base nueva.

La orden local equivalente es:

```bash
./scripts/schema-local.sh estado
./scripts/schema-local.sh migrar
```

Las semillas se mantienen separadas. `seed-local-db.sh` aplica el esquema con DbUp antes
de cargar las semillas autorizadas. Los archivos de demostración no deben ejecutarse en
producción sin una decisión explícita y revisión de su contenido.

## 5.2 El patrón común del modelo

Tres decisiones estructurales se repiten en todos los módulos y conviene conocerlas antes de leer
cualquier tabla concreta.

**Un registro se identifica con el par «módulo + identificador».** Las tablas que guardan lo que un
registro tiene *de varios* —localizaciones, prácticas musicales, archivos, aliados— no cuelgan de una
tabla concreta: guardan `ModuloId` y `RegistroId`. Lo mismo hacen la revisión institucional, las
propuestas de cambio, la procedencia y la bitácora de auditoría. Por eso incorporar un proceso nuevo
al Ecosistema no exige crear tablas de relación propias.

**Los vocabularios son cerrados y viven en catálogos.** Ningún campo con vocabulario controlado se
guarda como texto libre. Cuando un valor no está en la lista, el registro admite un campo «otra»
junto al catálogo, en vez de abrir el vocabulario.

**El territorio tiene una sola fuente.** Departamentos y municipios se leen de `Divipola`, con
codificación oficial del DANE. Ninguna tabla mantiene su propia lista territorial.

## 5.3 El modelo de organizaciones, procesos y ediciones

El módulo de Festivales registra los festivales de música del país, sus realizaciones anuales y el
circuito por el que pasan desde que se registran hasta que se publican.

### Las tres entidades

**`Festivales`** es la entidad permanente: el festival como tal, que existe año tras año. Guarda su
nombre, descripción, periodicidad, ámbito territorial —nivel de cobertura, departamento y
municipio—, los datos de contacto del festival y de su organizador, la organización responsable y el
estado del registro.

**`EdicionesFestival`** es cada realización concreta. Pertenece a un festival y añade lo que cambia
de una a otra: el año y el número de edición, las fechas de inicio y fin, la tipología, las fuentes
de financiación primaria y secundaria, si usa estampilla Procultura, el director y las prácticas
musicales que congrega.

**`VersionesFestival`** conserva las sucesivas versiones de la ficha pública del festival. Permite
saber qué decía un registro antes de una corrección, que es lo que sostiene la trazabilidad del
módulo.

La jerarquía no es negociable: **el festival es la entidad principal y la edición es un subelemento
suyo**. Una edición no existe sin su festival.

### Las relaciones del registro

Ocho tablas guardan lo que un registro tiene *de varios*. No cuelgan de una tabla concreta:
identifican a su dueño con el par **`ModuloId` + `RegistroId`**, de modo que sirven a cualquier
proceso del Ecosistema Musical sin duplicarse.

| Tabla | Qué guarda |
|---|---|
| `LocalizacionesDeRegistro` | dónde ocurre: departamento, municipio, zona urbana o rural y titulación colectiva |
| `PracticasMusicalesDeRegistro` | las prácticas musicales que congrega |
| `ExpresionesArtisticasDeRegistro` | las expresiones artísticas que incluye |
| `ModalidadesParticipacionDeRegistro` | cómo se participa en él |
| `TiposIngresoDeRegistro` | cómo se accede: entrada libre, gratuita con boletería, boletería paga o sin público |
| `TerritoriosSonorosDeRegistro` | los territorios sonoros con que se relaciona |
| `EntidadesAliadasDeRegistro` | las organizaciones aliadas |
| `ArchivosDeRegistro` | los materiales: imágenes, documentos y su ficha |

### Los catálogos

Los vocabularios son cerrados y viven en tablas propias, no como texto libre: `TipologiasFestival`,
`FuentesFinanciacion`, `TiposOrganizador`, `NaturalezasEntidad`, `ExpresionesArtisticas`,
`ModalidadesParticipacion`, `PracticasMusicales`, `TiposIngreso`, `TerritoriosSonoros`,
`RegionesOcad`, `ZonasUrbanoRural` y `TitulacionesColectivas`.

Cuando un valor no está en la lista, el registro admite un campo «otra» junto al catálogo
—`OtraTipologia`, `OtraFuenteFinanciacionPrimaria`, `OtraModalidadParticipacion`— en vez de abrir el
vocabulario.

El territorio no tiene catálogo propio: se lee de `Divipola`, que es la fuente única de
departamentos y municipios del sistema. La **región OCAD** tampoco se captura: se deriva del
departamento de cada localización mediante `DepartamentosRegionOcad`, que asigna cada uno de los 33
departamentos a una de las seis regiones del Sistema General de Regalías.

`TerritoriosSonoros` y `PracticasMusicales` son catálogos **transversales**: no pertenecen a
Festivales, y cualquier módulo los enlaza por su tabla «…DeRegistro». Cada valor tiene además una
ficha conceptual (`FichasConceptualesTerritoriosSonoros`, `FichasConceptualesPracticasMusicales`).

### El ciclo de vida

Festival, versión y edición guardan su estado editorial en `EstadoRegistro`, y la base solo admite
estos valores:

| Estado | Qué significa |
|---|---|
| `borrador` | en elaboración, visible solo para quien lo registra |
| `en_revision` | enviado para revisión institucional |
| `ajustes_solicitados` | devuelto al responsable con observaciones |
| `aprobado` | aprobado sin publicar todavía; no es un paso obligatorio |
| `publicado` | visible en el portal |
| `rechazado` | revisado y no aprobado |
| `archivado` | retirado de la vista pública sin eliminarlo |

Las ediciones tienen además dos ejes separados. **`Estado`** dice en qué punto está la realización
—`en_preparacion`, `programada`, `realizada` o `cancelada`—, y **`EstadoVisibilidad`** dice si se
muestra —`borrador`, `publicada` o `archivada`—. Son cosas distintas y no se mezclan: una edición ya
realizada puede seguir publicada.

El vocabulario completo de estados del sistema está en `EstadosContenido`, con su código, su nombre
y su descripción.

### La revisión institucional

Cuando un registro se envía a revisión, `EnviosDeRevision` guarda el envío y `RevisionesDeRegistro`
la revisión que abre el equipo. Las observaciones se registran **por sección y por campo** en
`RevisionesDeRegistroObservaciones`, de manera que quien recibe la devolución sabe exactamente qué
corregir y dónde.

La revisión termina en aprobación, que publica el registro, o en devolución con ajustes solicitados.

### Las propuestas de cambio

Sobre un registro **ya publicado** no se edita directamente: se propone. `PropuestasDeCambio` guarda
la propuesta y `PropuestasDeCambioCampos` el detalle campo por campo, con el valor anterior y el
propuesto. El equipo acepta o rechaza cada campo, y solo lo aceptado se aplica al registro público.

Esa es la diferencia entre **corregir** —un registro en borrador, que su responsable edita— y
**proponer** —un registro publicado, cuyo cambio pasa por revisión—.

### Procedencia y trazabilidad

Cada registro guarda en `ProcedenciasDeRegistro` de dónde viene, separando tres cosas que no se
confunden:

- **el contexto de origen**, que dice si lo incorporó el equipo desde la consola o lo registró una
  organización desde el espacio externo;
- **la organización de procedencia**, la entidad que lo incorporó al sistema;
- **la cuenta que ejecutó la acción**, con su fecha.

La organización responsable de un festival —quien lo desarrolla— es un dato distinto, y vive en
`OrganizacionPrincipalId`, sobre la propia tabla `Festivales`.

`FestivalesReferenciasHistoricas` vincula un festival con las organizaciones que lo tenían
registrado en la información histórica incorporada.


## 5.4 Semillas

Las semillas viven en `pnmc-database/seed/` y se aplican después del esquema. Distinguen dos cosas
que no deben mezclarse: los **catálogos institucionales**, que forman parte del sistema, y los
**datos de demostración**, que no deben ejecutarse en un ambiente servido sin decisión expresa.

## 5.5 La base SIMUS del Ministerio

La base SIMUS del Ministerio tenía su propio módulo de Festivales: 22 tablas `ART_MUS_*` con una sola
tabla ancha por versión, catálogos locales y campos de texto libre. `pnmc-database/simus-ministerio/`
es la línea de migraciones **Flyway** que lleva esa base al modelo descrito en este capítulo, sin
tocar las tablas heredadas.

| Migración | Tipo | Contenido |
|---|---|---|
| `V1__estado_inicial_simus` | estructura y catálogos | Las 22 tablas `ART_MUS_*` tal como existen en SIMUS. En la base del Ministerio no se ejecuta: se registra como línea base. |
| `V2__maestras_transversales` | estructura | Estados, tipos de documento, DIVIPOLA, usuarios, organizaciones, archivos, procedencia y `CorrespondenciasHeredadas`. |
| `V3__catalogos_transversales` | estructura | Territorios sonoros y prácticas musicales con sus fichas, regiones OCAD, zonas, titulaciones y naturalezas. |
| `V4__divipola` | datos | DIVIPOLA del DANE, MGN 2025: 33 departamentos y 1 122 municipios. |
| `V5__datos_maestros_transversales` | datos | Estados, tipos de documento, entidad institucional, catálogos transversales y su correspondencia con los heredados. |
| `V6__modulo_festivales` | estructura | Catálogos de Festivales, festival, versión, edición, relaciones del registro y ciclo de revisión. |
| `V7__catalogos_festivales` | datos | Tipologías, expresiones, fuentes, modalidades, tipos de ingreso y de organizador, con su correspondencia. |
| `V8__traslado_de_festivales_heredados` | procedimiento | La lista `TrasladosHeredadosFestival` y el procedimiento `TrasladarFestivalesHeredados`. No traslada nada por sí misma. |

**Los identificadores heredados no se reutilizan.** Cada catálogo nuevo se carga con sus propios
identificadores y el par «identificador heredado → identificador nuevo» queda en
`CorrespondenciasHeredadas`, que es también la tabla que usa el traslado.

**Los registros heredados se trasladan solo cuando se piden.** Los festivales que hoy contiene
`ART_MUS_FESTIVALES` son de prueba y no se incorporan. Para trasladar un festival real se inscribe
su identificador en `TrasladosHeredadosFestival` y se ejecuta `dbo.TrasladarFestivalesHeredados`.
El detalle de lo que hace con cada campo está en `pnmc-database/simus-ministerio/README.md`.

## 5.6 Versión del esquema y versión del producto

Son dos numeraciones distintas y no se derivan una de otra. La **versión del esquema** es la última
migración aplicada a una base —`V20260929_01` en la cadena DbUp, `8` en la línea Flyway— y solo dice
qué transformaciones ha recibido esa base. La **versión del producto** sigue SemVer en
`CHANGELOG.md` y dice qué puede hacer el sistema y si un cambio rompe la compatibilidad.

Se relacionan en un solo sentido: cada entrada del `CHANGELOG.md` que cambia la base indica la
migración mínima que exige en cada línea. Que Flyway llegue a `V2` no significa que el producto sea
la 2.0.0.
