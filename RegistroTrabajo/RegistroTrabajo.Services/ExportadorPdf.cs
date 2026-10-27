using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Genera el .pdf de un reporte: encabezado con empresa, título, filtros, resumen, tabla y numeración de páginas.</summary>
internal static class ExportadorPdf
{
    private static readonly object Candado = new();
    private static bool _fuentesListas;

    private static void PrepararFuentes()
    {
        lock (Candado)
        {
            if (_fuentesListas) return;
            // En Windows se usan las fuentes del sistema; en otros sistemas hay que registrar un FontResolver propio.
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            _fuentesListas = true;
        }
    }

    public static byte[] Generar(string empresa, string titulo, string generado, IReadOnlyList<string> filtros, IReadOnlyList<ResumenReporteDto> resumen,
        IReadOnlyList<ColumnaReporteDto> columnas, IReadOnlyList<object?[]> filas, string? nota)
    {
        PrepararFuentes();
        var doc = new Document();
        doc.Info.Title = $"{empresa} - {titulo}";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = "Arial";
        normal.Font.Size = 8;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.Orientation = Orientation.Landscape;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(2.2);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);

        var enc = sec.Headers.Primary.AddParagraph();
        enc.Format.Font.Size = 9;
        enc.Format.Font.Bold = true;
        enc.AddText(empresa);
        enc.AddText($"  ·  {titulo}");
        enc.Format.Borders.Bottom.Width = 0.5;

        var pie = sec.Footers.Primary.AddParagraph();
        pie.Format.Alignment = ParagraphAlignment.Center;
        pie.Format.Font.Size = 8;
        pie.AddText("Página ");
        pie.AddPageField();
        pie.AddText(" de ");
        pie.AddNumPagesField();

        var t = sec.AddParagraph(titulo);
        t.Format.Font.Size = 14;
        t.Format.Font.Bold = true;
        t.Format.SpaceAfter = 2;
        sec.AddParagraph($"Generado: {generado}").Format.SpaceAfter = 2;
        sec.AddParagraph("Filtros: " + (filtros.Count == 0 ? "ninguno" : string.Join("; ", filtros))).Format.SpaceAfter = 4;
        if (resumen.Count > 0)
        {
            var p = sec.AddParagraph(string.Join("   |   ", resumen.Select(r => $"{r.Etiqueta}: {r.Valor}")));
            p.Format.Font.Bold = true;
            p.Format.SpaceAfter = 4;
        }
        if (!string.IsNullOrWhiteSpace(nota))
        {
            var n = sec.AddParagraph(nota);
            n.Format.Font.Italic = true;
            n.Format.SpaceAfter = 6;
        }

        var tabla = sec.AddTable();
        tabla.Borders.Width = 0.25;
        tabla.Borders.Color = Colors.Gray;
        tabla.Rows.LeftIndent = 0;
        var ancho = 26.7; // A4 horizontal menos márgenes, en cm
        var pesos = columnas.Select((c, i) => Math.Clamp(Math.Max(c.Titulo.Length, filas.Take(50).Select(f => Largo(f, i)).DefaultIfEmpty(0).Max()), 6, 28)).ToArray();
        var suma = pesos.Sum();
        foreach (var peso in pesos) tabla.AddColumn(Unit.FromCentimeter(ancho * peso / suma));

        var cab = tabla.AddRow();
        cab.HeadingFormat = true;
        cab.Format.Font.Bold = true;
        cab.Shading.Color = Colors.LightGray;
        for (var i = 0; i < columnas.Count; i++) cab.Cells[i].AddParagraph(columnas[i].Titulo);

        foreach (var datos in filas)
        {
            var row = tabla.AddRow();
            for (var i = 0; i < columnas.Count; i++)
            {
                var v = i < datos.Length ? datos[i] : null;
                var celda = row.Cells[i].AddParagraph(Limpio(ReporteService.Texto(v)));
                if (columnas[i].Tipo == "numero") celda.Format.Alignment = ParagraphAlignment.Right;
            }
        }
        if (filas.Count == 0)
        {
            var vacio = tabla.AddRow();
            vacio.Cells[0].MergeRight = columnas.Count - 1;
            vacio.Cells[0].AddParagraph("Sin registros para los filtros indicados.");
        }

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    private static int Largo(object?[] fila, int i) => i < fila.Length ? ReporteService.Texto(fila[i]).Length : 0;

    private static string Limpio(string s) => new(s.Where(ch => ch is '\t' or '\n' or '\r' || ch >= ' ').ToArray());
}
