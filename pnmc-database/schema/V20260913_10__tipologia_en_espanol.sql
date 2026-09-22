/*
 * EL EJE DCMI, DICHO EN ESPAÑOL.
 *
 * QUE PASABA. La tabla guarda cada término con dos columnas: `Codigo`, que es el identificador
 * normalizado con el que se intercambia la ficha, y `Etiqueta`, que es «cómo se lee en pantalla».
 * Para los ejes RDA se escribieron etiquetas en español —«música notada», «disco de audio»—, pero
 * para el eje DCMI se escribió el propio término inglés en las dos: `Codigo = 'movingimage'` y
 * `Etiqueta = 'MovingImage'`. Resultado: «Tipo de recurso: MovingImage» en mitad de una consola en
 * español, y lo mismo en el formulario de alta.
 *
 * POR QUE ESTO NO ROMPE LA INTEROPERABILIDAD. El término normalizado de DCMI no es la etiqueta: es
 * el CODIGO, y el código no se toca. `movingimage` sigue siendo `movingimage`, que es lo que se
 * exporta y lo que permite que otro catálogo entienda la ficha. La etiqueta solo decide qué lee una
 * persona, y esa persona lee en español. La migración original razonó que «DCMI no se traduce», pero
 * eso vale para el identificador, no para el rótulo.
 *
 * SE ACTUALIZA POR CODIGO Y SOLO SI SIGUE EN INGLES: si alguien ya lo corrigió a mano, no se pisa.
 */
SET NOCOUNT ON;
GO

UPDATE dbo.TipologiasEditoriales SET Etiqueta = N'Texto'
WHERE Eje = N'recurso' AND Codigo = N'text' AND Etiqueta = N'Text';

UPDATE dbo.TipologiasEditoriales SET Etiqueta = N'Sonido'
WHERE Eje = N'recurso' AND Codigo = N'sound' AND Etiqueta = N'Sound';

UPDATE dbo.TipologiasEditoriales SET Etiqueta = N'Imagen en movimiento'
WHERE Eje = N'recurso' AND Codigo = N'movingimage' AND Etiqueta = N'MovingImage';

UPDATE dbo.TipologiasEditoriales SET Etiqueta = N'Imagen fija'
WHERE Eje = N'recurso' AND Codigo = N'image' AND Etiqueta = N'Image';

UPDATE dbo.TipologiasEditoriales SET Etiqueta = N'Recurso interactivo'
WHERE Eje = N'recurso' AND Codigo = N'interactiveresource' AND Etiqueta = N'InteractiveResource';

UPDATE dbo.TipologiasEditoriales SET Etiqueta = N'Conjunto de datos'
WHERE Eje = N'recurso' AND Codigo = N'dataset' AND Etiqueta = N'Dataset';

UPDATE dbo.TipologiasEditoriales SET Etiqueta = N'Colección'
WHERE Eje = N'recurso' AND Codigo = N'collection' AND Etiqueta = N'Collection';
GO
