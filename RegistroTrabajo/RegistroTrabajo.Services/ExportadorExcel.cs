using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using RegistroTrabajo.Models;

namespace RegistroTrabajo.Services;

/// <summary>Genera el .xlsx de un reporte: encabezado con empresa y filtros, tabla con filtros automáticos y hoja de resumen.</summary>
internal static class ExportadorExcel
{
    // Índices de CellFormat (ver Estilos()).
    private const uint EstiloEncabezado = 1, EstiloFecha = 2, EstiloFechaHora = 3, EstiloNumero = 4, EstiloNegrita = 5;

    public static byte[] Generar(string empresa, string titulo, DateTime generadoLocal, IReadOnlyList<string> filtros, IReadOnlyList<ResumenReporteDto> resumen,
        IReadOnlyList<ColumnaReporteDto> columnas, IReadOnlyList<object?[]> filas, string? nota)
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
        {
            var wb = doc.AddWorkbookPart();
            wb.Workbook = new Workbook();
            var estilos = wb.AddNewPart<WorkbookStylesPart>();
            estilos.Stylesheet = Estilos();

            var hojas = wb.Workbook.AppendChild(new Sheets());
            AgregarHoja(wb, hojas, 1, "Reporte", Principal(empresa, titulo, generadoLocal, filtros, columnas, filas, nota, out var rango, out var anchos), rango, anchos);
            AgregarHoja(wb, hojas, 2, "Resumen", Resumen(titulo, resumen), null, new[] { 40.0, 22.0 });
            wb.Workbook.Save();
        }
        return ms.ToArray();
    }

    private static void AgregarHoja(WorkbookPart wb, Sheets hojas, uint id, string nombre, SheetData datos, string? autoFiltro, double[] anchos)
    {
        var part = wb.AddNewPart<WorksheetPart>();
        var ws = new Worksheet();
        var cols = new Columns();
        for (var i = 0; i < anchos.Length; i++) cols.Append(new Column { Min = (uint)(i + 1), Max = (uint)(i + 1), Width = anchos[i], CustomWidth = true });
        ws.Append(cols);
        ws.Append(datos);
        if (autoFiltro is not null) ws.Append(new AutoFilter { Reference = autoFiltro });
        part.Worksheet = ws;
        hojas.Append(new Sheet { Id = wb.GetIdOfPart(part), SheetId = id, Name = nombre });
    }

    private static SheetData Principal(string empresa, string titulo, DateTime generadoLocal, IReadOnlyList<string> filtros, IReadOnlyList<ColumnaReporteDto> columnas,
        IReadOnlyList<object?[]> filas, string? nota, out string rango, out double[] anchos)
    {
        var sd = new SheetData();
        uint fila = 1;
        sd.Append(Fila(fila++, Texto(1, fila - 1, empresa, EstiloNegrita)));
        sd.Append(Fila(fila++, Texto(1, fila - 1, titulo, EstiloNegrita)));
        sd.Append(Fila(fila++, Texto(1, fila - 1, "Generado: " + generadoLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture))));
        sd.Append(Fila(fila++, Texto(1, fila - 1, "Filtros: " + (filtros.Count == 0 ? "ninguno" : string.Join("; ", filtros)))));
        if (!string.IsNullOrWhiteSpace(nota)) sd.Append(Fila(fila++, Texto(1, fila - 1, nota)));
        fila++; // fila en blanco

        var encabezado = fila++;
        var celdas = new List<Cell>();
        for (var i = 0; i < columnas.Count; i++) celdas.Add(Texto(i + 1, encabezado, columnas[i].Titulo, EstiloEncabezado));
        sd.Append(Fila(encabezado, celdas.ToArray()));

        anchos = columnas.Select(c => (double)Math.Clamp(c.Titulo.Length + 4, 12, 40)).ToArray();
        foreach (var datos in filas)
        {
            var cs = new List<Cell>();
            for (var i = 0; i < columnas.Count; i++)
            {
                var v = i < datos.Length ? datos[i] : null;
                var celda = Celda(i + 1, fila, v);
                if (celda is not null) cs.Add(celda);
                if (v is string s && s.Length + 2 > anchos[i]) anchos[i] = Math.Min(s.Length + 2, 50);
            }
            sd.Append(Fila(fila++, cs.ToArray()));
        }
        var ultima = Math.Max(encabezado, fila - 1);
        rango = $"A{encabezado}:{Letra(Math.Max(1, columnas.Count))}{ultima}";
        return sd;
    }

    private static SheetData Resumen(string titulo, IReadOnlyList<ResumenReporteDto> resumen)
    {
        var sd = new SheetData();
        uint fila = 1;
        sd.Append(Fila(fila, Texto(1, fila, "Resumen: " + titulo, EstiloNegrita))); fila++;
        foreach (var r in resumen)
        {
            sd.Append(Fila(fila, Texto(1, fila, r.Etiqueta), Texto(2, fila, r.Valor)));
            fila++;
        }
        return sd;
    }

    private static Row Fila(uint numero, params Cell[] celdas)
    {
        var r = new Row { RowIndex = numero };
        foreach (var c in celdas) r.Append(c);
        return r;
    }

    private static Cell? Celda(int columna, uint fila, object? v) => v switch
    {
        null => null,
        string s => Texto(columna, fila, s),
        DateOnly d => Numero(columna, fila, d.ToDateTime(TimeOnly.MinValue).ToOADate(), EstiloFecha),
        DateTime dt => Numero(columna, fila, dt.ToOADate(), EstiloFechaHora),
        bool b => Texto(columna, fila, b ? "Sí" : "No"),
        decimal m => Numero(columna, fila, (double)m, EstiloNumero),
        int or long or short or byte => Numero(columna, fila, Convert.ToDouble(v, CultureInfo.InvariantCulture), 0),
        double x => Numero(columna, fila, x, EstiloNumero),
        _ => Texto(columna, fila, v.ToString() ?? "")
    };

    // Texto en línea: Excel lo trata como literal, nunca como fórmula (=, +, -, @ al inicio no se evalúan).
    private static Cell Texto(int columna, uint fila, string texto, uint estilo = 0) => new()
    {
        CellReference = $"{Letra(columna)}{fila}", DataType = CellValues.InlineString, StyleIndex = estilo,
        InlineString = new InlineString(new Text(Limpio(texto)) { Space = SpaceProcessingModeValues.Preserve })
    };

    private static Cell Numero(int columna, uint fila, double valor, uint estilo) => new()
    {
        CellReference = $"{Letra(columna)}{fila}", DataType = CellValues.Number, StyleIndex = estilo,
        CellValue = new CellValue(valor.ToString("R", CultureInfo.InvariantCulture))
    };

    /// <summary>Quita caracteres de control que no son válidos en XML.</summary>
    private static string Limpio(string s) => new(s.Where(ch => ch is '\t' or '\n' or '\r' || ch >= ' ').ToArray());

    private static string Letra(int columna)
    {
        var s = "";
        for (var n = columna; n > 0; n = (n - 1) / 26) s = (char)('A' + (n - 1) % 26) + s;
        return s;
    }

    private static Stylesheet Estilos() => new(
        new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "dd/mm/yyyy" },
            new NumberingFormat { NumberFormatId = 165, FormatCode = "dd/mm/yyyy hh:mm" }) { Count = 2 },
        new Fonts(new Font(), new Font(new Bold())) { Count = 2 },
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFD9E2F3" }) { PatternType = PatternValues.Solid })) { Count = 3 },
        new Borders(new Border()) { Count = 1 },
        new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 1, FillId = 2, ApplyFont = true, ApplyFill = true },
            new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 165, ApplyNumberFormat = true },
            new CellFormat { NumberFormatId = 2, ApplyNumberFormat = true },
            new CellFormat { FontId = 1, ApplyFont = true }) { Count = 6 });
}
