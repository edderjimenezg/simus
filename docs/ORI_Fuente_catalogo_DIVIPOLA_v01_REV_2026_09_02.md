# Fuente territorial oficial — DIVIPOLA

**Versión:** v01  
**Estado:** REV  
**Fecha:** 2026_09_02  
**Deriva de:** decisión de incorporar territorialidad oficial al registro externo  
**Fuentes:** servicio DIVIPOLA MGN 2025 y Geoportal DANE  
**Destinatario:** equipo funcional y técnico de SIMUS

## Fuente seleccionada

SIMUS utilizará como fuente territorial inicial el servicio oficial **DIVIPOLA según Marco Geoestadístico Nacional — MGN 2025**, publicado por el Departamento Administrativo Nacional de Estadística (DANE).

- Servicio: `https://geoportal.dane.gov.co/mparcgis/rest/services/Divipola/Serv_DIVIPOLA_MGN_2025/FeatureServer`
- Capa de departamentos: `319`.
- Capa de municipios: `317`.
- Incorporación: operación explícita de preparación local; no consulta remota en tiempo de registro.

## Datos que se incorporan

| Entidad SIMUS | Campo oficial DANE | Uso |
|---|---|---|
| Departamento | `DPTO_CCDGO` | Código DIVIPOLA de dos dígitos. |
| Departamento | `DPTO_CNMBRE` | Nombre mostrado en el formulario. |
| Municipio | `MPIO_CCDGO` | Código municipal de tres dígitos, único junto con el departamento. |
| Municipio | `DPTO_CCDGO` | Relación con el departamento. |
| Municipio | `MPIO_CNMBRE` | Nombre mostrado en el formulario. |

No se incorporan geometrías, centros poblados ni áreas no municipalizadas en este corte, porque el registro inicial solo solicita departamento y municipio.

## Regla de actualización

La importación no reemplaza datos existentes. Si el catálogo ya contiene registros, el proceso se detiene para exigir una revisión explícita de diferencias, dependencias y versión fuente. La procedencia queda registrada en `territorio.FuentesCatalogo`.

## Estado

La herramienta de importación está preparada y compilada. Su ejecución requiere una base local inicializada y una decisión posterior para actualizar de 2025 a una nueva versión publicada por DANE.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Documenta fuente DANE y alcance territorial inicial. |
