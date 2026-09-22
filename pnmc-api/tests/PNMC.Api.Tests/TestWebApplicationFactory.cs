using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PNMC.Api.Endpoints;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Si el arnés abre por su cuenta el enlace de confirmación de correo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>POR QUE EXISTE Y POR QUE VIENE ENCENDIDO.</b> Desde una
    /// organización cuyo correo nadie ha comprobado puede entrar y preparar su registro pero no
    /// <b>entregarle nada al Programa</b>. Casi ninguna de las pruebas de esta suite trata sobre la
    /// confirmación: tratan sobre lo que pasa DESPUES —enviar a revisión, publicar una edición,
    /// proponer cambios—, y obligarlas a recorrer el enlace las ataría a un flujo ajeno.
    /// </para>
    /// <para>
    /// <b>ESTO NO ES SALTARSE LA REGLA, ES HACER DE PERSONA.</b> El arnés se comporta como alguien
    /// que abre su correo y pulsa el enlace nada más registrarse: el testigo se emite de verdad, el
    /// mensaje queda en la cola de salida de verdad, y la confirmación recorre el mismo camino. Lo
    /// único que se salta es la espera.
    /// </para>
    /// <para>
    /// <b>LAS PRUEBAS DE LA CONFIRMACION LO APAGAN</b> —<c>LaOrganizacionYSusProcesosTests</c>— y son
    /// las que comprueban el estado de nacimiento, el bloqueo de la entrega, el vencimiento y el
    /// reenvío. Si esto viniera apagado por omisión, las otras dieciséis clases tendrían que repetir
    /// la misma línea y la primera que se olvidara fallaría por un motivo que no es el suyo.
    /// </para>
    /// </remarks>
    public bool ConfirmaElCorreoAlRegistrar { get; init; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            if (ConfirmaElCorreoAlRegistrar)
            {
                services.RemoveAll<PNMC.Infrastructure.Correo.IEnviadorDeCorreo>();
                services.AddScoped<PNMC.Infrastructure.Correo.IEnviadorDeCorreo, EnviadorQueAbreElEnlace>();
            }

            services.RemoveAll<DbContextOptions<PnmcDbContext>>();
            services.RemoveAll<PnmcDbContext>();

            var tempDbPath = Path.Combine(Path.GetTempPath(), $"pnmc-migration-tests-{Guid.NewGuid():N}.db");
            services.AddDbContext<PnmcDbContext>(options =>
                options.UseSqlite($"Data Source={tempDbPath}"));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.Database.EnsureCreated();

            db.Roles.Add(new RoleRow
            {
                Id = 1,
                Name = "webmaster"
            });
            db.Roles.Add(new RoleRow { Id = 2, Name = "gestor_interno" });
            db.Roles.Add(new RoleRow { Id = 6, Name = "externo" });

            // ROL RESIDUAL A PROPOSITO. No pertenece al modelo —los tres de plataforma son
            // webmaster, gestor_interno y externo— y por eso esta aqui: hasta el 22 de agosto
            // de 2026 la puerta institucional era una lista negra de un solo elemento
            // («rechaza "externo"»), asi que un nombre de rol que nadie hubiera previsto
            // entraba en la consola por omision. Es exactamente lo que pasaba con los tres
            // aliado_*. Esta fila existe para que PuertaInstitucionalTests pueda comprobar que
            // la lista blanca lo rechaza; si alguien la borra, esa prueba deja de medir nada.
            db.Roles.Add(new RoleRow { Id = 7, Name = "rol_desconocido" });

            var webmasterUser = new UserRow
            {
                Id = 1,
                FullName = "Usuario Prueba",
                Email = "test@pnmc.local",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            webmasterUser.PasswordHash = AdminAuthEndpoints.HashPassword(webmasterUser, "pnmc-master");
            db.Users.Add(webmasterUser);

            // GESTOR INTERNO. Es el vehiculo de las pruebas de «sesion valida + rol
            // insuficiente = 403»: entra por la puerta institucional y NO alcanza los guardas
            // reservados al webmaster (publicar contenido, importar el CMS, gestionar
            // usuarios). Antes ese papel lo hacia aliado.admin@pnmc.local, que entraba sin ser
            // interno; al retirarlo habria desaparecido la clase entera de prueba.
            var gestorUser = new UserRow
            {
                Id = 2,
                FullName = "Gestor Interno Prueba",
                Email = "gestor@pnmc.local",
                AccessChannel = "interno",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            gestorUser.PasswordHash = AdminAuthEndpoints.HashPassword(gestorUser, "pnmc-gestor");
            db.Users.Add(gestorUser);

            // PERSONA DEL ECOSISTEMA. Rol «externo»: es a la vez la persona registrada y el
            // usuario externo del modelo; los distingue el vinculo en UsuariosEntidades, no el
            // rol. No debe cruzar la puerta institucional en ningun caso.
            var externoUser = new UserRow
            {
                Id = 3,
                FullName = "Persona Del Ecosistema",
                Email = "externo@pnmc.local",
                AccessChannel = "externo",
                ProfileType = "organizacion",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            externoUser.PasswordHash = AdminAuthEndpoints.HashPassword(externoUser, "pnmc-externo");
            db.Users.Add(externoUser);

            // Portador del rol residual. Ver la nota de RoleRow Id = 7.
            var rolDesconocidoUser = new UserRow
            {
                Id = 4,
                FullName = "Rol Que Nadie Declaro",
                Email = "desconocido@pnmc.local",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            rolDesconocidoUser.PasswordHash = AdminAuthEndpoints.HashPassword(rolDesconocidoUser, "pnmc-desconocido");
            db.Users.Add(rolDesconocidoUser);

            // LAS FILAS DE dbo.UsuariosRoles, QUE ES LO UNICO QUE DA EL ROL DESDE LA TRANSICION.
            //
            // La columna `Usuarios.IdRol` ya no existe. Sin estas cuatro filas, las cuatro cuentas
            // de prueba existen y no pueden iniciar sesion: el login responde 403 con el motivo
            // `sin_rol_valido`.
            //
            // Los Id se fijan a mano igual que los de las filas de arriba, para que dos corridas
            // del arnes fabriquen la misma base.
            db.UsuariosRoles.AddRange(
                new UsuarioRolRow { Id = 1, UserId = webmasterUser.Id, RoleId = 1, CreatedAt = DateTime.UtcNow },
                new UsuarioRolRow { Id = 2, UserId = gestorUser.Id, RoleId = 2, CreatedAt = DateTime.UtcNow },
                new UsuarioRolRow { Id = 3, UserId = externoUser.Id, RoleId = 6, CreatedAt = DateTime.UtcNow },
                new UsuarioRolRow { Id = 4, UserId = rolDesconocidoUser.Id, RoleId = 7, CreatedAt = DateTime.UtcNow });

            // EL CATALOGO DE PERMISOS Y SU REPARTO — el defecto U8 del plan de construccion.
            //
            // `EnsureCreated` crea las tablas del modelo pero no ejecuta los guiones de `schema/`,
            // asi que en esta via las dos nacian VACIAS. Sembrarlas es lo que hace que el dia que
            // una guarda empiece a consultarlas, la suite mida algo en vez de pasar por vacio.
            // La lista vive en CatalogoDePermisosDeReferencia y CatalogoDePermisosTests comprueba
            // que dice lo mismo que el fichero .sql.
            var idsDeRol = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["webmaster"] = 1,
                ["gestor_interno"] = 2,
                ["externo"] = 6
            };

            var idsDePermiso = new Dictionary<string, int>(StringComparer.Ordinal);
            var siguiente = 1;
            foreach (var (codigo, modulo, nombre) in CatalogoDePermisosDeReferencia.Permisos)
            {
                idsDePermiso[codigo] = siguiente;
                db.Permisos.Add(new PermisoRow
                {
                    Id = siguiente,
                    Codigo = codigo,
                    Nombre = nombre,
                    Modulo = modulo,
                    CreatedAt = DateTime.UtcNow
                });
                siguiente++;
            }

            var siguienteReparto = 1;
            foreach (var (rol, codigo) in CatalogoDePermisosDeReferencia.Reparto)
            {
                db.RolesPermisos.Add(new RolPermisoRow
                {
                    Id = siguienteReparto++,
                    RoleId = idsDeRol[rol],
                    PermisoId = idsDePermiso[codigo],
                    CreatedAt = DateTime.UtcNow
                });
            }

            db.ContentStatuses.Add(new ContentStatusRow
            {
                Id = 1,
                Code = "borrador",
                Name = "Borrador"
            });
            db.ContentStatuses.Add(new ContentStatusRow { Id = 2, Code = "en_revision", Name = "En revision" });
            db.ContentStatuses.Add(new ContentStatusRow { Id = 3, Code = "ajustes_solicitados", Name = "Ajustes solicitados" });
            db.ContentStatuses.Add(new ContentStatusRow { Id = 4, Code = "aprobado", Name = "Aprobado" });
            db.ContentStatuses.Add(new ContentStatusRow { Id = 5, Code = "publicado", Name = "Publicado" });
            db.ContentStatuses.Add(new ContentStatusRow { Id = 6, Code = "rechazado", Name = "Rechazado" });
            db.ContentStatuses.Add(new ContentStatusRow { Id = 7, Code = "archivado", Name = "Archivado" });
            db.ContentStatuses.Add(new ContentStatusRow { Id = 8, Code = "registrada", Name = "Registrada" });

            // LOS TEXTOS REALES Y NO UNOS DE PRUEBA. Vienen de `PoliticasDeDatosSembradas`, que es
            // de donde los toma tambien el arranque: si aqui hubiera cadenas inventadas, las
            // pruebas que comprueban que el texto servido y el guardado coinciden pasarian sin
            // tocar nunca la redaccion que ve una persona de verdad.
            db.PoliticasDeDatos.AddRange(
                new PoliticaDeDatosRow
                {
                    Id = 1,
                    Clave = "tratamiento",
                    Version = PoliticasDeDatosSembradas.VersionVigente,
                    Titulo = PoliticasDeDatosSembradas.TituloTratamiento,
                    Texto = PoliticasDeDatosSembradas.TextoTratamiento,
                    UrlOficial = PoliticasDeDatosSembradas.UrlTratamiento,
                    ReferenciaOficial = "PL-GSI-002 v0",
                    Vigente = true,
                    FechaPublicacion = DateTime.UtcNow,
                    FechaCreacion = DateTime.UtcNow,
                },
                new PoliticaDeDatosRow
                {
                    Id = 2,
                    Clave = "terminos",
                    Version = PoliticasDeDatosSembradas.VersionVigente,
                    Titulo = PoliticasDeDatosSembradas.TituloTerminos,
                    Texto = PoliticasDeDatosSembradas.TextoTerminos,
                    UrlOficial = PoliticasDeDatosSembradas.UrlTerminos,
                    ReferenciaOficial = "PL-GSI-001 v0",
                    Vigente = true,
                    FechaPublicacion = DateTime.UtcNow,
                    FechaCreacion = DateTime.UtcNow,
                },
                new PoliticaDeDatosRow
                {
                    Id = 3,
                    Clave = "boletin",
                    Version = PoliticasDeDatosSembradas.VersionVigente,
                    Titulo = PoliticasDeDatosSembradas.TituloBoletin,
                    Texto = PoliticasDeDatosSembradas.TextoBoletin,
                    Vigente = true,
                    FechaPublicacion = DateTime.UtcNow,
                    FechaCreacion = DateTime.UtcNow,
                });

            db.DivipolaLocations.AddRange(
                new DivipolaLocationRow
                {
                    DepartmentCode = "11",
                    DepartmentName = "Bogota D.C.",
                    MunicipalityCode = "11001",
                    MunicipalityName = "Bogota D.C.",
                    LocationType = "MUNICIPALITY"
                },
                new DivipolaLocationRow
                {
                    DepartmentCode = "05",
                    DepartmentName = "Antioquia",
                    MunicipalityCode = "05001",
                    MunicipalityName = "Medellin",
                    LocationType = "MUNICIPALITY"
                });

            db.FestivalRecords.Add(new FestivalRow
            {
                Id = 1,
                Name = "Festival Test",
                CoverageLevel = "municipal",
                DepartmentCode = "05",
                MunicipalityCode = "05001",
                CreatedAt = DateTime.UtcNow
            });

            db.PracticasMusicales.Add(new PracticaMusicalRow
            {
                Id = 1,
                Nombre = "Música andina colombiana"
            });
            db.TerritoriosSonoros.Add(new TerritorioSonoroRow
            {
                Id = 1,
                Nombre = "Andino"
            });

            // CELEBRA LA MUSICA. La base real la siembra `schema/V20260911_16`; aquí hace falta
            // porque el arnés construye SQLite desde el modelo de EF y no ejecuta los guiones. Es
            // la misma razón por la que se siembran la práctica musical y el territorio sonoro.
            db.ProyectosTransversales.Add(new ProyectoTransversalRow
            {
                Id = 1,
                Codigo = "celebra-la-musica",
                Nombre = "Celebra la Música",
                Activo = true,
                OrdenVisualizacion = 1,
                FechaCreacion = DateTime.UtcNow,
            });

            // LA ORGANIZACION INSTITUCIONAL (, 25 ago 2026). Ningun festival esta sin
            // nadie que responda por el: mientras ninguna organizacion de la comunidad lo
            // reclame, responde la institucion. La base real la crea seed/V20260519_03 y le pone
            // indice unico filtrado para que solo pueda haber una; aqui se siembra porque el
            // predicado de /festivales/coincidencias la resuelve por su marca, y sin ella esa
            // lista sale vacia y la prueba que la cubre no mide nada.
            db.EntityProfiles.Add(new EntityProfileRow
            {
                EntityType = "organizacion",
                Name = "Plan Nacional de Música para la Convivencia",
                CoverageLevel = "nacional",
                StatusCode = "activa",
                IsActive = true,
                IsInstitutional = true,
                CreatedByUserId = 1,
                CreatedAt = DateTime.UtcNow
            });

            db.SaveChanges();
        });
    }
}
