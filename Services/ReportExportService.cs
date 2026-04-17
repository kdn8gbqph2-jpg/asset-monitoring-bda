using System.Text;
using asset_monitoring.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using NLog;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace asset_monitoring.Services
{
    public class ReportExportService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly TimeZoneInfo Ist = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
        private static DateTime ToIst(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Ist);

        // ── CSV ──────────────────────────────────────────────────────────────
        public FileContentResult ExportPumpsAsCsv(List<DashboardPumpDto> pumps)
        {
            Logger.Info("ExportPumpsAsCsv: generating CSV for {0} pumps", pumps.Count);

            var sb = new StringBuilder();
            sb.AppendLine("PumpId,VendorName,Location,OperatorName,OperatorMobile,JEName,JEMobile,Status,RunningMinutes,LastUpdated");
            foreach (var p in pumps)
            {
                sb.AppendLine($"{CsvEscape(p.PumpId)},{CsvEscape(p.VendorName)},{CsvEscape(p.Location)},{CsvEscape(p.OperatorName)},{CsvEscape(p.OperatorMobile)},{CsvEscape(p.JeName)},{CsvEscape(p.JeMobile)},{CsvEscape(p.Status)},{p.RunningMinutes},{p.LastUpdated:yyyy-MM-dd HH:mm:ss}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"PumpReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            Logger.Info("ExportPumpsAsCsv: done, fileName={0}", fileName);

            return new FileContentResult(bytes, "text/csv") { FileDownloadName = fileName };
        }

        // ── XLSX ─────────────────────────────────────────────────────────────
        public FileContentResult ExportPumpsAsXlsx(List<DashboardPumpDto> pumps)
        {
            Logger.Info("ExportPumpsAsXlsx: generating XLSX for {0} pumps", pumps.Count);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Pump Report");

            string[] headers = { "Pump ID", "Vendor", "Location", "Operator Name", "Operator Mobile",
                                  "JE Name", "JE Mobile", "Status", "Running (min)", "Last Updated" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int r = 0; r < pumps.Count; r++)
            {
                var p = pumps[r];
                int row = r + 2;
                ws.Cell(row, 1).Value  = p.PumpId;
                ws.Cell(row, 2).Value  = p.VendorName    ?? "";
                ws.Cell(row, 3).Value  = p.Location      ?? "";
                ws.Cell(row, 4).Value  = p.OperatorName  ?? "";
                ws.Cell(row, 5).Value  = p.OperatorMobile ?? "";
                ws.Cell(row, 6).Value  = p.JeName        ?? "";
                ws.Cell(row, 7).Value  = p.JeMobile      ?? "";
                ws.Cell(row, 8).Value  = p.Status        ?? "";
                ws.Cell(row, 9).Value  = p.RunningMinutes;
                ws.Cell(row, 10).Value = p.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss");

                if (r % 2 == 1)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F6FF");

                ws.Cell(row, 8).Style.Font.Bold = true;
                ws.Cell(row, 8).Style.Font.FontColor = p.Status switch
                {
                    "ON"          => XLColor.FromHtml("#16a34a"),
                    "OFF"         => XLColor.FromHtml("#dc2626"),
                    "MAINTENANCE" => XLColor.FromHtml("#d97706"),
                    _             => XLColor.Black
                };
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"PumpReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            Logger.Info("ExportPumpsAsXlsx: done, fileName={0}", fileName);

            return new FileContentResult(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            { FileDownloadName = fileName };
        }

        // ── PDF ──────────────────────────────────────────────────────────────
        public FileContentResult ExportPumpsAsPdf(List<DashboardPumpDto> pumps)
        {
            Logger.Info("ExportPumpsAsPdf: generating PDF for {0} pumps", pumps.Count);

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Bharatpur Development Authority")
                               .Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                            col.Item().Text($"Pump Status Report  —  {DateTime.Now:dd MMM yyyy, HH:mm}")
                               .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(80).AlignRight()
                           .Text($"Total: {pumps.Count} pumps").FontSize(9);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(40);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2.5f);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2.5f);
                            cols.RelativeColumn(2);
                            cols.ConstantColumn(65);
                            cols.ConstantColumn(55);
                        });

                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(Colors.Blue.Darken2).Padding(4).AlignCenter();

                        string[] heads = { "ID", "Vendor", "Location", "Operator", "Op. Mobile",
                                           "JE Name", "JE Mobile", "Status", "Running" };
                        table.Header(header =>
                        {
                            foreach (var h in heads)
                                header.Cell().Element(HeaderCell)
                                      .Text(h).FontColor(Colors.White).Bold().FontSize(8);
                        });

                        for (int i = 0; i < pumps.Count; i++)
                        {
                            var p = pumps[i];
                            string bg = i % 2 == 0 ? Colors.White : "#F0F6FF";

                            IContainer DataCell(IContainer c) =>
                                c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);

                            table.Cell().Element(DataCell).Text(p.PumpId.ToString());
                            table.Cell().Element(DataCell).Text(p.VendorName    ?? "");
                            table.Cell().Element(DataCell).Text(p.Location      ?? "");
                            table.Cell().Element(DataCell).Text(p.OperatorName  ?? "");
                            table.Cell().Element(DataCell).Text(p.OperatorMobile ?? "");
                            table.Cell().Element(DataCell).Text(p.JeName        ?? "");
                            table.Cell().Element(DataCell).Text(p.JeMobile      ?? "");

                            var statusColor = p.Status switch
                            {
                                "ON"          => Colors.Green.Darken2,
                                "OFF"         => Colors.Red.Darken2,
                                "MAINTENANCE" => Colors.Orange.Darken2,
                                _             => Colors.Grey.Darken1
                            };
                            table.Cell().Element(DataCell).Text(p.Status ?? "").Bold().FontColor(statusColor);

                            var mins = p.RunningMinutes;
                            var running = mins > 0 ? (mins >= 60 ? $"{mins/60}h {mins%60}m" : $"{mins}m") : "—";
                            table.Cell().Element(DataCell).Text(running);
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Page ").FontSize(8);
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" of ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            });

            var bytes = doc.GeneratePdf();
            var fileName = $"PumpReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            Logger.Info("ExportPumpsAsPdf: done, fileName={0}", fileName);

            return new FileContentResult(bytes, "application/pdf") { FileDownloadName = fileName };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  COMPLAINT EXPORTS
        // ══════════════════════════════════════════════════════════════════════

        // ── Complaint CSV ──────────────────────────────────────────────────────
        public FileContentResult ExportComplaintsAsCsv(List<ComplaintLog> complaints)
        {
            Logger.Info("ExportComplaintsAsCsv: generating CSV for {0} complaints", complaints.Count);

            var sb = new StringBuilder();
            sb.AppendLine("ComplaintId,Date,PumpId,Location,DashboardStatus,ActualStatus,OperatorName,OperatorMobile,JEName,JEMobile,ComplainantName,ComplainantMobile,Status");
            foreach (var c in complaints)
            {
                sb.AppendLine($"{c.ComplaintId},{c.RowInsertionDateTime:yyyy-MM-dd HH:mm:ss},{c.PumpId},{CsvEscape(c.Location)},{c.DashboardStatus},{c.ActualStatus},{CsvEscape(c.OperatorName)},{c.OperatorMobile},{CsvEscape(c.JeName)},{c.JeMobile},{CsvEscape(c.ComplainantName)},{c.ComplainantMobile},{c.Status}");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"ComplaintLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return new FileContentResult(bytes, "text/csv") { FileDownloadName = fileName };
        }

        // ── Complaint XLSX ─────────────────────────────────────────────────────
        public FileContentResult ExportComplaintsAsXlsx(List<ComplaintLog> complaints)
        {
            Logger.Info("ExportComplaintsAsXlsx: generating XLSX for {0} complaints", complaints.Count);

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Complaint Log");

            string[] headers = { "#", "Date", "Pump ID", "Location", "Dashboard Status", "Actual Status",
                                  "Operator", "Op. Mobile", "JE Name", "JE Mobile",
                                  "Complainant", "Comp. Mobile", "Status" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int r = 0; r < complaints.Count; r++)
            {
                var c = complaints[r];
                int row = r + 2;
                ws.Cell(row, 1).Value  = c.ComplaintId;
                ws.Cell(row, 2).Value  = ToIst(c.RowInsertionDateTime).ToString("dd MMM yyyy, hh:mm tt");
                ws.Cell(row, 3).Value  = c.PumpId;
                ws.Cell(row, 4).Value  = c.Location       ?? "";
                ws.Cell(row, 5).Value  = c.DashboardStatus ?? "";
                ws.Cell(row, 6).Value  = c.ActualStatus    ?? "";
                ws.Cell(row, 7).Value  = c.OperatorName    ?? "";
                ws.Cell(row, 8).Value  = c.OperatorMobile  ?? "";
                ws.Cell(row, 9).Value  = c.JeName          ?? "";
                ws.Cell(row, 10).Value = c.JeMobile        ?? "";
                ws.Cell(row, 11).Value = c.ComplainantName  ?? "";
                ws.Cell(row, 12).Value = c.ComplainantMobile ?? "";
                ws.Cell(row, 13).Value = c.Status;

                if (r % 2 == 1)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F6FF");

                ws.Cell(row, 13).Style.Font.Bold = true;
                ws.Cell(row, 13).Style.Font.FontColor = c.Status switch
                {
                    "OPEN"     => XLColor.FromHtml("#dc2626"),
                    "RESOLVED" => XLColor.FromHtml("#16a34a"),
                    "REJECTED" => XLColor.FromHtml("#6b7280"),
                    _          => XLColor.Black
                };
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"ComplaintLog_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return new FileContentResult(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            { FileDownloadName = fileName };
        }

        // ── Complaint PDF ──────────────────────────────────────────────────────
        public FileContentResult ExportComplaintsAsPdf(List<ComplaintLog> complaints)
        {
            Logger.Info("ExportComplaintsAsPdf: generating PDF for {0} complaints", complaints.Count);

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Bharatpur Development Authority")
                               .Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                            col.Item().Text($"Complaint Log  —  {DateTime.Now:dd MMM yyyy, HH:mm}")
                               .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(100).AlignRight()
                           .Text($"Total: {complaints.Count}").FontSize(9);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(25);   // #
                            cols.RelativeColumn(2);    // Date
                            cols.ConstantColumn(35);   // Pump ID
                            cols.RelativeColumn(2);    // Location
                            cols.RelativeColumn(1.2f); // Dash Status
                            cols.RelativeColumn(1.2f); // Actual Status
                            cols.RelativeColumn(1.5f); // Operator
                            cols.RelativeColumn(1.3f); // Op Mobile
                            cols.RelativeColumn(1.5f); // JE
                            cols.RelativeColumn(1.3f); // JE Mobile
                            cols.RelativeColumn(1.5f); // Complainant
                            cols.RelativeColumn(1.3f); // Comp Mobile
                        });

                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(Colors.Blue.Darken2).Padding(3).AlignCenter();

                        string[] heads = { "#", "Date", "Pump", "Location", "Dash.", "Actual",
                                           "Operator", "Op. Mob.", "JE", "JE Mob.",
                                           "Complainant", "Comp. Mob." };
                        table.Header(header =>
                        {
                            foreach (var h in heads)
                                header.Cell().Element(HeaderCell)
                                      .Text(h).FontColor(Colors.White).Bold().FontSize(7);
                        });

                        for (int i = 0; i < complaints.Count; i++)
                        {
                            var c = complaints[i];
                            string bg = i % 2 == 0 ? Colors.White : "#F0F6FF";

                            IContainer DataCell(IContainer dc) =>
                                dc.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);

                            table.Cell().Element(DataCell).Text(c.ComplaintId.ToString());
                            table.Cell().Element(DataCell).Text(ToIst(c.RowInsertionDateTime).ToString("dd MMM, HH:mm"));
                            table.Cell().Element(DataCell).Text(c.PumpId.ToString());
                            table.Cell().Element(DataCell).Text(c.Location       ?? "");
                            table.Cell().Element(DataCell).Text(c.DashboardStatus ?? "");
                            table.Cell().Element(DataCell).Text(c.ActualStatus    ?? "");
                            table.Cell().Element(DataCell).Text(c.OperatorName    ?? "");
                            table.Cell().Element(DataCell).Text(c.OperatorMobile  ?? "");
                            table.Cell().Element(DataCell).Text(c.JeName          ?? "");
                            table.Cell().Element(DataCell).Text(c.JeMobile        ?? "");
                            table.Cell().Element(DataCell).Text(c.ComplainantName  ?? "");
                            table.Cell().Element(DataCell).Text(c.ComplainantMobile ?? "");
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Page ").FontSize(8);
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" of ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            });

            var bytes = doc.GeneratePdf();
            var fileName = $"ComplaintLog_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return new FileContentResult(bytes, "application/pdf") { FileDownloadName = fileName };
        }

        private static string CsvEscape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
