namespace asset_monitoring.Models
{
    public class PumpEditDto
    {
        public int PumpId { get; set; }
        public string VendorName { get; set; } = "";
        public string Category { get; set; } = "";
        public string LocationName { get; set; } = "";
        public string Status { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
