using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NLog;

namespace asset_monitoring.Services
{
    public class ReportExportService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        static ReportExportService()
        {
            // Community licence — free for open-source / internal tools
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ── CSV ──────────────────────────────────────────────────────────────
        public FileContentResult ExportPumpsAsCsv(List<DashboardPumpDto> pumps)
        {
            Logger.Info("ExportPumpsAsCsv: generating CSV for {0} pumps", pumps.Count);

            var sb = new StringBuilder();
            sb.AppendLine("PumpId,VendorName,Location,OperatorName,OperatorMobile,JEName,JEMobile,Status,RunningMinutes,LastUpdated");
            foreach (var p in pumps)
            {
                sb.AppendLine($"{p.PumpId},{p.VendorName},{p.Location},{p.OperatorName},{p.OperatorMobile},{p.JeName},{p.JeMobile},{p.Status},{p.RunningMinutes},{p.LastUpdated:yyyy-MM-dd HH:mm:ss}");
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
    }
}
