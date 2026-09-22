using Microsoft.EntityFrameworkCore;

namespace PNMC.Infrastructure.Data;

public sealed class PnmcDbContext : DbContext
{
    public PnmcDbContext(DbContextOptions<PnmcDbContext> options)
        : base(options)
    {
    }

    public DbSet<ContentStatusRow> ContentStatuses => Set<ContentStatusRow>();
    public DbSet<DivipolaLocationRow> DivipolaLocations => Set<DivipolaLocationRow>();
    public DbSet<PoliticaDeDatosRow> PoliticasDeDatos => Set<PoliticaDeDatosRow>();
    public DbSet<AutorizacionDeDatosRow> AutorizacionesDeDatos => Set<AutorizacionDeDatosRow>();

    public DbSet<TagRow> Tags => Set<TagRow>();

    public DbSet<FestivalRow> FestivalRecords => Set<FestivalRow>();
    public DbSet<PracticaMusicalRow> PracticasMusicales => Set<PracticaMusicalRow>();
    public DbSet<CategoriaRow> Categorias => Set<CategoriaRow>();
    public DbSet<ProcedenciaDeRegistroRow> ProcedenciasDeRegistro => Set<ProcedenciaDeRegistroRow>();
    public DbSet<ProyectoTransversalRow> ProyectosTransversales => Set<ProyectoTransversalRow>();
    public DbSet<EventoAgendaProyectoTransversalRow> EventosAgendaProyectosTransversales => Set<EventoAgendaProyectoTransversalRow>();
    public DbSet<NoticiaProyectoTransversalRow> NoticiasProyectosTransversales => Set<NoticiaProyectoTransversalRow>();
    public DbSet<NoticiaArchivoRow> NoticiasArchivos => Set<NoticiaArchivoRow>();
    public DbSet<NoticiaPracticaMusicalRow> NoticiasPracticasMusicales => Set<NoticiaPracticaMusicalRow>();
    public DbSet<NoticiaTerritorioSonoroRow> NoticiasTerritoriosSonoros => Set<NoticiaTerritorioSonoroRow>();
    public DbSet<EventoAgendaPracticaMusicalRow> EventosAgendaPracticasMusicales => Set<EventoAgendaPracticaMusicalRow>();
    public DbSet<EventoAgendaTerritorioSonoroRow> EventosAgendaTerritoriosSonoros => Set<EventoAgendaTerritorioSonoroRow>();
    public DbSet<PublicacionEditorialPracticaMusicalRow> PublicacionesEditorialesPracticasMusicales => Set<PublicacionEditorialPracticaMusicalRow>();
    public DbSet<PublicacionEditorialTerritorioSonoroRow> PublicacionesEditorialesTerritoriosSonoros => Set<PublicacionEditorialTerritorioSonoroRow>();
    public DbSet<TerritorioSonoroRow> TerritoriosSonoros => Set<TerritorioSonoroRow>();
    public DbSet<FichaConceptualTerritorioSonoroRow> FichasConceptualesTerritoriosSonoros => Set<FichaConceptualTerritorioSonoroRow>();
    public DbSet<FichaConceptualPracticaMusicalRow> FichasConceptualesPracticasMusicales => Set<FichaConceptualPracticaMusicalRow>();
    public DbSet<VersionFestivalRow> VersionesFestival => Set<VersionFestivalRow>();

    /// <summary>
    /// Las ediciones de un Festival (2024, 2025, 2026). NO es <see cref="VersionesFestival"/>:
    /// aquella es la version del registro publicado y la lee el sitio publico.
    /// </summary>
    public DbSet<EdicionFestivalRow> EdicionesFestival => Set<EdicionFestivalRow>();

    // LAS CINCO CLASIFICACIONES, COMPARTIDAS POR TODOS LOS PROCESOS. Sustituyen a catorce tablas
    // que tenían la misma forma y solo se distinguían por de quién colgaban. Ver `Clasificaciones`,
    // que es por donde se leen y se escriben: filtrar a mano es donde se olvida el módulo.
    public DbSet<PracticaMusicalDeRegistroRow> PracticasMusicalesDeRegistro => Set<PracticaMusicalDeRegistroRow>();
    public DbSet<TerritorioSonoroDeRegistroRow> TerritoriosSonorosDeRegistro => Set<TerritorioSonoroDeRegistroRow>();
    public DbSet<ExpresionArtisticaDeRegistroRow> ExpresionesArtisticasDeRegistro => Set<ExpresionArtisticaDeRegistroRow>();
    public DbSet<ModalidadParticipacionDeRegistroRow> ModalidadesParticipacionDeRegistro => Set<ModalidadParticipacionDeRegistroRow>();
    public DbSet<TipoIngresoDeRegistroRow> TiposIngresoDeRegistro => Set<TipoIngresoDeRegistroRow>();

    // LAS TRES RELACIONES QUE NO SON DE CATALOGO, tambien compartidas. Sustituyen a seis tablas que
    // guardaban lo mismo con dos juegos de nombres: «socia» frente a «aliada», «archivo» frente a
    // «material», y dos localizaciones identicas campo por campo.
    public DbSet<LocalizacionDeRegistroRow> LocalizacionesDeRegistro => Set<LocalizacionDeRegistroRow>();
    public DbSet<EntidadAliadaDeRegistroRow> EntidadesAliadasDeRegistro => Set<EntidadAliadaDeRegistroRow>();
    public DbSet<ArchivoDeRegistroRow> ArchivosDeRegistro => Set<ArchivoDeRegistroRow>();
    public DbSet<BoletinSuscripcionRow> BoletinSuscripciones => Set<BoletinSuscripcionRow>();

    /// <summary>
    /// La revision institucional de un Festival y los cambios que pide campo por campo.
    /// </summary>
    /// <remarks>
    /// NO ES `RegistrosRevisionHistorial`. Aquella es el expediente cerrado —una fila por decision,
    /// inmutable—; estas dos son el borrador VIVO del funcionario, que se reescribe cada vez que
    /// guarda. Al enviar, el JSON de las notas se copia a `CamposObservados` de aquella tabla, que
    /// existe desde y hasta hoy no la llenaba ninguna pantalla.
    /// </remarks>

    /// <summary>Las revisiones por campos de cualquier proceso del Ecosistema. Ver `RevisionDeCampos.cs`.</summary>
    public DbSet<RevisionDeRegistroRow> RevisionesDeRegistro => Set<RevisionDeRegistroRow>();
    public DbSet<RevisionDeRegistroObservacionRow> RevisionesDeRegistroObservaciones => Set<RevisionDeRegistroObservacionRow>();

    // LA PROPUESTA DE CAMBIO SOBRE LO PUBLICADO, genérica por módulo como las revisiones.
    public DbSet<PropuestaDeCambioRow> PropuestasDeCambio => Set<PropuestaDeCambioRow>();
    public DbSet<PropuestaDeCambioCampoRow> PropuestasDeCambioCampos => Set<PropuestaDeCambioCampoRow>();

    // -------------------------------------------------------------------------------------
    // EL MODELO DE FESTIVALES DE SIMUS, mapeado.
    // -------------------------------------------------------------------------------------
    // Las once tablas de catalogo y las cinco puente existian en la base desde el 24 de agosto
    // (`schema/V20260824_02__festivales_simus.sql`) y NINGUNA estaba mapeada aqui: existian y
    // eran invisibles para el API. Sus 54 filas se sembraron el 28 desde el volcado de SIMUS.
    public DbSet<TipologiaFestivalRow> TipologiasFestival => Set<TipologiaFestivalRow>();
    public DbSet<ExpresionArtisticaRow> ExpresionesArtisticas => Set<ExpresionArtisticaRow>();
    public DbSet<FuenteFinanciacionRow> FuentesFinanciacion => Set<FuenteFinanciacionRow>();
    public DbSet<ModalidadParticipacionRow> ModalidadesParticipacion => Set<ModalidadParticipacionRow>();
    public DbSet<NaturalezaEntidadRow> NaturalezasEntidad => Set<NaturalezaEntidadRow>();
    public DbSet<TipoIngresoRow> TiposIngreso => Set<TipoIngresoRow>();
    public DbSet<TipoOrganizadorRow> TiposOrganizador => Set<TipoOrganizadorRow>();
    public DbSet<ZonaUrbanoRuralRow> ZonasUrbanoRural => Set<ZonaUrbanoRuralRow>();
    public DbSet<TitulacionColectivaRow> TitulacionesColectivas => Set<TitulacionColectivaRow>();
    public DbSet<RegionOcadRow> RegionesOcad => Set<RegionOcadRow>();
    public DbSet<DepartamentoRegionOcadRow> DepartamentosRegionOcad => Set<DepartamentoRegionOcadRow>();

    public DbSet<FileRow> Files => Set<FileRow>();

    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<RoleRow> Roles => Set<RoleRow>();

    /// <summary>
    /// Que roles tiene cada persona. <b>Fuente de verdad desde entonces</b>; ver
    /// <see cref="UsuarioRolRow"/> y <c>Security/RolesDeUsuario.cs</c>.
    /// </summary>
    public DbSet<UsuarioRolRow> UsuariosRoles => Set<UsuarioRolRow>();

    /// <summary>Catalogo de permisos. Inventario: hoy ninguna guarda lo consulta.</summary>
    public DbSet<PermisoRow> Permisos => Set<PermisoRow>();

    /// <summary>Reparto de permisos por rol. Inventario: hoy ninguna guarda lo consulta.</summary>
    public DbSet<RolPermisoRow> RolesPermisos => Set<RolPermisoRow>();
    public DbSet<AuditLogRow> AuditLogs => Set<AuditLogRow>();
    public DbSet<LoteImportacionRow> LotesImportacion => Set<LoteImportacionRow>();
    public DbSet<FilaImportacionRow> FilasImportacion => Set<FilaImportacionRow>();
    public DbSet<HallazgoImportacionRow> HallazgosImportacion => Set<HallazgoImportacionRow>();
    public DbSet<DecisionImportacionRow> DecisionesImportacion => Set<DecisionImportacionRow>();
    public DbSet<HistorialRevisionRegistroRow> HistorialesRevisionRegistros => Set<HistorialRevisionRegistroRow>();
    public DbSet<EnvioDeRevisionRow> EnviosDeRevision => Set<EnvioDeRevisionRow>();
    public DbSet<UserVerificationCodeRow> UserVerificationCodes => Set<UserVerificationCodeRow>();
    public DbSet<NotificationRow> Notifications => Set<NotificationRow>();
    public DbSet<RecordLinkRequestRow> RecordLinkRequests => Set<RecordLinkRequestRow>();
    public DbSet<RecordDuplicateCandidateRow> RecordDuplicateCandidates => Set<RecordDuplicateCandidateRow>();
    public DbSet<RecordQualityFlagRow> RecordQualityFlags => Set<RecordQualityFlagRow>();
    public DbSet<BorradorDeProcesoRow> BorradoresDeProceso => Set<BorradorDeProcesoRow>();
    public DbSet<AdministrationClaimRow> AdministrationClaims => Set<AdministrationClaimRow>();
    public DbSet<AdministrationClaimClarificationRow> AdministrationClaimClarifications => Set<AdministrationClaimClarificationRow>();
    public DbSet<AdministrationTransferRow> AdministrationTransfers => Set<AdministrationTransferRow>();
    public DbSet<AdministrationReconciliationRow> AdministrationReconciliations => Set<AdministrationReconciliationRow>();
    public DbSet<EntityProfileRow> EntityProfiles => Set<EntityProfileRow>();
    public DbSet<EntidadResponsableRow> EntidadesResponsable => Set<EntidadResponsableRow>();
    public DbSet<UserEntityRow> UserEntities => Set<UserEntityRow>();

    // ── Mercados musicales ───────────────────────────────────────────────────────────────────
    public DbSet<MercadoRow> Mercados => Set<MercadoRow>();
    public DbSet<EdicionMercadoRow> EdicionesMercado => Set<EdicionMercadoRow>();
    public DbSet<AlcanceMercadoRow> AlcancesMercado => Set<AlcanceMercadoRow>();
    public DbSet<ModalidadMercadoRow> ModalidadesMercado => Set<ModalidadMercadoRow>();
    public DbSet<MercadoPracticaMusicalRow> MercadosPracticasMusicales => Set<MercadoPracticaMusicalRow>();
    public DbSet<MercadoTerritorioSonoroRow> MercadosTerritoriosSonoros => Set<MercadoTerritorioSonoroRow>();

    public DbSet<ConfirmacionDeCorreoRow> ConfirmacionesDeCorreo => Set<ConfirmacionDeCorreoRow>();
    public DbSet<ReferenciaHistoricaFestivalRow> ReferenciasHistoricasFestival => Set<ReferenciaHistoricaFestivalRow>();

    public DbSet<ParticipationSubmissionRow> Participations => Set<ParticipationSubmissionRow>();

    /// <summary>Qué módulos de la consola tiene activados cada cuenta. Permisos POR CUENTA.</summary>
    public DbSet<ModuloPorCuentaRow> ModulosPorCuenta => Set<ModuloPorCuentaRow>();

    /// <summary>Los tipos de documento de identidad del país. Vocabulario controlado.</summary>
    public DbSet<TipoDocumentoRow> TiposDocumento => Set<TipoDocumentoRow>();

    public DbSet<ContenidoWebRow> ContenidoWeb => Set<ContenidoWebRow>();
    public DbSet<EquipoWebRow> EquipoWeb => Set<EquipoWebRow>();

    // ── Catálogo Editorial ──
    public DbSet<AgenteEditorialRow> AgentesEditoriales => Set<AgenteEditorialRow>();
    public DbSet<FuenteEditorialRow> FuentesEditoriales => Set<FuenteEditorialRow>();
    public DbSet<ProgramaEditorialRow> ProgramasEditoriales => Set<ProgramaEditorialRow>();
    public DbSet<PublicacionEditorialRow> PublicacionesEditoriales => Set<PublicacionEditorialRow>();
    public DbSet<PublicacionEditorialFuenteRow> PublicacionesEditorialesFuentes => Set<PublicacionEditorialFuenteRow>();
    public DbSet<CreditoEditorialRow> CreditosEditoriales => Set<CreditoEditorialRow>();
    public DbSet<IdentificadorEditorialRow> IdentificadoresEditoriales => Set<IdentificadorEditorialRow>();
    public DbSet<AccesoEditorialRow> AccesosEditoriales => Set<AccesoEditorialRow>();
    public DbSet<PublicacionEditorialProgramaRow> PublicacionesEditorialesProgramas => Set<PublicacionEditorialProgramaRow>();
    public DbSet<PalabraClaveEditorialRow> PalabrasClaveEditoriales => Set<PalabraClaveEditorialRow>();
    public DbSet<TipologiaEditorialRow> TipologiasEditoriales => Set<TipologiaEditorialRow>();
    public DbSet<PublicacionEditorialTipologiaRow> PublicacionesEditorialesTipologias => Set<PublicacionEditorialTipologiaRow>();
    public DbSet<NoticiaRow> Noticias => Set<NoticiaRow>();
    public DbSet<EtiquetaNoticiaRow> EtiquetasNoticia => Set<EtiquetaNoticiaRow>();
    public DbSet<EventoAgendaRow> EventosAgenda => Set<EventoAgendaRow>();
    public DbSet<EtiquetaEventoAgendaRow> EtiquetasEventoAgenda => Set<EtiquetaEventoAgendaRow>();
    public DbSet<EventoAgendaArchivoRow> EventosAgendaArchivos => Set<EventoAgendaArchivoRow>();
    public DbSet<HistorialDeContenidoWebRow> HistorialDeContenidoWeb => Set<HistorialDeContenidoWebRow>();
    public DbSet<ImagenWebRow> ImagenesWeb => Set<ImagenWebRow>();
    public DbSet<HistorialDeImagenWebRow> HistorialDeImagenesWeb => Set<HistorialDeImagenWebRow>();

    /// <summary>
    /// El identificador de la organizacion institucional, resuelto una vez por contexto.
    /// </summary>
    private int? _idInstitucional;
    private bool _idInstitucionalResuelto;

    /// <summary>
    /// NINGUN REGISTRO DEL ECOSISTEMA SE GUARDA SIN QUIEN RESPONDA POR EL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// POR QUE AQUI Y NO EN CADA MANEJADOR. Hay ocho sitios que crean registros de estos catalogos
    /// —cuatro altas de la consola, dos importaciones masivas y dos siembras del arranque—, y el
    /// noveno lo escribira alguien que no haya leido esto. Repartida, la regla se cumple hasta que
    /// alguien la olvida, y entonces falla contra la restriccion NOT NULL de la base con un error
    /// que no explica nada. Aqui no se puede olvidar.
    /// </para>
    /// <para>
    /// SOLO TOCA LO QUE NACE, y solo si viene vacio: un registro que YA tiene organizacion —porque
    /// alguien lo reclamo— no se le cambia nunca. Y si la entidad institucional todavia no existe
    /// se deja nulo en vez de inventar un identificador: en una base a medio sembrar, un cero o un
    /// uno apuntarian a cualquier cosa.
    /// </para>
    /// </remarks>
    private void AsignarOrganizacionResponsable()
    {
        var pendientes = ChangeTracker.Entries<ITieneOrganizacionResponsable>()
            .Where(entrada => entrada.State == EntityState.Added
                && entrada.Entity.OrganizacionResponsableId is null)
            .ToList();

        if (pendientes.Count == 0) return;

        if (!_idInstitucionalResuelto)
        {
            _idInstitucional = Set<EntityProfileRow>().AsNoTracking()
                .Where(item => item.IsInstitutional)
                .Select(item => (int?)item.Id)
                .FirstOrDefault();
            _idInstitucionalResuelto = true;
        }

        if (_idInstitucional is null) return;

        foreach (var entrada in pendientes)
        {
            entrada.Entity.OrganizacionResponsableId = _idInstitucional;
        }
    }

    public override int SaveChanges()
    {
        AsignarOrganizacionResponsable();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AsignarOrganizacionResponsable();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentStatusRow>(entity =>
        {
            entity.ToTable("EstadosContenido");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEstadoContenido");
            entity.Property(x => x.Code).HasColumnName("CodigoEstado");
            entity.Property(x => x.Name).HasColumnName("NombreEstado");
            entity.Property(x => x.Description).HasColumnName("DescripcionEstado");
        });

        modelBuilder.Entity<DivipolaLocationRow>(entity =>
        {
            entity.ToTable("Divipola");
            entity.HasKey(x => new { x.DepartmentCode, x.MunicipalityCode });
            entity.Property(x => x.DepartmentCode).HasColumnName("CodigoDepartamento");
            entity.Property(x => x.DepartmentName).HasColumnName("NombreDepartamento");
            entity.Property(x => x.MunicipalityCode).HasColumnName("CodigoMunicipio");
            entity.Property(x => x.MunicipalityName).HasColumnName("NombreMunicipio");
            entity.Property(x => x.LocationType).HasColumnName("TipoTerritorio");
            entity.Property(x => x.Latitude).HasColumnName("Latitud");
            entity.Property(x => x.Longitude).HasColumnName("Longitud");
            entity.Property(x => x.Latitude).HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasPrecision(9, 6);
        });

        modelBuilder.Entity<PoliticaDeDatosRow>(entity =>
        {
            entity.ToTable("PoliticasDatos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPolitica");
            entity.Property(x => x.Clave).HasColumnName("Clave").HasMaxLength(40);
            entity.Property(x => x.Version).HasColumnName("Version").HasMaxLength(40);
            entity.Property(x => x.Titulo).HasColumnName("Titulo").HasMaxLength(300);
            entity.Property(x => x.Texto).HasColumnName("Texto");
            entity.Property(x => x.UrlOficial).HasColumnName("UrlOficial").HasMaxLength(2000);
            entity.Property(x => x.ReferenciaOficial).HasColumnName("ReferenciaOficial").HasMaxLength(160);
            entity.Property(x => x.Vigente).HasColumnName("Vigente");
            entity.Property(x => x.FechaPublicacion).HasColumnName("FechaPublicacion");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.HasIndex(x => new { x.Clave, x.Version }).IsUnique();
        });

        modelBuilder.Entity<AutorizacionDeDatosRow>(entity =>
        {
            entity.ToTable("AutorizacionesDatos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdAutorizacion");
            entity.Property(x => x.Finalidad).HasColumnName("Finalidad").HasMaxLength(40);
            entity.Property(x => x.IdPolitica).HasColumnName("IdPolitica");
            entity.Property(x => x.Version).HasColumnName("Version").HasMaxLength(40);
            entity.Property(x => x.TextoAceptado).HasColumnName("TextoAceptado");
            entity.Property(x => x.TextoReconstruido).HasColumnName("TextoReconstruido");
            entity.Property(x => x.IdUsuario).HasColumnName("IdUsuario");
            entity.Property(x => x.CorreoTitular).HasColumnName("CorreoTitular").HasMaxLength(180);
            entity.Property(x => x.Origen).HasColumnName("Origen").HasMaxLength(40);
            entity.Property(x => x.ReferenciaId).HasColumnName("ReferenciaId").HasMaxLength(120);
            entity.Property(x => x.FechaOtorgada).HasColumnName("FechaOtorgada");
            entity.Property(x => x.FechaRevocacion).HasColumnName("FechaRevocacion");
            entity.Property(x => x.MotivoRevocacion).HasColumnName("MotivoRevocacion").HasMaxLength(400);

            // NO SE MAPEA COMO COLUMNA: `EstaVigente` es una lectura de `FechaRevocacion`, no un
            // dato propio. Guardarlo aparte permitiria que las dos discreparan.
            entity.Ignore(x => x.EstaVigente);

            // SIN INDICE UNICO POR (USUARIO, POLITICA), y es deliberado. El anterior lo tenia, y
            // con el puesto una persona no podia aceptar la v2 despues de haber aceptado la v1:
            // cada version aceptada es una fila propia, que es justo lo que hay que poder guardar.
            entity.HasIndex(x => new { x.IdUsuario, x.Finalidad, x.FechaOtorgada });
            entity.HasIndex(x => new { x.CorreoTitular, x.Finalidad, x.FechaOtorgada });
        });

        modelBuilder.Entity<TagRow>(entity =>
        {
            entity.ToTable("Etiquetas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEtiqueta");
            entity.Property(x => x.Name).HasColumnName("NombreEtiqueta");
        });

        modelBuilder.Entity<FestivalRow>(entity =>
        {
            entity.ToTable("Festivales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdFestival");
            entity.Property(x => x.Name).HasColumnName("NombreFestival");
            entity.Property(x => x.VersionsCount).HasColumnName("NumeroVersiones");
            entity.Property(x => x.LastEditionDate).HasColumnName("FechaUltimaVersion");
            entity.Property(x => x.Description).HasColumnName("Descripcion");
            entity.Property(x => x.OrganizerDisplayName).HasColumnName("Organizador");
            entity.Property(x => x.OrganizerContactEmail).HasColumnName("CorreoOrganizador");
            entity.Property(x => x.OrganizerContactPhone).HasColumnName("TelefonoOrganizador");
            entity.Property(x => x.OrganizerWebsiteUrl).HasColumnName("SitioWebOrganizador");
            entity.Property(x => x.ContactEmail).HasColumnName("CorreoFestival");
            entity.Property(x => x.InstagramUrl).HasColumnName("InstagramFestival");
            entity.Property(x => x.FacebookUrl).HasColumnName("FacebookFestival");
            entity.Property(x => x.WebsiteUrl).HasColumnName("SitioWebFestival");
            entity.Property(x => x.OtherUrl).HasColumnName("OtroEnlaceFestival");
            entity.Property(x => x.ContactPhone).HasColumnName("TelefonoFestival");
            entity.Property(x => x.CoverageLevel).HasColumnName("NivelCobertura");
            entity.Property(x => x.DepartmentCode).HasColumnName("CodigoDepartamento");
            entity.Property(x => x.MunicipalityCode).HasColumnName("CodigoMunicipio");
            entity.Property(x => x.HasCurrentYearEdition).HasColumnName("TieneVersionVigenteAnoActual");
            entity.Property(x => x.CurrentYearEditionStatus).HasColumnName("EstadoVersionAnoActual");
            entity.Property(x => x.CurrentYearStartDate).HasColumnName("FechaInicioVersionActual");
            entity.Property(x => x.CurrentYearEndDate).HasColumnName("FechaFinVersionActual");
            entity.Property(x => x.OrganizacionPrincipalId).HasColumnName("OrganizacionPrincipalId");
            entity.Property(x => x.Periodicidad).HasColumnName("Periodicidad");
            entity.Property(x => x.PeriodicidadDetalle).HasColumnName("PeriodicidadDetalle");
            entity.Property(x => x.ObservacionesContacto).HasColumnName("ObservacionesContacto");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");
            entity.Property(x => x.StatusCode).HasColumnName("EstadoRegistro");
        });

        modelBuilder.Entity<PracticaMusicalRow>(entity =>
        {
            entity.ToTable("PracticasMusicales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPracticaMusical");
            entity.Property(x => x.Nombre).HasColumnName("NombrePracticaMusical");
            entity.Property(x => x.Slug).HasColumnName("Slug");
            entity.Property(x => x.Descripcion).HasColumnName("Descripcion");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
        });

        modelBuilder.Entity<TerritorioSonoroRow>(entity =>
        {
            entity.ToTable("TerritoriosSonoros");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdTerritorioSonoro");
            entity.Property(x => x.Nombre).HasColumnName("NombreTerritorioSonoro");
            entity.Property(x => x.Slug).HasColumnName("Slug");
            entity.Property(x => x.Descripcion).HasColumnName("Descripcion");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
        });

        modelBuilder.Entity<FichaConceptualTerritorioSonoroRow>(entity =>
        {
            entity.ToTable("FichasConceptualesTerritoriosSonoros");
            entity.HasKey(x => x.TerritorioSonoroId);
            entity.Property(x => x.TerritorioSonoroId).HasColumnName("TerritorioSonoroId");
            entity.Property(x => x.DefinicionBreve).HasColumnName("DefinicionBreve");
            entity.Property(x => x.DefinicionAmpliada).HasColumnName("DefinicionAmpliada");
            entity.Property(x => x.DescripcionConceptual).HasColumnName("DescripcionConceptual");
            entity.Property(x => x.Caracteristicas).HasColumnName("Caracteristicas");
            entity.Property(x => x.RelacionTerritorial).HasColumnName("RelacionTerritorial");
            entity.Property(x => x.Contextos).HasColumnName("Contextos");
            entity.Property(x => x.Ejemplos).HasColumnName("Ejemplos");
            entity.Property(x => x.Fuentes).HasColumnName("Fuentes");
            entity.Property(x => x.RecursoVisualUrl).HasColumnName("RecursoVisualUrl");
            entity.Property(x => x.TextoAlternativoRecurso).HasColumnName("TextoAlternativoRecurso");
            entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
            entity.HasOne<TerritorioSonoroRow>().WithOne().HasForeignKey<FichaConceptualTerritorioSonoroRow>(x => x.TerritorioSonoroId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FichaConceptualPracticaMusicalRow>(entity =>
        {
            entity.ToTable("FichasConceptualesPracticasMusicales");
            entity.HasKey(x => x.PracticaMusicalId);
            entity.Property(x => x.PracticaMusicalId).HasColumnName("PracticaMusicalId");
            entity.Property(x => x.DefinicionBreve).HasColumnName("DefinicionBreve");
            entity.Property(x => x.DefinicionAmpliada).HasColumnName("DefinicionAmpliada");
            entity.Property(x => x.DescripcionConceptual).HasColumnName("DescripcionConceptual");
            entity.Property(x => x.Caracteristicas).HasColumnName("Caracteristicas");
            entity.Property(x => x.Contextos).HasColumnName("Contextos"); entity.Property(x => x.Ejemplos).HasColumnName("Ejemplos");
            entity.Property(x => x.Fuentes).HasColumnName("Fuentes"); entity.Property(x => x.RecursoVisualUrl).HasColumnName("RecursoVisualUrl");
            entity.Property(x => x.TextoAlternativoRecurso).HasColumnName("TextoAlternativoRecurso"); entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
            entity.HasOne<PracticaMusicalRow>().WithOne().HasForeignKey<FichaConceptualPracticaMusicalRow>(x => x.PracticaMusicalId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocalizacionDeRegistroRow>(entity =>
        {
            entity.ToTable("LocalizacionesDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdLocalizacionDeRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.CodigoDepartamento).HasColumnName("CodigoDepartamento").HasMaxLength(2);
            entity.Property(x => x.CodigoMunicipio).HasColumnName("CodigoMunicipio").HasMaxLength(5);
            entity.Property(x => x.ZonaUrbanoRuralId).HasColumnName("ZonaUrbanoRuralId");
            entity.Property(x => x.TitulacionColectivaId).HasColumnName("TitulacionColectivaId");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId, x.CodigoDepartamento, x.CodigoMunicipio }).IsUnique();
        });
        modelBuilder.Entity<EntidadAliadaDeRegistroRow>(entity =>
        {
            entity.ToTable("EntidadesAliadasDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEntidadAliadaDeRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.Nombre).HasColumnName("NombreEntidadAliada").HasMaxLength(300);
            entity.Property(x => x.Correo).HasColumnName("CorreoEntidadAliada").HasMaxLength(180);
            entity.Property(x => x.NaturalezaEntidadId).HasColumnName("NaturalezaEntidadId");
            entity.Property(x => x.EntidadId).HasColumnName("EntidadId");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
        });
        modelBuilder.Entity<ArchivoDeRegistroRow>(entity =>
        {
            entity.ToTable("ArchivosDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdArchivoDeRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ArchivoId).HasColumnName("ArchivoId");
            entity.Property(x => x.Url).HasColumnName("Url").HasMaxLength(1000);
            entity.Property(x => x.RolArchivo).HasColumnName("RolArchivo").HasMaxLength(80).IsRequired();
            entity.Property(x => x.DescripcionArchivo).HasColumnName("DescripcionArchivo").HasMaxLength(1000);
            entity.Property(x => x.OrdenVisualizacion).HasColumnName("OrdenVisualizacion");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId, x.RolArchivo, x.OrdenVisualizacion }).IsUnique();
        });
        modelBuilder.Entity<PracticaMusicalDeRegistroRow>(entity =>
        {
            entity.ToTable("PracticasMusicalesDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPracticaMusicalDeRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ValorId).HasColumnName("IdPracticaMusical");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            // LA CLAVE AJENA CONTRA EL CATALOGO SE CONSERVA: es la que impide guardar un valor que
            // no existe, y es la razón de que haya cinco tablas y no una con una columna «vocabulario».
            entity.HasOne<PracticaMusicalRow>().WithMany().HasForeignKey(x => x.ValorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId, x.ValorId }).IsUnique();
        });
        modelBuilder.Entity<TerritorioSonoroDeRegistroRow>(entity =>
        {
            entity.ToTable("TerritoriosSonorosDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdTerritorioSonoroDeRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ValorId).HasColumnName("IdTerritorioSonoro");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            // LA CLAVE AJENA CONTRA EL CATALOGO SE CONSERVA: es la que impide guardar un valor que
            // no existe, y es la razón de que haya cinco tablas y no una con una columna «vocabulario».
            entity.HasOne<TerritorioSonoroRow>().WithMany().HasForeignKey(x => x.ValorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId, x.ValorId }).IsUnique();
        });
        modelBuilder.Entity<ExpresionArtisticaDeRegistroRow>(entity =>
        {
            entity.ToTable("ExpresionesArtisticasDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdExpresionArtisticaDeRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ValorId).HasColumnName("IdExpresionArtistica");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            // LA CLAVE AJENA CONTRA EL CATALOGO SE CONSERVA: es la que impide guardar un valor que
            // no existe, y es la razón de que haya cinco tablas y no una con una columna «vocabulario».
            entity.HasOne<ExpresionArtisticaRow>().WithMany().HasForeignKey(x => x.ValorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId, x.ValorId }).IsUnique();
        });
        modelBuilder.Entity<ModalidadParticipacionDeRegistroRow>(entity =>
        {
            entity.ToTable("ModalidadesParticipacionDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdModalidadParticipacionDeRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ValorId).HasColumnName("IdModalidadParticipacion");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            // LA CLAVE AJENA CONTRA EL CATALOGO SE CONSERVA: es la que impide guardar un valor que
            // no existe, y es la razón de que haya cinco tablas y no una con una columna «vocabulario».
            entity.HasOne<ModalidadParticipacionRow>().WithMany().HasForeignKey(x => x.ValorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId, x.ValorId }).IsUnique();
        });
        modelBuilder.Entity<TipoIngresoDeRegistroRow>(entity =>
        {
            entity.ToTable("TiposIngresoDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdTipoIngresoDeRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.ValorId).HasColumnName("IdTipoIngreso");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            // LA CLAVE AJENA CONTRA EL CATALOGO SE CONSERVA: es la que impide guardar un valor que
            // no existe, y es la razón de que haya cinco tablas y no una con una columna «vocabulario».
            entity.HasOne<TipoIngresoRow>().WithMany().HasForeignKey(x => x.ValorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId, x.ValorId }).IsUnique();
        });
        modelBuilder.Entity<BoletinSuscripcionRow>(entity =>
        {
            entity.ToTable("BoletinSuscripciones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdSuscripcion");
            entity.Property(x => x.CorreoElectronico).HasColumnName("CorreoElectronico").HasMaxLength(180);
            entity.Property(x => x.Origen).HasColumnName("Origen").HasMaxLength(60);
            entity.Property(x => x.Estado).HasColumnName("Estado").HasMaxLength(30);
            entity.Property(x => x.FechaAlta).HasColumnName("FechaAlta");
            entity.Property(x => x.FechaBaja).HasColumnName("FechaBaja");
            // La misma unicidad que impone la base. Declararla aqui NO la sustituye —SQLite la
            // crea desde el modelo, SQL Server la trae del guion—, pero hace que las dos digan
            // lo mismo, que es lo que permite que la suite corra en los dos carriles.
            entity.HasIndex(x => x.CorreoElectronico).IsUnique();
        });

        modelBuilder.Entity<EdicionFestivalRow>(entity =>
        {
            entity.ToTable("EdicionesFestival");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEdicionFestival");
            entity.Property(x => x.FestivalId).HasColumnName("FestivalId");
            entity.Property(x => x.Anio).HasColumnName("Anio");
            entity.Property(x => x.NumeroEdicion).HasColumnName("NumeroEdicion");
            entity.Property(x => x.Nombre).HasColumnName("Nombre").HasMaxLength(240);
            entity.Property(x => x.Descripcion).HasColumnName("Descripcion");
            entity.Property(x => x.FechaInicio).HasColumnName("FechaInicio");
            entity.Property(x => x.FechaFin).HasColumnName("FechaFin");
            entity.Property(x => x.Director).HasColumnName("Director").HasMaxLength(240);
            entity.Property(x => x.TipologiaFestivalId).HasColumnName("TipologiaFestivalId");
            entity.Property(x => x.OtraTipologia).HasColumnName("OtraTipologia").HasMaxLength(120);
            entity.Property(x => x.FuenteFinanciacionPrimariaId).HasColumnName("FuenteFinanciacionPrimariaId");
            entity.Property(x => x.OtraFuenteFinanciacionPrimaria).HasColumnName("OtraFuenteFinanciacionPrimaria").HasMaxLength(120);
            entity.Property(x => x.FuenteFinanciacionSecundariaId).HasColumnName("FuenteFinanciacionSecundariaId");
            entity.Property(x => x.OtraFuenteFinanciacionSecundaria).HasColumnName("OtraFuenteFinanciacionSecundaria").HasMaxLength(120);
            entity.Property(x => x.UsaEstampillaProcultura).HasColumnName("UsaEstampillaProcultura");
            entity.Property(x => x.PracticasMusicalesQueCongrega).HasColumnName("PracticasMusicalesQueCongrega").HasMaxLength(500);
            entity.Property(x => x.OtraModalidadParticipacion).HasColumnName("OtraModalidadParticipacion").HasMaxLength(120);
            entity.Property(x => x.OtraExpresionArtistica).HasColumnName("OtraExpresionArtistica").HasMaxLength(120);
            entity.Property(x => x.EstadoRegistro).HasColumnName("EstadoRegistro").HasMaxLength(80);
            entity.Property(x => x.EstadoVisibilidad).HasColumnName("EstadoVisibilidad").HasMaxLength(40);
            entity.Property(x => x.Estado).HasColumnName("Estado").HasMaxLength(40);
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
            entity.HasIndex(x => new { x.FestivalId, x.Anio, x.NumeroEdicion });
        });

        // ── La revisión por campos de CUALQUIER proceso ─────────────────────────────────────
        //
        // ES LA MISMA IDEA QUE LAS DOS DE ARRIBA, SIN NOMBRAR UN PROCESO. Identifica el registro por
        // módulo + identificador, el mismo par con el que lo identifican la auditoría y la
        // procedencia. Nace para Mercados y sirve para los procesos que vengan.
        modelBuilder.Entity<RevisionDeRegistroRow>(entity =>
        {
            entity.ToTable("RevisionesDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdRevision");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(80).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(120).IsRequired();
            entity.Property(x => x.Estado).HasColumnName("Estado").HasMaxLength(40);
            entity.Property(x => x.IdUsuarioRevisor).HasColumnName("IdUsuarioRevisor");
            entity.Property(x => x.RevisorNombre).HasColumnName("RevisorNombre").HasMaxLength(480);
            entity.Property(x => x.IdUsuarioDestinatario).HasColumnName("IdUsuarioDestinatario");
            entity.Property(x => x.DestinatarioNombre).HasColumnName("DestinatarioNombre").HasMaxLength(480);
            entity.Property(x => x.IdOrganizacion).HasColumnName("IdOrganizacion");
            entity.Property(x => x.OrganizacionNombre).HasColumnName("OrganizacionNombre").HasMaxLength(480);
            entity.Property(x => x.ObservacionGeneral).HasColumnName("ObservacionGeneral").HasMaxLength(2400);
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
            entity.Property(x => x.FechaEnvio).HasColumnName("FechaEnvio");
            entity.Property(x => x.FechaCierre).HasColumnName("FechaCierre");
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId });
        });

        modelBuilder.Entity<RevisionDeRegistroObservacionRow>(entity =>
        {
            entity.ToTable("RevisionesDeRegistroObservaciones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdObservacion");
            entity.Property(x => x.IdRevision).HasColumnName("IdRevision");
            entity.Property(x => x.Ambito).HasColumnName("Ambito").HasMaxLength(40);
            entity.Property(x => x.SubregistroId).HasColumnName("SubregistroId").HasMaxLength(120);
            entity.Property(x => x.SeccionId).HasColumnName("SeccionId").HasMaxLength(80);
            entity.Property(x => x.CampoId).HasColumnName("CampoId").HasMaxLength(120);
            entity.Property(x => x.CampoEtiqueta).HasColumnName("CampoEtiqueta").HasMaxLength(240);
            entity.Property(x => x.ValorObservado).HasColumnName("ValorObservado");
            entity.Property(x => x.Nota).HasColumnName("Nota").HasMaxLength(2400);
            entity.Property(x => x.Estado).HasColumnName("Estado").HasMaxLength(40);
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
            entity.Property(x => x.FechaAtencion).HasColumnName("FechaAtencion");
            entity.Property(x => x.IdUsuarioAtiende).HasColumnName("IdUsuarioAtiende");
            entity.HasIndex(x => new { x.IdRevision, x.SeccionId, x.Id });
            // SIN CASCADA, y explícito a propósito: lo que EF pone por omisión en una foránea
            // obligatoria es Cascade, así que callarse aquí dejaría SQLite con una regla que SQL
            // Server no tiene, y la suite verde sobre una base distinta.
            entity.HasOne(x => x.Revision).WithMany()
                .HasForeignKey(x => x.IdRevision)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PropuestaDeCambioRow>(entity =>
        {
            entity.ToTable("PropuestasDeCambio");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPropuesta");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(80).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(120).IsRequired();
            entity.Property(x => x.SubregistroId).HasColumnName("SubregistroId").HasMaxLength(120);
            entity.Property(x => x.SubregistroResultanteId).HasColumnName("SubregistroResultanteId").HasMaxLength(120);
            entity.Property(x => x.Estado).HasColumnName("Estado").HasMaxLength(40);
            entity.Property(x => x.IdOrganizacion).HasColumnName("IdOrganizacion");
            entity.Property(x => x.OrganizacionNombre).HasColumnName("OrganizacionNombre").HasMaxLength(480);
            entity.Property(x => x.IdUsuarioProponente).HasColumnName("IdUsuarioProponente");
            entity.Property(x => x.ProponenteNombre).HasColumnName("ProponenteNombre").HasMaxLength(480);
            entity.Property(x => x.Motivo).HasColumnName("Motivo").HasMaxLength(2400);
            entity.Property(x => x.IdUsuarioDecide).HasColumnName("IdUsuarioDecide");
            entity.Property(x => x.DecideNombre).HasColumnName("DecideNombre").HasMaxLength(480);
            entity.Property(x => x.MotivoDeLaDecision).HasColumnName("MotivoDeLaDecision").HasMaxLength(2400);
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
            entity.Property(x => x.FechaEnvio).HasColumnName("FechaEnvio");
            entity.Property(x => x.FechaDecision).HasColumnName("FechaDecision");
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId });
            entity.HasIndex(x => x.Estado);
        });

        modelBuilder.Entity<PropuestaDeCambioCampoRow>(entity =>
        {
            entity.ToTable("PropuestasDeCambioCampos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdCampoPropuesto");
            entity.Property(x => x.IdPropuesta).HasColumnName("IdPropuesta");
            entity.Property(x => x.SeccionId).HasColumnName("SeccionId").HasMaxLength(80);
            entity.Property(x => x.CampoId).HasColumnName("CampoId").HasMaxLength(120);
            entity.Property(x => x.CampoEtiqueta).HasColumnName("CampoEtiqueta").HasMaxLength(240);
            entity.Property(x => x.ValorAnterior).HasColumnName("ValorAnterior");
            entity.Property(x => x.ValorPropuesto).HasColumnName("ValorPropuesto");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");
            entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
            entity.HasIndex(x => new { x.IdPropuesta, x.CampoId }).IsUnique();
            // SIN CASCADA, y explícito a propósito: lo que EF pone por omisión en una foránea
            // obligatoria es Cascade, así que callarse aquí dejaría SQLite con una regla que SQL
            // Server no tiene, y la suite verde sobre una base distinta.
            entity.HasOne(x => x.Propuesta).WithMany()
                .HasForeignKey(x => x.IdPropuesta)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VersionFestivalRow>(entity =>
        {
            entity.ToTable("VersionesFestival");
            entity.Property(x => x.TelefonoContacto).HasColumnName("TelefonoContacto");
            entity.Property(x => x.Instagram).HasColumnName("Instagram");
            entity.Property(x => x.Facebook).HasColumnName("Facebook");
            entity.Property(x => x.SitioWeb).HasColumnName("SitioWeb");
            entity.Property(x => x.OtroEnlace).HasColumnName("OtroEnlace");
            entity.Property(x => x.Director).HasColumnName("Director");
            entity.Property(x => x.TipoOrganizadorId).HasColumnName("TipoOrganizadorId");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdVersionFestival");
            entity.Property(x => x.FestivalOrigenId).HasColumnName("FestivalOrigenId");
            entity.Property(x => x.NumeroVersion).HasColumnName("NumeroVersion");
            entity.Property(x => x.EsVigente).HasColumnName("EsVigente");
            entity.Property(x => x.Nombre).HasColumnName("Nombre");
            entity.Property(x => x.Descripcion).HasColumnName("Descripcion");
            entity.Property(x => x.NivelCobertura).HasColumnName("NivelCobertura");
            entity.Property(x => x.CodigoDepartamento).HasColumnName("CodigoDepartamento");
            entity.Property(x => x.CodigoMunicipio).HasColumnName("CodigoMunicipio");
            entity.Property(x => x.Periodicidad).HasColumnName("Periodicidad");
            entity.Property(x => x.PeriodicidadDetalle).HasColumnName("PeriodicidadDetalle");
            entity.Property(x => x.CorreoContacto).HasColumnName("CorreoContacto");
            entity.Property(x => x.FechaPublicacion).HasColumnName("FechaPublicacion");
            entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion");

            // LAS DIECISIETE QUE FALTABAN. Seis llevaban sin mapear desde el 24 de agosto -las dos
            // fechas, la tipologia, las dos fuentes y la estampilla-; once las anadio
            // `schema/V20260828_02` el 28. `ParidadEsquemaSinArranqueTests` no podia verlo: mide
            // que exista en la base cada columna que EF mapea, no al reves.
            entity.Property(x => x.FechaInicio).HasColumnName("FechaInicio");
            entity.Property(x => x.FechaFin).HasColumnName("FechaFin");
            entity.Property(x => x.TipologiaFestivalId).HasColumnName("TipologiaFestivalId");
            entity.Property(x => x.FuenteFinanciacionPrimariaId).HasColumnName("FuenteFinanciacionPrimariaId");
            entity.Property(x => x.FuenteFinanciacionSecundariaId).HasColumnName("FuenteFinanciacionSecundariaId");
            entity.Property(x => x.UsaEstampillaProcultura).HasColumnName("UsaEstampillaProcultura");
            entity.Property(x => x.PracticasMusicalesQueCongrega).HasColumnName("PracticasMusicalesQueCongrega");
            entity.Property(x => x.OtraTipologia).HasColumnName("OtraTipologia");
            entity.Property(x => x.OtraModalidadParticipacion).HasColumnName("OtraModalidadParticipacion");
            entity.Property(x => x.OtraExpresionArtistica).HasColumnName("OtraExpresionArtistica");
            entity.Property(x => x.OtraFuenteFinanciacionPrimaria).HasColumnName("OtraFuenteFinanciacionPrimaria");
            entity.Property(x => x.OtraFuenteFinanciacionSecundaria).HasColumnName("OtraFuenteFinanciacionSecundaria");
            entity.Property(x => x.OtroTipoOrganizador).HasColumnName("OtroTipoOrganizador");
            entity.Property(x => x.PerteneceAOrganizacionColectiva).HasColumnName("PerteneceAOrganizacionColectiva");
            entity.Property(x => x.NombreOrganizacionColectiva).HasColumnName("NombreOrganizacionColectiva");
            entity.Property(x => x.ObservacionesContacto).HasColumnName("ObservacionesContacto");
            entity.Property(x => x.ObservacionesRechazo).HasColumnName("ObservacionesRechazo");
            entity.Property(x => x.EstadoRegistro).HasColumnName("EstadoRegistro");

            entity.HasOne<FestivalRow>().WithMany().HasForeignKey(x => x.FestivalOrigenId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.FestivalOrigenId, x.NumeroVersion }).IsUnique();
            entity.HasIndex(x => new { x.FestivalOrigenId, x.EsVigente }).IsUnique().HasFilter("[EsVigente] = 1");
        });

        // =================================================================================
        // Los once catalogos del Festival y las cinco tablas puente que vinieron de SIMUS.
        // =================================================================================
        // FORMA CALCADA DE `PracticaMusicalRow` Y `TerritorioSonoroRow`, que estan mapeados mas
        // arriba: `Id` + `Nombre` y nada mas. No se mapean `Slug`, `Descripcion` ni
        // `OrdenVisualizacion` porque para llenar un desplegable no hacen falta; el dia que el
        // orden importe, es una linea por catalogo.
        modelBuilder.Entity<TipologiaFestivalRow>(entity =>
        {
            entity.ToTable("TipologiasFestival");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdTipologiaFestival");
            entity.Property(x => x.Nombre).HasColumnName("NombreTipologiaFestival");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
            entity.Property(x => x.Slug).HasColumnName("Slug");
        });
        modelBuilder.Entity<ExpresionArtisticaRow>(entity =>
        {
            entity.ToTable("ExpresionesArtisticas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdExpresionArtistica");
            entity.Property(x => x.Nombre).HasColumnName("NombreExpresionArtistica");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
            entity.Property(x => x.Slug).HasColumnName("Slug");
        });
        modelBuilder.Entity<FuenteFinanciacionRow>(entity =>
        {
            entity.ToTable("FuentesFinanciacion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdFuenteFinanciacion");
            entity.Property(x => x.Nombre).HasColumnName("NombreFuenteFinanciacion");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
            entity.Property(x => x.Slug).HasColumnName("Slug");
        });
        modelBuilder.Entity<ModalidadParticipacionRow>(entity =>
        {
            entity.ToTable("ModalidadesParticipacion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdModalidadParticipacion");
            entity.Property(x => x.Nombre).HasColumnName("NombreModalidadParticipacion");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
            entity.Property(x => x.Slug).HasColumnName("Slug");
        });
        modelBuilder.Entity<NaturalezaEntidadRow>(entity =>
        {
            entity.ToTable("NaturalezasEntidad");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdNaturalezaEntidad");
            entity.Property(x => x.Nombre).HasColumnName("NombreNaturalezaEntidad");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
        });
        modelBuilder.Entity<TipoIngresoRow>(entity =>
        {
            entity.ToTable("TiposIngreso");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdTipoIngreso");
            entity.Property(x => x.Nombre).HasColumnName("NombreTipoIngreso");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
        });
        modelBuilder.Entity<TipoOrganizadorRow>(entity =>
        {
            entity.ToTable("TiposOrganizador");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdTipoOrganizador");
            entity.Property(x => x.Nombre).HasColumnName("NombreTipoOrganizador");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
        });
        modelBuilder.Entity<ZonaUrbanoRuralRow>(entity =>
        {
            entity.ToTable("ZonasUrbanoRural");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdZonaUrbanoRural");
            entity.Property(x => x.Nombre).HasColumnName("NombreZonaUrbanoRural");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
            entity.Property(x => x.Slug).HasColumnName("Slug");
        });
        modelBuilder.Entity<TitulacionColectivaRow>(entity =>
        {
            entity.ToTable("TitulacionesColectivas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdTitulacionColectiva");
            entity.Property(x => x.Nombre).HasColumnName("NombreTitulacionColectiva");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
        });
        modelBuilder.Entity<RegionOcadRow>(entity =>
        {
            entity.ToTable("RegionesOcad");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdRegionOcad");
            entity.Property(x => x.Nombre).HasColumnName("NombreRegionOcad");
            entity.Property(x => x.Orden).HasColumnName("OrdenVisualizacion");
        });
        // LA CLAVE ES EL CODIGO DE DEPARTAMENTO, no un identidad propio: un departamento
        // pertenece a UNA region OCAD. En el origen la tabla lleva un `ID IDENTITY` y nada impide
        // meter el mismo departamento en dos regiones.
        modelBuilder.Entity<DepartamentoRegionOcadRow>(entity =>
        {
            entity.ToTable("DepartamentosRegionOcad");
            entity.HasKey(x => x.CodigoDepartamento);
            entity.Property(x => x.CodigoDepartamento).HasColumnName("CodigoDepartamento");
            entity.Property(x => x.RegionOcadId).HasColumnName("RegionOcadId");
            entity.HasOne<RegionOcadRow>().WithMany().HasForeignKey(x => x.RegionOcadId).OnDelete(DeleteBehavior.Restrict);
        });

        // AQUI VIVIAN CINCO `Ignore<>` Y, MAS ARRIBA, SUS CINCO MAPEOS COMPLETOS.
        //
        // Escuelas de musica, mercados musicales, redes de documentacion, luteria y escenarios
        // se declaraban con su tabla, sus columnas y sus indices, y acto seguido se excluian del
        // modelo. El comentario que acompanaba a los `Ignore` anunciaba el retiro de las clases
        // «en el siguiente corte»: este es. Las clases ya no estan en `Rows.cs`, asi que excluir
        // lo que no existe seria un error de compilacion.
        //
        // LO QUE SE QUEDA A PROPOSITO son `IRegistroDeEcosistema`, `ITieneOrganizacionResponsable`
        // y `AsignarOrganizacionResponsable`. No son restos: son el contrato contra el que estos
        // cinco procesos volveran a implementarse con modelo y circuito propios. Ese guardian
        // resuelve, en un solo sitio, que ningun registro del ecosistema nazca sin quien responda
        // por el; volver a escribirlo despues seria rehacer algo ya resuelto.

        modelBuilder.Entity<FileRow>(entity =>
        {
            entity.ToTable("Archivos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdArchivo");
            entity.Property(x => x.OriginalName).HasColumnName("NombreOriginal");
            entity.Property(x => x.StoredName).HasColumnName("NombreAlmacenado");
            entity.Property(x => x.MimeType).HasColumnName("TipoMime");
            entity.Property(x => x.FileSizeBytes).HasColumnName("PesoBytes");
            entity.Property(x => x.StoragePath).HasColumnName("RutaAlmacenamiento");
            entity.Property(x => x.PublicUrl).HasColumnName("UrlPublica");
            entity.Property(x => x.AltText).HasColumnName("TextoAlternativo");
            entity.Property(x => x.Caption).HasColumnName("Pie");
            entity.Property(x => x.Credit).HasColumnName("Credito");
            entity.Property(x => x.Content).HasColumnName("Contenido");
            entity.Property(x => x.Fingerprint).HasColumnName("Huella").HasMaxLength(64).IsFixedLength();
            entity.Property(x => x.Width).HasColumnName("Ancho");
            entity.Property(x => x.Height).HasColumnName("Alto");
            entity.HasIndex(x => x.Fingerprint);
            entity.Property(x => x.UploadedByUserId).HasColumnName("IdUsuarioCarga");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCarga");
            entity.Property(x => x.OrganizacionId).HasColumnName("OrganizacionId");
        });

        modelBuilder.Entity<UserRow>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdUsuario");
            entity.Property(x => x.FullName).HasColumnName("NombreCompleto");
            entity.Property(x => x.Email).HasColumnName("CorreoElectronico");
            entity.Property(x => x.PasswordHash).HasColumnName("HashContrasena");
            entity.Property(x => x.AccessChannel).HasColumnName("CanalAcceso");
            entity.Property(x => x.ProfileType).HasColumnName("TipoPerfil");
            entity.Property(x => x.Telefono).HasColumnName("Telefono");
            entity.Property(x => x.Identificacion).HasColumnName("Identificacion").HasMaxLength(40);
            entity.Property(x => x.CodigoTipoDocumento).HasColumnName("CodigoTipoDocumento").HasMaxLength(20);
            entity.Property(x => x.PrimerNombre).HasColumnName("PrimerNombre").HasMaxLength(80);
            entity.Property(x => x.SegundoNombre).HasColumnName("SegundoNombre").HasMaxLength(80);
            entity.Property(x => x.PrimerApellido).HasColumnName("PrimerApellido").HasMaxLength(80);
            entity.Property(x => x.SegundoApellido).HasColumnName("SegundoApellido").HasMaxLength(80);
            entity.Property(x => x.DebeCambiarContrasena).HasColumnName("DebeCambiarContrasena");
            entity.Property(x => x.PerfilCompletado).HasColumnName("PerfilCompletado");
            entity.Property(x => x.IsActive).HasColumnName("Activo");
            entity.Property(x => x.LastLoginAt).HasColumnName("UltimoAcceso");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");

            // AQUI HUBO UNA FORANEA HACIA dbo.Roles, Y SE FUE CON LA COLUMNA.
            //
            // Se declaro el 25 ago 2026 porque la base la tenia y el modelo no, y esa diferencia
            // dejaba ciego al carril rapido: `EnsureCreated` fabricaba la tabla de SQLite sin la
            // restriccion, una fila con IdRol = 0 entraba sin protestar, y crear un usuario
            // respondia 500 contra SQL Server con la suite en 433 de 433.
            //
            // La leccion NO se fue con la columna, y por eso queda escrita: las restricciones que
            // el modelo no declara no existen en la base que fabrica el arnes. Las dos de
            // dbo.UsuariosRoles, mas abajo, estan declaradas por esta misma razon.
        });

        modelBuilder.Entity<RoleRow>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdRol");
            entity.Property(x => x.Name).HasColumnName("NombreRol");
            entity.Property(x => x.Description).HasColumnName("DescripcionRol");
        });

        modelBuilder.Entity<UsuarioRolRow>(entity =>
        {
            entity.ToTable("UsuariosRoles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdUsuarioRol");
            entity.Property(x => x.UserId).HasColumnName("IdUsuario");
            entity.Property(x => x.RoleId).HasColumnName("IdRol");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");

            // EL MISMO UNICO QUE LA BASE (UQ_UsuariosRoles). Sin el, la via de SQLite del arnes
            // admitiria la fila repetida que SQL Server rechaza, y una prueba de idempotencia
            // pasaria en un carril y fallaria en el otro.
            entity.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();

            // Y LAS DOS FORANEAS, por la misma razon que la de arriba: que el carril rapido
            // rechace lo que la base rechaza. Sin cascada, igual que en el guion de esquema:
            // borrar un rol que alguien tiene debe FALLAR, no vaciar cuentas en silencio.
            entity.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RoleRow>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PermisoRow>(entity =>
        {
            entity.ToTable("Permisos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPermiso");
            entity.Property(x => x.Codigo).HasColumnName("CodigoPermiso");
            entity.Property(x => x.Nombre).HasColumnName("NombrePermiso");
            entity.Property(x => x.Descripcion).HasColumnName("DescripcionPermiso");
            entity.Property(x => x.Modulo).HasColumnName("Modulo");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<RolPermisoRow>(entity =>
        {
            entity.ToTable("RolesPermisos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdRolPermiso");
            entity.Property(x => x.RoleId).HasColumnName("IdRol");
            entity.Property(x => x.PermisoId).HasColumnName("IdPermiso");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.HasIndex(x => new { x.RoleId, x.PermisoId }).IsUnique();
        });

        modelBuilder.Entity<AuditLogRow>(entity =>
        {
            entity.ToTable("BitacoraAuditoria");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdAuditoria");
            entity.Property(x => x.UserId).HasColumnName("IdUsuario");
            entity.Property(x => x.TableName).HasColumnName("TablaAfectada");
            entity.Property(x => x.RecordId).HasColumnName("IdRegistroAfectado");
            entity.Property(x => x.Action).HasColumnName("Accion");
            entity.Property(x => x.PreviousValuesJson).HasColumnName("ValoresAnteriores");
            entity.Property(x => x.NewValuesJson).HasColumnName("ValoresNuevos");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaAccion");
        });

        modelBuilder.Entity<LoteImportacionRow>(entity =>
        {
            entity.ToTable("LotesImportacion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdLoteImportacion");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40);
            entity.Property(x => x.NombreArchivo).HasColumnName("NombreArchivo").HasMaxLength(255);
            entity.Property(x => x.Formato).HasColumnName("Formato").HasMaxLength(10);
            entity.Property(x => x.HuellaArchivo).HasColumnName("HuellaArchivo").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            entity.Property(x => x.HuellaPlan).HasColumnName("HuellaPlan").HasMaxLength(64).IsFixedLength().IsUnicode(false);
            entity.Property(x => x.VersionContrato).HasColumnName("VersionContrato");
            entity.Property(x => x.Estado).HasColumnName("Estado").HasMaxLength(30);
            entity.Property(x => x.UsuarioId).HasColumnName("IdUsuario");
            entity.Property(x => x.ClaveIdempotencia).HasColumnName("ClaveIdempotencia").HasMaxLength(100);
            entity.Property(x => x.FechaPrevisualizacion).HasColumnName("FechaPrevisualizacion");
            entity.Property(x => x.FechaAplicacion).HasColumnName("FechaAplicacion");
            entity.Property(x => x.FechaDepuracion).HasColumnName("FechaDepuracion");
            entity.Property(x => x.TotalFilas).HasColumnName("TotalFilas");
            entity.Property(x => x.FilasImportables).HasColumnName("FilasImportables");
            entity.Property(x => x.FilasRechazadas).HasColumnName("FilasRechazadas");
            entity.Property(x => x.FilasAplicadas).HasColumnName("FilasAplicadas");
            entity.Property(x => x.FilasExcluidas).HasColumnName("FilasExcluidas");
            entity.HasIndex(x => x.ClaveIdempotencia).IsUnique().HasFilter("[ClaveIdempotencia] IS NOT NULL");
            entity.HasIndex(x => new { x.ModuloId, x.FechaPrevisualizacion });
            entity.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FilaImportacionRow>(entity =>
        {
            entity.ToTable("FilasImportacion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdFilaImportacion");
            entity.Property(x => x.LoteImportacionId).HasColumnName("IdLoteImportacion");
            entity.Property(x => x.NumeroFila).HasColumnName("NumeroFila");
            entity.Property(x => x.ContenidoNormalizadoJson).HasColumnName("ContenidoNormalizadoJson");
            entity.Property(x => x.Resultado).HasColumnName("Resultado").HasMaxLength(40);
            entity.Property(x => x.PuedeImportarse).HasColumnName("PuedeImportarse");
            entity.Property(x => x.RegistroCoincidenteId).HasColumnName("IdRegistroCoincidente");
            entity.Property(x => x.RegistroCreadoId).HasColumnName("IdRegistroCreado");
            entity.HasIndex(x => new { x.LoteImportacionId, x.NumeroFila }).IsUnique();
            entity.HasOne<LoteImportacionRow>().WithMany().HasForeignKey(x => x.LoteImportacionId).OnDelete(DeleteBehavior.Restrict);
            // SIN FORANEA A FESTIVALES: a qué tabla apunta el registro coincidente lo dice el
            // `Dominio` del lote, y una foránea no puede apuntar a dos tablas.
        });

        modelBuilder.Entity<HallazgoImportacionRow>(entity =>
        {
            entity.ToTable("HallazgosImportacion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdHallazgoImportacion");
            entity.Property(x => x.FilaImportacionId).HasColumnName("IdFilaImportacion");
            entity.Property(x => x.Severidad).HasColumnName("Severidad").HasMaxLength(20);
            entity.Property(x => x.Codigo).HasColumnName("Codigo").HasMaxLength(60);
            entity.Property(x => x.Campo).HasColumnName("Campo").HasMaxLength(80);
            entity.Property(x => x.Mensaje).HasColumnName("Mensaje").HasMaxLength(600);
            entity.HasOne<FilaImportacionRow>().WithMany().HasForeignKey(x => x.FilaImportacionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DecisionImportacionRow>(entity =>
        {
            entity.ToTable("DecisionesImportacion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdDecisionImportacion");
            entity.Property(x => x.LoteImportacionId).HasColumnName("IdLoteImportacion");
            entity.Property(x => x.FilaImportacionId).HasColumnName("IdFilaImportacion");
            entity.Property(x => x.UsuarioId).HasColumnName("IdUsuario");
            entity.Property(x => x.Decision).HasColumnName("Decision").HasMaxLength(30);
            entity.Property(x => x.Fecha).HasColumnName("Fecha");
            entity.HasIndex(x => x.FilaImportacionId).IsUnique();
            entity.HasOne<LoteImportacionRow>().WithMany().HasForeignKey(x => x.LoteImportacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FilaImportacionRow>().WithMany().HasForeignKey(x => x.FilaImportacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HistorialRevisionRegistroRow>(entity =>
        {
            entity.ToTable("RegistrosRevisionHistorial");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdRevisionHistorial");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId");
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId");
            entity.Property(x => x.EstadoAnterior).HasColumnName("EstadoAnterior");
            entity.Property(x => x.EstadoNuevo).HasColumnName("EstadoNuevo");
            entity.Property(x => x.Accion).HasColumnName("Accion");
            entity.Property(x => x.Comentario).HasColumnName("Comentario");
            entity.Property(x => x.MotivoRechazo).HasColumnName("MotivoRechazo");
            entity.Property(x => x.CamposObservados).HasColumnName("CamposObservados");
            entity.Property(x => x.UsuarioId).HasColumnName("IdUsuario");
            entity.Property(x => x.Fecha).HasColumnName("Fecha");
            entity.Property(x => x.MetadataJson).HasColumnName("MetadataJson");
            entity.Property(x => x.OrganizacionId).HasColumnName("OrganizacionId");
            entity.Property(x => x.OrganizacionNombre).HasColumnName("OrganizacionNombre");
            entity.Property(x => x.ResponsableNombre).HasColumnName("ResponsableNombre");
            entity.Property(x => x.ActorNombre).HasColumnName("ActorNombre");
        });

        modelBuilder.Entity<EnvioDeRevisionRow>(entity =>
        {
            entity.ToTable("EnviosDeRevision");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEnvioDeRevision");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId").HasMaxLength(40).IsRequired();
            entity.Property(x => x.RegistroId).HasColumnName("RegistroId").HasMaxLength(64).IsRequired();
            entity.Property(x => x.NumeroEnvio).HasColumnName("NumeroEnvio");
            entity.Property(x => x.UsuarioRemitenteId).HasColumnName("IdUsuarioRemitente");
            entity.Property(x => x.OrganizacionId).HasColumnName("IdOrganizacion");
            entity.Property(x => x.RevisionOrigenId).HasColumnName("IdRevisionOrigen");
            entity.Property(x => x.EstadoAnterior).HasColumnName("EstadoAnterior").HasMaxLength(40);
            entity.Property(x => x.DatosJson).HasColumnName("DatosJson");
            entity.Property(x => x.FechaEnvio).HasColumnName("FechaEnvio");
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId, x.NumeroEnvio }).IsUnique();
            entity.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UsuarioRemitenteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EntityProfileRow>().WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RevisionDeRegistroRow>().WithMany().HasForeignKey(x => x.RevisionOrigenId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserVerificationCodeRow>(entity =>
        {
            entity.ToTable("UsuariosCodigosVerificacion");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdUsuarioCodigoVerificacion");
            entity.Property(x => x.UserId).HasColumnName("IdUsuario");
            entity.Property(x => x.Purpose).HasColumnName("Proposito");
            entity.Property(x => x.Code).HasColumnName("Codigo");
            entity.Property(x => x.ExpiresAt).HasColumnName("FechaExpiracion");
            entity.Property(x => x.ConsumedAt).HasColumnName("FechaConsumo");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
        });

        modelBuilder.Entity<NotificationRow>(entity =>
        {
            entity.ToTable("Notificaciones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdNotificacion");
            entity.Property(x => x.RecipientUserId).HasColumnName("UsuarioDestinatarioId");
            entity.Property(x => x.RecipientEmail).HasColumnName("CorreoDestinatario");
            entity.Property(x => x.EventType).HasColumnName("TipoEvento");
            entity.Property(x => x.AccessScope).HasColumnName("AmbitoAcceso");
            entity.Property(x => x.Channel).HasColumnName("Canal");
            entity.Property(x => x.Title).HasColumnName("Titulo");
            entity.Property(x => x.Body).HasColumnName("Cuerpo");
            entity.Property(x => x.Status).HasColumnName("Estado");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId");
            entity.Property(x => x.RecordId).HasColumnName("RegistroId");
            entity.Property(x => x.MetadataJson).HasColumnName("MetadataJson");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.Property(x => x.SentAt).HasColumnName("FechaEnvio");
            entity.Property(x => x.ReadAt).HasColumnName("FechaLectura");
            entity.Property(x => x.DismissedAt).HasColumnName("FechaOcultacion");
            entity.Property(x => x.Attempts).HasColumnName("Intentos");
            entity.Property(x => x.Error).HasColumnName("Error");
        });

        modelBuilder.Entity<RecordLinkRequestRow>(entity =>
        {
            entity.ToTable("SolicitudesVinculacionRegistros");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdSolicitudVinculacionRegistro");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId");
            entity.Property(x => x.RecordId).HasColumnName("RegistroId");
            entity.Property(x => x.RequestingUserId).HasColumnName("UsuarioSolicitanteId");
            entity.Property(x => x.EntidadId).HasColumnName("EntidadId");
            entity.Property(x => x.RequestedScope).HasColumnName("AlcanceSolicitado");
            entity.Property(x => x.Reason).HasColumnName("Justificacion");
            entity.Property(x => x.EvidenceText).HasColumnName("EvidenciaTexto");
            entity.Property(x => x.Status).HasColumnName("Estado");
            entity.Property(x => x.ReviewerUserId).HasColumnName("UsuarioRevisorId");
            entity.Property(x => x.ReviewComment).HasColumnName("ComentarioRevision");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");
        });

        modelBuilder.Entity<RecordDuplicateCandidateRow>(entity =>
        {
            entity.ToTable("RegistrosDuplicadosCandidatos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdDuplicadoCandidato");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId");
            entity.Property(x => x.SourceRecordId).HasColumnName("RegistroOrigenId");
            entity.Property(x => x.CandidateRecordId).HasColumnName("RegistroCandidatoId");
            entity.Property(x => x.SimilarityLevel).HasColumnName("NivelCoincidencia");
            entity.Property(x => x.SimilarityScore).HasColumnName("PuntajeCoincidencia").HasPrecision(5, 2);
            entity.Property(x => x.EvidenceJson).HasColumnName("EvidenciaJson");
            entity.Property(x => x.Status).HasColumnName("Estado");
            entity.Property(x => x.Decision).HasColumnName("Decision");
            entity.Property(x => x.DecisionComment).HasColumnName("ComentarioDecision");
            entity.Property(x => x.ReviewerUserId).HasColumnName("UsuarioRevisorId");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");
        });

        modelBuilder.Entity<BorradorDeProcesoRow>(entity =>
        {
            entity.ToTable("BorradoresProceso"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdBorradorProceso"); entity.Property(x => x.ModuloId).HasColumnName("ModuloId");
            entity.Property(x => x.OrganizacionId).HasColumnName("OrganizacionId"); entity.Property(x => x.PersonaId).HasColumnName("PersonaId");
            entity.Property(x => x.Estado).HasColumnName("Estado"); entity.Property(x => x.DatosJson).HasColumnName("DatosJson");
            entity.Property(x => x.Version).HasColumnName("Version"); entity.Property(x => x.FechaCreacion).HasColumnName("FechaCreacion"); entity.Property(x => x.FechaActualizacion).HasColumnName("FechaActualizacion");
        });
        modelBuilder.Entity<AdministrationClaimRow>(entity =>
        {
            entity.ToTable("ReclamacionesAdministracion"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdReclamacion"); entity.Property(x => x.ModuloId).HasColumnName("ModuloId"); entity.Property(x => x.CanonicalRecordId).HasColumnName("RegistroCanonicoId");
            entity.Property(x => x.RequestingOrganizationId).HasColumnName("OrganizacionSolicitanteId"); entity.Property(x => x.RequestingPersonId).HasColumnName("PersonaSolicitanteId"); entity.Property(x => x.ProcessDraftId).HasColumnName("BorradorProcesoId");
            entity.Property(x => x.Status).HasColumnName("Estado"); entity.Property(x => x.SignalsJson).HasColumnName("SenalesJson"); entity.Property(x => x.Justification).HasColumnName("Justificacion"); entity.Property(x => x.EvidenceJson).HasColumnName("EvidenciasJson");
            entity.Property(x => x.Decision).HasColumnName("Decision"); entity.Property(x => x.DecisionReason).HasColumnName("MotivoDecision"); entity.Property(x => x.DeciderId).HasColumnName("DecisorId");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion"); entity.Property(x => x.SubmittedAt).HasColumnName("FechaEnvio"); entity.Property(x => x.DecidedAt).HasColumnName("FechaDecision"); entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion"); entity.Property(x => x.Version).HasColumnName("Version");
        });
        modelBuilder.Entity<AdministrationClaimClarificationRow>(entity =>
        {
            entity.ToTable("ReclamacionesAclaraciones"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("IdAclaracion");
            entity.Property(x => x.ClaimId).HasColumnName("ReclamacionId"); entity.Property(x => x.RequestedById).HasColumnName("SolicitadaPorId"); entity.Property(x => x.Comment).HasColumnName("Comentario"); entity.Property(x => x.Response).HasColumnName("Respuesta"); entity.Property(x => x.RespondedById).HasColumnName("RespondidaPorId"); entity.Property(x => x.RequestedAt).HasColumnName("FechaSolicitud"); entity.Property(x => x.RespondedAt).HasColumnName("FechaRespuesta");
        });
        modelBuilder.Entity<AdministrationTransferRow>(entity =>
        {
            entity.ToTable("TransferenciasAdministracion"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("IdTransferencia");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId"); entity.Property(x => x.CanonicalRecordId).HasColumnName("RegistroCanonicoId"); entity.Property(x => x.PreviousOrganizationId).HasColumnName("OrganizacionAnteriorId"); entity.Property(x => x.NewOrganizationId).HasColumnName("OrganizacionNuevaId"); entity.Property(x => x.ClaimId).HasColumnName("ReclamacionId"); entity.Property(x => x.DeciderId).HasColumnName("DecisorId"); entity.Property(x => x.Reason).HasColumnName("Motivo"); entity.Property(x => x.EvidenceJson).HasColumnName("EvidenciasJson"); entity.Property(x => x.PreviousStatus).HasColumnName("EstadoAnterior"); entity.Property(x => x.NewStatus).HasColumnName("EstadoPosterior"); entity.Property(x => x.ExecutedAt).HasColumnName("FechaEjecucion");
        });
        modelBuilder.Entity<AdministrationReconciliationRow>(entity =>
        {
            entity.ToTable("ReclamacionesConciliaciones"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdConciliacion"); entity.Property(x => x.ClaimId).HasColumnName("ReclamacionId");
            entity.Property(x => x.HistoricalValuesJson).HasColumnName("ValoresHistoricosJson"); entity.Property(x => x.DraftValuesJson).HasColumnName("ValoresBorradorJson");
            entity.Property(x => x.SelectionsJson).HasColumnName("SeleccionesJson"); entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion"); entity.Property(x => x.ResolvedAt).HasColumnName("FechaResolucion");
        });

        modelBuilder.Entity<RecordQualityFlagRow>(entity =>
        {
            entity.ToTable("RegistrosCalidadDatos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdRegistroCalidadDatos");
            entity.Property(x => x.ModuloId).HasColumnName("ModuloId");
            entity.Property(x => x.RecordId).HasColumnName("RegistroId");
            entity.Property(x => x.FlagType).HasColumnName("TipoBandera");
            entity.Property(x => x.Severity).HasColumnName("Severidad");
            entity.Property(x => x.Status).HasColumnName("Estado");
            entity.Property(x => x.Detail).HasColumnName("Detalle");
            entity.Property(x => x.CreatedByUserId).HasColumnName("CreadoPorUsuarioId");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");
        });

        modelBuilder.Entity<EntityProfileRow>(entity =>
        {
            entity.ToTable("Entidades");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEntidad");
            entity.Property(x => x.EntityType).HasColumnName("TipoEntidad");
            entity.Property(x => x.Name).HasColumnName("Nombre");
            entity.Property(x => x.IdentificationNumber).HasColumnName("NumeroIdentificacion");
            entity.Property(x => x.IdentificationType).HasColumnName("TipoIdentificacion");
            entity.Property(x => x.LegalName).HasColumnName("NombreLegal");
            entity.Property(x => x.Description).HasColumnName("Descripcion");
            entity.Property(x => x.ContactEmail).HasColumnName("CorreoContacto");
            entity.Property(x => x.ContactPhone).HasColumnName("TelefonoContacto");
            entity.Property(x => x.WebsiteUrl).HasColumnName("SitioWeb");
            entity.Property(x => x.FacebookUrl).HasColumnName("Facebook");
            entity.Property(x => x.InstagramUrl).HasColumnName("Instagram");
            entity.Property(x => x.OtherUrl).HasColumnName("OtroEnlace");
            entity.Property(x => x.HeadquartersDepartmentCode).HasColumnName("CodigoDepartamentoSede");
            entity.Property(x => x.HeadquartersMunicipalityCode).HasColumnName("CodigoMunicipioSede");
            entity.Property(x => x.AddressText).HasColumnName("Direccion");
            entity.Property(x => x.StatusCode).HasColumnName("EstadoRegistro");
            entity.Property(x => x.IsActive).HasColumnName("Activo");
            entity.Property(x => x.IsInstitutional).HasColumnName("EsInstitucional");
            entity.Property(x => x.CreatedByUserId).HasColumnName("IdUsuarioCreador");
            entity.Property(x => x.ResponsibleUserId).HasColumnName("IdUsuarioResponsable");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");
            entity.Property(x => x.ReviewedAt).HasColumnName("FechaRevision");
            entity.Property(x => x.ApprovedAt).HasColumnName("FechaAprobacion");
            entity.Property(x => x.PublishedAt).HasColumnName("FechaPublicacion");
            entity.HasIndex(x => x.IdentificationNumber).IsUnique().HasDatabaseName("UQ_Entidades_NumeroIdentificacion");
            entity.Property(x => x.ArchivoFotoId).HasColumnName("ArchivoFotoId");
        });

        // El satelite 1:1 de Entidades. Los nombres de propiedad ya son los de las columnas,
        // asi que no hace falta un HasColumnName por campo: lo unico que hay que declarar es
        // que IdEntidad es a la vez clave y foranea, y que el correo de acceso es unico.
        modelBuilder.Entity<EntidadResponsableRow>(entity =>
        {
            entity.ToTable("EntidadesResponsable");
            entity.HasKey(x => x.IdEntidad);
            entity.Property(x => x.IdEntidad).ValueGeneratedNever();
            entity.HasIndex(x => x.ResponsableCorreo).HasDatabaseName("IX_EntidadesResponsable_Correo");
        });

        modelBuilder.Entity<ConfirmacionDeCorreoRow>(entity =>
        {
            entity.ToTable("ConfirmacionesDeCorreo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdConfirmacionDeCorreo");
            entity.Property(x => x.HashTestigo).HasMaxLength(64);
            entity.Property(x => x.CorreoDestino).HasMaxLength(240);
            entity.HasIndex(x => x.HashTestigo).IsUnique().HasDatabaseName("UQ_ConfirmacionesDeCorreo_HashTestigo");
        });

        modelBuilder.Entity<UserEntityRow>(entity =>
        {
            entity.ToTable("UsuariosEntidades");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdUsuarioEntidad");
            entity.Property(x => x.UserId).HasColumnName("IdUsuario");
            entity.Property(x => x.EntityId).HasColumnName("IdEntidad");
            entity.Property(x => x.EntityRole).HasColumnName("RolEntidad");
            entity.Property(x => x.IsActive).HasColumnName("Activo");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
        });

        modelBuilder.Entity<ReferenciaHistoricaFestivalRow>(entity =>
        {
            entity.ToTable("FestivalesReferenciasHistoricas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdReferenciaHistoricaFestival");
            entity.Property(x => x.OrganizacionId).HasColumnName("IdOrganizacion");
            entity.Property(x => x.FestivalId).HasColumnName("IdFestival");
            entity.Property(x => x.CreatedAt).HasColumnName("FechaCreacion");
        });

        modelBuilder.Entity<ParticipationSubmissionRow>(entity =>
        {
            entity.ToTable("Participaciones");
            entity.HasKey(x => x.Reference);
            entity.Property(x => x.Reference).HasColumnName("Referencia").HasMaxLength(64);
            entity.Property(x => x.SubmittedAt).HasColumnName("FechaEnvio");
            entity.Property(x => x.ActorType).HasColumnName("TipoActor").HasMaxLength(80);
            entity.Property(x => x.ActorName).HasColumnName("NombreActor").HasMaxLength(240);
            entity.Property(x => x.Email).HasColumnName("CorreoElectronico").HasMaxLength(240);
            entity.Property(x => x.Department).HasColumnName("Departamento").HasMaxLength(120);
            entity.Property(x => x.Municipality).HasColumnName("Municipio").HasMaxLength(120);
            entity.Property(x => x.PayloadJson).HasColumnName("DatosFormularioJson");
            entity.HasIndex(x => x.SubmittedAt);
            entity.HasIndex(x => x.ActorType);
            entity.HasIndex(x => x.Department);
        });

        modelBuilder.Entity<TipoDocumentoRow>(entity =>
        {
            entity.ToTable("TiposDocumento");
            // LA CLAVE ES EL CODIGO, que es lo que se guarda en las cuentas y en las entidades.
            entity.HasKey(x => x.Codigo);
            entity.Property(x => x.Codigo).HasColumnName("CodigoTipoDocumento").HasMaxLength(20);
            entity.Property(x => x.Nombre).HasColumnName("NombreTipoDocumento").HasMaxLength(120).IsRequired();
            entity.Property(x => x.OrdenVisualizacion).HasColumnName("OrdenVisualizacion");
            entity.Property(x => x.Activo).HasColumnName("Activo");
        });

        modelBuilder.Entity<ModuloPorCuentaRow>(entity =>
        {
            entity.ToTable("ModulosPorCuenta");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdModuloPorCuenta");
            entity.Property(x => x.IdUsuario).HasColumnName("IdUsuario");
            entity.Property(x => x.CodigoModulo).HasColumnName("CodigoModulo").HasMaxLength(60).IsRequired();
            entity.Property(x => x.FechaOtorgado).HasColumnName("FechaOtorgado");
            entity.Property(x => x.IdUsuarioQueOtorgo).HasColumnName("IdUsuarioQueOtorgo");
            // UN MODULO NO SE CONCEDE DOS VECES A LA MISMA CUENTA. Sin esto, quitarlo tendría que
            // borrar un número indeterminado de filas y una podría sobrevivir.
            entity.HasIndex(x => new { x.IdUsuario, x.CodigoModulo }).IsUnique();
        });

        modelBuilder.Entity<ContenidoWebRow>(entity =>
        {
            entity.ToTable("ContenidoWeb");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdContenidoWeb");
            entity.Property(x => x.Key).HasColumnName("Clave").HasMaxLength(160).IsRequired();
            entity.Property(x => x.GroupId).HasColumnName("GrupoId").HasMaxLength(120).IsRequired();
            entity.Property(x => x.GroupLabel).HasColumnName("GrupoEtiqueta").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Section).HasColumnName("Seccion").HasMaxLength(120).IsRequired();
            entity.Property(x => x.Label).HasColumnName("Etiqueta").HasMaxLength(240).IsRequired();
            entity.Property(x => x.CharacterLimit).HasColumnName("LimiteCaracteres");
            entity.Property(x => x.Draft).HasColumnName("Borrador").IsRequired();
            entity.Property(x => x.Published).HasColumnName("Publicado");
            entity.Property(x => x.Retired).HasColumnName("FechaRetiro");
            // Token de concurrencia: EF anade "AND Version = @original" al UPDATE, asi
            // que dos guardados simultaneos de la misma clave no se pisan en silencio.
            entity.Property(x => x.Version).HasColumnName("Version").IsConcurrencyToken();
            entity.Property(x => x.UpdatedBy).HasColumnName("ActualizadoPor").HasMaxLength(160).IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");
            entity.HasIndex(x => x.Key).IsUnique();
            entity.HasIndex(x => x.GroupId);
        });

        modelBuilder.Entity<HistorialDeContenidoWebRow>(entity =>
        {
            entity.ToTable("ContenidoWebHistorial");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdContenidoWebHistorial");
            entity.Property(x => x.Key).HasColumnName("Clave").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Action).HasColumnName("Accion").HasMaxLength(40).IsRequired();
            entity.Property(x => x.Value).HasColumnName("Valor");
            entity.Property(x => x.User).HasColumnName("Usuario").HasMaxLength(160).IsRequired();
            entity.Property(x => x.At).HasColumnName("Fecha");
            // La consulta que sirve el panel es «las N ultimas de esta clave».
            entity.HasIndex(x => new { x.Key, x.At });
        });

        modelBuilder.Entity<EquipoWebRow>(entity =>
        {
            entity.ToTable("EquipoWeb", table => table.HasCheckConstraint(
                "CK_EquipoWeb_Unica",
                $"[IdEquipoWeb] = {EquipoWebRow.SingletonId}"));
            entity.HasKey(x => x.Id);
            // Sin IDENTITY: el identificador es fijo, no una secuencia.
            entity.Property(x => x.Id).HasColumnName("IdEquipoWeb").ValueGeneratedNever();
            entity.Property(x => x.Draft).HasColumnName("Borrador").IsRequired();
            entity.Property(x => x.Published).HasColumnName("Publicado");
            entity.Property(x => x.Version).HasColumnName("Version").IsConcurrencyToken();
            entity.Property(x => x.UpdatedBy).HasColumnName("ActualizadoPor").HasMaxLength(160).IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");
        });

        // LAS IMAGENES ADMINISTRABLES. El DDL de verdad vive en
        // pnmc-database/schema/V20260829_01__medios_web.sql, con diecisiete restricciones CHECK —trece en dbo.MediosWeb y cuatro en su historial, contadas
        // contra sys.check_constraints— que
        // este mapeo NO reproduce y que el carril de SQLite por tanto no ejercita. Lo que las
        // prueba es ImagenesWebEsquemaSqlServerTests en el carril PNMC_PRUEBAS_SQLSERVER=1; lo que garantiza
        // que las columnas de aqui y las de alli coinciden es ParidadEsquemaSinArranque.
        modelBuilder.Entity<ImagenWebRow>(entity =>
        {
            entity.ToTable("MediosWeb");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdMedioWeb");
            entity.Property(x => x.Key).HasColumnName("Clave").HasMaxLength(160).IsRequired();
            entity.Property(x => x.GroupId).HasColumnName("GrupoId").HasMaxLength(120).IsRequired();
            entity.Property(x => x.GroupLabel).HasColumnName("GrupoEtiqueta").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Section).HasColumnName("Seccion").HasMaxLength(120).IsRequired();
            entity.Property(x => x.Label).HasColumnName("Etiqueta").HasMaxLength(240).IsRequired();
            entity.Property(x => x.Use).HasColumnName("Uso").HasMaxLength(40).IsRequired();
            entity.Property(x => x.SuggestedWidth).HasColumnName("AnchoSugerido");
            entity.Property(x => x.SuggestedHeight).HasColumnName("AltoSugerido");
            entity.Property(x => x.Editable).HasColumnName("Editable");
            entity.Property(x => x.AltText).HasColumnName("TextoAlternativo").HasMaxLength(300).IsRequired();

            entity.Property(x => x.DraftContent).HasColumnName("BorradorContenido");
            entity.Property(x => x.DraftMime).HasColumnName("BorradorTipo").HasMaxLength(40);
            entity.Property(x => x.DraftBytes).HasColumnName("BorradorBytes");
            entity.Property(x => x.DraftWidth).HasColumnName("BorradorAncho");
            entity.Property(x => x.DraftHeight).HasColumnName("BorradorAlto");
            entity.Property(x => x.DraftHash).HasColumnName("BorradorHuella").HasColumnType("char(64)").IsFixedLength();
            entity.Property(x => x.DraftThumbnail).HasColumnName("BorradorMiniatura");

            entity.Property(x => x.PublishedContent).HasColumnName("PublicadoContenido");
            entity.Property(x => x.PublishedMime).HasColumnName("PublicadoTipo").HasMaxLength(40);
            entity.Property(x => x.PublishedBytes).HasColumnName("PublicadoBytes");
            entity.Property(x => x.PublishedWidth).HasColumnName("PublicadoAncho");
            entity.Property(x => x.PublishedHeight).HasColumnName("PublicadoAlto");
            entity.Property(x => x.PublishedHash).HasColumnName("PublicadoHuella").HasColumnType("char(64)").IsFixedLength();
            entity.Property(x => x.PublishedThumbnail).HasColumnName("PublicadoMiniatura");

            entity.Property(x => x.Retired).HasColumnName("FechaRetiro");
            // Mismo token de concurrencia que ContenidoWeb, y aqui pesa mas: dos editores
            // subiendo a la misma ranura pisarian megabytes en silencio.
            entity.Property(x => x.Version).HasColumnName("Version").IsConcurrencyToken();
            entity.Property(x => x.UpdatedBy).HasColumnName("ActualizadoPor").HasMaxLength(160).IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("FechaActualizacion");
            entity.HasIndex(x => x.Key).IsUnique();
            entity.HasIndex(x => x.GroupId);
        });

        modelBuilder.Entity<HistorialDeImagenWebRow>(entity =>
        {
            entity.ToTable("MediosWebHistorial");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdMedioWebHistorial");
            entity.Property(x => x.Key).HasColumnName("Clave").HasMaxLength(160).IsRequired();
            entity.Property(x => x.Action).HasColumnName("Accion").HasMaxLength(40).IsRequired();
            entity.Property(x => x.Mime).HasColumnName("Tipo").HasMaxLength(40);
            entity.Property(x => x.Bytes).HasColumnName("Bytes");
            entity.Property(x => x.Width).HasColumnName("Ancho");
            entity.Property(x => x.Height).HasColumnName("Alto");
            entity.Property(x => x.Hash).HasColumnName("Huella").HasColumnType("char(64)").IsFixedLength();
            entity.Property(x => x.User).HasColumnName("Usuario").HasMaxLength(160).IsRequired();
            entity.Property(x => x.At).HasColumnName("Fecha");
            entity.HasIndex(x => new { x.Key, x.At });
        });

        /*
         * ───────────────────────── Catálogo Editorial ─────────────────────────
         *
         * Las columnas de derechos se aplanan en la publicación y en cada acceso en vez de vivir
         * en una tabla aparte. No es comodidad: los derechos son UNA decisión por ficha y UNA por
         * acceso, nunca varias, y una tabla hija admitiría dos filas contradictorias para el mismo
         * recurso sin que nada lo impida.
         */
        modelBuilder.Entity<AgenteEditorialRow>(entity =>
        {
            entity.ToTable("AgentesEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdAgenteEditorial");
            entity.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.NombrePreferido).HasMaxLength(300).IsRequired();
            entity.Property(x => x.FormaNormalizada).HasMaxLength(300);
            entity.Property(x => x.Seudonimo).HasMaxLength(300);
            entity.Property(x => x.Acronimo).HasMaxLength(60);
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<FuenteEditorialRow>(entity =>
        {
            entity.ToTable("FuentesEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdFuenteEditorial");
            entity.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Referencia).HasMaxLength(600);
            entity.Property(x => x.Url).HasMaxLength(1000);
            entity.Property(x => x.VerificadaPor).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<ProgramaEditorialRow>(entity =>
        {
            entity.ToTable("ProgramasEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdProgramaEditorial");
            entity.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<PublicacionEditorialRow>(entity =>
        {
            entity.ToTable("PublicacionesEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPublicacionEditorial");
            entity.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Titulo).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Subtitulo).HasMaxLength(500);
            entity.Property(x => x.DesignacionVolumen).HasMaxLength(120);
            entity.Property(x => x.SerieOColeccion).HasMaxLength(300);
            entity.Property(x => x.FechaEdtf).HasMaxLength(60);
            entity.Property(x => x.Idioma).HasMaxLength(120);
            entity.Property(x => x.TipoPublicacion).HasMaxLength(240);
            entity.HasOne<CategoriaRow>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Ambito).HasMaxLength(160);
            entity.Property(x => x.MiniaturaRuta).HasMaxLength(500);
            entity.Property(x => x.SeccionPrincipal).HasMaxLength(200);
            entity.Property(x => x.RutaSeccion).HasMaxLength(500);
            entity.Property(x => x.PracticaMusical).HasMaxLength(160);
            entity.Property(x => x.Subcategoria).HasMaxLength(160);
            entity.Property(x => x.TamanoFormato).HasMaxLength(120);
            entity.Property(x => x.Paginas).HasMaxLength(160);
            entity.Property(x => x.Duracion).HasMaxLength(160);
            entity.HasIndex(x => x.SeccionPrincipal);
            entity.Property(x => x.EstadoCatalogacion).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EstadoPublicacion).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DerechosEstado).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DerechosLicenciaONota).HasMaxLength(600);
            entity.Property(x => x.DerechosVerificadoPor).HasMaxLength(200);
            entity.HasIndex(x => x.Codigo).IsUnique();
            entity.HasIndex(x => new { x.EstadoPublicacion, x.EstadoCatalogacion });
            entity.HasOne<FuenteEditorialRow>().WithMany().HasForeignKey(x => x.DerechosFuenteId).OnDelete(DeleteBehavior.Restrict);
            // Lo que trajo el acervo real. Ver `V20260913_04__tipologia_editorial.sql`.
            entity.Property(x => x.Formato).HasMaxLength(20);
            entity.Property(x => x.NotaFecha).HasMaxLength(300);
            entity.Property(x => x.CategoriaSecundaria).HasMaxLength(240);
            entity.Property(x => x.AmbitoTexto).HasMaxLength(240);
            entity.Property(x => x.NotasCatalogacion).HasMaxLength(500);
            entity.Property(x => x.Confianza).HasMaxLength(20);
            entity.Property(x => x.DiapositivaOrigen).HasMaxLength(20);
        });

        modelBuilder.Entity<TipologiaEditorialRow>(entity =>
        {
            entity.ToTable("TipologiasEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdTipologiaEditorial");
            entity.Property(x => x.Eje).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Etiqueta).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Norma).HasMaxLength(60).IsRequired();
            entity.HasIndex(x => new { x.Eje, x.Codigo }).IsUnique();
        });

        modelBuilder.Entity<PublicacionEditorialTipologiaRow>(entity =>
        {
            entity.ToTable("PublicacionesEditorialesTipologias");
            entity.HasKey(x => new { x.PublicacionEditorialId, x.TipologiaEditorialId });
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TipologiaEditorialRow>().WithMany().HasForeignKey(x => x.TipologiaEditorialId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PublicacionEditorialFuenteRow>(entity =>
        {
            entity.ToTable("PublicacionesEditorialesFuentes");
            entity.HasKey(x => new { x.PublicacionEditorialId, x.FuenteEditorialId });
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            // NINGUNA CASCADA EN TODO EL CATALOGO. `EstructuraSimusSqlServerTests` lo impone:
            // una cascada borra filas en silencio y deja el mismo DELETE comportandose de dos
            // maneras segun la tabla. Retirar una publicacion es un ESTADO del dominio, no un
            // borrado; si alguna vez hay que borrarla, sus hijos se retiran a la vista.
            entity.HasOne<FuenteEditorialRow>().WithMany().HasForeignKey(x => x.FuenteEditorialId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditoEditorialRow>(entity =>
        {
            entity.ToTable("CreditosEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdCreditoEditorial");
            entity.Property(x => x.RolCodigo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RolEtiqueta).HasMaxLength(120).IsRequired();
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AgenteEditorialRow>().WithMany().HasForeignKey(x => x.AgenteEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PublicacionEditorialId, x.AgenteEditorialId, x.RolCodigo }).IsUnique();
        });

        modelBuilder.Entity<IdentificadorEditorialRow>(entity =>
        {
            entity.ToTable("IdentificadoresEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdIdentificadorEditorial");
            entity.Property(x => x.Esquema).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CodigoRecibido).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Cualificador).HasMaxLength(120);
            entity.Property(x => x.ObservacionValidacion).HasMaxLength(400);
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.Esquema, x.CodigoRecibido });
        });

        modelBuilder.Entity<AccesoEditorialRow>(entity =>
        {
            entity.ToTable("AccesosEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdAccesoEditorial");
            entity.Property(x => x.Tipo).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(1000);
            entity.Property(x => x.UbicacionFisica).HasMaxLength(600);
            entity.Property(x => x.Etiqueta).HasMaxLength(200);
            entity.Property(x => x.Nota).HasMaxLength(600);
            entity.Property(x => x.DerechosEstado).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DerechosLicenciaONota).HasMaxLength(600);
            entity.Property(x => x.DerechosVerificadoPor).HasMaxLength(200);
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FileRow>().WithMany().HasForeignKey(x => x.ArchivoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FuenteEditorialRow>().WithMany().HasForeignKey(x => x.DerechosFuenteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PublicacionEditorialId, x.Orden });
        });

        modelBuilder.Entity<NoticiaRow>(entity =>
        {
            entity.ToTable("Noticias");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdNoticia");
            entity.Property(x => x.Slug).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Titulo).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Resumen).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ImagenRuta).HasMaxLength(500);
            entity.Property(x => x.ImagenAlternativa).HasMaxLength(300);
            entity.Property(x => x.AutoriaNombre).HasMaxLength(200);
            entity.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            entity.HasOne<CategoriaRow>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.CategoriaId);
            entity.HasIndex(x => new { x.Estado, x.FechaPublicacion });
        });

        modelBuilder.Entity<CategoriaRow>(entity =>
        {
            entity.ToTable("Categorias");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdCategoria");
            entity.Property(x => x.CodigoModulo).HasMaxLength(80).IsRequired();
            entity.Property(x => x.NombreCategoria).HasMaxLength(140).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(600);
            entity.HasIndex(x => new { x.CodigoModulo, x.Slug }).IsUnique();
            entity.HasIndex(x => new { x.CodigoModulo, x.NombreCategoria }).IsUnique();
        });

        modelBuilder.Entity<NoticiaPracticaMusicalRow>(entity =>
        {
            entity.ToTable("NoticiasPracticasMusicales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdNoticiaPracticaMusical");
            entity.HasOne<NoticiaRow>().WithMany().HasForeignKey(x => x.NoticiaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PracticaMusicalRow>().WithMany().HasForeignKey(x => x.PracticaMusicalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.NoticiaId, x.PracticaMusicalId }).IsUnique();
        });

        modelBuilder.Entity<NoticiaTerritorioSonoroRow>(entity =>
        {
            entity.ToTable("NoticiasTerritoriosSonoros");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdNoticiaTerritorioSonoro");
            entity.HasOne<NoticiaRow>().WithMany().HasForeignKey(x => x.NoticiaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TerritorioSonoroRow>().WithMany().HasForeignKey(x => x.TerritorioSonoroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.NoticiaId, x.TerritorioSonoroId }).IsUnique();
        });

        modelBuilder.Entity<EventoAgendaPracticaMusicalRow>(entity =>
        {
            entity.ToTable("EventosAgendaPracticasMusicales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEventoAgendaPracticaMusical");
            entity.HasOne<EventoAgendaRow>().WithMany().HasForeignKey(x => x.EventoAgendaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PracticaMusicalRow>().WithMany().HasForeignKey(x => x.PracticaMusicalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.EventoAgendaId, x.PracticaMusicalId }).IsUnique();
        });

        modelBuilder.Entity<EventoAgendaTerritorioSonoroRow>(entity =>
        {
            entity.ToTable("EventosAgendaTerritoriosSonoros");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEventoAgendaTerritorioSonoro");
            entity.HasOne<EventoAgendaRow>().WithMany().HasForeignKey(x => x.EventoAgendaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TerritorioSonoroRow>().WithMany().HasForeignKey(x => x.TerritorioSonoroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.EventoAgendaId, x.TerritorioSonoroId }).IsUnique();
        });

        modelBuilder.Entity<PublicacionEditorialPracticaMusicalRow>(entity =>
        {
            entity.ToTable("PublicacionesEditorialesPracticasMusicales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPublicacionEditorialPracticaMusical");
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PracticaMusicalRow>().WithMany().HasForeignKey(x => x.PracticaMusicalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PublicacionEditorialId, x.PracticaMusicalId }).IsUnique();
        });

        modelBuilder.Entity<PublicacionEditorialTerritorioSonoroRow>(entity =>
        {
            entity.ToTable("PublicacionesEditorialesTerritoriosSonoros");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPublicacionEditorialTerritorioSonoro");
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TerritorioSonoroRow>().WithMany().HasForeignKey(x => x.TerritorioSonoroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PublicacionEditorialId, x.TerritorioSonoroId }).IsUnique();
        });

        modelBuilder.Entity<EventoAgendaRow>(entity =>
        {
            entity.ToTable("EventosAgenda");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEventoAgenda");
            entity.Property(x => x.Slug).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Titulo).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Modalidad).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Lugar).HasMaxLength(300);
            entity.Property(x => x.CodigoDepartamento).HasMaxLength(2).IsFixedLength();
            entity.Property(x => x.CodigoMunicipio).HasMaxLength(5).IsFixedLength();
            entity.Property(x => x.Url).HasMaxLength(500);
            entity.Property(x => x.ImagenRuta).HasMaxLength(500);
            entity.Property(x => x.ImagenAlternativa).HasMaxLength(300);
            entity.Property(x => x.Organizador).HasMaxLength(220);
            entity.HasOne<CategoriaRow>().WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.NivelCobertura).HasMaxLength(20).IsRequired();
            entity.HasOne<FestivalRow>().WithMany().HasForeignKey(x => x.FestivalId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.CategoriaId);
            entity.HasIndex(x => new { x.Estado, x.FechaInicio });
            entity.HasIndex(x => x.CodigoDepartamento);
            // La foránea al catálogo territorial la declara el esquema; aquí solo se refleja la
            // relación para que el modelo de EF no la contradiga al fabricar la base de pruebas.
            entity.HasOne<DivipolaLocationRow>()
                .WithMany()
                .HasForeignKey(x => new { x.CodigoDepartamento, x.CodigoMunicipio })
                .HasPrincipalKey(x => new { x.DepartmentCode, x.MunicipalityCode })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventoAgendaArchivoRow>(entity =>
        {
            entity.ToTable("EventosAgendaArchivos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEventoAgendaArchivo");
            entity.Property(x => x.RolArchivo).HasMaxLength(80).IsRequired();
            entity.HasOne<EventoAgendaRow>().WithMany().HasForeignKey(x => x.EventoAgendaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FileRow>().WithMany().HasForeignKey(x => x.ArchivoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.EventoAgendaId, x.ArchivoId, x.RolArchivo }).IsUnique();
        });

        modelBuilder.Entity<ProyectoTransversalRow>(entity =>
        {
            entity.ToTable("ProyectosTransversales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdProyectoTransversal");
            entity.Property(x => x.Codigo).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Descripcion).HasMaxLength(1000);
            entity.HasIndex(x => x.Codigo).IsUnique();
            entity.HasIndex(x => x.Nombre).IsUnique();
        });

        modelBuilder.Entity<EventoAgendaProyectoTransversalRow>(entity =>
        {
            entity.ToTable("EventosAgendaProyectosTransversales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEventoAgendaProyectoTransversal");
            entity.HasOne<EventoAgendaRow>().WithMany().HasForeignKey(x => x.EventoAgendaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProyectoTransversalRow>().WithMany().HasForeignKey(x => x.ProyectoTransversalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.EventoAgendaId, x.ProyectoTransversalId }).IsUnique();
        });

        modelBuilder.Entity<NoticiaProyectoTransversalRow>(entity =>
        {
            entity.ToTable("NoticiasProyectosTransversales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdNoticiaProyectoTransversal");
            entity.HasOne<NoticiaRow>().WithMany().HasForeignKey(x => x.NoticiaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProyectoTransversalRow>().WithMany().HasForeignKey(x => x.ProyectoTransversalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.NoticiaId, x.ProyectoTransversalId }).IsUnique();
        });

        modelBuilder.Entity<ProcedenciaDeRegistroRow>(entity =>
        {
            entity.ToTable("ProcedenciasDeRegistro");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdProcedencia");
            entity.Property(x => x.ModuloId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RegistroId).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ContextoOrigen).HasMaxLength(20).IsRequired();
            entity.HasOne<EntityProfileRow>().WithMany().HasForeignKey(x => x.OrganizacionProcedenciaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UsuarioCreadorId).OnDelete(DeleteBehavior.Restrict);
            // UN REGISTRO, UNA PROCEDENCIA: nace una vez y no cambia.
            entity.HasIndex(x => new { x.ModuloId, x.RegistroId }).IsUnique();
        });

        modelBuilder.Entity<NoticiaArchivoRow>(entity =>
        {
            entity.ToTable("NoticiasArchivos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdNoticiaArchivo");
            entity.Property(x => x.RolArchivo).HasMaxLength(80).IsRequired();
            entity.HasOne<NoticiaRow>().WithMany().HasForeignKey(x => x.NoticiaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FileRow>().WithMany().HasForeignKey(x => x.ArchivoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.NoticiaId, x.ArchivoId, x.RolArchivo }).IsUnique();
        });

        modelBuilder.Entity<EtiquetaEventoAgendaRow>(entity =>
        {
            entity.ToTable("EtiquetasEventoAgenda");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEtiquetaEventoAgenda");
            entity.Property(x => x.Termino).HasMaxLength(120).IsRequired();
            entity.HasOne<EventoAgendaRow>().WithMany().HasForeignKey(x => x.EventoAgendaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.EventoAgendaId, x.Termino }).IsUnique();
            entity.HasIndex(x => x.Termino);
        });

        modelBuilder.Entity<EtiquetaNoticiaRow>(entity =>
        {
            entity.ToTable("EtiquetasNoticia");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEtiquetaNoticia");
            entity.Property(x => x.Termino).HasMaxLength(120).IsRequired();
            entity.HasOne<NoticiaRow>().WithMany().HasForeignKey(x => x.NoticiaId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.NoticiaId, x.Termino }).IsUnique();
            entity.HasIndex(x => x.Termino);
        });

        modelBuilder.Entity<PalabraClaveEditorialRow>(entity =>
        {
            entity.ToTable("PalabrasClaveEditoriales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPalabraClaveEditorial");
            entity.Property(x => x.Termino).HasMaxLength(120).IsRequired();
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PublicacionEditorialId, x.Termino }).IsUnique();
            entity.HasIndex(x => x.Termino);
        });

        modelBuilder.Entity<PublicacionEditorialProgramaRow>(entity =>
        {
            entity.ToTable("PublicacionesEditorialesProgramas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdPublicacionEditorialPrograma");
            entity.Property(x => x.VerificadaPor).HasMaxLength(200).IsRequired();
            entity.HasOne<PublicacionEditorialRow>().WithMany().HasForeignKey(x => x.PublicacionEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProgramaEditorialRow>().WithMany().HasForeignKey(x => x.ProgramaEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FuenteEditorialRow>().WithMany().HasForeignKey(x => x.FuenteEditorialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.PublicacionEditorialId, x.ProgramaEditorialId }).IsUnique();
        });

        // ── Mercados musicales ───────────────────────────────────────────────────────────────
        //
        // SIN CASCADAS, en ninguna de las ocho tablas. El proyecto no las admite y una prueba lo
        // comprueba: borrar una entidad no puede llevarse por delante sus mercados en silencio.
        modelBuilder.Entity<MercadoRow>(entity =>
        {
            entity.ToTable("Mercados");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdMercado");
            entity.Property(x => x.Nombre).HasColumnName("NombreMercado").HasMaxLength(240).IsRequired();
            entity.Property(x => x.NivelCobertura).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CodigoDepartamento).HasMaxLength(2).IsFixedLength();
            entity.Property(x => x.CodigoMunicipio).HasMaxLength(5).IsFixedLength();
            entity.Property(x => x.EstadoRegistro).HasMaxLength(80).IsRequired();
            entity.HasOne<AlcanceMercadoRow>().WithMany().HasForeignKey(x => x.AlcanceMercadoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ModalidadMercadoRow>().WithMany().HasForeignKey(x => x.ModalidadMercadoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FestivalRow>().WithMany().HasForeignKey(x => x.FestivalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EntityProfileRow>().WithMany().HasForeignKey(x => x.OrganizacionPrincipalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.OrganizacionPrincipalId, x.EstadoRegistro });
            entity.HasIndex(x => x.FestivalId);
        });

        modelBuilder.Entity<EdicionMercadoRow>(entity =>
        {
            entity.ToTable("EdicionesMercado");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdEdicionMercado");
            entity.Property(x => x.CodigoDepartamento).HasMaxLength(2).IsFixedLength();
            entity.Property(x => x.CodigoMunicipio).HasMaxLength(5).IsFixedLength();
            entity.Property(x => x.Estado).HasMaxLength(40).IsRequired();
            entity.Property(x => x.EstadoVisibilidad).HasMaxLength(40).IsRequired();
            entity.HasOne<MercadoRow>().WithMany().HasForeignKey(x => x.MercadoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MercadoId, x.Anio }).IsUnique();
        });

        modelBuilder.Entity<AlcanceMercadoRow>(entity =>
        {
            entity.ToTable("AlcancesMercado");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdAlcanceMercado");
            entity.Property(x => x.Nombre).HasColumnName("NombreAlcance").HasMaxLength(120).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<ModalidadMercadoRow>(entity =>
        {
            entity.ToTable("ModalidadesMercado");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdModalidadMercado");
            entity.Property(x => x.Nombre).HasColumnName("NombreModalidad").HasMaxLength(120).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<MercadoPracticaMusicalRow>(entity =>
        {
            entity.ToTable("MercadosPracticasMusicales");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdMercadoPracticaMusical");
            entity.HasOne<MercadoRow>().WithMany().HasForeignKey(x => x.MercadoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PracticaMusicalRow>().WithMany().HasForeignKey(x => x.PracticaMusicalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MercadoId, x.PracticaMusicalId }).IsUnique();
        });

        modelBuilder.Entity<MercadoTerritorioSonoroRow>(entity =>
        {
            entity.ToTable("MercadosTerritoriosSonoros");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("IdMercadoTerritorioSonoro");
            entity.HasOne<MercadoRow>().WithMany().HasForeignKey(x => x.MercadoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TerritorioSonoroRow>().WithMany().HasForeignKey(x => x.TerritorioSonoroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.MercadoId, x.TerritorioSonoroId }).IsUnique();
        });

    }
}
