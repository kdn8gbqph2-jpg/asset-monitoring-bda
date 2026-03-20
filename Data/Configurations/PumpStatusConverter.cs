using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using asset_monitoring.Models;

namespace asset_monitoring.Data.Configurations
{
    /// <summary>
    /// Converts PumpStatus enum ↔ DB string values "ON" / "OFF" / "MAINTENANCE".
    /// HasConversion&lt;string&gt;() would store "On"/"Off" (C# name) which mismatches
    /// the MySQL enum column definition.  Use these converters instead.
    /// </summary>
    public static class PumpStatusConverter
    {
        private static PumpStatus FromString(string? v) => (v ?? "").ToUpperInvariant() switch
        {
            "ON"          => PumpStatus.On,
            "MAINTENANCE" => PumpStatus.Maintenance,
            _             => PumpStatus.Off
        };

        private static string ToString(PumpStatus v) => v switch
        {
            PumpStatus.On          => "ON",
            PumpStatus.Maintenance => "MAINTENANCE",
            _                      => "OFF"
        };

        /// <summary>For non-nullable Status columns.</summary>
        public static ValueConverter<PumpStatus, string> Instance { get; } =
            new(v => ToString(v), v => FromString(v));

        /// <summary>For nullable OldStatus / NewStatus columns.</summary>
        public static ValueConverter<PumpStatus?, string?> NullableInstance { get; } =
            new(
                v => v.HasValue ? ToString(v.Value) : null,
                v => v == null  ? (PumpStatus?)null : FromString(v)
            );
    }
}
