using System.Runtime.Serialization;

namespace asset_monitoring.Models
{
    public enum BdaUserType
    {
        [EnumMember(Value = "ADMIN")]
        Admin,

        [EnumMember(Value = "OPERATOR")]
        Operator,

        [EnumMember(Value = "BDA_OFFICIAL")]
        BdaOfficial
    }
    public enum PumpStatus
    {
        [EnumMember(Value = "ON")]
        on,

        [EnumMember(Value = "OFF")]
        off,

        [EnumMember(Value = "MAINTAINENCE")]
        maintenance
    }
}
