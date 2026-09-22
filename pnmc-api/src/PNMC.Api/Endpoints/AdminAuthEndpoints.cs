using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;
using PNMC.Api.Observability;
using PNMC.Api.Security;

namespace PNMC.Api.Endpoints;

public static class AdminAuthEndpoints
{
    private static readonly PasswordHasher<UserRow> PasswordHasher = new();

    /// <summary>
    /// PNMC-039. Categoria de registro de la puerta institucional. Uno de los tres endpoints
    /// instrumentados como patron: la autenticacion es donde primero se pregunta "quien entro y
    /// desde cuando", y hasta hoy la respuesta era que no se sabia.
    /// </summary>
    private const string CategoriaRegistro = "PNMC.Autenticacion";

    public static RouteGroupBuilder MapAdminAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/admin/auth").WithTags("admin-auth");

        // ANONIMA A PROPOSITO (3 de 39). Es la puerta: exigir sesion para poder iniciar
        // sesion es un candado con la llave dentro. La llama AdminService.loginAdmin
        // (pnmc-web/src/app/core/services/admin.service.ts) cuando todavia no hay
        // cookie. No queda indefensa: el propio manejador valida correo y contrasena,
        // rechaza al usuario inactivo y al del rol "externo" con 401, y solo entonces
        // firma la cookie institucional.
        //
        // PNMC-003: anonima no quiere decir gratis. Hasta este parche, comprobar una
        // contrasena aqui costaba una peticion y nada mas, sin tope de ninguna clase:
        // la puerta gemela del canal externo llevaba RequireRateLimiting("external-login")
        // y esta, la de la consola de administracion, no llevaba ninguna. El cupo
        // "admin-login" (20 por minuto y por direccion de origen) esta razonado en
        // Program.cs, en CreateAdminLoginRateLimitPartition; el numero es alto a
        // proposito porque una alcaldia entera comparte una sola IP publica.
        auth.MapPost("/login", async (
            AdminLoginRequest request,
            PnmcDbContext dbContext,
            HttpContext httpContext,
            ILoggerFactory registros,
            CancellationToken cancellationToken) =>
        {
            // PNMC-039. PATRON DE INSTRUMENTACION, ENDPOINT DE AUTENTICACION.
            //
            // QUE SE REGISTRA: el resultado (concedido / rechazado), el MOTIVO del rechazo, el
            // seudonimo del correo, el identificador de fila del usuario cuando existe, y cuanto
            // tardo. Con eso se responde a las tres preguntas que se hacen de verdad un lunes:
            // "¿entro alguien?", "¿es la misma cuenta la de los catorce intentos de las 03:00?"
            // y "¿por que le rebota a esta persona?".
            //
            // QUE NO SE REGISTRA, Y ES LO IMPORTANTE: el correo en claro y la contrasena. El
            // correo sale como [correo:#huella] — misma huella para la misma cuenta dentro del
            // proceso, asi que los catorce intentos se agrupan sin que el registro sepa de quien
            // son. La contrasena no se pasa a ninguna sentencia de registro, ni siquiera su
            // longitud: la longitud es informacion sobre la contrasena.
            //
            // POR QUE UN SOLO SEUDONIMO CALCULADO ARRIBA: para que todas las salidas de este
            // manejador usen exactamente la misma huella y se puedan cruzar entre si.
            var registro = registros.CreateLogger(CategoriaRegistro);
            var reloj = Stopwatch.StartNew();

            var email = CorreoElectronico.Normalizar(request.Email);
            var correoSeudonimo = RedaccionDatosPersonales.Correo(email);

            if (ValidationHelpers.IsMissing(email) || ValidationHelpers.IsMissing(request.Password))
            {
                registro.LogWarning(
                    "Inicio de sesion institucional rechazado. Motivo {Motivo}, correo {Correo}, {DuracionMs} ms",
                    "credenciales_incompletas",
                    correoSeudonimo,
                    reloj.ElapsedMilliseconds);

                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["credentials"] = ["Correo y contrasena son obligatorios."]
                });
            }

            var user = await FindUserByEmailAsync(dbContext, email, cancellationToken);
            if (user is null || !user.IsActive || !IsPasswordValid(user, request.Password))
            {
                // El motivo se distingue en el registro pero NO en la respuesta: quien llama
                // recibe el mismo 401 en los tres casos, para no confirmarle que un correo
                // existe. El registro puede ser mas preciso que la respuesta porque el registro
                // lo lee quien opera el sistema, no quien lo esta atacando.
                var motivo = user is null
                    ? "usuario_inexistente"
                    : !user.IsActive ? "usuario_inactivo" : "contrasena_incorrecta";

                registro.LogWarning(
                    "Inicio de sesion institucional rechazado. Motivo {Motivo}, correo {Correo}, {DuracionMs} ms",
                    motivo,
                    correoSeudonimo,
                    reloj.ElapsedMilliseconds);

                return Results.Unauthorized();
            }

            // LOS ROLES SALEN DE dbo.UsuariosRoles, NO DE Usuarios.IdRol.
            // Una cuenta sin ninguna asignacion NO entra, y no hay respaldo a la columna vieja:
            // ver Security/RolesDeUsuario.cs sobre por que un respaldo silencioso convertiria la
            // su retirada en un fallo invisible.
            var roles = await RolesDeUsuario.ObtenerAsync(dbContext, user.Id, cancellationToken);
            if (roles.Count == 0)
            {
                registro.LogWarning(
                    "Inicio de sesion institucional rechazado. Motivo {Motivo}, correo {Correo}, usuario {UsuarioId}, {DuracionMs} ms",
                    "sin_rol_valido",
                    correoSeudonimo,
                    user.Id,
                    reloj.ElapsedMilliseconds);

                return Results.Problem("El usuario no tiene un rol administrativo valido.", statusCode: StatusCodes.Status403Forbidden);
            }

            // LISTA BLANCA, NO LISTA NEGRA. Hasta esta puerta decia
            // `== "externo"`: rechazaba un nombre y admitia todo lo demas. Con seis roles en el
            // catalogo eso equivalia por accidente a admitir a los internos, pero admitia
            // tambien a los tres aliado_*, que no eran internos, y habria admitido a cualquier
            // rol nuevo que nadie se acordara de añadir a la lista negra. Ahora pregunta por
            // Permisos.RolesInternos, que es la MISMA lista que exige PoliticaFuncionario y la
            // misma de la que sale IsAssignableGlobalRole: lo que no esta nombrado, no entra.
            if (!Permisos.AlgunoEsRolInterno(roles))
            {
                registro.LogWarning(
                    "Inicio de sesion institucional rechazado. Motivo {Motivo}, correo {Correo}, usuario {UsuarioId}, {DuracionMs} ms",
                    "rol_no_interno_en_puerta_institucional",
                    correoSeudonimo,
                    user.Id,
                    reloj.ElapsedMilliseconds);

                return Results.Unauthorized();
            }

            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, user.Id, "Usuarios", user.Id.ToString(), "iniciar_sesion", cancellationToken);

            var principal = BuildPrincipal(user, roles);
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                });

            if (registro.IsEnabled(LogLevel.Information))
            {
                registro.LogInformation(
                    "Inicio de sesion institucional concedido. Correo {Correo}, usuario {UsuarioId}, roles {Roles}, {DuracionMs} ms",
                    correoSeudonimo,
                    user.Id,
                    string.Join(',', roles),
                    reloj.ElapsedMilliseconds);
            }

            return Results.Ok(new AdminAuthResponse(ToDto(user, roles)));
        }).RequireRateLimiting("admin-login").AllowAnonymous();

        // ANONIMA A PROPOSITO (4 de 39). Aunque el nombre suene administrativo, es la
        // sonda de sesion del SPA: AdminService.fetchAdminMe
        // (pnmc-web/src/app/core/services/admin.service.ts) la llama al arrancar la
        // consola, justo para averiguar si hay sesion, es decir cuando todavia puede no
        // haberla. Su respuesta no depende de que la ruta este abierta: el manejador
        // resuelve el usuario desde el ClaimsPrincipal y devuelve 401 si no hay ninguno
        // — o si la fila ya no existe, o si el usuario fue desactivado, casos que la
        // cookie por si sola no detectaria. Dejarla anonima conserva ese 401 propio en
        // vez de delegarlo al middleware; lo que se filtra sin sesion es exactamente
        // nada.
        auth.MapGet("/me", async (
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var current = await ResolveCurrentUserAsync(principal, dbContext, cancellationToken);
            return current is null
                ? Results.Unauthorized()
                : Results.Ok(new AdminAuthResponse(ToDto(current.Value.User, current.Value.Roles)));
        }).AllowAnonymous();

        auth.MapPut("/profile", async (
            UpdateProfileRequest request,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = GetCurrentUserId(principal);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            // ACTIVO, NO SOLO EXISTENTE. Esta busqueda ignoraba `Activo`, y al final del manejador
            // hay un SignInAsync que re-emite un tiquete fresco de 8 h: una cuenta ya desactivada
            // podia renovarse la sesion por aqui indefinidamente. Con la revalidacion cableada la
            // peticion ya no llegaria hasta aqui —el principal se rechaza antes—, pero la guarda
            // se pone igual: una ruta que re-emite credenciales no debe depender de que otra capa
            // haya hecho su trabajo.
            var user = await dbContext.Users.FirstOrDefaultAsync(
                item => item.Id == userId.Value && item.IsActive, cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            if (ValidationHelpers.IsMissing(request.FullName) || ValidationHelpers.IsMissing(request.Email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["profile"] = ["Nombre y correo son obligatorios."]
                });
            }

            var email = CorreoElectronico.Normalizar(request.Email);
            if (!ValidationHelpers.IsValidEmail(email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["El correo electronico no es valido."]
                });
            }

            var existingUser = await dbContext.Users.FirstOrDefaultAsync(item => item.Email == email && item.Id != user.Id, cancellationToken);
            if (existingUser is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["El correo electronico ya esta en uso por otro usuario."]
                });
            }

            user.FullName = request.FullName.Trim();
            user.Email = email;
            user.Telefono = request.Telefono?.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                if (request.Password.Length < 10)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["password"] = ["La contrasena debe tener minimo 10 caracteres."]
                    });
                }
                user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var roles = await RolesDeUsuario.ObtenerAsync(dbContext, user.Id, cancellationToken);
            // LA MISMA LISTA BLANCA QUE LA PUERTA. `Permisos.EsRolInterno` solo vivia en el login
            // (:131). Como aqui abajo se firma una cookie institucional nueva, sin repetirlo esta
            // ruta era una segunda puerta con menos guardas que la primera: a quien le hubieran
            // cambiado el rol a uno no interno, esto le renovaba el pase institucional.
            if (!Permisos.AlgunoEsRolInterno(roles))
            {
                return Results.Unauthorized();
            }

            var newPrincipal = BuildPrincipal(user, roles);
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                newPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = true
                });

            // EL VERBO ES `actualizar`, NO `actualizar_perfil`. CK_BitacoraAuditoria_Accion
            // admite trece verbos tecnicos y `actualizar_perfil` no esta entre ellos: hasta el
            // 23 ago 2026 esta ruta guardaba el perfil, re-emitia la cookie y DESPUES moria con
            // 500 al insertar la bitacora contra SQL Server. La suite de SQLite no tiene ese
            // CHECK y lo daba por bueno. El nombre del evento viaja en ValoresNuevos, como en
            // el resto de rutas que distinguen evento funcional de verbo tecnico.
            await WriteAuditAsync(
                dbContext,
                user.Id,
                "Usuarios",
                user.Id.ToString(),
                AccionesAuditoria.Actualizar,
                cancellationToken,
                JsonSerializer.Serialize(new { evento = "actualizar_perfil", user.FullName, user.Email, user.Telefono })
            );

            return Results.Ok(new AdminAuthResponse(ToDto(user, roles)));
        }).RequireAuthorization();

        auth.MapPost("/logout", async (
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var current = await ResolveCurrentUserAsync(principal, dbContext, cancellationToken);
            if (current is not null)
            {
                await WriteAuditAsync(dbContext, current.Value.User.Id, "Usuarios", current.Value.User.Id.ToString(), "cerrar_sesion", cancellationToken);
            }

            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        // EL PRIMER INGRESO, EN DOS PASOS Y EN ESTE ORDEN. Una cuenta administrativa se entrega con
        // correo y contraseña por omisión; mientras no la cambie, esa credencial la conocen dos
        // personas. Después dice quién es. Lo decidió la dirección de producto de
        // 2026 y describe una creación deliberadamente controlada: los módulos se deciden ANTES de
        // entregar la cuenta.
        auth.MapPost("/mi-contrasena", async (
            CambioDeContrasenaPropia solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var id = GetCurrentUserId(principal);
            if (id is null) return Results.Unauthorized();

            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
            if (user is null) return Results.Unauthorized();

            // LA ACTUAL SE COMPRUEBA AUNQUE HAYA SESION. Una sesión abierta en un equipo prestado no
            // puede bastar para cambiar la contraseña: quien la cambia tiene que saber cuál es.
            if (PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, solicitud.Actual ?? string.Empty)
                == PasswordVerificationResult.Failed)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["actual"] = ["La contraseña actual no es la correcta."]
                });
            }

            var nueva = solicitud.Nueva ?? string.Empty;
            if (nueva.Length < 10)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nueva"] = ["La contraseña debe tener mínimo 10 caracteres."]
                });
            }

            // NO PUEDE SER LA MISMA. Si lo fuera, el paso quedaría dado sin haber cambiado nada y la
            // credencial que otra persona eligió seguiría siendo la buena.
            if (PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, nueva) != PasswordVerificationResult.Failed)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["nueva"] = ["La contraseña nueva tiene que ser distinta de la actual."]
                });
            }

            user.PasswordHash = PasswordHasher.HashPassword(user, nueva);
            user.DebeCambiarContrasena = false;
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { debeCambiarContrasena = false, perfilCompletado = user.PerfilCompletado });
        })
        .WithName("CambiarMiContrasena")
        .RequireAuthorization(Permisos.PoliticaFuncionario);

        auth.MapPost("/mi-perfil", async (
            PerfilPropio solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var id = GetCurrentUserId(principal);
            if (id is null) return Results.Unauthorized();

            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
            if (user is null) return Results.Unauthorized();

            // EL ORDEN IMPORTA Y SE APLICA AQUI, no solo en la pantalla: primero la contraseña.
            if (user.DebeCambiarContrasena)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["contrasena"] = ["Antes de completar tu perfil tienes que cambiar la contraseña."]
                });
            }

            var errores = new Dictionary<string, string[]>();
            var primerNombre = (solicitud.PrimerNombre ?? string.Empty).Trim();
            var primerApellido = (solicitud.PrimerApellido ?? string.Empty).Trim();
            var identificacion = (solicitud.Identificacion ?? string.Empty).Trim();
            var tipo = (solicitud.TipoDocumento ?? string.Empty).Trim().ToLowerInvariant();

            // LOS OBLIGATORIOS SON LOS QUE IDENTIFICAN. El segundo nombre y el segundo apellido no
            // los tiene todo el mundo; el primero de cada uno, sí.
            if (primerNombre.Length == 0) errores["primerNombre"] = ["Falta el primer nombre."];
            if (primerApellido.Length == 0) errores["primerApellido"] = ["Falta el primer apellido."];
            if (identificacion.Length == 0) errores["identificacion"] = ["Falta el número de documento."];
            if (tipo.Length == 0) errores["tipoDocumento"] = ["Falta el tipo de documento."];
            else if (!await dbContext.TiposDocumento.AsNoTracking().AnyAsync(t => t.Codigo == tipo && t.Activo, cancellationToken))
            {
                errores["tipoDocumento"] = ["Ese tipo de documento no pertenece al catálogo."];
            }

            if (errores.Count > 0) return Results.ValidationProblem(errores);

            user.PrimerNombre = primerNombre;
            user.SegundoNombre = Opcional(solicitud.SegundoNombre);
            user.PrimerApellido = primerApellido;
            user.SegundoApellido = Opcional(solicitud.SegundoApellido);
            user.Identificacion = identificacion;
            user.CodigoTipoDocumento = tipo;
            user.Telefono = Opcional(solicitud.Telefono);
            // EL NOMBRE COMPLETO SE COMPONE Y NO SE PIDE APARTE: pedirlo dos veces es dejar que
            // discrepen. Lo leen la bitácora, las fichas y media consola.
            user.FullName = NombreCompuesto(user);
            user.PerfilCompletado = true;
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { debeCambiarContrasena = false, perfilCompletado = true, nombreCompleto = user.FullName });
        })
        .WithName("CompletarMiPerfil")
        .RequireAuthorization(Permisos.PoliticaFuncionario);

        auth.MapGet("/users", async (
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (!UserHasRole(principal, "webmaster"))
            {
                return Results.Forbid();
            }

            // DOS CONSULTAS FIJAS, PASE LO QUE PASE. Con la columna escalar esto era un Join y
            // una sola ida a la base. La traduccion ingenua a N:M —pedir los roles dentro del
            // bucle— convierte la pantalla en 1 + N consultas, que es el defecto que
            // ContadorDeConsultas vigila en otras rutas. RolesDeUsuario.ObtenerDeVariosAsync
            // resuelve todos los roles de golpe.
            var userRows = await dbContext.Users.AsNoTracking()
                .OrderByDescending(user => user.IsActive)
                .ThenBy(user => user.FullName)
                .ToListAsync(cancellationToken);

            var rolesPorUsuario = await RolesDeUsuario.ObtenerDeVariosAsync(
                dbContext,
                userRows.Select(user => user.Id).ToList(),
                cancellationToken);

            // EL CATALOGO ENTERO EN UNA CONSULTA, no una por cuenta. Son ocho filas que no cambian:
            // pedirlas veintidós veces para resolver veintidós etiquetas sería el mismo defecto que
            // el proyecto ya corrigió en la bitácora.
            var tiposDeDocumento = await dbContext.TiposDocumento.AsNoTracking()
                .ToDictionaryAsync(t => t.Codigo, t => t.Nombre, StringComparer.OrdinalIgnoreCase, cancellationToken);

            // LOS APARTADOS DE TODAS LAS CUENTAS, EN UNA CONSULTA. Quién puede abrir qué es la
            // pregunta de esta pantalla, y respondía solo cuenta por cuenta: había que abrir la
            // ficha de cada una. Pedirlo por fila seria 1 + N consultas, que es el defecto que
            // `ContadorDeConsultas` vigila.
            var concedidosPorCuenta = (await dbContext.ModulosPorCuenta.AsNoTracking()
                .Select(x => new { x.IdUsuario, x.CodigoModulo })
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.IdUsuario)
                .ToDictionary(g => g.Key, g => g.Select(x => x.CodigoModulo).ToList());

            var users = new List<AdminUserDto>(userRows.Count);
            foreach (var item in userRows)
            {
                var suyos = rolesPorUsuario.TryGetValue(item.Id, out var asignados) ? asignados : [];
                // UNA CUENTA SIN ROLES SE LISTA IGUAL, con la lista vacia. Ocultarla dejaria al
                // webmaster sin forma de arreglarla desde la consola: no la veria para editarla.
                users.Add(ToDto(
                    item,
                    suyos,
                    EtiquetaDeTipo(tiposDeDocumento, item.CodigoTipoDocumento),
                    ApartadosDe(
                        suyos,
                        concedidosPorCuenta.TryGetValue(item.Id, out var concedidos) ? concedidos : [])));
            }

            return Results.Ok(users);
        }).RequireAuthorization();

        auth.MapPost("/users", async (
            AdminUserUpsertRequest request,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            RevalidacionDeSesion revalidacion,
            CancellationToken cancellationToken) =>
        {
            if (!UserHasRole(principal, "webmaster"))
            {
                return Results.Forbid();
            }

            // `Roles` MANDA SOBRE `Role` CUANDO VIENE. No se unen: unir convertiria un envio de
            // dos roles en uno de tres sin que nadie lo pidiera. Ver AdminUserUpsertRequest.
            var rolesPedidos = RolesDeUsuario.Ordenar(
                request.Roles is { Count: > 0 } ? request.Roles : [request.Role]);

            if (ValidationHelpers.IsMissing(request.FullName)
                || ValidationHelpers.IsMissing(request.Email)
                || rolesPedidos.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["user"] = ["Nombre, correo y rol son obligatorios."]
                });
            }

            if (rolesPedidos.Any(nombre => !IsAssignableGlobalRole(nombre)))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = ["El rol indicado no puede asignarse desde usuarios globales."]
                });
            }

            // LA GUARDA DEL DEFECTO U7, y la condicion escrita que bloqueaba la transicion.
            // {externo, gestor_interno} solo es representable desde que los roles son un
            // conjunto, y esa persona tendria a la vez la cookie institucional y la del
            // ecosistema: seria la revisada y quien revisa. Se cierra AQUI, donde el estado se
            // crea, y no en cada puerta, porque cerrarlo en las puertas obliga a acordarse en
            // todas las que existan y en todas las que vengan. Ver Permisos.MezclaExternoConInterno.
            if (Permisos.MezclaExternoConInterno(rolesPedidos))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] =
                    [
                        "Una cuenta no puede tener a la vez el rol externo y un rol interno del "
                        + "Ministerio: son dos ambitos de acceso distintos."
                    ]
                });
            }

            var roleRows = await dbContext.Roles
                .Where(item => rolesPedidos.Contains(item.Name.ToLower()))
                .ToListAsync(cancellationToken);
            if (roleRows.Count != rolesPedidos.Count)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = ["El rol indicado no existe."]
                });
            }

            var email = CorreoElectronico.Normalizar(request.Email);
            var existing = await FindUserByEmailAsync(dbContext, email, cancellationToken);
            if (existing is not null
                && (!int.TryParse(request.Id, out var requestId) || existing.Id != requestId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["Ya existe un usuario con ese correo."]
                });
            }

            UserRow user;
            var isNew = !int.TryParse(request.Id, out var id) || id <= 0;
            if (isNew)
            {
                if (ValidationHelpers.IsMissing(request.Password) || request.Password.Length < 10)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["password"] = ["La contrasena inicial debe tener minimo 10 caracteres."]
                    });
                }

                user = new UserRow
                {
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Users.Add(user);
            }
            else
            {
                user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                    ?? throw new InvalidOperationException("Usuario no encontrado.");
            }

            // NINGUNA EDICION PUEDE DEJAR LA CONSOLA SIN ADMINISTRADOR.
            //
            // `DELETE /users/{id}` no borra nada: hace `IsActive = false`, y lo protege con dos
            // guardas. Esta ruta hacia lo mismo —y mas— sin ninguna. No era un rodeo teorico: el
            // panel desactiva por aqui, no por DELETE (`saveAdminUser` en admin.service.ts), asi
            // que el camino guardado era el que nadie usa.
            //
            // Y hay DOS formas de perder un webmaster, no una: desactivarlo (`isActive: false`) y
            // cambiarle el rol (la asignacion de abajo, que tampoco tenia guarda). La
            // segunda es la que de verdad deja la consola huerfana, porque la de la propia cuenta
            // no la cubre: el ultimo webmaster puede degradarse a si mismo a gestor y nadie —el
            // incluido— vuelve a entrar al panel. Salir de ese estado no esta en el producto:
            // hay que entrar a la base de datos.
            //
            // Por eso la guarda no pregunta «¿esto es un borrado?» sino «¿esta operacion deja de
            // haber webmasters que puedan entrar?».
            // POR CONJUNTO, NO POR ROL UNICO. Con N:M «era webmaster» significa «webmaster
            // estaba ENTRE sus roles», y «seguira siendolo» significa que sigue estando entre los
            // que se piden. Preguntarlo por el rol principal habria abierto un agujero nuevo:
            // a quien tuviera {webmaster, gestor_interno} se le podria quitar el webmaster sin
            // que la guarda se enterase, porque el principal de {gestor_interno} ya no es
            // webmaster y la comparacion antigua solo mira el principal de ANTES.
            var rolesActuales = isNew
                ? (IReadOnlyList<string>)[]
                : await RolesDeUsuario.ObtenerAsync(dbContext, user.Id, cancellationToken);
            var eraWebmasterUtilizable = !isNew
                && user.IsActive
                && rolesActuales.Contains(Permisos.Webmaster, StringComparer.Ordinal)
                && EsHashUtilizable(user.PasswordHash);
            var seguiraSiendoWebmasterUtilizable =
                request.IsActive && rolesPedidos.Contains(Permisos.Webmaster, StringComparer.Ordinal);

            if (eraWebmasterUtilizable && !seguiraSiendoWebmasterUtilizable)
            {
                if (await WebmastersQuePuedenEntrarAsync(dbContext, cancellationToken) <= 1)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["user"] = ["Debe existir al menos un webmaster activo."]
                    });
                }
            }

            // La propia cuenta, aparte: aunque queden otros webmasters, desactivarse a uno mismo
            // desde el panel es siempre un accidente —se cierra la sesion en curso— y el DELETE ya
            // lo prohibia. Editar el propio perfil sigue permitido; lo que no, es apagarse.
            if (!isNew && user.IsActive && !request.IsActive && GetCurrentUserId(principal) == user.Id)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["user"] = ["No puedes desactivar tu propia cuenta."]
                });
            }

            // EL TIPO DE DOCUMENTO SE COMPRUEBA CONTRA EL CATALOGO, no se acepta tal cual. Es un
            // vocabulario controlado: guardar «cedula» o «CC.» dejaría la ficha enseñando un código
            // que ninguna lista reconoce, y la pregunta «cuántas cuentas tienen cédula» sin
            // respuesta.
            var tipoPedido = (request.TipoDocumento ?? string.Empty).Trim().ToLowerInvariant();
            if (tipoPedido.Length > 0
                && !await dbContext.TiposDocumento.AsNoTracking()
                    .AnyAsync(t => t.Codigo == tipoPedido && t.Activo, cancellationToken))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tipoDocumento"] = ["Ese tipo de documento no pertenece al catálogo."]
                });
            }

            // UN NUMERO SIN TIPO NO DICE QUE ES, y un tipo sin número no identifica a nadie: o van
            // los dos o no va ninguno.
            var identificacionPedida = (request.Identificacion ?? string.Empty).Trim();
            if ((identificacionPedida.Length > 0) != (tipoPedido.Length > 0))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["identificacion"] = ["El número de documento y su tipo van juntos: faltó uno de los dos."]
                });
            }

            // UNA CUENTA NUEVA NACE PENDIENTE DE SU PRIMER INGRESO. Se entrega con correo y una
            // contraseña que eligió otra persona: mientras no la cambie, esa credencial la conocen
            // dos. Y mientras no diga quién es, no se puede responder quién hizo qué.
            //
            // AL EDITAR NO SE TOCAN: reabrir la ficha de alguien para corregirle el correo no puede
            // mandarlo otra vez por el recorrido de bienvenida.
            if (isNew)
            {
                user.DebeCambiarContrasena = true;
                user.PerfilCompletado = false;
            }

            user.FullName = request.FullName.Trim();
            user.Email = email;
            user.IsActive = request.IsActive;
            user.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
            user.Identificacion = identificacionPedida.Length == 0 ? null : identificacionPedida;
            user.CodigoTipoDocumento = tipoPedido.Length == 0 ? null : tipoPedido;
            user.UpdatedAt = DateTime.UtcNow;
            if (!ValidationHelpers.IsMissing(request.Password))
            {
                if (request.Password.Length < 10)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["password"] = ["La contrasena debe tener minimo 10 caracteres."]
                    });
                }

                user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);
            }

            // UN GUARDADO DE MAS, Y SOLO PARA LAS ALTAS. `UsuariosRoles.IdUsuario` necesita el Id
            // del usuario, y una fila recien anadida no lo tiene hasta que EF la escribe. Deja
            // una ventana en la que la cuenta existe sin ninguna fila de asignacion; se acepta
            // porque ese estado es RUIDOSO —esa persona no inicia sesion y la consola la lista con
            // la columna de roles vacia, lista para editar— y no silencioso, que es lo que habria
            // pasado si se le hubiera dejado un rol por omision.
            if (isNew)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var rolesAsignados = await RolesDeUsuario.AsignarAsync(dbContext, user, roleRows, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            // EFECTO INMEDIATO. Sin esto, desactivar o cambiar de rol a alguien tardaria hasta el
            // techo de la cache en notarse (Security/RevalidacionDeSesion.cs). Se purga siempre,
            // no solo al desactivar: cambiar el rol tambien invalida el tiquete que la persona
            // lleva encima, y distinguir aqui que cambio seria una optimizacion sin premio.
            revalidacion.Invalidar(user.Id);
            await WriteAuditAsync(
                dbContext,
                GetCurrentUserId(principal),
                "Usuarios",
                user.Id.ToString(),
                isNew ? "crear" : "actualizar",
                cancellationToken,
                JsonSerializer.Serialize(new { user.FullName, user.Email, roles = rolesAsignados, user.IsActive }));

            return Results.Ok(new AdminAuthResponse(ToDto(user, rolesAsignados)));
        }).RequireAuthorization();

        auth.MapDelete("/users/{id:int}", async (
            int id,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            RevalidacionDeSesion revalidacion,
            CancellationToken cancellationToken) =>
        {
            if (!UserHasRole(principal, "webmaster"))
            {
                return Results.Forbid();
            }

            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId == id)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["user"] = ["No puedes eliminar tu propia cuenta activa."]
                });
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (user is null)
            {
                return Results.NotFound();
            }

            var rolesDelUsuario = await RolesDeUsuario.ObtenerAsync(dbContext, user.Id, cancellationToken);

            if (rolesDelUsuario.Contains(Permisos.Webmaster, StringComparer.Ordinal) && user.IsActive)
            {
                var activeWebmasters = await WebmastersQuePuedenEntrarAsync(dbContext, cancellationToken);

                if (activeWebmasters <= 1)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["user"] = ["Debe existir al menos un webmaster activo."]
                    });
                }
            }

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            // Que la baja se note en la siguiente peticion de esa persona, no dentro de medio
            // minuto. Ver Security/RevalidacionDeSesion.cs.
            revalidacion.Invalidar(user.Id);
            await WriteAuditAsync(
                dbContext,
                currentUserId,
                "Usuarios",
                user.Id.ToString(),
                "eliminar",
                cancellationToken,
                JsonSerializer.Serialize(new { user.FullName, user.Email, user.IsActive }));

            return Results.NoContent();
        }).RequireAuthorization();

        // ELIMINAR DE VERDAD, SOLO LO QUE NUNCA ACTUO.
        //
        // `DELETE /users/{id}` desactiva: la cuenta deja de entrar pero sigue existiendo, porque la
        // bitacora, los contenidos y las revisiones la nombran. La direccion de producto pidio el 15 de
        // septiembre de 2026 «permitir la eliminacion», y lo unico que se puede eliminar sin perder
        // trazabilidad es una cuenta que nunca hizo nada: la que se creo por error, la de prueba.
        // Tres candados: solo un webmaster; nunca la propia; y primero desactivada, para que borrar no
        // sea un solo clic desde una cuenta viva. Si actuo, se dice cuantas veces y se queda
        // desactivada; si algun registro la nombra, la base lo impide y se dice tambien.
        auth.MapDelete("/users/{id:int}/definitiva", async (
            int id,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            RevalidacionDeSesion revalidacion,
            CancellationToken cancellationToken) =>
        {
            if (!UserHasRole(principal, "webmaster"))
            {
                return Results.Forbid();
            }

            var currentUserId = GetCurrentUserId(principal);
            if (currentUserId == id)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["user"] = ["No puedes eliminar tu propia cuenta."]
                });
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (user is null)
            {
                return Results.NotFound();
            }

            if (user.IsActive)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["user"] = ["Primero desactiva la cuenta: una cuenta activa no se elimina de un solo paso."]
                });
            }

            var actuaciones = await dbContext.AuditLogs.CountAsync(fila => fila.UserId == id, cancellationToken);
            if (actuaciones > 0)
            {
                return Results.Conflict(new
                {
                    message = $"«{user.FullName}» tiene {actuaciones} {(actuaciones == 1 ? "actuación" : "actuaciones")} en la bitácora y no se puede eliminar sin perder la trazabilidad. Queda desactivada.",
                });
            }

            var estrategia = dbContext.Database.CreateExecutionStrategy();
            var resultado = await estrategia.ExecuteAsync<IResult>(async () =>
            {
                await using var transaccion = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                // LO QUE ES DE LA CUENTA SE VA CON ELLA: sus roles, sus modulos y su bandeja de avisos.
                // Todo lo demas que la nombre —contenidos, revisiones, vinculos— la protege por clave
                // foranea, y eso es lo que se quiere.
                dbContext.UsuariosRoles.RemoveRange(dbContext.UsuariosRoles.Where(fila => fila.UserId == id));
                dbContext.ModulosPorCuenta.RemoveRange(dbContext.ModulosPorCuenta.Where(fila => fila.IdUsuario == id));
                dbContext.Notifications.RemoveRange(dbContext.Notifications.Where(fila => fila.RecipientUserId == id));
                dbContext.Users.Remove(user);
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Results.Conflict(new
                    {
                        message = $"«{user.FullName}» tiene registros a su nombre —contenidos, revisiones o vínculos— y no se puede eliminar sin perder la trazabilidad. Queda desactivada.",
                    });
                }

                await WriteAuditAsync(
                    dbContext,
                    currentUserId,
                    "Usuarios",
                    id.ToString(CultureInfo.InvariantCulture),
                    "eliminar",
                    cancellationToken,
                    JsonSerializer.Serialize(new { user.FullName, user.Email, eliminacion = "definitiva" }));
                await transaccion.CommitAsync(cancellationToken);
                return Results.NoContent();
            });

            revalidacion.Invalidar(id);
            return resultado;
        }).RequireAuthorization();

        return group;
    }

    public static PasswordVerificationResult VerifyPassword(UserRow user, string password)
    {
        return PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
    }

    public static string HashPassword(UserRow user, string password)
    {
        return PasswordHasher.HashPassword(user, password);
    }

    /// <summary>
    /// Construye el principal con <b>un claim de rol por cada rol</b> de la persona.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ES LO QUE HACE QUE EL RESTO DEL API NO SE ENTERE DE LA TRANSICION. `IsInRole` y las politicas
    /// de autorizacion de ASP.NET ya sabian leer varios claims de rol; el modelo anterior solo
    /// escribia uno. Por eso <c>Permisos.EsFuncionario</c> y <c>Permisos.EsWebmaster</c> no han
    /// tenido que cambiar ni una linea: preguntaban por `IsInRole`, y ahora hay mas de uno que
    /// responder.
    /// </para>
    /// <para>
    /// EL ORDEN DE LOS CLAIMS IMPORTA, y por eso la lista llega ya ordenada
    /// (<see cref="RolesDeUsuario.Ordenar"/>). La revalidacion de cada peticion compara el
    /// conjunto de la cookie contra el de la base; si los dos lados no ordenaran igual, la
    /// comparacion fallaria por el orden y cerraria sesiones sanas.
    /// </para>
    /// <para>
    /// <c>pnmc_role_label</c> sigue siendo UNO: es la etiqueta que pinta la consola, no una
    /// decision de permisos. Lleva el rol principal por precedencia.
    /// </para>
    /// </remarks>
    private static ClaimsPrincipal BuildPrincipal(UserRow user, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new("pnmc_role_label", RolesDeUsuario.Principal(roles)),
            new(SimusAuthentication.AccessScopeClaim, SimusAuthentication.InstitutionalScope)
        };

        claims.AddRange(roles.Select(rol => new Claim(ClaimTypes.Role, rol)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static async Task<(UserRow User, IReadOnlyList<string> Roles)?> ResolveCurrentUserAsync(
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId(principal);
        if (userId is null)
        {
            return null;
        }

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId.Value && item.IsActive, cancellationToken);
        if (user is null)
        {
            return null;
        }

        // SIN ROLES NO HAY USUARIO RESUELTO, igual que en el login. Devolver la fila con la
        // lista vacia obligaria a cada llamante a acordarse de mirarla; devolver null hace que
        // /me responda 401, que es lo que corresponde a una sesion que ya no puede sostenerse.
        var roles = await RolesDeUsuario.ObtenerAsync(dbContext, user.Id, cancellationToken);

        return roles.Count == 0 ? null : (user, roles);
    }

    /// <summary>
    /// La cuenta que tiene ese correo, o ninguna.
    /// </summary>
    /// <remarks>
    /// ADMITE NULO A PROPOSITO. Desde que la normalización del correo vive en un solo sitio
    /// —<see cref="CorreoElectronico.Normalizar"/>—, un valor en blanco llega aquí como nulo y no
    /// como cadena vacía. Sin cuenta no hay correo, así que contestar «ninguna» es lo correcto: lo
    /// alternativo sería buscar la cuenta cuyo correo es «», que no debería existir nunca.
    /// </remarks>
    private static async Task<UserRow?> FindUserByEmailAsync(PnmcDbContext dbContext, string? email, CancellationToken cancellationToken)
    {
        if (email is null) return null;

        return await dbContext.Users.FirstOrDefaultAsync(
            item => item.Email == email,
            cancellationToken);
    }

    /// <summary>
    /// Cuantos webmasters quedan que puedan INICIAR SESION de verdad.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No basta con <c>Activo = 1</c> y rol webmaster. La semilla de administracion crea
    /// <c>sistema@pnmc.local</c> —la cuenta a la que se atribuyen las acciones automaticas— con el
    /// literal <c>pendiente_configurar_hash_seguro</c> en la columna del hash
    /// (<c>seed/V20260519_03__administracion_control_seed.sql:99</c>). Ese literal no es Base64,
    /// asi que <c>VerifyHashedPassword</c> lanza <see cref="FormatException"/>,
    /// <see cref="IsPasswordValid"/> lo traduce a «contrasena incorrecta» y el login responde 401
    /// SIEMPRE: ninguna contrasena abre esa cuenta.
    /// </para>
    /// <para>
    /// Hasta el 24 ago 2026 la guarda del ultimo webmaster la contaba igual. En la base local eso
    /// significa que de los dos webmasters «activos» solo uno puede entrar, y la guarda —creyendo
    /// que quedaban dos— habria dejado desactivar al unico real. Contar a alguien que no puede
    /// entrar es exactamente lo que la guarda existe para impedir.
    /// </para>
    /// <para>
    /// El filtro por rol y estado va en SQL; el del hash se resuelve en memoria porque la
    /// comprobacion de Base64 no se traduce a SQL, y el conjunto que llega es de un pun~ado de
    /// filas (los webmasters activos), no la tabla.
    /// </para>
    /// </remarks>
    private static async Task<int> WebmastersQuePuedenEntrarAsync(PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        // POR dbo.UsuariosRoles, NO POR Usuarios.IdRol. El Distinct no sobra: con N:M
        // el mismo usuario podria aparecer una vez por cada rol que tenga si algun dia el filtro
        // se ensancha, y contar dos veces al ultimo webmaster convertiria la guarda en un
        // permiso para dejar la consola huerfana.
        var candidatos = await dbContext.Users
            .Where(activeUser => activeUser.IsActive)
            .Join(
                dbContext.UsuariosRoles,
                activeUser => activeUser.Id,
                asignacion => asignacion.UserId,
                (activeUser, asignacion) => new { User = activeUser, asignacion.RoleId })
            .Join(
                dbContext.Roles,
                item => item.RoleId,
                role => role.Id,
                (item, role) => new { item.User, Role = role })
            .Where(item => item.Role.Name == "webmaster")
            .Select(item => new { item.User.Id, item.User.PasswordHash })
            .Distinct()
            .ToListAsync(cancellationToken);

        return candidatos.Count(candidato => EsHashUtilizable(candidato.PasswordHash));
    }

    /// <summary>
    /// ¿Este hash puede llegar a validar alguna contrasena? Un marcador de siembra no lo es.
    /// </summary>
    private static bool EsHashUtilizable(string? hash) =>
        !string.IsNullOrWhiteSpace(hash)
        && Convert.TryFromBase64String(hash, new byte[hash.Length], out var escritos)
        && escritos > 0;

    private static bool IsPasswordValid(UserRow user, string password)
    {
        try
        {
            return VerifyPassword(user, password) is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// El usuario tal y como lo ve la consola: el rol principal en <c>Role</c> y el conjunto
    /// entero en <c>Roles</c>.
    /// </summary>
    /// <remarks>
    /// <c>RoleLabel</c> repite el principal a proposito. Antes de la transicion llevaba el nombre sin
    /// normalizar de la fila de <c>dbo.Roles</c>; ahora los nombres viajan normalizados desde
    /// <see cref="RolesDeUsuario"/> y no hay una segunda grafia que ofrecer. Se conserva el campo
    /// porque la consola lo lee, no porque diga algo distinto.
    /// </remarks>
    /// <summary>Cómo se lee ese tipo de documento, o nada si la cuenta no tiene.</summary>
    /// <remarks>
    /// UN CODIGO QUE EL CATALOGO NO CONOCE SE DEVUELVE TAL CUAL Y NO SE OCULTA: la ficha tiene que
    /// poder enseñar que ahí hay algo raro para que alguien lo corrija, igual que hace la bitácora
    /// con un verbo que no sabe nombrar.
    /// </remarks>
    private static string? EtiquetaDeTipo(Dictionary<string, string> catalogo, string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        return catalogo.TryGetValue(codigo, out var etiqueta) ? etiqueta : codigo;
    }

    private static string? Opcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>El nombre completo, compuesto de sus cuatro partes.</summary>
    /// <remarks>
    /// SE COMPONE Y NO SE PIDE APARTE. Pedir el nombre entero y sus partes es dejar que discrepen, y
    /// entonces hay que decidir cuál manda cada vez que se lee.
    /// </remarks>
    private static string NombreCompuesto(UserRow user) =>
        string.Join(' ', new[] { user.PrimerNombre, user.SegundoNombre, user.PrimerApellido, user.SegundoApellido }
            .Where(parte => !string.IsNullOrWhiteSpace(parte))
            .Select(parte => parte!.Trim()));

    /// <summary>
    /// Cuántos apartados puede abrir una cuenta, con la MISMA regla que su ficha.
    /// </summary>
    /// <remarks>
    /// <b>SE REPITE LA REGLA A PROPOSITO Y NO SE LLAMA A <c>PermisosDeModulo.DeLaCuentaAsync</c>:</b>
    /// esa pide a la base por cada cuenta, y aquí ya están todas las filas en memoria. Lo que no se
    /// repite es el criterio —concedidos ∪ siempre activados, y todos si es webmaster—, porque sale
    /// de las mismas dos listas de <see cref="ModulosDeLaConsola"/>.
    /// </remarks>
    private static (int Activos, int Totales) ApartadosDe(
        IReadOnlyList<string> roles, IReadOnlyList<string> concedidos)
    {
        var totales = ModulosDeLaConsola.Todos.Length;
        if (roles.Contains(Permisos.Webmaster, StringComparer.OrdinalIgnoreCase))
        {
            return (totales, totales);
        }

        var tiene = new HashSet<string>(concedidos, StringComparer.OrdinalIgnoreCase);
        foreach (var siempre in ModulosDeLaConsola.SiempreActivados) tiene.Add(siempre);
        return (ModulosDeLaConsola.Todos.Count(tiene.Contains), totales);
    }

    private static AdminUserDto ToDto(
        UserRow user,
        IReadOnlyList<string> roles,
        string? tipoDocumentoEtiqueta = null,
        (int Activos, int Totales)? apartados = null)
    {
        var principal = RolesDeUsuario.Principal(roles);
        return new AdminUserDto(
            user.Id.ToString(),
            user.FullName,
            user.Email,
            principal,
            principal,
            user.IsActive,
            user.LastLoginAt,
            Telefono: user.Telefono,
            Roles: roles,
            Identificacion: user.Identificacion,
            TipoDocumento: user.CodigoTipoDocumento,
            // EL CODIGO NUNCA SALE A PANTALLA. «cc» no es un tipo de documento para quien lee: la
            // etiqueta la resuelve el servidor, que es quien tiene el catálogo.
            TipoDocumentoEtiqueta: tipoDocumentoEtiqueta,
            DebeCambiarContrasena: user.DebeCambiarContrasena,
            PerfilCompletado: user.PerfilCompletado,
            PrimerNombre: user.PrimerNombre,
            SegundoNombre: user.SegundoNombre,
            PrimerApellido: user.PrimerApellido,
            SegundoApellido: user.SegundoApellido,
            ApartadosActivos: apartados?.Activos ?? 0,
            ApartadosTotales: apartados?.Totales ?? ModulosDeLaConsola.Todos.Length);
    }


    private static string CleanRoleName(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Roles que la consola puede asignar a mano. Son los tres del modelo definitivo, y salen
    /// de <see cref="Permisos.RolesDePlataforma"/> para que esta lista no pueda separarse de
    /// la que abre la puerta institucional.
    /// </summary>
    private static bool IsAssignableGlobalRole(string role)
    {
        return Permisos.EsRolDePlataforma(role);
    }

    private static bool UserHasRole(ClaimsPrincipal principal, string role)
    {
        return principal.Identity?.IsAuthenticated == true
            && principal.IsInRole(role);
    }

    private static int? GetCurrentUserId(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private static async Task WriteAuditAsync(
        PnmcDbContext dbContext,
        int? userId,
        string tableName,
        string recordId,
        string action,
        CancellationToken cancellationToken,
        string? newValuesJson = null)
    {
        dbContext.AuditLogs.Add(new AuditLogRow
        {
            UserId = userId,
            TableName = tableName,
            RecordId = recordId,
            Action = action,
            NewValuesJson = newValuesJson,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
