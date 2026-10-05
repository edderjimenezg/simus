# Migración de Festivales en la base SIMUS del Ministerio

Esta carpeta es la línea de migraciones **Flyway** que lleva el módulo de Festivales de la base SIMUS
del Ministerio desde sus tablas `ART_MUS_*` al modelo de la plataforma. Es independiente de la
cadena DbUp de `pnmc-database/schema/` y produce la misma estructura en las tablas que ambas
comparten.

```
simus-ministerio/
├── flyway.toml                     configuración (sin credenciales)
├── migraciones/V1 … V8             las migraciones
└── verificacion/
    ├── verificar_estado_objetivo.sql   comprobación del estado final
    ├── huella_del_esquema.sql          huella canónica de la estructura
    └── prerrequisitos_de_prueba.sql    solo para pruebas en una base vacía
```

## Estado de partida

Las 22 tablas `ART_MUS_*` del módulo:

- Una cabecera, `ART_MUS_FESTIVALES`, y una tabla ancha por versión, `ART_MUS_FESTIVALES_VERSION`,
  que mezcla la realización, el organizador y el contacto.
- Doce catálogos locales con prefijo del módulo.
- Seis tablas de relación con la versión.
- La correspondencia entre departamento y región OCAD.

Dependen de dos tablas propias de SIMUS que no forman parte del módulo: `ART_MUSICA_USUARIO` y
`BAS_ZONAS_GEOGRAFICAS`.

## Estado resultante

Se crean 45 tablas, un procedimiento y una función. Las tablas `ART_MUS_*` no se modifican ni se
borran: su retiro es una migración posterior.

| Grupo | Tablas |
|---|---|
| Maestras transversales | `EstadosContenido`, `TiposDocumento`, `Divipola`, `CatalogosReferenciaFuente`, `Usuarios`, `Archivos`, `Entidades`, `EntidadesResponsable`, `UsuariosEntidades`, `ProcedenciasDeRegistro`, `CorrespondenciasHeredadas` |
| Catálogos transversales | `TerritoriosSonoros` y `PracticasMusicales` con su ficha conceptual, `RegionesOcad`, `DepartamentosRegionOcad`, `ZonasUrbanoRural`, `TitulacionesColectivas`, `NaturalezasEntidad` |
| Catálogos de Festivales | `TipologiasFestival`, `ExpresionesArtisticas`, `FuentesFinanciacion`, `ModalidadesParticipacion`, `TiposIngreso`, `TiposOrganizador` |
| Núcleo | `Festivales`, `VersionesFestival`, `EdicionesFestival`, `FestivalesReferenciasHistoricas` |
| Relaciones del registro | `TerritoriosSonorosDeRegistro`, `PracticasMusicalesDeRegistro`, `ExpresionesArtisticasDeRegistro`, `ModalidadesParticipacionDeRegistro`, `TiposIngresoDeRegistro`, `LocalizacionesDeRegistro`, `EntidadesAliadasDeRegistro`, `ArchivosDeRegistro` |
| Revisión y cambios | `RevisionesDeRegistro`, `RevisionesDeRegistroObservaciones`, `EnviosDeRevision`, `PropuestasDeCambio`, `PropuestasDeCambioCampos`, `RegistrosRevisionHistorial` |
| Traslado | `TrasladosHeredadosFestival`, `dbo.TrasladarFestivalesHeredados`, `dbo.TextoHeredado` |

## Qué cambia en el modelo

| Antes (`ART_MUS_*`) | Ahora | Por qué |
|---|---|---|
| Una tabla ancha por versión | `EdicionesFestival` (cada realización) y `VersionesFestival` (instantáneas de la ficha pública) | La realización y el perfil publicado son cosas distintas. |
| Organizador y colectivo en texto, en cada versión | El festival pertenece a una organización (`OrganizacionPrincipalId` → `Entidades`) | La organización es un actor, no un texto repetido. |
| Contacto en el festival y en la versión | Solo en el festival | Un dato, un lugar. |
| `VERSIONES_REALIZADAS`, `FECHA_ULTIMA_VERSION` | Se derivan de las ediciones | Lo histórico vive en las ediciones. |
| Fechas `nchar(10)` | `date`, con fin ≥ inicio | Validación en la base. |
| Estado entero (6 valores) | Código de `EstadosContenido`, con CHECK de los siete estados editoriales | Un vocabulario común a todos los módulos. |
| Región OCAD elegible | Derivada del departamento (`DepartamentosRegionOcad`, 33 departamentos) | Es un dato administrativo, no una respuesta. |
| `ZON_ID varchar(5)`, departamento y municipio en la misma columna | `Divipola` con código de departamento y de municipio, y FK compuesta | Territorio validado contra la fuente oficial. |
| Prácticas musicales en texto libre | Catálogo `PracticasMusicales` y relación N:M; el texto libre se conserva como complemento | Filtrar y agregar exige un vocabulario. |
| Territorios sonoros ligados a la versión, con «N/A» como valor | Catálogo transversal con ficha; la ausencia de territorio es ausencia de filas | Otros módulos lo reutilizan. |
| Tablas `…XVERSION` con FK nulas y sin UNIQUE | Tablas `…DeRegistro` con UNIQUE, FK al catálogo e índice | Sin duplicados ni filas huérfanas. |
| `ENTIDADES_ALIADAS.ID_FESTIVAL` apuntaba a la versión | `EntidadesAliadasDeRegistro`, dueño explícito, identificable por nombre o entidad | Relación y nombre correctos. |
| Material como URL con su rol en la descripción | `ArchivosDeRegistro` con rol (`portada`, `material`) y orden | El afiche es la imagen principal. |
| `OBSERVACIONES_RECHAZO`, un campo que se sobrescribe | Revisión por campo, envíos e historial | Trazabilidad de cada decisión. |
| Sin procedencia | `ProcedenciasDeRegistro`: contexto, organización y cuenta | Todo registro dice de dónde viene. |

El detalle de la estructura, los estados y las relaciones está en `docs/05-base-de-datos.md`.

## Correspondencia con las historias de usuario

| Historia | Dónde queda en el modelo |
|---|---|
| HU 1: registro de festival y versiones | `Festivales`, `EdicionesFestival`, catálogos de caracterización, `LocalizacionesDeRegistro`, `ArchivosDeRegistro`, `EntidadesAliadasDeRegistro` |
| HU 2: validación interna | `RevisionesDeRegistro` y sus observaciones, `EnviosDeRevision`, `RegistrosRevisionHistorial`, `PropuestasDeCambio` |
| HU 3: consulta pública | Estado `publicado` y visibilidad `publicada`; filtros sobre `Divipola`, tipología, expresiones, tipo de ingreso y territorios sonoros |
| HU 4: mapa y gráficas | Coordenadas de `Divipola`; agregación por municipio, territorio sonoro, tipología y expresión |
| HU 5: reportes | Todas las relaciones de la edición son consultables; las listas múltiples se agregan desde las tablas «…DeRegistro» |

## Datos maestros

| Datos | Origen | Filas |
|---|---|---|
| DIVIPOLA | DANE, MGN 2025 | 33 departamentos, 1 122 municipios |
| Estados | Plataforma | 12 |
| Territorios sonoros | Catálogo del Programa (14); fichas con definición para los 10 delimitados por MinCultura, *Oferta institucional para entidades territoriales* (2023) | 14 |
| Prácticas musicales | Catálogo del Programa | 16 |
| Regiones OCAD | SIMUS, más San Andrés en la región Caribe | 6 regiones, 33 departamentos |
| Catálogos de Festivales | SIMUS, normalizados; «Otro» añadido a tipos de organizador | 10 + 11 + 9 + 5 + 4 + 8 |
| Entidad institucional | Plan Nacional de Música para la Convivencia | 1 |

La entidad institucional necesita un usuario creador. La migración crea para ello la cuenta
`sistema@simus.invalid`, que está desactivada y no tiene contraseña utilizable.

## Traslado de registros heredados

`V8` no traslada nada. Cuando haya festivales reales en `ART_MUS_FESTIVALES`:

```sql
INSERT INTO dbo.TrasladosHeredadosFestival (IdFestivalHeredado) VALUES (<id>), (<id>);
EXEC dbo.TrasladarFestivalesHeredados;
SELECT * FROM dbo.TrasladosHeredadosFestival;
```

Cada festival se traslada en su propia transacción y una sola vez. Con cada uno:

- El festival pasa a `Festivales`, con la entidad institucional como responsable, procedencia
  `historico` y el nivel de cobertura deducido de sus localizaciones.
- Cada versión pasa a `EdicionesFestival`, con sus relaciones.
- Las localizaciones con un código que no existe en DIVIPOLA se descartan y se informan en
  `Resultado`.
- Los datos sin lugar en el modelo nuevo (organizador en texto, pertenencia a colectivo, tipo de
  organizador, observación de rechazo, usuario creador) quedan en `RegistrosRevisionHistorial`.
- Los identificadores quedan en `CorrespondenciasHeredadas`.
- Los archivos físicos no se mueven: se conserva su URL.

## Despliegue en la base del Ministerio

Requisitos: SQL Server 2016 o posterior, porque se usan `ISJSON`, `STRING_AGG` e índices filtrados,
y un respaldo completo de la base.

```bash
export FLYWAY_URL='jdbc:sqlserver://<servidor>;databaseName=SIMUS;encrypt=true'
export FLYWAY_USER=… FLYWAY_PASSWORD=…
flyway -configFiles=flyway.toml info       # comprobar que no hay historial previo
flyway -configFiles=flyway.toml baseline   # registra V1: las tablas ART_MUS_* ya existen
flyway -configFiles=flyway.toml migrate    # aplica V2 … V8
sqlcmd … -i verificacion/verificar_estado_objetivo.sql
```

- `cleanDisabled = true` impide que un `flyway clean` borre la base.
- Una base que no tenga el módulo de Festivales se registra con `baseline -baselineVersion=0` y
  recibe desde `V1`.
- Nunca se modifica una migración ya aplicada: los cambios van en una `V9` o posterior.

**Intercalación.** Las tablas nuevas heredan la intercalación de la base. El modelo está probado
con `SQL_Latin1_General_CP1_CI_AS`; con una intercalación sensible a mayúsculas, los CHECK de
vocabulario pasan a distinguirlas.

## Validación

```bash
scripts/validar-simus-ministerio.sh --estado-inicial <exportación de SIMUS.sql> --comparar-con-desarrollo
```

El script levanta un SQL Server 2022 desechable, ejecuta Flyway en contenedor y exige:

1. **Instalación completa:** una base vacía con las tablas propias de SIMUS llega a `V8` y pasa
   `verificar_estado_objetivo.sql`.
2. **Actualización:** la exportación recibida, con línea base en `V1`, llega a `V8` y pasa la misma
   verificación.
3. Las dos bases tienen la **misma huella de esquema**.
4. El **traslado de prueba** de todos los festivales heredados termina sin errores y la verificación
   sigue pasando.
5. Con `--comparar-con-desarrollo`, las tablas comunes tienen la **misma huella** que la cadena DbUp.

`verificar_estado_objetivo.sql` comprueba:

- que existen las tablas;
- que no hay claves ni CHECK sin validar;
- los recuentos de los datos maestros;
- que todo departamento tiene región OCAD;
- que cada catálogo heredado tiene su correspondencia completa;
- que ninguna fila de las relaciones del registro queda huérfana.

## Migraciones y versión del producto

La versión del esquema de esta línea es el número de la última migración Flyway. La del producto es
SemVer en `CHANGELOG.md`, y su entrada indica qué migración exige. Retirar las tablas `ART_MUS_*`
romperá a quien todavía las lea y corresponderá a una versión mayor del producto.
