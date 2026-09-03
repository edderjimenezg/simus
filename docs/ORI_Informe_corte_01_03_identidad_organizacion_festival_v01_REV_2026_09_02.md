# Informe de implementación — cortes 01 a 03 (Identidad, Organizaciones, Festival inicial)

**Versión:** v01
**Estado:** REV
**Fecha:** 2026_09_02
**Deriva de:** ORI_Plan_corte_01_identidad, ORI_Plan_panel_organizacion, ORI_Plan_corte_02_festival_inicial
**Fuentes:** implementación y verificaciones locales del núcleo SIMUS, incluida la revisión de rigor solicitada antes de pausar esta ruta
**Destinatario:** equipo funcional y técnico de SIMUS

## Propósito de este informe

Registra en qué quedó el **plan inicial** (núcleo funcional y de gestión) en el momento exacto en
que se pausa para abrir la rama paralela del portal público (`ORI_Plan_portal_publico_v01_BOR`).
Al retomar el plan inicial, este documento es el punto de partida: qué está cerrado, qué se
verificó y cómo, y cuál es el siguiente corte pendiente.

## Entregado

| Corte | Resultado esperado | Estado |
|---|---|---|
| 01. Identidad | Inicio/cierre de sesión y roles mínimos | Completado |
| 02. Organizaciones | Una persona externa registra y administra su organización | Completado |
| 03. Festival inicial | Crear, guardar borrador y consultar un Festival propio | Completado |

## Verificaciones realizadas

| Verificación | Resultado |
|---|---|
| Registro externo completo en navegador (persona + organización + consentimientos) | Correcto, con DIVIPOLA y documentos legales reales |
| Recorrido del panel (Resumen, Procesos, Organización, Administradores, Cuenta y seguridad) | Correcto, sin datos inventados, estados vacíos honestos |
| Redundancia del panel frente al panel de referencia | Ninguna encontrada; el propio equipo de referencia migró de una pantalla única a rutas separadas por la misma razón que el núcleo ya aplica |
| Responsive (móvil, tablet, escritorio) del registro y del panel | 3 bugs reales encontrados y corregidos: desborde horizontal por `<select>` con opción larga, encabezado del panel truncado sin puntos suspensivos, lista de administradores con columna mal calculada por auto-colocación de grid |
| Aislamiento entre organizaciones (criterio de aceptación de corte 02) | Bug real encontrado y corregido: `Results.Forbid()` sin esquema de autenticación devolvía 500 en vez de 403 en los 4 endpoints que validan administración de organización/Festival. Cubierto con prueba automatizada de extremo a extremo |
| Límite de intentos compartido entre registro e ingreso | Bug real encontrado y corregido: ambos consumían el mismo contador por IP. Separados en dos políticas independientes, límite configurable |
| Pruebas de integración contra base de datos real (`scripts/dev-test-integracion.sh`) | 11/11 correctas: registro, ingreso, actualizar organización, crear/listar Festival, aislamiento entre organizaciones |
| `dotnet build`, `dotnet test`, `npm run build`, `npm test` | Correctos |

## Ajustes de diseño aplicados durante el cierre

- Registro externo convertido de formulario único con scroll a recorrido de 3 pasos (`ORI_Referencia_diseno_registro_paso_a_paso_v01`), recreado a partir de la referencia sin copiar su código ni su layout.
- Avisos de interfaz reescritos: varios mensajes sonaban a nota interna de desarrollo en vez de redacción de producto.
- `/ingresar` y `/registro` son ahora rutas propias (antes una sola URL con una señal local), con enlace directo funcional en ambas.

## Riesgos y límites actuales

- PT-001 a PT-007 (`ORI_Pendientes_tecnicos_nucleo_v07`) siguen vigentes sin cambios: verificación de correo, restablecimiento de contraseña, invitación de administradores, gestión visible de múltiples organizaciones, reglas de validación por tipo de identificación, menores de edad, y confirmación jurídica de las referencias de MinCulturas.
- El corte 04 (Gobierno de Festival: envío, revisión, decisión, auditoría) no ha comenzado.
- La regla de verificación responsive (`[[feedback-verificacion-responsive]]`) y la de revisar la referencia antes de construir frontend (`[[feedback-frontend-referencia-primero]]`) quedan como estándar permanente para todo trabajo posterior, incluida la rama del portal público.

## Punto de pausa

A partir de aquí el plan inicial queda pausado, sin nada pendiente de cerrar ni cortado a medias.
La siguiente vez que se retome, el paso siguiente es **corte 04 — Gobierno de Festival**, según el
orden ya definido en `ORI_Carta_nucleo_Simus_v01`.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Cierra y registra el estado de los cortes 01–03 antes de pausar el plan inicial para el portal público. |
