/*
  SOLO PARA PRUEBAS. Crea una versión mínima de las tablas propias de SIMUS que referencian las
  tablas ART_MUS_*, para poder instalar el estado inicial en una base vacía. En la base del
  Ministerio estas tablas ya existen con su estructura real y este guion no se ejecuta.
*/
SET NOCOUNT ON;

CREATE TABLE dbo.ART_MUSICA_USUARIO (Id int NOT NULL PRIMARY KEY);
INSERT INTO dbo.ART_MUSICA_USUARIO (Id) VALUES (30476);

CREATE TABLE dbo.BAS_ZONAS_GEOGRAFICAS (ZON_ID varchar(5) NOT NULL PRIMARY KEY);
INSERT INTO dbo.BAS_ZONAS_GEOGRAFICAS (ZON_ID)
VALUES ('05'), ('08'), ('11'), ('13'), ('15'), ('17'), ('18'), ('19'), ('20'), ('23'), ('25'), ('27'),
       ('41'), ('44'), ('47'), ('50'), ('52'), ('54'), ('63'), ('66'), ('68'), ('70'), ('73'), ('76'),
       ('81'), ('85'), ('86'), ('88'), ('91'), ('94'), ('95'), ('97'), ('99'),
       ('05002'), ('08296'), ('11001'), ('20011'), ('81001'), ('88564'), ('91263'), ('731');

-- Las dos tablas ART_MUS_TIP_* de la exportación completa dependen de estas.
CREATE TABLE dbo.ART_MUSICA_ENTIDAD_INSTITUCIONALIDAD (ENT_ID numeric(18,0) NOT NULL PRIMARY KEY);
CREATE TABLE dbo.ART_MUSICA_ENTIDAD_PARTICIPACION (ENT_ID numeric(18,0) NOT NULL PRIMARY KEY);
CREATE TABLE dbo.ART_MUSICA_TIPO_DOCUMENTO_CREACION (ART_MUS_TIP_DOC_CRE_ID smallint NOT NULL PRIMARY KEY);
CREATE TABLE dbo.ART_MUSICA_TIPO_PROY_ORG_COM (ART_MUS_TIP_PROY_ORG_COM_ID smallint NOT NULL PRIMARY KEY);
