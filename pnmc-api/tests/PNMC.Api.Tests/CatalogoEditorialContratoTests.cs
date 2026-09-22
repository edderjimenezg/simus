using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

public sealed class CatalogoEditorialContratoTests
{
    [Fact]
    public void CatalogacionYPublicacionSonDecisionesSeparadas()
    {
        Assert.Contains("validada", CatalogoEditorialContrato.EstadosCatalogacion);
        Assert.DoesNotContain("publicado", CatalogoEditorialContrato.EstadosCatalogacion);
        Assert.Contains("publicado", CatalogoEditorialContrato.EstadosPublicacion);
        Assert.DoesNotContain("validada", CatalogoEditorialContrato.EstadosPublicacion);
    }

    [Theory]
    [InlineData("archivo", 7L, null, null, true)]
    [InlineData("enlace", null, "https://www.mincultura.gov.co/recurso", null, true)]
    [InlineData("ubicacion", null, null, "Centro de documentación", true)]
    [InlineData("archivo", 7L, "https://www.mincultura.gov.co/recurso", null, false)]
    [InlineData("enlace", null, null, null, false)]
    [InlineData("otro", null, "https://www.mincultura.gov.co/recurso", null, false)]
    public void ElAccesoExigeUnSoloDestinoCorrespondiente(
        string tipo,
        long? archivoId,
        string? url,
        string? ubicacion,
        bool esperado)
    {
        var acceso = new AccesoEditorialSolicitud
        {
            Tipo = tipo,
            ArchivoId = archivoId,
            Url = url,
            UbicacionFisica = ubicacion,
            Orden = 1,
        };

        Assert.Equal(esperado, CatalogoEditorialContrato.TieneDestinoValido(acceso));
    }

    [Fact]
    public void LosVocabulariosEstructuralesVanEnMinuscula()
    {
        // El portal distingue autoría personal de corporativa comparando este vocabulario. Mientras
        // estuvo escrito solo aquí y en el CHECK, el frontend pudo comparar contra «Entidad» durante
        // todo el módulo sin que nada fallara: la autoría corporativa no salió nunca y no hubo error.
        foreach (var tipo in CatalogoEditorialContrato.TiposAgente)
        {
            Assert.Equal(tipo.ToLowerInvariant(), tipo);
        }

        Assert.Equal(["persona", "entidad"], CatalogoEditorialContrato.TiposAgente);
    }

    [Fact]
    public void LosVocabulariosEstructuralesNoTienenDuplicados()
    {
        Assert.Equal(CatalogoEditorialContrato.EstadosCatalogacion.Count, CatalogoEditorialContrato.EstadosCatalogacion.Distinct().Count());
        Assert.Equal(CatalogoEditorialContrato.EstadosPublicacion.Count, CatalogoEditorialContrato.EstadosPublicacion.Distinct().Count());
        Assert.Equal(CatalogoEditorialContrato.TiposAcceso.Count, CatalogoEditorialContrato.TiposAcceso.Distinct().Count());
        Assert.Equal(CatalogoEditorialContrato.EstadosDerechos.Count, CatalogoEditorialContrato.EstadosDerechos.Distinct().Count());
    }
}
