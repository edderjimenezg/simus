# 5. Base de datos

El motor es **SQL Server**. El esquema se administra con **DbUp** a través de `PNMC.Migrador`, y el
modelo está escrito íntegramente en español: tablas, columnas y restricciones.

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
| `TiposIngresoDeRegistro` | cómo se accede: gratuito, con boleta, mixto |
| `TerritoriosSonoros` de registro | los territorios sonoros con que se relaciona |
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
departamentos y municipios del sistema.

### El ciclo de vida

Un festival atraviesa tres estados en `EstadoRegistro`:

| Estado | Qué significa | Cómo se llega |
|---|---|---|
| `borrador` | en elaboración, visible solo para quien lo registra | al crearlo |
| `en_revision` | enviado para revisión institucional | al enviarlo, que fija `FechaEnvioARevision` |
| `publicado` | visible en el portal | cuando la revisión lo aprueba |

Las ediciones tienen dos ejes separados. **`Estado`** dice en qué punto está la realización
—`en_preparacion` o `realizada`—, y **`EstadoVisibilidad`** dice si se muestra —`publicada` o
`archivada`—. Son cosas distintas y no se mezclan: una edición ya realizada puede seguir publicada.

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

`FestivalesReferenciasHistoricas` conserva el vínculo con el identificador que un festival tuviera en
un registro anterior, para los que se incorporaron desde información histórica.


## 5.4 Semillas

Las semillas viven en `pnmc-database/seed/` y se aplican después del esquema. Distinguen dos cosas
que no deben mezclarse: los **catálogos institucionales**, que forman parte del sistema, y los
**datos de demostración**, que no deben ejecutarse en un ambiente servido sin decisión expresa.
