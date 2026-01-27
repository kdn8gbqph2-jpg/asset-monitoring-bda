using System.Text;
using Microsoft.AspNetCore.Mvc;
using asset_monitoring.Services;

namespace asset_monitoring.Services
{
    public class ReportExportService
    {
        // Example: Export pumps as CSV
        public FileContentResult ExportPumpsAsCsv(List<DashboardPumpDto> pumps)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PumpId,VendorName,Location,Status,Mobile,RunningMinutes,LastUpdated");
            foreach (var p in pumps)
            {
                sb.AppendLine($"{p.PumpId},{p.VendorName},{p.Location},{p.Status},{p.MobileNumber},{p.RunningMinutes},{p.LastUpdated:yyyy-MM-dd HH:mm:ss}");
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return new FileContentResult(bytes, "text/csv")
            {
                FileDownloadName = $"PumpReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
        }
    }
}