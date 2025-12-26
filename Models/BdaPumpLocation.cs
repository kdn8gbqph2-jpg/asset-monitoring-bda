using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace asset_monitoring.Models
{
    public class BdaPumpLocation
    {
        [Key]
        [ForeignKey(nameof(PumpMaster))]
        [Required]
        public int PumpId { get; set; }

        [MaxLength(150)]
        public string? LocationName { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(11,8)")]
        public decimal? Longitude { get; set; }

        public int RowActionCount { get; set; } = 1;

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }

        // Navigation to master (optional on this side)
        public BdaPumpMaster? PumpMaster { get; set; }
    }
}