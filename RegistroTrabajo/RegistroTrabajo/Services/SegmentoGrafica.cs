using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Un tramo de una gráfica (dona o barra apilada).</summary>
public record SegmentoGrafica(string Etiqueta, int Valor, string Color);
