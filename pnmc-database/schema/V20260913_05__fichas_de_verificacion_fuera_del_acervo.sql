/*
  SIMUS · Catálogo Editorial — las tres fichas de verificación dejan libre el código del acervo

  QUE PASA. al conectar el diseño del catálogo, se crearon a mano tres
  fichas para poder verlo funcionando: «Acento», «La música cuenta» y «Clásicos del siglo XX», con
  los códigos PNMC-ED-001, -002 y -003. Sirvieron para lo que se hicieron.

  POR QUE ESTORBAN AHORA. Esos tres códigos son los de las tres primeras obras del acervo real
  —«Módulos de capacitación para instrumentistas y directores de banda», «Música vocal escolar
  Vol. 1» y «8 arreglos para banda infantil y juvenil»—, y la semilla del acervo solo inserta lo que
  no está: con los códigos ocupados, esas tres obras del Plan no entrarían nunca. Comprobado: la primera
  carga dejó 171 fichas de las cuales tres eran las de verificación y 168 el acervo.

  NO SE BORRAN, SE APARTAN. Borrar registros no es una operación que este proyecto haga dentro de una
  migración sin que el dueño lo valide, y aquí no hace falta: lo único que se necesita es que suelten
  el código. Pasan a PNMC-ED-VERIF-00N y quedan retiradas, así que no se ven en el portal, no se
  cuentan como acervo y siguen ahí por si alguien quiere mirarlas. Eliminarlas es una acción de la
  consola, y la decide una persona.

  SE RECONOCEN POR CONSTRUCCION Y NO POR EL TITULO. Toda ficha del acervo trae `Confianza` y
  `DiapositivaOrigen` —las 171 las declaran en la fuente— y ninguna de las tres de verificación las
  tiene. La condición mira eso, no el nombre: si mañana alguien cataloga una obra que de verdad se
  llame «Acento», esta migración ya habrá pasado y no volverá a mirar.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

UPDATE dbo.PublicacionesEditoriales
SET Codigo = N'PNMC-ED-VERIF-' + RIGHT(Codigo, 3),
    EstadoPublicacion = N'retirado',
    NotasCatalogacion = N'Ficha de verificación. No pertenece al acervo del Proyecto Editorial.',
    FechaActualizacion = SYSUTCDATETIME()
WHERE Codigo IN (N'PNMC-ED-001', N'PNMC-ED-002', N'PNMC-ED-003')
  AND Confianza IS NULL
  AND DiapositivaOrigen IS NULL;
GO
