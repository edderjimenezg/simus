using System.Globalization;
using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Observability;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Correo;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public static class ExternalAuthEndpoints
{
    private static readonly PasswordHasher<UserRow> PasswordHasher = new();

    public static RouteGroupBuilder MapExternalAuthEndpoints(this RouteGroupBuilder group)
    {
        var external = group.MapGroup("/externo/auth").WithTags("externo-auth");

        // ANONIMA PORQUE SE CONSULTA ANTES DE QUE EXISTA LA CUENTA, y porque el art. 12 de la Ley
        // 1581 obliga a informar ANTES de pedir la autorización: detrás de una sesión, esa
        // información llegaría después de haberla dado. Devuelve los textos vigentes ENTEROS —no
        // enlaces a ellos— porque es lo que la pantalla muestra y lo que el servidor copiará dentro
        // de cada autorización, y tienen que ser lo mismo. El alta no queda disponible si falta
        // alguno de los textos exigidos o el catálogo territorial.
        external.MapGet("/register-preparation", async (PnmcDbContext dbContext, CancellationToken cancellationToken) =>
            Results.Ok(await GetRegistrationPreparationAsync(dbContext, cancellationToken))).AllowAnonymous();

        // ANONIMA A PROPOSITO (5 de 39). El alta del ecosistema: la organizacion, la persona que
        // responde por ella y su acceso, en UN SOLO ACTO. Quien se registra no tiene cuenta
        // todavia, por definicion, asi que esta ruta no puede ir detras de una sesion. No va
        // desnuda: valida la peticion entera, rechaza el correo duplicado con 409 y la limita
        // "external-register" por direccion de origen.
        //
        // (1) YA NO HAY CUENTA PERSONAL SUELTA. Antes esto creaba una persona y nada mas, y la
        //     organizacion venia despues por otra ruta con la sesion ya abierta. De ahi salian las
        //     dos mitades rotas: cuentas sin organizacion, que no podian hacer nada, y
        //     organizaciones sin nadie identificado —dbo.EntidadesResponsable llego a tener CERO
        //     filas frente a diecisiete entidades—. Ahora entran las tres cosas o no entra ninguna.
        //
        // (2) LA CUENTA NACE ACTIVA Y CON EL CORREO SIN CONFIRMAR, y eso es distinto de lo que
        //     habia. La pantalla /verify-email original pedia un codigo que no llegaba a ningun
        //     sitio: no habia ni una linea de envio de correo, el codigo solo viajaba en la
        //     respuesta bajo Development, no habia reenvio, y a los veinte minutos caducaba dejando
        //     ese correo inservible para siempre. Se retiro con la nota «vuelve el dia que haya
        //     remitente de verdad».
        //
        //     AHORA VUELVE, Y MONTADA ENTERA: testigo con hash, tres dias de vigencia, reenvio que
        //     invalida el anterior, y una ruta anonima que lo confirma. La diferencia que importa es
        //     que NO ENCIERRA A NADIE: confirmar no hace falta para entrar ni para preparar el
        //     registro, solo para entregarle algo al Programa. Lo unico que sigue faltando es el
        //     transporte, aislado en `IEnviadorDeCorreo`; el mensaje queda en la cola de salida con
        //     estado `pendiente`, que es la verdad.
        //
        // (3) TODO DENTRO DE UNA TRANSACCION. Son cinco escrituras en cuatro tablas —usuario, rol,
        //     entidad, vinculo y responsable— y antes iban en tres guardados sueltos sin
        //     transaccion. Bastaba con que el cliente cortase por tiempo de espera (20 s) entre el
        //     primero y el segundo para dejar un usuario sin rol y sin nada mas: correo quemado,
        //     y la persona solo habia leido «La solicitud tardo demasiado en responder».
        external.MapPost("/register", async (
            ExternalRegisterRequest request,
            PnmcDbContext dbContext,
            IEnviadorDeCorreo correo,
            CancellationToken cancellationToken) =>
        {
            var errors = await ValidateRegisterRequestAsync(request, dbContext, cancellationToken);
            var preparation = await GetRegistrationPreparationAsync(dbContext, cancellationToken);
            if (!preparation.RegistroDisponible)
                errors["registro"] = preparation.Impedimentos.ToArray();

            var finalidadesAceptadas = request.PoliticasAceptadas
                .Where(clave => !string.IsNullOrWhiteSpace(clave))
                .Select(clave => clave.Trim())
                .Where(FinalidadesDeDatos.EsConocida)
                .ToHashSet(StringComparer.Ordinal);

            // SE EXIGEN LAS OBLIGATORIAS Y SOLO ESAS. El boletín está en la lista de políticas que
            // el alta ofrece, pero no en la de exigidas: quien no lo marque se registra igual. Es
            // la diferencia entre una autorización para fines determinados —Ley 1581 art. 9— y una
            // casilla que hay que marcar para poder continuar, que no es una autorización libre.
            var faltantes = preparation.Politicas
                .Where(politica => politica.Obligatoria && !finalidadesAceptadas.Contains(politica.Clave))
                .ToList();
            if (faltantes.Count > 0)
                errors["politicasAceptadas"] =
                    [.. faltantes.Select(politica => $"Debes aceptar: {politica.Titulo}.")];
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var email = CorreoElectronico.Normalizar(request.Email);
            if (email is null)
            {
                // LA VALIDACION DE ARRIBA YA LO EXIGE, así que esto no es una defensa contra quien
                // se registra sino contra el futuro: si alguien afloja aquella regla, el fallo sale
                // aquí y no en forma de una cuenta creada sin correo, que es el estado del que
                // después no se sabe salir.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["El correo electrónico es obligatorio."],
                });
            }

            var existing = await dbContext.Users.AsNoTracking()
                .AnyAsync(user => user.Email == email, cancellationToken);
            if (existing)
            {
                return Results.Conflict(new { message = "Ya existe una cuenta registrada con ese correo." });
            }

            var role = await dbContext.Roles.FirstOrDefaultAsync(item => item.Name == "externo", cancellationToken)
                ?? throw new InvalidOperationException("No existe el rol externo.");

            var now = DateTime.UtcNow;
            var identidad = IdentidadDeResponsable.Desde(request);

            // LA TRANSACCION VA DENTRO DE LA ESTRATEGIA DE EJECUCION, Y NO AL REVES.
            // SQL Server esta configurado con reintentos, y `SqlServerRetryingExecutionStrategy`
            // se niega a trabajar sobre una transaccion abierta a mano: si algo falla a mitad no
            // sabria desde donde reintentar. Abrirla directamente devuelve un 500 con
            // InvalidOperationException, no un error de datos.
            //
            // ESTO NO SE VE CON SQLite, que es el motor de casi toda la suite: alli no hay
            // estrategia de reintentos y `BeginTransactionAsync` a secas funciona. La via de
            // SQL Server fue la que lo dijo. Es la misma leccion de siempre: lo que impone el
            // motor no se deduce leyendo el ORM.
            var estrategia = dbContext.Database.CreateExecutionStrategy();
            return await estrategia.ExecuteAsync(async () =>
            {
            await using var transaccion = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // 180 Y NO 240: es lo que mide dbo.Usuarios.NombreCompleto. Recortar a 240 contra una
            // columna de 180 convertia un nombre pegado de mas de 180 caracteres en un 500 por
            // truncamiento, es decir, en un limite que solo conocia la base.
            var user = new UserRow
            {
                FullName = ValidationHelpers.SanitizeText(identidad.NombreCompleto, 180),
                Email = email,
                AccessChannel = "externo",
                ProfileType = "organizacion",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);

            // LA FILA DE UsuariosRoles, QUE ES LO QUE DA EL ROL. Desde la transicion es lo unico:
            // `Usuarios.IdRol` ya no existe. Va DESPUES del guardado porque necesita el Id que EF
            // acaba de asignar a la fila nueva.
            await RolesDeUsuario.AsignarAsync(dbContext, user, [role], cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var entidad = await AltaDeOrganizacion.CrearAsync(
                dbContext,
                user,
                request.OrganizationName,
                request.OrganizationIdentificationNumber,
                request.Email,
                request.HeadquartersDepartmentCode,
                request.HeadquartersMunicipalityCode,
                identidad.NombreCompleto,
                identidad.TipoDocumento,
                identidad.NumeroDocumento,
                identidad.PrimerNombre,
                identidad.SegundoNombre,
                identidad.PrimerApellido,
                identidad.SegundoApellido,
                request.Phone,
                finalidadesAceptadas.Contains(FinalidadesDeDatos.Tratamiento),
                now,
                cancellationToken);

            // LAS AUTORIZACIONES, CON EL TEXTO COPIADO DENTRO. Van por `RegistroDeAutorizaciones`
            // y no escribiendo aquí la fila a mano: es la pieza que garantiza que toda autorización
            // —venga del alta, del boletín o de donde entre mañana— guarde lo mismo. Antes cada
            // puerta guardaba cosas distintas y ninguna guardaba las dos que hacen falta.
            //
            // DENTRO DE LA MISMA TRANSACCIÓN que el resto del alta, y por el mismo motivo que el
            // enlace de confirmación: una cuenta creada cuya autorización no llegó a escribirse es
            // una cuenta cuyos datos se están tratando sin constancia de permiso.
            foreach (var clave in FinalidadesDeDatos.Todas.Where(finalidadesAceptadas.Contains))
            {
                await RegistroDeAutorizaciones.OtorgarAsync(
                    dbContext, clave, "registro", now, user.Id, user.Email,
                    referenciaId: null, cancellationToken);
            }

            // EL BOLETÍN, SI LO PIDIÓ, TAMBIÉN ENTRA EN LA LISTA DE ENVÍO. La autorización dice que
            // puede recibirlo; la suscripción es lo que hace que lo reciba. Guardar solo la primera
            // dejaría a la persona autorizando algo que nunca le llega.
            if (finalidadesAceptadas.Contains(FinalidadesDeDatos.Boletin)
                && !await dbContext.BoletinSuscripciones.AnyAsync(item => item.CorreoElectronico == user.Email, cancellationToken))
            {
                dbContext.BoletinSuscripciones.Add(new()
                {
                    CorreoElectronico = user.Email,
                    Origen = "registro",
                    Estado = "activa",
                    FechaAlta = now,
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // EL ENLACE DE CONFIRMACION, DENTRO DE LA MISMA TRANSACCION que el alta. Si se emitiera
            // despues, un fallo entre medias dejaria una cuenta sin forma de confirmarse y sin
            // nadie que se enterara.
            await ConfirmacionDeCorreo.EmitirAsync(dbContext, correo, user, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaccion.CommitAsync(cancellationToken);

            return Results.Created($"/api/v1/externo/auth/users/{user.Id}", new ExternalRegisterResponse(
                user.Id.ToString(CultureInfo.InvariantCulture),
                user.Email,
                "activo",
                entidad.Id.ToString(CultureInfo.InvariantCulture),
                entidad.Name));
            });
        }).RequireRateLimiting("external-register").AllowAnonymous();

        // ANONIMA A PROPOSITO (6 de 39). La puerta del canal externo, gemela de
        // /admin/auth/login: sin ella nadie puede obtener jamas la cookie externa. El
        // manejador comprueba contrasena, que la cuenta este activa y que el rol sea
        // exactamente "externo" — un institucional no entra por aqui — y solo entonces
        // firma con SimusAuthentication.ExternalScheme. Lleva su propio limitador
        // "external-login" contra la fuerza bruta de credenciales.
        external.MapPost("/login", async (
            ExternalLoginRequest request,
            PnmcDbContext dbContext,
            HttpContext httpContext,
            ILoggerFactory registros,
            CancellationToken cancellationToken) =>
        {
            // MISMA INSTRUMENTACION QUE LA PUERTA INSTITUCIONAL. Hasta el 23 ago 2026 esta
            // puerta rechazaba en silencio: un relleno de credenciales contra las cuentas
            // externas —organizaciones y personas— era invisible, mientras la institucional
            // ya registraba motivo, correo seudonimizado y duracion. El correo nunca va en
            // claro: pasa por RedaccionDatosPersonales, como en AdminAuthEndpoints.
            var registro = registros.CreateLogger("PNMC.Autenticacion.Externa");
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var email = CorreoElectronico.Normalizar(request.Email);
            var correoSeudonimo = RedaccionDatosPersonales.Correo(email);
            if (ValidationHelpers.IsMissing(email) || ValidationHelpers.IsMissing(request.Password))
            {
                registro.LogWarning(
                    "Inicio de sesion externo rechazado. Motivo {Motivo}, correo {Correo}, {DuracionMs} ms",
                    "credenciales_incompletas",
                    correoSeudonimo,
                    reloj.ElapsedMilliseconds);
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["credentials"] = ["Correo y contrasena son obligatorios."]
                });
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
            // POR CONJUNTO, Y EXIGIENDO QUE `externo` ESTE DENTRO. No se compara el rol principal:
            // con N:M, una cuenta {externo, gestor_interno} tiene por principal `webmaster`/
            // `gestor_interno`, y compararlo contra "externo" la dejaria fuera de su propia
            // puerta. Esa combinacion no deberia existir —la prohibe la guarda U7 en la ruta de
            // asignacion, Permisos.MezclaExternoConInterno— pero esta puerta no depende de que la
            // otra haya hecho su trabajo.
            var rolesDelUsuario = user is null
                ? (IReadOnlyList<string>)[]
                : await RolesDeUsuario.ObtenerAsync(dbContext, user.Id, cancellationToken);
            var esExterno = rolesDelUsuario.Contains(Permisos.Externo, StringComparer.Ordinal);
            if (user is null
                || !user.IsActive
                || !esExterno
                || !IsPasswordValid(user, request.Password))
            {
                // El motivo va al registro, nunca a la respuesta: al cliente se le devuelve
                // el mismo 401 en los cuatro casos para no permitir enumerar cuentas.
                var motivo = user is null ? "usuario_inexistente"
                    : !user.IsActive ? "usuario_inactivo"
                    : !esExterno ? "rol_no_externo"
                    : "contrasena_incorrecta";
                registro.LogWarning(
                    "Inicio de sesion externo rechazado. Motivo {Motivo}, correo {Correo}, {DuracionMs} ms",
                    motivo,
                    correoSeudonimo,
                    reloj.ElapsedMilliseconds);
                return Results.Unauthorized();
            }

            // SE MIRA ANTES DE PISARLO. `LastLoginAt` es el unico rastro de si esta cuenta habia
            // entrado alguna vez, y la linea siguiente lo sobrescribe: leerlo despues daria siempre
            // «ya habia entrado». Es lo que sostiene la bienvenida del §15.2 sin columna nueva.
            var esPrimerIngreso = user.LastLoginAt is null;

            var now = DateTime.UtcNow;
            user.LastLoginAt = now;
            user.UpdatedAt = now;
            await WriteAuditAsync(dbContext, user.Id, "Usuarios", user.Id.ToString(), "iniciar_sesion_externa", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await httpContext.SignInAsync(
                SimusAuthentication.ExternalScheme,
                BuildExternalPrincipal(user),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                });

            return Results.Ok(await ToSessionResponseAsync(dbContext, user, cancellationToken, esPrimerIngreso));
        }).RequireRateLimiting("external-login").AllowAnonymous();

        external.MapGet("/me", async (
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var user = await ResolveExternalUserAsync(principal, dbContext, cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(await ToSessionResponseAsync(dbContext, user, cancellationToken));
        }).RequireAuthorization(SimusAuthentication.ExternalPolicy);

        external.MapPost("/logout", async (
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var user = await ResolveExternalUserAsync(principal, dbContext, cancellationToken);
            if (user is not null)
            {
                // Un borrador de alta no es una ficha persistente: al cerrar sesión se descarta
                // para que una nueva entrada no reabra un formulario anterior por sorpresa.
                var borradoresAbiertos = await dbContext.BorradoresDeProceso
                    .Where(item => item.PersonaId == user.Id && item.Estado == "borrador")
                    .ToListAsync(cancellationToken);
                foreach (var borrador in borradoresAbiertos)
                {
                    borrador.Estado = "cancelado";
                    borrador.FechaActualizacion = DateTime.UtcNow;
                }
                await WriteAuditAsync(dbContext, user.Id, "Usuarios", user.Id.ToString(), "cerrar_sesion_externa", cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await httpContext.SignOutAsync(SimusAuthentication.ExternalScheme);
            return Results.NoContent();
        }).RequireAuthorization(SimusAuthentication.ExternalPolicy);

        return group;
    }

    /// <summary>
    /// Valida el alta entera: la persona responsable, su acceso y la organizacion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SE DEVUELVEN TODOS LOS ERRORES A LA VEZ, no el primero. El formulario es largo —organizacion,
    /// persona y acceso en una sola pantalla— y contestar de uno en uno obliga a enviarlo otras
    /// tantas veces para descubrir cuantos fallos habia.
    /// </para>
    /// <para>
    /// LA CEDULA SE MIDE SOBRE LOS DIGITOS, no sobre lo tecleado. Quien la escribe la copia del
    /// documento, con puntos, y el limite de 6 a 15 se refiere a los digitos: contar los puntos
    /// rechazaria cedulas validas escritas como se leen.
    /// </para>
    /// </remarks>
    private static async Task<Dictionary<string, string[]>> ValidateRegisterRequestAsync(
        ExternalRegisterRequest request,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // ---- La persona que responde ----
        var identidad = IdentidadDeResponsable.Desde(request);
        if (identidad.EsSegmentada)
        {
            if (ValidationHelpers.IsMissing(identidad.PrimerNombre)) errors["firstName"] = ["El primer nombre es obligatorio."];
            if (ValidationHelpers.IsMissing(identidad.PrimerApellido)) errors["firstSurname"] = ["El primer apellido es obligatorio."];
        }
        else if (ValidationHelpers.IsMissing(identidad.NombreCompleto))
        {
            errors["fullName"] = ["El nombre de la persona responsable es obligatorio."];
        }

        if (!ExternalOrganizationEndpoints.TiposDeDocumento.ContainsKey(identidad.TipoDocumento))
            errors[identidad.EsSegmentada ? "documentType" : "numeroDocumento"] = ["El tipo de documento no pertenece al catálogo."];

        var documento = AltaDeOrganizacion.NormalizarDocumento(identidad.NumeroDocumento);
        if (documento.Length is < 6 or > 15)
            errors[identidad.EsSegmentada ? "documentNumber" : "numeroDocumento"] = ["El número de documento debe tener entre 6 y 15 dígitos."];

        // ---- El acceso ----
        // EL CORREO NO SE COMPRUEBA AQUI. Es uno solo —el institucional de la organizacion, que es
        // tambien el de acceso— y lo mira ValidarAsync mas abajo, que ya tenia que validarlo de
        // todos modos. Comprobarlo en los dos sitios devolvia dos errores para un unico fallo.
        var errorContrasena = PoliticaDeContrasena.Error(request.Password, request.Email,
            [identidad.PrimerNombre, identidad.SegundoNombre, identidad.PrimerApellido, identidad.SegundoApellido, identidad.NombreCompleto]);
        if (errorContrasena is not null) errors["password"] = [errorContrasena];

        // LO QUE SE ACEPTA NO SE VALIDA AQUI. Se comprueba contra las politicas VIGENTES que el
        // servidor sirvio, en la propia ruta del alta: cuales son obligatorias lo dice el catalogo
        // y no una lista escrita a mano en dos sitios. Antes habia aqui dos booleanos fijos que
        // nombraban dos documentos concretos, y el dia que apareciera un tercero seguirian
        // diciendo que con esos dos bastaba.

        // ---- La organizacion, con las mismas reglas que la otra ruta que las crea ----
        await AltaDeOrganizacion.ValidarAsync(
            errors,
            "organization",
            "email",
            request.OrganizationName,
            request.OrganizationIdentificationNumber,
            request.Email,
            dbContext,
            cancellationToken);

        await AltaDeOrganizacion.ValidarSedeAsync(
            errors, request.HeadquartersDepartmentCode, request.HeadquartersMunicipalityCode,
            dbContext, cancellationToken);

        return errors;
    }

    private static async Task<PreparacionDeRegistroDto> GetRegistrationPreparationAsync(
        PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var vigentes = await dbContext.PoliticasDeDatos.AsNoTracking()
            .Where(politica => politica.Vigente)
            .ToListAsync(cancellationToken);

        // EL ORDEN LO PONE EL CATALOGO. Primero lo que sostiene la cuenta, al final lo voluntario:
        // es el orden en que la pantalla las presenta y en el que tienen sentido leidas seguidas.
        var politicas = FinalidadesDeDatos.Todas
            .Select(clave => vigentes.FirstOrDefault(politica => politica.Clave == clave))
            .Where(politica => politica is not null)
            .Select(politica => new PoliticaParaRegistroDto(
                politica!.Clave, politica.Version, politica.Titulo, politica.Texto,
                politica.UrlOficial, politica.ReferenciaOficial,
                Array.IndexOf(FinalidadesDeDatos.ExigidasEnElAlta, politica.Clave) >= 0))
            .ToList();

        var impedimentos = new List<string>();

        // FALTA UNA OBLIGATORIA -> NO HAY ALTA, y esto no es rigidez. Registrar a alguien sin texto
        // que copiar dejaria una cuenta creada y una autorizacion vacia: datos personales tratados
        // sin constancia de que se informara de nada. Es preferible que el formulario diga que no
        // esta disponible.
        if (FinalidadesDeDatos.ExigidasEnElAlta.Any(clave => politicas.All(politica => politica.Clave != clave)))
            impedimentos.Add("Los textos de autorización vigentes no están disponibles.");

        if (!await dbContext.DivipolaLocations.AsNoTracking().AnyAsync(cancellationToken))
            impedimentos.Add("El catálogo territorial no está disponible.");

        return new(impedimentos.Count == 0, politicas, impedimentos);
    }


    private static string Clean(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static bool IsPasswordValid(UserRow user, string password)
    {
        try
        {
            return PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed record IdentidadDeResponsable(
        string PrimerNombre,
        string? SegundoNombre,
        string PrimerApellido,
        string? SegundoApellido,
        string TipoDocumento,
        string NumeroDocumento,
        bool EsSegmentada)
    {
        public string NombreCompleto => string.Join(' ', new[] { PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido }
            .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!.Trim()));

        public static IdentidadDeResponsable Desde(ExternalRegisterRequest solicitud)
        {
            var segmentada = !string.IsNullOrWhiteSpace(solicitud.FirstName)
                || !string.IsNullOrWhiteSpace(solicitud.FirstSurname)
                || !string.IsNullOrWhiteSpace(solicitud.DocumentType)
                || !string.IsNullOrWhiteSpace(solicitud.DocumentNumber);
            if (segmentada)
                return new(solicitud.FirstName, solicitud.SecondName, solicitud.FirstSurname, solicitud.SecondSurname,
                    (solicitud.DocumentType ?? string.Empty).Trim().ToLowerInvariant(), solicitud.DocumentNumber, true);

            return new(solicitud.FullName, null, string.Empty, null, "cc", solicitud.NumeroDocumento, false);
        }
    }

    private static ClaimsPrincipal BuildExternalPrincipal(UserRow user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, "externo"),
            new(SimusAuthentication.AccessScopeClaim, SimusAuthentication.ExternalScope)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, SimusAuthentication.ExternalScheme));
    }

    private static async Task<UserRow?> ResolveExternalUserAsync(
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
        {
            return null;
        }

        // POR dbo.UsuariosRoles. Con la columna escalar este Join devolvia una fila como
        // mucho; con N:M devolveria una por rol, asi que el filtro va sobre la tabla de asignacion
        // y el `Any` deja el resultado en «existe alguna asignacion a externo», que es la
        // pregunta que la ruta quiere hacer.
        return await dbContext.Users
            .Where(user => user.Id == userId
                && user.IsActive
                && dbContext.UsuariosRoles
                    .Join(dbContext.Roles, asignacion => asignacion.RoleId, role => role.Id, (asignacion, role) => new { asignacion.UserId, role.Name })
                    .Any(par => par.UserId == user.Id && par.Name == "externo"))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// La sesion tal como la ve el sitio: quien es, y que organizaciones administra.
    /// </summary>
    /// <remarks>
    /// SOLO LOS VINCULOS ACTIVOS. `UsuariosEntidades.Activo` es como se retira a alguien de una
    /// organizacion sin borrar el historial; devolver tambien los inactivos pondria en su panel
    /// organizaciones que ya no puede tocar, y el primer boton que pulsara daria 403.
    ///
    /// ORDENADAS POR NOMBRE para que el panel no cambie de organizacion principal entre dos cargas:
    /// sin ORDER BY, el motor puede devolverlas en cualquier orden.
    /// </remarks>
    private static async Task<ExternalSessionResponse> ToSessionResponseAsync(
        PnmcDbContext dbContext,
        UserRow user,
        CancellationToken cancellationToken,
        bool esPrimerIngreso = false)
    {
        var organizaciones = await dbContext.UserEntities.AsNoTracking()
            // La sesión alimenta el selector de contexto de Gestión. Debe enumerar exactamente
            // las organizaciones que sus endpoints permiten administrar: ni vínculos inactivos
            // ni roles de contacto/colaboración que solo podrían conducir a un 403 posterior.
            .Where(vinculo => vinculo.UserId == user.Id
                && vinculo.IsActive
                && RolesDeEntidad.QueResponden.Contains(vinculo.EntityRole))
            .Join(
                dbContext.EntityProfiles.AsNoTracking().Where(entidad => entidad.IsActive),
                vinculo => vinculo.EntityId,
                entidad => entidad.Id,
                (vinculo, entidad) => new { entidad.Id, entidad.Name, vinculo.EntityRole })
            .OrderBy(fila => fila.Name)
            .ToListAsync(cancellationToken);

        return new ExternalSessionResponse(
            user.Id.ToString(CultureInfo.InvariantCulture),
            user.FullName,
            user.Email,
            user.IsActive ? "activo" : "inactivo",
            organizaciones
                .Select(fila => new ExternalSessionOrganization(
                    fila.Id.ToString(CultureInfo.InvariantCulture),
                    fila.Name,
                    fila.EntityRole))
                .ToList(),
            user.CorreoConfirmado,
            esPrimerIngreso);
    }

    private static Task WriteAuditAsync(
        PnmcDbContext dbContext,
        int userId,
        string tableName,
        string recordId,
        string action,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLogRow
        {
            UserId = userId,
            TableName = tableName,
            RecordId = recordId,
            Action = action,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
