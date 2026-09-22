namespace PNMC.Api.Endpoints;

/// <summary>
/// El unico tipo que admite <c>Entidades.TipoEntidad</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>DE NUEVE A UNO, en dos pasos y por dos motivos distintos.</b> El <c>CHECK</c> heredado del
/// desarrollo de septiembre admitia nueve valores. <c>V20260912_03</c> retiro los siete que
/// nombraban procesos y lugares —<c>festival</c>, <c>escuela_musica</c>, <c>mercado_musical</c>,
/// <c>espacio</c>, <c>lutier</c>, <c>colectivo</c>, <c>individuo</c>— porque en esta base cada
/// proceso tiene su tabla y su circuito, y un tipo que nombra un proceso en la tabla de actores es
/// la puerta por la que vuelve el modelo generico. <c>V20260912_04</c> retiro el que quedaba,
/// <c>agrupacion</c>, por Criterio de producto: «no va a existir
/// agrupacion».
/// </para>
/// <para>
/// <b>ESTA CLASE SIGUE EXISTIENDO CON UN SOLO VALOR A PROPOSITO.</b> El literal
/// <c>"organizacion"</c> aparece en consultas de media docena de ficheros; tenerlo en un sitio es lo
/// que permite que el dia que cambie el vocabulario no haya que buscarlo a mano. Y
/// <see cref="Admitidos"/> es lo que compara <c>Pnmc059VocabularioEstadosTests</c> contra el
/// <c>CHECK</c> real: si alguien reabre la tabla sin tocar aqui, la prueba lo dice.
/// </para>
/// </remarks>
public static class TiposDeEntidad
{
    /// <summary>El actor que registra y administra procesos. No hay otro.</summary>
    public const string Organizacion = "organizacion";

    /// <summary>Lo que admite <c>CK_Entidades_Tipo</c>.</summary>
    public static readonly string[] Admitidos = [Organizacion];

    /// <summary>Indica si un valor pasaria <c>CK_Entidades_Tipo</c>.</summary>
    public static bool EsAdmitido(string? tipo) =>
        tipo is not null && Array.IndexOf(Admitidos, tipo) >= 0;
}
