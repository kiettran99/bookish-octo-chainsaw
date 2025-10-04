using System.ComponentModel;

namespace Common.Enums;

public enum ERoles
{
    [Description("User")]
    User = 0,

    [Description("VIP User")]
    VipUser = 1,

    [Description("Partner")]
    Partner = 3,

    [Description("Administrator")]
    Administrator = 99
}
