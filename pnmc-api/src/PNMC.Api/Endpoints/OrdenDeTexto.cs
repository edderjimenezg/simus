using System.Globalization;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Como se pone en orden alfabetico un texto en español.
/// </summary>
/// <remarks>
/// <para>
/// NO VALE <see cref="StringComparer.Ordinal"/>, QUE ES LO QUE SE USA EN EL RESTO DE ESTOS
/// FICHEROS PARA CODIGOS. Ordinal compara numeros de caracter: la «Ó» de CORDOBA vale 0xD3 y la
/// «U» de CUNDINAMARCA vale 0x55, asi que CORDOBA caeria DESPUES de CUNDINAMARCA. Ocho de los
/// treinta y tres departamentos colombianos llevan tilde, y los nombres de los registros del
/// ecosistema estan llenos de ellas.
/// </para>
/// <para>
/// VIVE APARTE PARA QUE HAYA UNA SOLA DEFINICION. Nacio dentro de
/// <see cref="AdminOrganizacionesEndpoints"/> y ese mismo dia hizo falta
/// en <see cref="AdminDataEndpoints"/>: dos copias de «como ordena el español» se separan sin que
/// el compilador diga nada, y entonces dos desplegables de la misma consola ordenan distinto.
/// </para>
/// <para>
/// EL PROYECTO NO CORRE EN MODO INVARIANTE: <c>PNMC.Api.csproj</c> declara
/// <c>&lt;InvariantGlobalization&gt;false&lt;/InvariantGlobalization&gt;</c>. Si eso cambiara,
/// <see cref="CultureInfo.GetCultureInfo(string)"/> devolveria la cultura invariante en silencio y
/// esto volveria a ordenar como Ordinal.
/// </para>
/// </remarks>
internal static class OrdenDeTexto
{
    internal static readonly StringComparer Espanol =
        StringComparer.Create(CultureInfo.GetCultureInfo("es-CO"), ignoreCase: true);
}
