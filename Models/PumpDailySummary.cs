using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace asset_monitoring.Models
{
    /// <summary>
    /// Pre-computed daily running summary for each pump.
    /// Aggregated from pump_status_log_tbl entries.
    /// </summary>
    public class PumpDailySummary
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SummaryId { get; set; }

        [Required]
        public int PumpId { get; set; }

        /// <summary>The date this summary covers (IST date, time = 00:00:00).</summary>
        [Required]
        public DateTime SummaryDate { get; set; }

        /// <summary>Total minutes the pump was ON this day.</summary>
        public int OnMinutes { get; set; }

        /// <summary>Total minutes the pump was OFF this day.</summary>
        public int OffMinutes { get; set; }

        /// <summary>Total minutes the pump was in MAINTENANCE this day.</summary>
        public int MaintenanceMinutes { get; set; }

        /// <summary>Number of status changes recorded this day.</summary>
        public int StatusChangeCount { get; set; }

        /// <summary>First status recorded this day.</summary>
        public PumpStatus? FirstStatus { get; set; }

        /// <summary>Last status recorded this day.</summary>
        public PumpStatus? LastStatus { get; set; }

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }

        // Navigation
        public BdaPumpMaster? PumpMaster { get; set; }
    }
}
