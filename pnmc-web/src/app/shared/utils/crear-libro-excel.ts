/**
 * Carga ExcelJS únicamente cuando una persona importa o descarga un archivo.
 * La consola administrativa no necesita incorporar este paquete al abrirse.
 */
export async function crearLibroExcel(): Promise<import('exceljs').Workbook> {
  const { default: ExcelJS } = await import('exceljs');
  return new ExcelJS.Workbook();
}
