# 8. API e integraciones

Qué expone `pnmc-api` y cómo está organizado.

La referencia es `pnmc-api/openapi.yaml`, el contrato que una prueba mantiene igual al código.
Declara **310 operaciones en 261 caminos**, repartidas en once prefijos, de los que **cuatro
concentran 292**. Para recontarlas sobre el contrato vigente:
`python3 scripts/contar-contrato.py`.

---

## 8.1 Los ámbitos, y qué sesión exige cada uno

La convención del proyecto es `/api/v1/{ámbito}/{recurso}`, con cuatro ámbitos:

| Ámbito | Quién entra | Operaciones |
|---|---|---|
| `/api/v1/publico/` | cualquiera, sin sesión | 26 |
| `/api/v1/externo/` | una organización, con su cookie externa | 89 |
| `/api/v1/institucional/` | el equipo del PNMC, con su cookie institucional | 93 |
| `/api/v1/admin/` | operación y gobierno de la consola | 84 |

Suman **292 de las 310**.

**Lo que creció fue el circuito de un proceso, no la superficie pública.** Desde la medición anterior
el API pasó de 245 a 310 operaciones, y las 65 nuevas están casi todas en `/externo` y en
`/institucional`: son Mercados Musicales recorriendo el mismo circuito que Festivales —registro,
envío, revisión campo por campo, publicación, propuesta de cambio, retiro, ediciones— y las
propuestas de cambio sobre lo publicado. Lo público subió de 22 a 26: un directorio y una ficha más.

## 8.2 Lo que no encaja en esos cuatro

Las 18 restantes viven en siete prefijos más. Están medidas, no supuestas:

| Prefijo | Operaciones | Qué es |
|---|---|---|
| `/api/v1/mapa/` | 7 | Cartografía y capas del mapa ecosistémico. Ámbito propio legítimo. |
| `/api/v1/notificaciones/` | 3 | Lectura de avisos. Sirve a los dos espacios, por eso no cuelga de ninguno. |
| `/api/v1/participaciones/` | 3 | El formulario de participación en el ecosistema. **Sin entrada desde la interfaz**: ver §6. |
| `/api/v1/imagenes-web/`, `/contenido-web/`, `/equipo-web/` | 4 | El CMS: imágenes, contenido y equipo del sitio. |
| `/api/v1/solicitudes-de-vinculacion` | 1 | Pedir vincularse a un registro del ecosistema. |

**No queda ninguna ruta en inglés**, salvo los tres prefijos del CMS que se explican abajo.

`auth` se conserva en los dos lados —`/admin/auth/` y `/externo/auth/`— a propósito: renombrar uno
y no el otro habría cambiado una incoherencia por otra.

Los tres prefijos del CMS siguen en inglés y **no se tocaron**: `web-content` no es solo una ruta,
es el nombre del concepto en las tablas, en el catálogo versionado y en el frontend. Renombrar la
ruta sin renombrar el concepto crea una incoherencia nueva; es una decisión aparte.

## 8.3 El contrato

`pnmc-api/openapi.yaml` se versiona y **una prueba comprueba que corresponde al código**. Si un
cambio añade o cambia un endpoint, la prueba falla hasta regenerarlo:

```bash
cd pnmc-api && ACTUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApi
```

Hay endpoints cuyos esquemas de respuesta no quedan expandidos en el contrato —los de importación,
comprobado—. Es una limitación conocida del generador, no una ruta sin documentar.

## 8.4 Comprobar qué hay registrado, sin leer código

Con el API en marcha:

```bash
curl -s http://127.0.0.1:8180/swagger/v1/swagger.json | python3 -c \
  "import json,sys; d=json.load(sys.stdin); print(len(d['paths']),'caminos')"
```

Una ruta escrita y no registrada no aparece ahí, que es justo lo que interesa: para quien la llama,
no existe.

## 8.5 Participaciones

`/api/v1/participaciones` recoge un formulario de «quiero participar en el ecosistema»: tipo de
actor —persona, colectivo, organización, festival, mercado o espacio—, nombre, correo, departamento,
municipio y el resto del formulario en JSON. Devuelve una **referencia** con la que consultarlo
después. El POST es público; el listado exige sesión institucional. Se guarda en
`dbo.Participaciones`, que existe desde la primera migración.

**Está entera y no tiene entrada ni salida.** El frontend retiró los botones que la abrían —hay una
prueba que lo vigila— y la consola no tiene sección para leer lo recibido. La tabla tiene 0 filas.
Es una capacidad completa a la espera de que se decida si se abre o se retira.

## 8.6 Por módulo

- **Festivales y Ediciones**: 85 operaciones. El recorrido completo está en
  el mapa de rutas del módulo.
- **Mercados Musicales**: 53 operaciones —23 en `/externo`, 22 en `/institucional`, 6 en `/publico`
  y 2 en `/admin`—, el mismo circuito que Festivales.
- **Mantenimiento**: `/institucional/festivales/normalizar-versiones-historicas` y su
  `diagnostico-…` se invocan a mano, no desde la interfaz. Tienen pruebas. No son código muerto.
- **Cómo se sabe qué llama una pantalla, y cómo no.** Un contraste estático entre los caminos del
  contrato y los literales del frontend **no sirve**: media docena de servicios componen la
  dirección —`` `${base}/consultar` ``, `` `.../${id}/baja` ``—, así que un buscador de textos da
  decenas de falsos huérfanos. El 17 de septiembre de 2026 dio 74, y el primero de la lista era la
  Consulta Guiada, que funciona. Para saberlo de verdad hay que **mirar el tráfico** con la
  aplicación en marcha, o instrumentar el cliente.

- **`/participaciones`**: tres operaciones **que ninguna pantalla llama** —y esta sí está
  comprobada: la palabra «participacion» no aparece en todo el frontend—. Su tabla existe y está
  **vacía**; su código llegó de un desarrollo de referencia y conserva la nomenclatura inglesa
  —`actorType`, `department`, y valores como `individual`, `collective`, `market`—, que el proyecto
  no admite en lo suyo. O se engancha a un formulario público de participación, o se retira; las dos
  cosas son decisión de producto y están pendientes.

---

