# Catálogo inicial de identificación de personas

**Versión:** v01  
**Estado:** REV  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v07_APR_2026_09_02  
**Fuentes:** Registraduría Nacional del Estado Civil y Migración Colombia  
**Destinatario:** equipo funcional y técnico de SIMUS

## Propósito

Definir los tipos de identificación disponibles para el alta de personas adultas. El tipo y el número serán obligatorios. Este catálogo no afirma que SIMUS valide la autenticidad del documento; solo controla qué tipo declara la persona.

## Catálogo inicial propuesto

| Código | Etiqueta en la interfaz | Aplicación |
|---|---|---|
| CC | Cédula de ciudadanía | Personas colombianas mayores de edad. Incluye la cédula digital, que no se registra como tipo independiente. |
| CE | Cédula de extranjería | Personas extranjeras titulares o beneficiarias de visa, cuando aplique. |
| PA | Pasaporte | Personas extranjeras que se identifican con pasaporte. |
| PPT | Permiso por Protección Temporal | Personas venezolanas con PPT vigente. |
| DIE | Documento de identidad del país de origen | Personas extranjeras que se identifican con el documento nacional de su país. |
| CD | Carné diplomático | Personas con identificación diplomática. |

La Registraduría establece la cédula de ciudadanía para colombianos mayores de edad y reconoce, para extranjeros, cédula de extranjería, pasaporte, carné diplomático o documento de identidad del país de origen. La cédula digital tiene equivalencia funcional con la cédula física. Migración Colombia describe la cédula de extranjería y el PPT como documentos de identificación dentro de sus respectivos ámbitos. [Registraduría](https://www.registraduria.gov.co/Definiciones-de-identificacion.html), [Registraduría — identificación de extranjeros](https://registraduria.gov.co/Cuales-son-los-documentos-que-deben-presentar-el-denunciante-y-los-testigos.html), [Migración Colombia — cédula de extranjería](https://portal.migracioncolombia.gov.co/tramites-y-servicios/tramites-generales/cedula-de-extranjeria), [Migración Colombia — PPT](https://portal.migracioncolombia.gov.co/tramites-y-servicios/tramites-de-regularizacion/ppt).

## Tipos que no se habilitan inicialmente

- Tarjeta de identidad: corresponde a menores de edad y el registro de menores aún no ha sido decidido.
- Permiso Especial de Permanencia y certificaciones de trámite: no se añaden como tipos nuevos de alta. Su vigencia y uso requieren una decisión específica de política migratoria; la certificación de trámite del PPT tuvo vigencia temporal hasta el 31 de diciembre de 2024 según Migración Colombia. [Resolución 2189 de 2024](https://portal.migracioncolombia.gov.co/fileadmin/user_upload/normatividad/Resoluciones/resolucion-2189-de-2024-ampliacion_certificacion_ppt.pdf)
- Salvoconducto: es un documento temporal para regularizar la permanencia o salida y exigiría manejar fechas de vigencia. No se habilita mientras esa regla no exista. [Migración Colombia](https://portal.migracioncolombia.gov.co/tramites-y-servicios/tramites-generales/salvoconducto)

## Reglas para implementar

1. El formulario exige tipo y número de identificación.
2. El sistema normaliza el número sin modificar la forma que la persona ve durante el diligenciamiento.
3. La combinación `tipo + número normalizado` debe ser única por persona; no se deduce identidad por el correo.
4. Las longitudes y patrones específicos por tipo se definen únicamente cuando exista regla confirmada; el primer corte no inventará validaciones documentales nacionales.

## Decisión pendiente

No se ha decidido si una persona menor de edad podrá crear cuenta o administrar una organización. Hasta que exista definición funcional, legal y de acompañamiento, el registro solo admitirá a mayores de edad y no mostrará tarjeta de identidad.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Propone catálogo inicial de personas adultas con fuentes oficiales y registra la decisión pendiente sobre menores. |
