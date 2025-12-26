using asset_monitoring.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace asset_monitoring.Models
{
    public class PumpStatusLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }
        [Required]
        public int PumpId { get; set; }

        [MaxLength(150)]
        public string? Location { get; set; }

        public PumpStatus? OldStatus { get; set; }

        public PumpStatus NewStatus { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [MaxLength(255)]
        public string? Remarks { get; set; }

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }

        // Navigation
        public BdaPumpMaster? PumpMaster { get; set; }
    }
}