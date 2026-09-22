using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

using PNMC.Api.Security;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Nace una organizacion del ecosistema: la entidad, quien la administra y quien responde por ella.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE ESTO VIVE EN UN SITIO Y NO EN DOS. Hay dos rutas que crean organizaciones y hacen falta
/// las dos: <c>POST /external/auth/register</c>, para quien todavia no tiene cuenta, y
/// <c>POST /external/organizations</c>, para quien ya la tiene y registra una segunda. Antes cada
/// una llevaba su propia copia de las reglas —que codigo de cobertura es valido, que el NIT no
/// este repetido, que el municipio exista en DIVIPOLA—, y una copia de una regla es una regla que
/// va a divergir. Aqui se escribe una vez.
/// </para>
/// <para>
/// LAS TRES FILAS SON UNA SOLA COSA. Una entidad sin su vinculo es una organizacion que nadie
/// puede administrar; un vinculo sin <c>EntidadesResponsable</c> es la promesa de la pantalla
/// —«identificaremos a la persona responsable»— incumplida, que es exactamente como esa tabla
/// llego a tener cero filas frente a diecisiete entidades. Se escriben juntas, y quien las llame
/// tiene que estar dentro de una transaccion.
/// </para>
/// </remarks>
internal static class AltaDeOrganizacion
{
    /// <summary>
    /// Qué puede ser una identificación: letras, números, puntos y guiones, de 3 a 60 caracteres.
    /// </summary>
    /// <remarks>
    /// ES INTERNA PORQUE LA IMPORTACION TAMBIEN LA NECESITA. Escribir el mismo patrón en el
    /// planificador de organizaciones importadas habría dado dos criterios para lo mismo: uno
    /// admitiría un NIT que el otro rechaza, según por dónde entrara la organización.
    /// </remarks>
    internal static readonly Regex PatronDeIdentificacion = new("^[A-Za-z0-9.-]{3,60}$", RegexOptions.Compiled);

    /// <summary>
    /// El estado con el que nace una organizacion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LO DECIDE EL CORREO DE QUIEN RESPONDE POR ELLA, no la puerta por la que entro. Si esa cuenta
    /// ya comprobo su correo —el caso de un alta hecha desde la consola por un funcionario—, la
    /// organizacion nace <c>activa</c>. Si no, nace <c>pendiente_de_confirmacion</c>: puede entrar y
    /// preparar su registro, pero no entregarle nada al Programa hasta confirmar.
    /// </para>
    /// <para>
    /// UNA ORGANIZACION SIN CUENTA NACE PENDIENTE. Es el alta administrativa de una organizacion que
    /// todavia nadie reclama: no hay correo comprobado porque no hay a quien comprobarselo, y darla
    /// por activa afirmaria un contacto que nadie ha verificado.
    /// </para>
    /// </remarks>
    internal static string EstadoInicialSegun(UserRow? responsableDeLaOrganizacion) =>
        responsableDeLaOrganizacion?.CorreoConfirmado == true
            ? EstadosDeOrganizacion.Activa
            : EstadosDeOrganizacion.PendienteDeConfirmacion;

    // ---------- Normalizadores, compartidos para que las dos rutas guarden lo mismo ----------

    internal static string? NormalizarOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();


    internal static string? NormalizarIdentificacion(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();

    /// <summary>
    /// El numero de documento, sin puntos ni espacios.
    /// </summary>
    /// <remarks>
    /// Quien escribe su cedula la escribe como la lee en el documento —«1.020.304.050»— y quien la
    /// busca despues la teclea seguida. Guardar las dos formas es no poder cruzarlas nunca, asi que
    /// se guarda una: solo digitos.
    /// </remarks>
    internal static string NormalizarDocumento(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    /// <summary>La sede se valida aparte del alcance: una ubicación no convierte a la entidad en municipal.</summary>
    internal static async Task ValidarSedeAsync(
        Dictionary<string, string[]> errores,
        string? codigoDepartamento,
        string? codigoMunicipio,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var departamento = NormalizarOpcional(codigoDepartamento);
        var municipio = NormalizarOpcional(codigoMunicipio);
        if (departamento is null)
        {
            errores["headquartersDepartmentCode"] = ["El departamento de la sede es obligatorio."];
            return;
        }
        if (municipio is null)
        {
            errores["headquartersMunicipalityCode"] = ["El municipio de la sede es obligatorio."];
            return;
        }
        var existe = await dbContext.DivipolaLocations.AsNoTracking().AnyAsync(
            item => item.DepartmentCode == departamento && item.MunicipalityCode == municipio,
            cancellationToken);
        if (!existe)
            errores["headquartersMunicipalityCode"] = ["El municipio indicado no pertenece al departamento seleccionado."];
    }

    // ---------- Validacion ----------------------------------------------------------------

    /// <summary>
    /// Comprueba los datos de la organizacion contra las reglas del API y contra DIVIPOLA.
    /// </summary>
    /// <remarks>
    /// El prefijo de los nombres de error existe porque las dos rutas que llaman aqui tienen
    /// formularios distintos: en el alta completa el campo se llama <c>organizationName</c> y en el
    /// alta con sesion, <c>name</c>. Sin el prefijo, el front recibiria un error apuntando a un
    /// campo que su formulario no tiene, y el mensaje no se pintaria en ninguna parte.
    /// </remarks>
    /// <param name="claveCorreo">
    /// Campo al que atribuir un correo de organizacion invalido. <c>null</c> usa el nombre derivado
    /// del prefijo; el alta completa pasa <c>"email"</c> porque alli el correo es uno solo.
    /// </param>
    internal static async Task ValidarAsync(
        Dictionary<string, string[]> errores,
        string prefijo,
        string? claveCorreo,
        string? nombre,
        string? identificacion,
        string? correoContacto,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken,
        // LA ORGANIZACION QUE SE ESTA EDITANDO, si es que se esta editando alguna.
        //
        // SIN ESTO, GUARDAR EL PROPIO PERFIL ERA IMPOSIBLE: la comprobacion de «un correo, una
        // organizacion» encontraba SU PROPIA fila y contestaba «ya existe una organizacion con este
        // correo», sin manera de salir de ahi. Es exactamente la trampa que el NIT ya tenia resuelta
        // unas lineas mas abajo en `ValidarPerfilAsync`, alli por otro camino: aquel no pregunta
        // cuando el numero no cambia. El correo no puede usar ese camino porque ademas se valida su
        // formato, y no preguntar significaria dejar de validarlo.
        //
        // Lo cazaron las siete pruebas de `PerfilDeLaOrganizacionTests`.
        int? idQueSeExcluye = null)
    {
        string Campo(string nombreCorto) =>
            prefijo.Length == 0 ? nombreCorto : prefijo + char.ToUpperInvariant(nombreCorto[0]) + nombreCorto[1..];

        if (ValidationHelpers.IsMissing(nombre))
        {
            errores[Campo("name")] = ["El nombre de la organización es obligatorio."];
        }

        // LA CLAVE DEL ERROR DEL CORREO NO ES LA MISMA EN LAS DOS RUTAS. El alta completa pide UN
        // SOLO correo —el institucional de la organizacion, que es tambien el de acceso— y su campo
        // se llama `email`; el alta con sesion pide uno aparte y el suyo se llama `contactEmail`.
        // Sin esta distincion, el alta completa recibiria el error apuntando a un campo que su
        // formulario ya no tiene, y el mensaje no se pintaria en ninguna parte.
        if (!ValidationHelpers.IsValidEmail(correoContacto))
        {
            errores[claveCorreo ?? Campo("contactEmail")] = ["El correo de la organización no es válido."];
        }
        else
        {
            // UN CORREO, UNA ORGANIZACION. La segunda mitad de la regla que se define el 28
            // de agosto de 2026: si el correo ya es el de otra organizacion, este alta no procede.
            //
            // El valor de entrada y la columna se guardan normalizados en minúsculas y sin
            // espacios. Compararlos directamente conserva una consulta indexable y mantiene
            // `Contacto@X.co` y `contacto@x.co` como el mismo buzón.
            // AQUI NO PUEDE SER NULO: esta rama es la del `else` de `IsValidEmail`, así que el
            // correo ya viene con forma. Y la consulta exige además `ContactEmail != null`, de modo
            // que un nulo tampoco encontraría a nadie por accidente.
            var correoNormalizado = CorreoElectronico.Normalizar(correoContacto);
            var yaEsDeOtra = await dbContext.EntityProfiles.AsNoTracking().AnyAsync(
                item => item.ContactEmail != null
                    && item.ContactEmail == correoNormalizado
                    && (idQueSeExcluye == null || item.Id != idQueSeExcluye),
                cancellationToken);
            if (yaEsDeOtra)
            {
                errores[claveCorreo ?? Campo("contactEmail")] =
                    ["Ya existe una organización registrada con este correo. Un correo solo puede estar atado a una organización."];
            }
        }

        var numero = NormalizarIdentificacion(identificacion);
        if (!string.IsNullOrEmpty(numero) && !PatronDeIdentificacion.IsMatch(numero))
        {
            errores[Campo("identificationNumber")] =
                ["La identificación debe contener entre 3 y 60 caracteres alfanuméricos, puntos o guiones."];
        }
        else if (!string.IsNullOrEmpty(numero)
                 && await dbContext.EntityProfiles.AsNoTracking()
                     .AnyAsync(item => item.IdentificationNumber == numero, cancellationToken))
        {
            errores[Campo("identificationNumber")] = ["Ya existe una organización registrada con esta identificación."];
        }

    }

    // ---------- Escritura -----------------------------------------------------------------

    /// <summary>
    /// Escribe la entidad, el vinculo de administracion y la fila del responsable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL LLAMANTE TIENE QUE ESTAR EN UNA TRANSACCION. Son tres tablas y la de en medio necesita el
    /// identificador que la primera acaba de recibir, asi que hay un <c>SaveChangesAsync</c> por
    /// dentro. Sin transaccion alrededor, un corte entre los dos guardados deja una organizacion
    /// que nadie administra.
    /// </para>
    /// <para>
    /// EL TERRITORIO SE LIMPIA CUANDO LA COBERTURA ES NACIONAL, aunque venga puesto. La validacion
    /// de arriba deja pasar esa combinacion —no es un error de quien rellena, es un residuo de
    /// haber elegido municipal antes— y la CHECK de la base la rechazaria. Se corrige aqui en vez de
    /// devolver un error por algo que el formulario ya no muestra.
    /// </para>
    /// </remarks>
    internal static async Task<EntityProfileRow> CrearAsync(
        PnmcDbContext dbContext,
        UserRow responsable,
        string nombre,
        string? identificacion,
        string? correoContacto,
        string? codigoDepartamentoSede,
        string? codigoMunicipioSede,
        string nombreResponsable,
        string tipoDocumentoResponsable,
        string numeroDocumento,
        string? primerNombreResponsable,
        string? segundoNombreResponsable,
        string? primerApellidoResponsable,
        string? segundoApellidoResponsable,
        string? telefono,
        bool autorizacionDatos,
        DateTime ahora,
        CancellationToken cancellationToken,
        // SI LA ORGANIZACION NACE SIN CUENTA, NADIE LA ADMINISTRA TODAVIA.
        //
        // El alta externa la registra la persona que la va a administrar, y por eso se crea el
        // vinculo. La consola registra organizaciones que AUN NO TIENEN CUENTA: vincular ahi al
        // funcionario lo pondria como administrador de una entidad del ecosistema que no es suya,
        // y ese vinculo acabaria contandose como si el Programa gestionara la organizacion. La
        // administracion llega cuando la organizacion la reclame, por el circuito que ya existe.
        // DE DONDE VIENE EL ALTA.
        //
        // Por omision, `externo`: es como nacio esta funcion y es el camino que recorre casi todo
        // el ecosistema. La consola institucional pasa `administrativo`, y esa diferencia es la
        // que despues permite responder «quien incorporo esta organizacion al sistema».
        string contexto = ProcedenciaDeRegistro.Externo,
        // QUIEN ADMINISTRA LA ORGANIZACION RECIEN CREADA.
        //
        // En el alta externa es quien la registra, porque es su organizacion. En el alta
        // administrativa no hay nadie todavia: ver la nota junto al vinculo.
        bool vincularResponsableComoAdministrador = true,
        // EL CORREO DEL RESPONSABLE, cuando no es el de quien ejecuta el alta.
        //
        // En el alta externa coinciden. En la administrativa, quien ejecuta es un funcionario y
        // el responsable es otra persona: copiar el correo del funcionario dejaria a la
        // organizacion con un contacto que no es suyo.
        string? correoResponsable = null)
    {
        var entidad = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = ValidationHelpers.SanitizeText(nombre, 240),
            IdentificationNumber = NormalizarIdentificacion(identificacion),
            ContactEmail = CorreoElectronico.Normalizar(correoContacto),
            HeadquartersDepartmentCode = NormalizarOpcional(codigoDepartamentoSede),
            HeadquartersMunicipalityCode = NormalizarOpcional(codigoMunicipioSede),
            StatusCode = EstadoInicialSegun(vincularResponsableComoAdministrador ? responsable : null),
            // LA BASE IMPIDE QUE ESTO CONTRADIGA AL ESTADO (CK_Entidades_VigenciaCoherente). Los dos
            // estados con los que puede nacer son de los que dejan entrar, asi que es 1 en los dos.
            IsActive = true,
            CreatedByUserId = responsable.Id,
            // SIN CUENTA NO HAY USUARIO RESPONSABLE. Apuntar al funcionario diria que el Programa
            // responde por la organizacion, que es justo lo que hay que poder distinguir.
            ResponsibleUserId = vincularResponsableComoAdministrador ? responsable.Id : null,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        dbContext.EntityProfiles.Add(entidad);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (vincularResponsableComoAdministrador)
        {
            dbContext.UserEntities.Add(new UserEntityRow
            {
                UserId = responsable.Id,
                EntityId = entidad.Id,
                EntityRole = "administrador",
                IsActive = true,
                CreatedAt = ahora,
            });
        }

        // LA FILA QUE FALTABA. La pantalla prometia identificar a la persona responsable desde el
        // 25 de agosto y nadie escribia esta tabla: cero filas frente a diecisiete entidades. El
        // nombre y el numero viven aqui y no en `Entidades`, que se lee en rutas anonimas: sacar el
        // documento de una organizacion tiene que exigir un JOIN deliberado, no venir de regalo.
        dbContext.EntidadesResponsable.Add(new EntidadResponsableRow
        {
            IdEntidad = entidad.Id,
            ResponsableNombre = ValidationHelpers.SanitizeText(nombreResponsable, 240),
            ResponsablePrimerNombre = NormalizarOpcional(primerNombreResponsable),
            ResponsableSegundoNombre = NormalizarOpcional(segundoNombreResponsable),
            ResponsablePrimerApellido = NormalizarOpcional(primerApellidoResponsable),
            ResponsableSegundoApellido = NormalizarOpcional(segundoApellidoResponsable),
            ResponsableTipoDocumento = NormalizarOpcional(tipoDocumentoResponsable)?.ToUpperInvariant() ?? string.Empty,
            ResponsableNumeroDocumento = NormalizarDocumento(numeroDocumento),
            ResponsableCorreo = CorreoElectronico.Normalizar(correoResponsable ?? responsable.Email),
            ResponsableTelefono = NormalizarOpcional(telefono),
            ResponsableDesde = ahora,
            ResponsableAutorizacionDatos = autorizacionDatos,
            FechaCreacion = ahora,
        });

        // DE DONDE VINO LA ORGANIZACION.
        //
        // CUANDO SE REGISTRA A SI MISMA, la entidad de procedencia es ella: nadie la incorporo
        // por ella. Cuando la crea la consola, es la entidad institucional, porque quien la
        // incorporo fue el Programa —sin que eso la convierta en responsable de nada suyo—.
        await ProcedenciaDeRegistro.AnotarAsync(
            dbContext,
            Modulos.Organizaciones,
            entidad.Id.ToString(CultureInfo.InvariantCulture),
            contexto,
            contexto == ProcedenciaDeRegistro.Externo
                ? entidad.Id
                : await ProcedenciaDeRegistro.IdInstitucionalAsync(dbContext, cancellationToken),
            responsable.Id,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return entidad;
    }
}
