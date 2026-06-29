using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace asset_monitoring.Models
{
    /// <summary>
    /// A browser Web Push subscription for a logged-in user. One user (by UserId /
    /// Mobile) may have several rows — one per device/browser.
    /// </summary>
    public class PushSubscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int? UserId { get; set; }

        [MaxLength(15)]
        public string? Mobile { get; set; }

        [MaxLength(20)]
        public string? UserType { get; set; }

        [Required]
        [MaxLength(500)]
        public string Endpoint { get; set; } = "";

        [Required]
        [MaxLength(255)]
        public string P256dh { get; set; } = "";

        [Required]
        [MaxLength(255)]
        public string Auth { get; set; } = "";

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }
    }
}
