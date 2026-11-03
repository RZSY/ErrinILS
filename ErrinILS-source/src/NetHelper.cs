using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace Errin
{
    public static class NetHelper
    {
        public static List<string> GetReachableIpAddresses()
        {
            var ips = new List<string>();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                // Prioritize Up operational interfaces, Wireless (Hotspots/Wi-Fi) and Ethernet
                var validList = new List<Tuple<int, string>>();

                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    int prio = 10;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) prio = 1;
                    else if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) prio = 2;

                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ip = ua.Address.ToString();
                            // Exclude APIPA / autoconfiguration (169.254.x.x) unless no other IP exists
                            if (!ip.StartsWith("169.254.") && !ip.StartsWith("127."))
                            {
                                validList.Add(Tuple.Create(prio, ip));
                            }
                        }
                    }
                }

                foreach (var item in validList.OrderBy(x => x.Item1))
                {
                    if (!ips.Contains(item.Item2)) ips.Add(item.Item2);
                }
            }
            catch { }

            return ips;
        }

        public static void EnsureFirewallRule()
        {
            try
            {
                // Register application with Windows Firewall via COM if not already registered
                Type fwMgrType = Type.GetTypeFromProgID("HNetCfg.FwMgr", false);
                if (fwMgrType != null)
                {
                    dynamic fwMgr = Activator.CreateInstance(fwMgrType);
                    dynamic profile = fwMgr.LocalPolicy.CurrentProfile;
                    Type appType = Type.GetTypeFromProgID("HNetCfg.FwAuthorizedApplication", false);
                    if (appType != null)
                    {
                        dynamic app = Activator.CreateInstance(appType);
                        app.ProcessImageFileName = Application.ExecutablePath;
                        app.Name = "ErrinILS";
                        app.Scope = 0; // All networks (Public, Private, Domain)
                        app.IpVersion = 2; // Any IP version
                        app.Enabled = true;
                        profile.AuthorizedApplications.Add(app);
                    }
                }
            }
            catch { }
        }
    }
}
