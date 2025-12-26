using System;
using System.ComponentModel.DataAnnotations;

namespace asset_monitoring.Models
{
    public class BdaPumpMaster
    {
        [Key]
        public int PumpId { get; set; }

        public string VendorName { get; set; } = null!;

        public string? Category { get; set; }

        public bool IsActive { get; set; } = true;

        public int RowActionCount { get; set; } = 1;

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }
    }
}