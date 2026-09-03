# Informe — hardening base del núcleo SIMUS

**Versión:** v01
**Estado:** REV
**Fecha:** 2026_09_02
**Deriva de:** ORI_Informe_linea_base_verificada_v01_REV_2026_09_02
**Fuentes:** implementación, pruebas automatizadas y configuración vigente
**Destinatario:** equipo técnico de SIMUS

## Implementado

- CORS ya no está fijado en código: los orígenes se leen desde `Cors:Origenes`. El valor local versionado permite únicamente `http://localhost:4200`; producción debe definir sus orígenes mediante configuración de entorno.
- `AllowedHosts` local se restringe a `localhost` y `127.0.0.1`.
- La API añade `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` y CSP restrictiva. HSTS se activa fuera de desarrollo.
- Las mutaciones autenticadas de organización, Festival y cierre de sesión exigen un token antiforgery emitido tras validar la sesión. Angular lo solicita antes de acceder al panel y lo remite en `X-SIMUS-CSRF`.
- Las pruebas de integración usan ese flujo real y pasaron 11/11.

## Límites y decisiones pendientes

- El dominio público, el proxy inverso y los orígenes productivos no están definidos; no se incluyeron valores supuestos.
- Los roles existen en `identidad.Roles`, pero aún no hay endpoints institucionales que deban aplicar una política de rol. El siguiente dominio administrativo debe incorporar autorización API por rol desde su primer endpoint.
- No se configura confianza en cabeceras reenviadas hasta conocer el proxy autorizado; confiar indiscriminadamente en ellas alteraría el límite por IP.

## Verificación

`dotnet build`, `npm run build`, `npm test -- --watch=false` y `scripts/dev-test-integracion.sh` finalizaron correctamente durante este corte.

## Registro de versiones

| Versión | Fecha | Estado | Cambio |
|---|---|---|---|
| v01 | 2026_09_02 | REV | Registra configuración por entorno, cabeceras y antiforgery del núcleo. |
