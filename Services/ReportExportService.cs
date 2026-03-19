using System.Text;
using Microsoft.AspNetCore.Mvc;
using asset_monitoring.Services;
using NLog;

namespace asset_monitoring.Services
{
    public class ReportExportService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public FileContentResult ExportPumpsAsCsv(List<DashboardPumpDto> pumps)
        {
            Logger.Info("ExportPumpsAsCsv: generating CSV for {0} pumps", pumps.Count);

            var sb = new StringBuilder();
            sb.AppendLine("PumpId,VendorName,Location,Status,Mobile,RunningMinutes,LastUpdated");
            foreach (var p in pumps)
            {
                sb.AppendLine($"{p.PumpId},{p.VendorName},{p.Location},{p.Status},{p.MobileNumber},{p.RunningMinutes},{p.LastUpdated:yyyy-MM-dd HH:mm:ss}");
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            var fileName = $"PumpReport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            Logger.Info("ExportPumpsAsCsv: CSV ready, fileName={0}", fileName);

            return new FileContentResult(bytes, "text/csv")
            {
                FileDownloadName = fileName
            };
        }
    }
}