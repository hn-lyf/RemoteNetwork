using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteNetwork.Config
{
    public class TunDriveConfig
    {
        public virtual string TunDriveIP { get; set; }
        public virtual string TunDriveName { get; set; } = "网卡名称";

        public virtual string P2PHostName { get; set; } = "P2P打洞域名";
        public virtual string DataExchangeHostName { get; set; } = "数据交换域名";

    }
}
