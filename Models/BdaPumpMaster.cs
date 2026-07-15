using System;
using System.ComponentModel.DataAnnotations;

namespace asset_monitoring.Models
{
    public class BdaPumpMaster
    {
        [Key]
        public int PumpId { get; set; }

        /// <summary>
        /// LEGACY free-text composite (e.g. "Pump_2_Devanshi_contractor"). Still
        /// written and still the display fallback for pumps that have no VendorId
        /// (the inactive test rows). Superseded by VendorId + PumpNo.
        /// </summary>
        public string VendorName { get; set; } = null!;

        /// <summary>Contractor this pump belongs to. NULL for un-migrated rows.</summary>
        public int? VendorId { get; set; }

        /// <summary>Pump number *within its contractor* (1..n), not a global id.</summary>
        public int? PumpNo { get; set; }

        /// <summary>Snapshot of the original VendorName taken during the Phase 2 backfill.</summary>
        [MaxLength(100)]
        public string? LegacyVendorName { get; set; }

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        public string? Category { get; set; }

        public bool IsActive { get; set; } = true;

        public int RowActionCount { get; set; } = 1;

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }
    }
}