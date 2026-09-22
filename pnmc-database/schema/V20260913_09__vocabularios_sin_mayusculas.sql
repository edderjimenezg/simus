/*
 * EL VOCABULARIO DE TIPO DE AGENTE, PROTEGIDO DE VERDAD.
 *
 * POR QUE HACE FALTA ESTA MIGRACION. La tabla ya traía
 * `CK_AgentesEditoriales_Tipo CHECK (Tipo IN (N'persona', N'entidad'))`, y parecía suficiente. No lo
 * era: la base está en `SQL_Latin1_General_CP1_CI_AS` —en la columna y en la base—,
 * una intercalación INSENSIBLE A MAYUSCULAS, así que el CHECK admitía «Entidad», «ENTIDAD» o
 * «EnTiDaD» como si fueran el valor bueno. El vocabulario estaba escrito, pero no custodiado.
 *
 * QUE COSTO TUVO. El adaptador del portal comparaba el tipo contra «Entidad» capitalizado. Ni el
 * compilador —el campo iba como `string`— ni la base podían avisar de que ese valor no existe en los
 * datos. La comparación no se cumplió nunca: la autoría corporativa jamás se separó de la personal y
 * la fila «Autor corporativo» de la ficha pública no llegó a verse en ninguna de las 171
 * publicaciones del acervo. Nada falló; solo faltaba un dato en pantalla, que es la forma más cara
 * de fallar porque no deja rastro.
 *
 * SEGURA POR MEDICION, NO POR SUPOSICION. Antes de escribirla se contaron los agentes por su valor
 * exacto con `COLLATE Latin1_General_BIN2`: 379 `persona` y 24 `entidad`, los 403 en minúscula. No
 * hay ninguna fila que esta restricción vaya a rechazar, y no se modifica ni se borra ningún dato:
 * solo se cambia qué admitirá la tabla de aquí en adelante.
 */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AgentesEditoriales_Tipo')
BEGIN
    ALTER TABLE dbo.AgentesEditoriales DROP CONSTRAINT CK_AgentesEditoriales_Tipo;
END
GO

/*
 * El `COLLATE Latin1_General_BIN2` es lo que hace la comparación byte a byte. Sin él, este CHECK
 * sería exactamente el anterior otra vez.
 */
ALTER TABLE dbo.AgentesEditoriales WITH CHECK
    ADD CONSTRAINT CK_AgentesEditoriales_Tipo
    CHECK (Tipo COLLATE Latin1_General_BIN2 IN (N'persona', N'entidad'));
GO
