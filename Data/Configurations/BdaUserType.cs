using System.Runtime.Serialization;

namespace asset_monitoring.Models
{
    public enum BdaUserType
    {
        ADMIN,
        OPERATOR,
        JE
    }
    public enum PumpStatus
    {
        [EnumMember(Value = "ON")]
        On,

        [EnumMember(Value = "OFF")]
        Off,

        [EnumMember(Value = "MAINTENANCE")]
        Maintenance
    }
}
