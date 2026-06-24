using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace asset_monitoring.Data
{
    /// <summary>
    /// Pins every database connection's session time zone to UTC.
    ///
    /// Running-hours math assumes ALL stored timestamps are UTC. The app already
    /// writes <c>DateTime.UtcNow</c> explicitly, but the schema's
    /// <c>DEFAULT CURRENT_TIMESTAMP</c> / <c>ON UPDATE CURRENT_TIMESTAMP</c> columns
    /// evaluate in the MySQL <em>session</em> time zone. If the server is ever not
    /// on UTC (managed MySQL, a TZ change, a restore), any DB-defaulted timestamp
    /// would silently be off by the server offset and corrupt day-bucketing.
    /// Forcing <c>+00:00</c> on connect enforces the UTC assumption at the source.
    ///
    /// Note: DATETIME columns are timezone-naive (no conversion on read/write), so
    /// this only affects CURRENT_TIMESTAMP/NOW() evaluation — it does not alter the
    /// app's explicit UtcNow writes. '+00:00' is used (not 'UTC') so it works
    /// without the MySQL named-time-zone tables being loaded.
    /// </summary>
    public sealed class UtcSessionTimeZoneInterceptor : DbConnectionInterceptor
    {
        private const string SetUtc = "SET SESSION time_zone = '+00:00'";

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = SetUtc;
            cmd.ExecuteNonQuery();
            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection, ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = SetUtc;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
