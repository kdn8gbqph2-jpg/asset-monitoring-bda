using System;
using System.ComponentModel.DataAnnotations;

namespace asset_monitoring.Models
{
    public class BdaUserMaster
    {
        [Key]
        public int UserId { get; set; }

        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Username { get; set; }

        // Stored as enum string in DB; configured in EF mapping
        public BdaUserType? UserType { get; set; }

        [MaxLength(255)]
        public string? Password { get; set; }

        [MaxLength(15)]
        public string? MobileNumber { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime RowInsertionDateTime { get; set; }

        public DateTime RowUpdationDateTime { get; set; }
    }
}