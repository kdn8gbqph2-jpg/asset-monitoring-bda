using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace asset_monitoring.Pages
{
    public class AboutModel : PageModel
    {
        // Assembly version, computed once at process start
        public static readonly string AppVersion =
            Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "1.0.0";

        // Build date derived from the main executable's last-write time
        public static readonly string BuildDate =
            System.IO.File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location)
                .ToString("dd MMM yyyy");

        public void OnGet() { }
    }
}
