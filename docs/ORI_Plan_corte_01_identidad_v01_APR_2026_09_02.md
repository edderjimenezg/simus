# Plan de construcción — corte 01 Identidad y organizaciones

**Versión:** v01  
**Estado:** APR  
**Fecha:** 2026_09_02  
**Deriva de:** ORI_Decisiones_nucleo_v09_APR_2026_09_02  
**Fuentes:** decisiones funcionales confirmadas para el núcleo SIMUS  
**Destinatario:** equipo funcional y técnico de SIMUS

## Resultado esperado

Una base nueva puede crear, desde cero, las estructuras vacías de personas, roles, organizaciones, vínculos de administración, sesiones, territorio y consentimientos. No contiene personas, organizaciones, Festivales, documentos legales ni datos demostrativos.

## Entregas por bloque

| Bloque | Entrega | Límite |
|---|---|---|
| 01A | Aplicador de esquema y modelo SQL limpio. | No expone registro ni crea datos operativos. |
| 01B | API de disponibilidad de registro, sesión y aprovisionamiento técnico único del Webmaster. | El alta externa seguirá no disponible mientras falten textos legales y territorio oficial. |
| 01C | Interfaz de acceso y panel externo vacío. | Muestra con transparencia las dependencias pendientes. |
| 01D | Registro público completo, al disponer de documentos legales y DIVIPOLA oficial. | No usará datos ficticios. |

## Modelo mínimo

| Concepto | Persistencia | Regla |
|---|---|---|
| Persona | `identity.Persons` | Correo y documento identifican a la persona; no a su organización. |
| Rol global | `identity.Roles` y `identity.PersonRoles` | Roles técnicos iniciales: Webmaster y externo. |
| Organización | `organizations.Organizations` | Entidad independiente, con municipio y departamento obligatorios cuando se cree. |
| Administración | `organizations.Administrators` | Relación muchos a muchos entre persona y organización. |
| Sesión | `identity.Sessions` | Revocable, con inactividad y vencimiento máximo. |
| Consentimiento | `legal.Documents` y `legal.Acceptances` | Conserva versión aceptada; no se crean textos ficticios. |
| Territorio | `territory.Departments` y `territory.Municipalities` | Vacío hasta incorporar fuente oficial DIVIPOLA. |

## Criterios de aceptación de 01A

1. El aplicador crea una base nueva sin depender de PNMC.Migrador ni de scripts anteriores.
2. No inserta organizaciones, personas ni Festivales demostrativos.
3. Una persona puede vincularse a varias organizaciones y una organización a varias personas administradoras.
4. El correo de una persona, la identificación de una persona y el correo de contacto de una organización no comparten una restricción artificial.
5. La ejecución es repetible y registra la versión de esquema aplicada.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | APR | Define el primer bloque técnico de identidad y organizaciones. |
