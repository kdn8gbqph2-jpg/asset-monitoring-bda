using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace asset_monitoring.Models
{
    public class ComplaintLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ComplaintId { get; set; }

        [Required]
        public int PumpId { get; set; }

        [MaxLength(150)]
        public string? Location { get; set; }

        [MaxLength(20)]
        public string? DashboardStatus { get; set; }  // ON/OFF/MAINTENANCE

        [MaxLength(20)]
        public string? ActualStatus { get; set; }      // ON/OFF/NOT_WORKING

        [MaxLength(100)]
        public string? OperatorName { get; set; }

        [MaxLength(20)]
        public string? OperatorMobile { get; set; }

        [MaxLength(100)]
        public string? JeName { get; set; }

        [MaxLength(20)]
        public string? JeMobile { get; set; }

        [MaxLength(100)]
        public string? ComplainantName { get; set; }

        [MaxLength(20)]
        public string? ComplainantMobile { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "OPEN"; // OPEN/RESOLVED/REJECTED

        [MaxLength(500)]
        public string? Remarks { get; set; }

        /// <summary>
        /// Relative path (under wwwroot) of an optional photo attached to the
        /// complaint at submission time. Null when the complainant didn't
        /// attach a photo. Example: "uploads/complaints/2026-05/abcd.jpg".
        /// </summary>
        [MaxLength(255)]
        public string? PhotoPath { get; set; }

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }

        // Navigation
        public BdaPumpMaster? PumpMaster { get; set; }
    }
}
