# Carta de inicio — núcleo SIMUS

**Versión:** v01  
**Estado:** BOR  
**Fecha:** 2026_09_02  
**Deriva de:** decisión de reconstrucción desde cero; los repositorios anteriores son referencia de auditoría, no fuente de copia  
**Fuentes:** decisiones del equipo funcional y evidencia documentada en la reconstrucción previa  
**Destinatario:** equipo funcional y técnico de SIMUS

## Propósito

Construir SIMUS desde un núcleo nuevo, comprensible y verificable. Cada capacidad entra solo cuando cuenta con un recorrido completo desde la persona usuaria hasta los datos, y viceversa.

## Límites iniciales

Este repositorio no adopta automáticamente:

- código, tablas, scripts o contratos del desarrollo previo;
- rutas históricas ni compatibilidades temporales;
- migraciones de estructuras anteriores;
- datos de demostración, organizaciones, Festivales, contenidos o usuarios heredados;
- módulos futuros que todavía no se hayan definido y aprobado.

La consulta de repositorios anteriores sirve para entender reglas, identificar riesgos y formular preguntas. Cada pieza que se incorpore se rediseña y prueba aquí.

## Regla de construcción

Una capacidad solo se considera incorporada cuando incluye, según corresponda:

```text
decisión funcional
  → modelo de datos mínimo
  → regla de autorización
  → lógica de negocio
  → endpoint y contrato API
  → servicio frontend
  → interfaz y estados de carga/error
  → prueba automatizada
  → documentación de trazabilidad
```

No se crean tablas, endpoints o pantallas «por si acaso».

## Orden de construcción

| Corte | Resultado verificable | No incluye todavía |
|---|---|---|
| 00. Base técnica | Repositorio, configuración segura, arranque vacío y prueba técnica | Modelo de negocio. |
| 01. Identidad | Inicio/cierre de sesión y roles mínimos | Organizaciones, Festivales y administración. |
| 02. Organizaciones | Una persona externa registra y administra su organización | Procesos culturales. |
| 03. Festival inicial | Crear, guardar borrador y consultar un Festival propio | Ediciones, revisión, publicación y coincidencias. |
| 04. Gobierno Festival | Envío, revisión, decisión y auditoría | IA e importaciones. |
| 05. Evolución Festival | Ediciones, cambios, eliminación lógica y reclamaciones | Otros procesos. |
| 06. Capacidades transversales | Coincidencias, importación asistida, IA y notificaciones | Módulos futuros no aprobados. |

## Datos iniciales

El sistema inicia sin datos operativos. Se permitirán únicamente catálogos cuando un corte los necesite y se documentará para qué regla existen. Los datos de prueba vivirán fuera de la siembra normal y serán explícitos, efímeros y aislados.

## Primeras decisiones por tomar

1. Qué personas podrán iniciar sesión desde el primer corte y para qué.
2. Cómo se entrega la primera cuenta Webmaster sin dejar una contraseña o cuenta de prueba en el repositorio.
3. Qué información mínima debe pedir el registro de una organización.
4. Qué significa que una organización esté activa, en revisión o inactiva en esta nueva versión.

## Criterio de cierre del corte 00

El corte 00 termina cuando una persona técnica pueda arrancar una instancia vacía, sin secretos en el repositorio, y comprobar que frontend, API y base de datos se comunican mediante una prueba mínima. No crea cuentas ni datos de negocio.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | BOR | Define el núcleo independiente, sus límites y el orden de construcción. |
