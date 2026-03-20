using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace asset_monitoring.Models
{
    public class PumpStatusEntry
    {
        [Key]
        [ForeignKey(nameof(PumpMaster))]
        public int PumpId { get; set; }

        public PumpStatus Status { get; set; }

        [MaxLength(255)]
        public string? Remarks { get; set; }

        public DateTime? CurrentStartTime { get; set; }

        public DateTime? CurrentEndTime { get; set; }

        public DateTime? LastRunTime { get; set; }

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }   // name of the user who last updated

        [MaxLength(15)]
        public string? OperatorMobile { get; set; } // mobile of assigned operator (used for filtering)

        [MaxLength(15)]
        public string? JeMobile { get; set; }    // mobile of assigned JE

        public int RowActionCount { get; set; } = 1;

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }

        // Navigation
        public BdaPumpMaster? PumpMaster { get; set; }
    }
}