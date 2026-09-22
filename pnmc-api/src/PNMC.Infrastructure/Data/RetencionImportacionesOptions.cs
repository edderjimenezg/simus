namespace PNMC.Infrastructure.Data;

/// <summary>
/// Regla operativa para datos temporales de importación. La evidencia de lotes aplicados no se
/// elimina aquí: su disposición final pertenece a la tabla de retención documental institucional.
/// </summary>
public sealed class RetencionImportacionesOptions
{
    public const string SectionName = "Importaciones:Retencion";
    public int DiasPrevisualizacion { get; set; } = 30;
    public int IntervaloHoras { get; set; } = 24;

    public void Validar()
    {
        if (DiasPrevisualizacion is < 1 or > 365)
            throw new InvalidOperationException("Importaciones:Retencion:DiasPrevisualizacion debe estar entre 1 y 365.");
        if (IntervaloHoras is < 1 or > 168)
            throw new InvalidOperationException("Importaciones:Retencion:IntervaloHoras debe estar entre 1 y 168.");
    }
}
