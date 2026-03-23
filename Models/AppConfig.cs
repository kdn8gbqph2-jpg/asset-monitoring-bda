using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace asset_monitoring.Models
{
    public class AppConfig
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ConfigId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ConfigKey { get; set; } = "";

        [MaxLength(2000)]
        public string? ConfigValue { get; set; }

        public DateTime RowUpdationDateTime { get; set; }
    }
}
