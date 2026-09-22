namespace PNMC.Contracts;

/// <summary>
/// Un elemento de los catálogos con los que se clasifica un contenido: una práctica musical o un
/// territorio sonoro.
/// </summary>
/// <remarks>
/// <para>
/// VIAJA CON NOMBRE Y NO SOLO CON IDENTIFICADOR porque quien lee la ficha —el portal, la consola—
/// necesita enseñarlo, y pedir el catálogo entero en cada pantalla para traducir tres números es
/// gastar una consulta en lo que el servidor ya sabe.
/// </para>
/// <para>
/// ES EL MISMO TIPO PARA LOS DOS CATALOGOS. Prácticas musicales y territorios sonoros son listas
/// distintas, pero se consumen igual: identificador, nombre y dirección. Dos registros gemelos
/// solo darían dos sitios donde arreglar lo mismo.
/// </para>
/// </remarks>
public sealed record ElementoDeClasificacionDto(int Id, string Nombre, string Slug);
