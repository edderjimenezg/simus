/*
    Las fichas conceptuales son información editorial propia. Eliminar una fila
    maestra no debe borrar esa información en silencio; la operación debe fallar
    hasta que exista una decisión explícita sobre la ficha dependiente.
*/

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_FichasConceptualesTerritoriosSonoros_Territorio'
      AND parent_object_id = OBJECT_ID(N'dbo.FichasConceptualesTerritoriosSonoros')
)
BEGIN
    ALTER TABLE dbo.FichasConceptualesTerritoriosSonoros
        DROP CONSTRAINT FK_FichasConceptualesTerritoriosSonoros_Territorio;

    ALTER TABLE dbo.FichasConceptualesTerritoriosSonoros WITH CHECK
        ADD CONSTRAINT FK_FichasConceptualesTerritoriosSonoros_Territorio
        FOREIGN KEY (TerritorioSonoroId)
        REFERENCES dbo.TerritoriosSonoros (IdTerritorioSonoro);
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_FichasConceptualesPracticas_Practica'
      AND parent_object_id = OBJECT_ID(N'dbo.FichasConceptualesPracticasMusicales')
)
BEGIN
    ALTER TABLE dbo.FichasConceptualesPracticasMusicales
        DROP CONSTRAINT FK_FichasConceptualesPracticas_Practica;

    ALTER TABLE dbo.FichasConceptualesPracticasMusicales WITH CHECK
        ADD CONSTRAINT FK_FichasConceptualesPracticas_Practica
        FOREIGN KEY (PracticaMusicalId)
        REFERENCES dbo.PracticasMusicales (IdPracticaMusical);
END;
GO
