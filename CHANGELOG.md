# Registro de cambios

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el versionado es
[semántico](https://semver.org/lang/es/).

## [Sin publicar]

### Añadido

- **Migración de la base SIMUS del Ministerio**: línea Flyway (`pnmc-database/simus-ministerio/`, V1–V8)
  que incorpora el modelo de Festivales, sus catálogos transversales y DIVIPOLA MGN 2025 junto a las
  tablas `ART_MUS_*`, con correspondencia de identificadores y traslado gobernado de registros
  heredados. Exige Flyway `V8` en la base SIMUS.
- `scripts/validar-simus-ministerio.sh`: valida la instalación completa, la actualización y la
  igualdad de estructura con la cadena DbUp.

### Cambiado

- El modelo de Festivales admite solo estados editoriales, exige fuentes de financiación distintas en
  la edición, distingue localizaciones por zona y entidades aliadas identificables. San Andrés entra
  en la región OCAD Caribe y los tipos de organizador incluyen «Otro». Exige `V20260929_01` en la
  cadena DbUp.

### Retirado

- Cinco columnas sin uso de `Festivales`: `Activo`, `IdUsuarioCreador`, `TipoOrganizadorId`,
  `Director` y `FechaEnvioARevision`.

## [1.0.0] — 2026-09-21

Primera versión consolidada del Sistema de Información de la Música.

### Incluye

- **Portal público** con la ficha de cada proceso del Ecosistema Musical, el geovisor territorial,
  la agenda, las noticias, la galería y el catálogo editorial.
- **Espacio de gestión de las organizaciones**: alta de la entidad, confirmación de correo, registro
  y mantenimiento de sus propios procesos y ediciones, envío a revisión y atención de ajustes.
- **Espacio de gestión administrativa** con quince módulos, permisos concedidos por cuenta, bandeja
  de solicitudes y revisiones, y bitácora de auditoría.
- **Ecosistema Musical** con Festivales y Mercados Musicales operativos de punta a punta: registro,
  ediciones anuales, revisión institucional campo por campo, publicación, propuestas de cambio y
  solicitud de retiro.
- **Gestión de contenidos** del sitio: páginas y bloques, imágenes, equipo, boletín y banco de
  archivos.
- **Tratamiento de datos personales** conforme a la Ley 1581 de 2012, con políticas versionadas y
  autorizaciones que conservan el texto aceptado.
- **Importación asistida** con previsualización, decisión humana y política de retención.
- **Consulta guiada** sobre los datos del sistema, disponible en los dos espacios de gestión.
