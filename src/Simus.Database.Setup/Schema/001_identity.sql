IF SCHEMA_ID(N'core') IS NULL EXEC(N'CREATE SCHEMA core');
IF SCHEMA_ID(N'identity') IS NULL EXEC(N'CREATE SCHEMA identity');
IF SCHEMA_ID(N'organizations') IS NULL EXEC(N'CREATE SCHEMA organizations');
IF SCHEMA_ID(N'legal') IS NULL EXEC(N'CREATE SCHEMA legal');
IF SCHEMA_ID(N'territory') IS NULL EXEC(N'CREATE SCHEMA territory');

CREATE TABLE core.SchemaVersions (Version nvarchar(80) NOT NULL PRIMARY KEY, AppliedAt datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME());
CREATE TABLE core.BootstrapEvents (EventCode nvarchar(80) NOT NULL PRIMARY KEY, CompletedAt datetime2(0) NOT NULL);

CREATE TABLE identity.Persons (
 Id uniqueidentifier NOT NULL PRIMARY KEY, GivenName nvarchar(120) NOT NULL, MiddleName nvarchar(120) NULL,
 FirstSurname nvarchar(120) NOT NULL, SecondSurname nvarchar(120) NULL, IdentityTypeCode nvarchar(10) NOT NULL,
 IdentityNumberNormalized nvarchar(120) NOT NULL, EmailNormalized nvarchar(320) NOT NULL, Phone nvarchar(40) NULL,
 PasswordHash nvarchar(512) NOT NULL, AccountState nvarchar(30) NOT NULL, EmailVerificationState nvarchar(30) NOT NULL,
 CreatedAt datetime2(0) NOT NULL, UpdatedAt datetime2(0) NOT NULL,
 CONSTRAINT UQ_Persons_Email UNIQUE (EmailNormalized), CONSTRAINT UQ_Persons_Identity UNIQUE (IdentityTypeCode, IdentityNumberNormalized),
 CONSTRAINT CK_Persons_AccountState CHECK (AccountState IN (N'active',N'suspended',N'closed')),
 CONSTRAINT CK_Persons_Verification CHECK (EmailVerificationState IN (N'not_configured',N'pending',N'verified'))
);
CREATE TABLE identity.Roles (Code nvarchar(40) NOT NULL PRIMARY KEY, Name nvarchar(120) NOT NULL);
INSERT INTO identity.Roles (Code,Name) VALUES (N'webmaster',N'Webmaster'),(N'external',N'Persona externa');
CREATE TABLE identity.PersonRoles (
 PersonId uniqueidentifier NOT NULL, RoleCode nvarchar(40) NOT NULL, GrantedAt datetime2(0) NOT NULL,
 PRIMARY KEY(PersonId,RoleCode), CONSTRAINT FK_PersonRoles_Person FOREIGN KEY(PersonId) REFERENCES identity.Persons(Id),
 CONSTRAINT FK_PersonRoles_Role FOREIGN KEY(RoleCode) REFERENCES identity.Roles(Code)
);

CREATE TABLE territory.Departments (Code nvarchar(10) NOT NULL PRIMARY KEY, Name nvarchar(160) NOT NULL);
CREATE TABLE territory.Municipalities (
 Code nvarchar(10) NOT NULL, DepartmentCode nvarchar(10) NOT NULL, Name nvarchar(160) NOT NULL, PRIMARY KEY(Code,DepartmentCode),
 CONSTRAINT FK_Municipalities_Department FOREIGN KEY(DepartmentCode) REFERENCES territory.Departments(Code)
);
CREATE TABLE organizations.Organizations (
 Id uniqueidentifier NOT NULL PRIMARY KEY, Name nvarchar(240) NOT NULL, IdentificationNumber nvarchar(80) NULL,
 ContactEmail nvarchar(320) NULL, DepartmentCode nvarchar(10) NOT NULL, MunicipalityCode nvarchar(10) NOT NULL,
 State nvarchar(30) NOT NULL, CreatedAt datetime2(0) NOT NULL, UpdatedAt datetime2(0) NOT NULL,
 CONSTRAINT FK_Organizations_Department FOREIGN KEY(DepartmentCode) REFERENCES territory.Departments(Code),
 CONSTRAINT FK_Organizations_Municipality FOREIGN KEY(MunicipalityCode,DepartmentCode) REFERENCES territory.Municipalities(Code,DepartmentCode),
 CONSTRAINT CK_Organizations_State CHECK (State IN (N'active',N'archived'))
);
CREATE TABLE organizations.Administrators (
 OrganizationId uniqueidentifier NOT NULL, PersonId uniqueidentifier NOT NULL, GrantedAt datetime2(0) NOT NULL, RevokedAt datetime2(0) NULL,
 PRIMARY KEY(OrganizationId,PersonId), CONSTRAINT FK_Administrators_Organization FOREIGN KEY(OrganizationId) REFERENCES organizations.Organizations(Id),
 CONSTRAINT FK_Administrators_Person FOREIGN KEY(PersonId) REFERENCES identity.Persons(Id)
);

CREATE TABLE legal.Documents (
 Id uniqueidentifier NOT NULL PRIMARY KEY, Code nvarchar(80) NOT NULL, Version nvarchar(40) NOT NULL, Title nvarchar(240) NOT NULL,
 PublicUrl nvarchar(500) NOT NULL, IsCurrent bit NOT NULL, PublishedAt datetime2(0) NOT NULL, CONSTRAINT UQ_LegalDocuments_CodeVersion UNIQUE(Code,Version)
);
CREATE UNIQUE INDEX UX_LegalDocuments_Current ON legal.Documents(Code) WHERE IsCurrent=1;
CREATE TABLE legal.Acceptances (
 Id uniqueidentifier NOT NULL PRIMARY KEY, PersonId uniqueidentifier NOT NULL, DocumentId uniqueidentifier NOT NULL, AcceptedAt datetime2(0) NOT NULL,
 CONSTRAINT UQ_Acceptances_PersonDocument UNIQUE(PersonId,DocumentId), CONSTRAINT FK_Acceptances_Person FOREIGN KEY(PersonId) REFERENCES identity.Persons(Id),
 CONSTRAINT FK_Acceptances_Document FOREIGN KEY(DocumentId) REFERENCES legal.Documents(Id)
);
CREATE TABLE identity.Sessions (
 Id uniqueidentifier NOT NULL PRIMARY KEY, PersonId uniqueidentifier NOT NULL, SecretHash varbinary(64) NOT NULL,
 IssuedAt datetime2(0) NOT NULL, LastActivityAt datetime2(0) NOT NULL, IdleExpiresAt datetime2(0) NOT NULL,
 AbsoluteExpiresAt datetime2(0) NOT NULL, RevokedAt datetime2(0) NULL, CONSTRAINT UQ_Sessions_SecretHash UNIQUE(SecretHash),
 CONSTRAINT FK_Sessions_Person FOREIGN KEY(PersonId) REFERENCES identity.Persons(Id)
);
CREATE INDEX IX_Sessions_PersonId ON identity.Sessions(PersonId,RevokedAt,AbsoluteExpiresAt);
INSERT INTO core.SchemaVersions (Version) VALUES (N'001_identity');
