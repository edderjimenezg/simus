# Frontend de SIMUS

Aplicación Angular 21 con componentes standalone, Signals, Router, HttpClient,
Tailwind CSS, Leaflet y MapLibre.

## Estructura

- `src/app/core/`: configuración y servicios globales.
- `src/app/features/`: capacidades organizadas por dominio.
- `src/app/shared/`: piezas reutilizables sin dependencia de una capacidad.
- `e2e/`: recorridos funcionales de Playwright.
- `e2e-audit/`: auditorías de dispositivos, DOM y accesibilidad.
- `tools/`: comprobaciones y mantenimiento del frontend.
- `trinquete/`: límites cuantificados de deuda técnica.

El gestor de paquetes canónico es npm. `package-lock.json` es el único lock vigente.

## Desarrollo

```bash
npm ci
npm start
```

La aplicación queda en `http://127.0.0.1:4300`. El proxy reenvía `/api` al backend en
`http://localhost:8180`.

## Verificación

```bash
npm run lint
npm run trinquete
npm test
npm run build
npm run e2e:ci
```

En producción, el frontend consume rutas relativas del mismo origen. El servidor debe
redirigir `/api` al backend y devolver `index.html` para las rutas de la aplicación.

