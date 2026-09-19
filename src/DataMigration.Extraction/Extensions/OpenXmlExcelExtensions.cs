using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DataMigration.Extraction.Extensions;

public static class OpenXmlExcelExtensions
{
    public static string[] LoadSharedStrings(this WorkbookPart workbookPart)
    {
        var sstPart = workbookPart.SharedStringTablePart;
        if (sstPart?.SharedStringTable is null) return [];
        
        return
        [
            ..sstPart.SharedStringTable.Elements<SharedStringItem>().Select(item => item.InnerText)
        ];
    }
    
    public static string[] ReadRowValues(this Row row, string[] sharedStrings)
    {
        // Determine max column index present so we can preserve column positions
        // (cells with empty values are often omitted entirely from the XML).
        var cells = row.Elements<Cell>().ToList();
        if (cells.Count == 0) return [];

        int maxCol = cells.Max(c => c.CellReference!.Value!.GetColumnIndex());
        string[] values = new string[maxCol + 1];

        foreach (var cell in cells)
        {
            int colIndex = cell.CellReference!.Value!.GetColumnIndex();
            values[colIndex] = GetCellValue(cell, sharedStrings);
        }

        return values;
    }
    
    /// <summary>
    /// Converts an Excel column reference (e.g. "C7", "AB12") into a zero-based column index.
    /// Only the leading letters are used; any trailing row number is ignored.
    /// Excel columns follow a "bijective base-26" scheme (A=1 ... Z=26, AA=27, ...),
    /// so this loops through each letter treating it as a base-26 digit (A=1, B=2, ..., Z=26),
    /// then subtracts 1 at the end to convert from Excel's 1-based numbering to a 0-based index.
    /// Examples: "A1" -> 0, "C7" -> 2, "AA1" -> 26.
    /// </summary>
    public static int GetColumnIndex(this string cellReference)
    {
        int index = 0;
        foreach (char c in cellReference)
        {
            if (!char.IsLetter(c)) break;
            int colIndexForCurrentLetter = char.ToUpper(c) - 'A' + 1;
            index = index * 26 + colIndexForCurrentLetter;
        }
        
        return index - 1;
    }
    
    private static string GetCellValue(Cell cell, string[] sharedStrings)
    {
        string raw = cell.CellValue?.InnerText ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return int.TryParse(raw, out int idx) && idx < sharedStrings.Length
                ? sharedStrings[idx]
                : string.Empty;
        }

        return raw;
    }
}