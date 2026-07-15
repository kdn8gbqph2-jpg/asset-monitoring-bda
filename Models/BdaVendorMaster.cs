using System;
using System.ComponentModel.DataAnnotations;

namespace asset_monitoring.Models
{
    /// <summary>
    /// A contractor. One contractor owns many pumps; each pump carries its own
    /// per-contractor number (BdaPumpMaster.PumpNo), so a pump is identified to
    /// users as "{VendorName} - Pump {PumpNo}" instead of the old free-text
    /// composite that used to live in BdaPumpMaster.VendorName.
    /// </summary>
    public class BdaVendorMaster
    {
        [Key]
        public int VendorId { get; set; }

        [MaxLength(100)]
        public string VendorName { get; set; } = null!;

        [MaxLength(15)]
        public string? ContactMobile { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }
    }
}
