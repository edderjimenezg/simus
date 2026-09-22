import { AdminService } from '../../../core/services/admin.service';
import { ADMIN_MODULES } from '../domain/admin-config';

/**
 * Panel de registros · capacidades con respaldo real.
 *
 * <b>Qué queda de este fichero y por qué.</b> Tenía cuatro pruebas. Tres medían la «preparación
 * de ficha desde texto», que proponía campos con reglas locales sin modelo externo y rechazaba
 * PDF e imágenes. Esa función vivía dentro del formulario CRUD heredado y solo se abría desde él;
 * el formulario se retiró en una revisión anterior junto con los ocho módulos sin circuito que lo usaban,
 * así que sus pruebas se fueron con la función que medían.
 *
 * <b>No es una pérdida silenciosa.</b> Las reglas locales de aquella preparación siguen en el
 * historial de git de `admin-records-panel.component.ts` y merecen volver cuando el Catálogo
 * Editorial tenga su propio formulario: no inventaban ningún campo que no reconocieran, y esa es
 * la parte difícil de repetir.
 *
 * <b>La que sobrevive</b> no depende del formulario: fija que el servicio no invente operaciones
 * que el contrato OpenAPI no declara. Es la guarda que impide que vuelva a aparecer un botón
 * llamando a una ruta que no existe — exactamente lo que le pasaba al guardado retirado, que
 * apuntaba a `module.endpoint` y recibía 404.
 */
describe('Panel de registros · capacidades con respaldo real', () => {
  it('el servicio no expone las dos operaciones que faltan en OpenAPI', () => {
    const prototipo = AdminService.prototype as unknown as Record<string, unknown>;

    expect(prototipo['importBulkRecords']).toBeUndefined();
    expect(prototipo['analyzeTextWithAI']).toBeUndefined();
  });

  it('ningún módulo declarado promete una ruta de escritura', () => {
    // El campo `endpoint` se retiró del contrato en una revisión anterior: los nueve módulos lo tenían
    // apuntando a rutas que devuelven 404. Esta prueba impide que vuelva por la puerta de atrás.
    for (const modulo of ADMIN_MODULES) {
      expect((modulo as unknown as Record<string, unknown>)['endpoint'])
        .withContext(`el módulo ${modulo.id} volvió a declarar un endpoint`)
        .toBeUndefined();
    }
  });

  it('solo declara módulos con circuito comprobado', () => {
    // ADMIN_MODULES contenía nueve módulos y un segundo filtro dejaba pasar uno. Ahora la lista
    // dice la verdad por sí sola; cuando un proceso vuelva con su modelo, entra aquí y se le ve.
    expect(ADMIN_MODULES.map(modulo => modulo.id)).toEqual(['festivals']);
  });
});
