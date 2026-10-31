using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using System.Globalization;

namespace SiteFlow.API.Services;

public static class ReportFiles
{
    // Inline strings prevent spreadsheet formula execution in user-supplied cells.
    public static byte[] Excel(IReadOnlyList<string[]> rows)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
            Entry("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Entry("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Entry("xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="SiteFlow Report" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Entry("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
                """);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheet = new XElement(ns + "worksheet", new XElement(ns + "sheetData",
                rows.Select((row, index) => new XElement(ns + "row", new XAttribute("r", index + 1),
                    row.Select((value, column) =>
                    {
                        var numeric = index > 0 && (rows[0][column].Contains("LKR") || rows[0][column] == "Quantity") &&
                            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
                        return numeric
                            ? new XElement(ns + "c", new XAttribute("t", "n"), new XElement(ns + "v", value))
                            : new XElement(ns + "c", new XAttribute("t", "inlineStr"),
                                new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"),
                                    new string(value.Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray()))));
                    })))));
            Entry("xl/worksheets/sheet1.xml", sheet.ToString(SaveOptions.DisableFormatting));
        }
        return output.ToArray();
    }

    public static byte[] Pdf(string title, IReadOnlyList<string[]> rows)
    {
        // One labelled field per line keeps wide reports legible on A4 pages.
        var lines = new List<string> { "SiteFlow ERP - " + title, "Generated UTC: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"), "" };
        if (rows.Count > 0)
            foreach (var row in rows.Skip(1))
            {
                for (var i = 0; i < row.Length; i++)
                {
                    var text = $"{rows[0][i]}: {row[i]}".Replace('\r', ' ').Replace('\n', ' ');
                    while (text.Length > 95) { lines.Add(text[..95]); text = text[95..]; }
                    lines.Add(text);
                }
                lines.Add("");
            }
        if (rows.Count <= 1) lines.Add("No records found.");
        var pages = lines.Chunk(48).ToList();
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
        var kids = new List<string>();
        foreach (var page in pages)
        {
            var id = objects.Count + 1;
            kids.Add($"{id} 0 R");
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {id + 1} 0 R >>");
            static string Escape(string value) => new string(value.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            var content = "BT /F1 10 Tf 45 800 Td 15 TL\n" + string.Join("\n", page.Select(line => $"({Escape(line)}) Tj T*")) + "\nET";
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }
        objects[1] = $"<< /Type /Pages /Count {pages.Count} /Kids [{string.Join(" ", kids)}] >>";
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
