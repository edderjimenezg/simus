import { crearLibroExcel } from './crear-libro-excel';

/** Lo que la plantilla necesita saber de cada columna. */
export interface CampoDePlantilla {
  name: string;
  label: string;
  type?: string;
  required?: boolean;
}

export interface PeticionDePlantilla<TCampo extends CampoDePlantilla = CampoDePlantilla> {
  /** Sin extensión: `plantilla_mercados`. */
  nombreDelArchivo: string;
  campos: readonly TCampo[];
  /** Una fila de ejemplo, alineada con `campos`, para que quien abra el archivo vea qué va en cada columna. */
  filaDeEjemplo: readonly unknown[];
  /** Las opciones válidas de un campo con vocabulario controlado, o vacío si es texto libre. */
  opcionesDe: (campo: TCampo) => readonly string[];
}

/**
 * Construye y descarga la plantilla Excel de importación de un módulo.
 *
 * <b>VIVIA DENTRO DEL PANEL DE FESTIVALES, EN CIEN LINEAS.</b> Hasta
 * la única forma de que otro módulo tuviera «Plantilla» era copiarlas, y la dirección de producto
 * pidió ese día que Mercados tuviera todo lo que tiene Festivales, plantilla incluida. Copiar cien
 * líneas es la definición de «dos copias que divergen»; aquí hay una.
 *
 * <b>TRES HOJAS, Y LA SEGUNDA ESCONDIDA.</b> «Plantilla» es la que se rellena, con la primera fila
 * congelada y una fila de ejemplo; «Catalogos» guarda las listas de valores válidos y se oculta
 * —`veryHidden`, para que no aparezca ni con clic derecho— porque es el andamiaje de las
 * validaciones, no algo que leer; «Instrucciones» dice qué campo es qué y cuál es obligatorio.
 *
 * <b>LAS LISTAS SE VALIDAN EN LA CELDA</b>, hasta la fila 500: quien escriba un alcance que no
 * existe se entera al escribirlo, no al importar.
 */
export async function descargarPlantillaDeImportacion<TCampo extends CampoDePlantilla>(peticion: PeticionDePlantilla<TCampo>): Promise<void> {
  const { campos, filaDeEjemplo } = peticion;
  const workbook = await crearLibroExcel();
  workbook.creator = 'Entorno Virtual PNMC';
  workbook.created = new Date();

  const hojaDePlantilla = workbook.addWorksheet('Plantilla', { views: [{ state: 'frozen', ySplit: 1 }] });
  const hojaDeCatalogos = workbook.addWorksheet('Catalogos');
  const hojaDeGuia = workbook.addWorksheet('Instrucciones');
  hojaDeCatalogos.state = 'veryHidden';

  hojaDePlantilla.addRow(campos.map(c => c.label));
  hojaDePlantilla.addRow([...filaDeEjemplo]);
  hojaDePlantilla.getRow(1).font = { bold: true, color: { argb: 'FFFFFFFF' } };
  hojaDePlantilla.getRow(1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF291242' } };
  hojaDePlantilla.getRow(1).alignment = { vertical: 'middle', wrapText: true };
  hojaDePlantilla.getRow(2).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFF8FAFC' } };
  hojaDePlantilla.columns = campos.map((c, i) => {
    const largoDelRotulo = String(c.label || '').length;
    const largoDelEjemplo = String(filaDeEjemplo[i] ?? '').length;
    return { width: Math.min(42, Math.max(16, Math.max(largoDelRotulo, largoDelEjemplo) + 3)) };
  });

  let columnaDeCatalogo = 1;
  const rangos = new Map<string, string>();
  for (const campo of campos) {
    const opciones = peticion.opcionesDe(campo);
    if (!opciones.length) continue;
    hojaDeCatalogos.getCell(1, columnaDeCatalogo).value = campo.name;
    opciones.forEach((opcion, i) => { hojaDeCatalogos.getCell(i + 2, columnaDeCatalogo).value = opcion; });
    const letra = hojaDeCatalogos.getColumn(columnaDeCatalogo).letter;
    rangos.set(campo.name, `Catalogos!$${letra}$2:$${letra}$${opciones.length + 1}`);
    columnaDeCatalogo++;
  }

  const filasValidadas = 500;
  campos.forEach((campo, i) => {
    const rango = rangos.get(campo.name);
    if (!rango) return;
    for (let fila = 2; fila <= filasValidadas; fila++) {
      hojaDePlantilla.getCell(fila, i + 1).dataValidation = {
        type: 'list', allowBlank: true, formulae: [rango], showErrorMessage: true,
        errorTitle: 'Valor inválido', error: 'Seleccione un valor de la lista oficial.',
      };
    }
  });

  hojaDeGuia.columns = [
    { header: 'Campo', key: 'field', width: 32 },
    { header: 'Tipo', key: 'type', width: 18 },
    { header: 'Obligatorio', key: 'required', width: 14 },
    { header: 'Observación', key: 'note', width: 70 },
  ];
  hojaDeGuia.getRow(1).font = { bold: true };
  for (const campo of campos) {
    hojaDeGuia.addRow({
      field: campo.label,
      type: campo.type || 'Texto',
      required: campo.required ? 'Sí' : 'No',
      note: campo.required ? 'Este campo es obligatorio para que el registro pase a estado verificado.' : 'Opcional.',
    });
  }

  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
  const url = URL.createObjectURL(blob);
  const enlace = document.createElement('a');
  enlace.href = url;
  enlace.download = `${peticion.nombreDelArchivo}.xlsx`;
  document.body.appendChild(enlace);
  enlace.click();
  enlace.remove();
  URL.revokeObjectURL(url);
}
