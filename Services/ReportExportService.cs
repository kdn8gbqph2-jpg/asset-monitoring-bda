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
            sb.AppendLine("PumpId,VendorName,Location,OperatorName,OperatorMobile,JEName,JEMobile,Status,CurrentRunMinutes,LastUpdated");
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
                                  "JE Name", "JE Mobile", "Status", "Current Run (min)", "Last Updated" };
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
                                           "JE Name", "JE Mobile", "Status", "Current Run" };
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
        //  PUMP RUNNING SUMMARY EXPORTS
        //  Same trio of formats as the pump report. TODAY columns are emitted only
        //  when the requested period is the current month — for past months they
        //  would be all zeros, so we omit them to keep the report readable.
        // ══════════════════════════════════════════════════════════════════════

        private static string RunningSummaryFileBase(int year, int month) =>
            $"PumpRunningSummary_{year:D4}-{month:D2}";

        private static string FormatMinutes(int minutes)
        {
            if (minutes <= 0) return "—";
            return minutes >= 60 ? $"{minutes / 60}h {minutes % 60}m" : $"{minutes}m";
        }

        // ── Running Summary CSV ──────────────────────────────────────────────
        public FileContentResult ExportRunningSummaryAsCsv(
            List<PumpRunningSummaryDto> rows, int year, int month, bool isCurrentMonth)
        {
            Logger.Info("ExportRunningSummaryAsCsv: {0} rows, period={1:D4}-{2:D2}, isCurrentMonth={3}",
                rows.Count, year, month, isCurrentMonth);

            var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy");
            var sb = new StringBuilder();

            if (isCurrentMonth)
            {
                sb.AppendLine("PumpId,Vendor,Location,Operator,Status," +
                              "TodayRun(min),TodayOff(min),TodayMaint(min)," +
                              $"\"{monthLabel} Run(min)\",\"{monthLabel} Off(min)\",\"{monthLabel} Maint(min)\"," +
                              "LastUpdated");
                foreach (var r in rows)
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        CsvEscape(r.PumpId), CsvEscape(r.VendorName), CsvEscape(r.Location),
                        CsvEscape(r.OperatorName), CsvEscape(r.Status),
                        r.TodayRunMinutes.ToString(), r.TodayOffMinutes.ToString(), r.TodayMaintenanceMinutes.ToString(),
                        r.MonthRunMinutes.ToString(), r.MonthOffMinutes.ToString(), r.MonthMaintenanceMinutes.ToString(),
                        r.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss")
                    }));
                }
            }
            else
            {
                sb.AppendLine("PumpId,Vendor,Location,Operator,Status," +
                              $"\"{monthLabel} Run(min)\",\"{monthLabel} Off(min)\",\"{monthLabel} Maint(min)\"," +
                              "LastUpdated");
                foreach (var r in rows)
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        CsvEscape(r.PumpId), CsvEscape(r.VendorName), CsvEscape(r.Location),
                        CsvEscape(r.OperatorName), CsvEscape(r.Status),
                        r.MonthRunMinutes.ToString(), r.MonthOffMinutes.ToString(), r.MonthMaintenanceMinutes.ToString(),
                        r.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss")
                    }));
                }
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"{RunningSummaryFileBase(year, month)}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return new FileContentResult(bytes, "text/csv") { FileDownloadName = fileName };
        }

        // ── Running Summary XLSX ─────────────────────────────────────────────
        public FileContentResult ExportRunningSummaryAsXlsx(
            List<PumpRunningSummaryDto> rows, int year, int month, bool isCurrentMonth)
        {
            Logger.Info("ExportRunningSummaryAsXlsx: {0} rows, period={1:D4}-{2:D2}, isCurrentMonth={3}",
                rows.Count, year, month, isCurrentMonth);

            var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add($"Summary {year:D4}-{month:D2}");

            // Build headers conditionally — TODAY group only for the current month.
            var headers = new List<string> { "Pump ID", "Vendor", "Location", "Operator", "Status" };
            if (isCurrentMonth)
                headers.AddRange(new[] { "Today Run (min)", "Today Off (min)", "Today Maint. (min)" });
            headers.AddRange(new[]
            {
                $"{monthLabel} Run (min)",
                $"{monthLabel} Off (min)",
                $"{monthLabel} Maint. (min)",
                "Last Updated"
            });

            for (int i = 0; i < headers.Count; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int statusCol = 5;
            int lastUpdatedCol = headers.Count;

            for (int r = 0; r < rows.Count; r++)
            {
                var p = rows[r];
                int row = r + 2;
                int col = 1;
                ws.Cell(row, col++).Value = p.PumpId;
                ws.Cell(row, col++).Value = p.VendorName ?? "";
                ws.Cell(row, col++).Value = p.Location   ?? "";
                ws.Cell(row, col++).Value = p.OperatorName ?? "";
                ws.Cell(row, col++).Value = p.Status     ?? "";
                if (isCurrentMonth)
                {
                    ws.Cell(row, col++).Value = p.TodayRunMinutes;
                    ws.Cell(row, col++).Value = p.TodayOffMinutes;
                    ws.Cell(row, col++).Value = p.TodayMaintenanceMinutes;
                }
                ws.Cell(row, col++).Value = p.MonthRunMinutes;
                ws.Cell(row, col++).Value = p.MonthOffMinutes;
                ws.Cell(row, col++).Value = p.MonthMaintenanceMinutes;
                ws.Cell(row, col++).Value = p.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss");

                if (r % 2 == 1)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F6FF");

                ws.Cell(row, statusCol).Style.Font.Bold = true;
                ws.Cell(row, statusCol).Style.Font.FontColor = p.Status switch
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
            var fileName = $"{RunningSummaryFileBase(year, month)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return new FileContentResult(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            { FileDownloadName = fileName };
        }

        // ── Running Log (detailed status-change events for selected pumps) XLSX ──
        // One section per selected pump: the pump's month running total (authoritative,
        // from the daily summaries) followed by its status-change events. A grand
        // "TOTAL RUNNING HOURS" for all selected pumps is appended at the end.
        public FileContentResult ExportRunningLogAsXlsx(
            List<PumpRunningSummaryDto> selectedPumps,
            Dictionary<int, List<PumpLogDto>> eventsByPump,
            int year, int month)
        {
            var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy");
            Logger.Info("ExportRunningLogAsXlsx: {0} pumps, period={1:D4}-{2:D2}", selectedPumps.Count, year, month);

            const int LASTCOL = 8;
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add($"Running Log {year:D4}-{month:D2}");

            int row = 1;
            ws.Cell(row, 1).Value = $"Pump Running Log — {monthLabel}";
            ws.Range(row, 1, row, LASTCOL).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            row += 2;

            int grandTotalMinutes = 0;

            foreach (var pump in selectedPumps)
            {
                int.TryParse(pump.PumpId, out var pid);
                var events = eventsByPump.TryGetValue(pid, out var ev) ? ev : new List<PumpLogDto>();
                grandTotalMinutes += pump.MonthRunMinutes;

                // Pump section header
                ws.Cell(row, 1).Value = $"Pump {pump.PumpId}  ·  {pump.VendorName}  ·  {pump.Location}";
                ws.Range(row, 1, row, LASTCOL).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
                row++;

                // Authoritative monthly running total (matches the dashboard card)
                ws.Cell(row, 1).Value = $"Total Running Hours ({monthLabel}): {(pump.MonthRunMinutes > 0 ? FormatMinutes(pump.MonthRunMinutes) : "0m")}";
                ws.Range(row, 1, row, LASTCOL).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#16A34A");
                row++;

                // Column headers
                string[] cols = { "#", "Old", "New", "Started (IST)", "Ended (IST)", "Duration", "Remarks", "Operator" };
                for (int c = 0; c < cols.Length; c++)
                {
                    var cell = ws.Cell(row, c + 1);
                    cell.Value = cols[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
                }
                row++;

                if (events.Count == 0)
                {
                    ws.Cell(row, 1).Value = "No status-change events in this month.";
                    ws.Range(row, 1, row, LASTCOL).Merge();
                    ws.Cell(row, 1).Style.Font.Italic = true;
                    ws.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
                    row++;
                }
                else
                {
                    int serial = 1;
                    foreach (var e in events)
                    {
                        int col = 1;
                        ws.Cell(row, col++).Value = serial++;
                        ws.Cell(row, col++).Value = e.OldStatus;
                        ws.Cell(row, col++).Value = e.NewStatus;
                        ws.Cell(row, col++).Value = e.StartTime.HasValue
                            ? ToIst(e.StartTime.Value).ToString("dd MMM yyyy, hh:mm tt") : "—";
                        ws.Cell(row, col++).Value = e.EndTime.HasValue
                            ? ToIst(e.EndTime.Value).ToString("dd MMM yyyy, hh:mm tt") : "—";
                        ws.Cell(row, col++).Value = e.DurationMinutes.HasValue
                            ? FormatMinutes(e.DurationMinutes.Value) : "—";
                        ws.Cell(row, col++).Value = e.Remarks ?? "";
                        ws.Cell(row, col++).Value = e.UpdatedBy ?? "";

                        // Tint by the status held during the period (Old): green=running.
                        var tint = e.OldStatus switch
                        {
                            "ON"          => "#E6F4EA",
                            "OFF"         => "#FDECEA",
                            "MAINTENANCE" => "#FFF6E0",
                            _             => null
                        };
                        if (tint != null)
                            ws.Range(row, 1, row, LASTCOL).Style.Fill.BackgroundColor = XLColor.FromHtml(tint);
                        row++;
                    }
                }
                row++; // spacer between pumps
            }

            // Grand total across all selected pumps
            ws.Cell(row, 1).Value =
                $"TOTAL RUNNING HOURS — {selectedPumps.Count} pump(s), {monthLabel}: {(grandTotalMinutes > 0 ? FormatMinutes(grandTotalMinutes) : "0m")}";
            ws.Range(row, 1, row, LASTCOL).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#16A34A");

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"PumpRunningLog_{year:D4}-{month:D2}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return new FileContentResult(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            { FileDownloadName = fileName };
        }

        // ── Running Summary PDF ──────────────────────────────────────────────
        public FileContentResult ExportRunningSummaryAsPdf(
            List<PumpRunningSummaryDto> rows, int year, int month, bool isCurrentMonth)
        {
            Logger.Info("ExportRunningSummaryAsPdf: {0} rows, period={1:D4}-{2:D2}, isCurrentMonth={3}",
                rows.Count, year, month, isCurrentMonth);

            var monthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy");

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
                            col.Item().Text($"Pump Running Summary  —  {monthLabel}")
                               .FontSize(10).FontColor(Colors.Grey.Darken1);
                            col.Item().Text($"Generated {DateTime.Now:dd MMM yyyy, HH:mm}")
                               .FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                        row.ConstantItem(100).AlignRight()
                           .Text($"Total: {rows.Count} pumps").FontSize(9);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(35);   // Pump ID
                            cols.RelativeColumn(2.5f); // Vendor
                            cols.RelativeColumn(2.5f); // Location
                            cols.RelativeColumn(2);    // Operator
                            cols.ConstantColumn(55);   // Status
                            if (isCurrentMonth)
                            {
                                cols.ConstantColumn(55); // Today Run
                                cols.ConstantColumn(55); // Today Off
                                cols.ConstantColumn(55); // Today Maint
                            }
                            cols.ConstantColumn(60); // Month Run
                            cols.ConstantColumn(60); // Month Off
                            cols.ConstantColumn(60); // Month Maint
                        });

                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(Colors.Blue.Darken2).Padding(4).AlignCenter();

                        var heads = new List<string> { "ID", "Vendor", "Location", "Operator", "Status" };
                        if (isCurrentMonth)
                            heads.AddRange(new[] { "Today\nRun", "Today\nOff", "Today\nMaint." });
                        heads.AddRange(new[]
                        {
                            $"{monthLabel}\nRun",
                            $"{monthLabel}\nOff",
                            $"{monthLabel}\nMaint."
                        });

                        table.Header(header =>
                        {
                            foreach (var h in heads)
                                header.Cell().Element(HeaderCell)
                                      .Text(h).FontColor(Colors.White).Bold().FontSize(7);
                        });

                        for (int i = 0; i < rows.Count; i++)
                        {
                            var p = rows[i];
                            string bg = i % 2 == 0 ? Colors.White : "#F0F6FF";

                            IContainer DataCell(IContainer c) =>
                                c.Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);

                            table.Cell().Element(DataCell).Text(p.PumpId);
                            table.Cell().Element(DataCell).Text(p.VendorName   ?? "");
                            table.Cell().Element(DataCell).Text(p.Location     ?? "");
                            table.Cell().Element(DataCell).Text(p.OperatorName ?? "");

                            var statusColor = p.Status switch
                            {
                                "ON"          => Colors.Green.Darken2,
                                "OFF"         => Colors.Red.Darken2,
                                "MAINTENANCE" => Colors.Orange.Darken2,
                                _             => Colors.Grey.Darken1
                            };
                            table.Cell().Element(DataCell).Text(p.Status ?? "").Bold().FontColor(statusColor);

                            if (isCurrentMonth)
                            {
                                table.Cell().Element(DataCell).AlignRight().Text(FormatMinutes(p.TodayRunMinutes));
                                table.Cell().Element(DataCell).AlignRight().Text(FormatMinutes(p.TodayOffMinutes));
                                table.Cell().Element(DataCell).AlignRight().Text(FormatMinutes(p.TodayMaintenanceMinutes));
                            }
                            table.Cell().Element(DataCell).AlignRight().Text(FormatMinutes(p.MonthRunMinutes));
                            table.Cell().Element(DataCell).AlignRight().Text(FormatMinutes(p.MonthOffMinutes));
                            table.Cell().Element(DataCell).AlignRight().Text(FormatMinutes(p.MonthMaintenanceMinutes));
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
            var fileName = $"{RunningSummaryFileBase(year, month)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
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
