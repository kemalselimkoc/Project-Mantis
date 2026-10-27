// Project Mantis — Windows system-care utility by kemalselimkoc.
// Source: https://github.com/kemalselimkoc/Project-Mantis
// Copyright (c) 2026 kemalselimkoc. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectMantis
{
    // Local network scan (spec 3.5). Maps ONLY the private subnet this PC is connected to: no address
    // can be typed in, public or oversized ranges are refused, and only a few common ports are touched.
    // Open ports are reported as information about your own devices, never probed further.
    internal static class NetworkScanner
    {
        private static readonly int[] Ports = { 22, 80, 135, 139, 443, 445, 3389, 8080 };

        private sealed class Host { public IPAddress Ip; public string Mac, Name; public bool Self, Gateway; public List<int> Open = new List<int>(); }

        public static ModuleResult Run(CancellationToken token)
        {
            ModuleResult r = new ModuleResult { ModuleId = "network" };
            IPAddress self, mask, gateway;
            if (!LocalSubnet(out self, out mask, out gateway))
            {
                r.Add(Finding.Issue("NET_NONE", Severity.Info, "Taranacak yerel ağ bulunamadı", "No local network to scan", "Bilgisayar bir ağa bağlı değil veya bağlantı özel (yerel) bir adres kullanmıyor.", "The PC is not connected, or the connection does not use a private (local) address."));
                return r;
            }
            byte[] ip = self.GetAddressBytes(), m = mask.GetAddressBytes();
            int prefix = m.Sum(b => Convert.ToString(b, 2).Count(c => c == '1'));
            bool limited = prefix < 24;
            if (limited) m = new byte[] { 255, 255, 255, 0 };   // large networks: only this PC's /24 neighbourhood
            uint net = ToUInt(ip) & ToUInt(m), broadcast = net | ~ToUInt(m);
            List<IPAddress> targets = new List<IPAddress>();
            for (uint a = net + 1; a < broadcast && targets.Count < 254; a++) targets.Add(FromUInt(a));
            string range = FromUInt(net) + "/" + (limited ? 24 : prefix);

            // Discovery: ping sweep (300 ms, 64 at a time), then whatever the ARP cache learned.
            HashSet<string> alive = new HashSet<string>();
            using (SemaphoreSlim gate = new SemaphoreSlim(64))
            {
                Task[] pings = targets.Select(t => Task.Run(async () =>
                {
                    await gate.WaitAsync(token);
                    try { using (Ping p = new Ping()) { PingReply reply = await p.SendPingAsync(t, 300); if (reply.Status == IPStatus.Success) lock (alive) alive.Add(t.ToString()); } }
                    catch { }
                    finally { gate.Release(); }
                })).ToArray();
                try { Task.WaitAll(pings, token); } catch (AggregateException) { }
            }
            token.ThrowIfCancellationRequested();
            Dictionary<string, string> arp = Native.ArpTable();
            foreach (KeyValuePair<string, string> kv in arp)
            {
                IPAddress a; if (!IPAddress.TryParse(kv.Key, out a)) continue;
                uint v = ToUInt(a.GetAddressBytes());
                if (v > net && v < broadcast) alive.Add(kv.Key);
            }
            alive.Add(self.ToString());

            List<Host> hosts = alive.Select(s => IPAddress.Parse(s)).OrderBy(a => ToUInt(a.GetAddressBytes()))
                .Select(a => new Host { Ip = a, Mac = arp.ContainsKey(a.ToString()) ? arp[a.ToString()] : null, Self = a.Equals(self), Gateway = gateway != null && a.Equals(gateway) }).ToList();

            // Names and a light port check, both with short timeouts and bounded concurrency.
            using (SemaphoreSlim gate = new SemaphoreSlim(32))
            {
                List<Task> work = new List<Task>();
                foreach (Host h in hosts)
                {
                    Host host = h;
                    work.Add(Task.Run(() => { host.Name = HostName(host.Ip); }));
                    foreach (int port in Ports)
                    {
                        int pt = port;
                        work.Add(Task.Run(async () =>
                        {
                            await gate.WaitAsync(token);
                            try { if (await IsOpen(host.Ip, pt, 400)) lock (host.Open) host.Open.Add(pt); }
                            catch { }
                            finally { gate.Release(); }
                        }));
                    }
                }
                try { Task.WaitAll(work.ToArray(), token); } catch (AggregateException) { }
            }
            token.ThrowIfCancellationRequested();

            string devices = string.Join("\n", hosts.Select(h => Describe(h, true)));
            string devicesEn = string.Join("\n", hosts.Select(h => Describe(h, false)));
            r.Add(Finding.Issue("NET_DEVICES", Severity.Info, range + " ağında " + hosts.Count + " cihaz bulundu", hosts.Count + " devices found on " + range,
                devices + (limited ? "\nAğ /24'ten büyük; yalnızca bu bilgisayarın çevresindeki 254 adres tarandı." : ""), devicesEn + (limited ? "\nThe network is larger than /24; only the 254 addresses around this PC were scanned." : "")));

            Host me = hosts.FirstOrDefault(h => h.Self);
            if (me != null && (me.Open.Contains(445) || me.Open.Contains(3389)))
                r.Add(Finding.Issue("NET_SELF_EXPOSED", Severity.Info, "Bu bilgisayar ağda dosya paylaşımı / Uzak Masaüstü sunuyor", "This PC offers file sharing / Remote Desktop on the network",
                    "Açık: " + string.Join(", ", me.Open.Where(p => p == 445 || p == 3389).Select(PortName)) + ". Sağlamlaştırma Denetimi'ndeki SMBv1, RDP ve Güvenlik Duvarı sonuçlarıyla birlikte değerlendir.",
                    "Open: " + string.Join(", ", me.Open.Where(p => p == 445 || p == 3389).Select(PortName)) + ". Review together with the SMBv1, RDP and Firewall results of the Hardening Audit."));
            List<Host> remoteDesk = hosts.Where(h => !h.Self && h.Open.Contains(3389)).ToList();
            if (remoteDesk.Count > 0)
                r.Add(Finding.Issue("NET_RDP_DEVICES", Severity.Info, remoteDesk.Count + " cihaz Uzak Masaüstü kabul ediyor", remoteDesk.Count + " devices accept Remote Desktop",
                    string.Join(", ", remoteDesk.Select(h => h.Name ?? h.Ip.ToString())) + " — tanıdığın cihazlar değilse ağ yöneticine ya da modem ayarlarına bak.", string.Join(", ", remoteDesk.Select(h => h.Name ?? h.Ip.ToString())) + " — if you don't know these devices, check with your network admin or router settings."));
            Host router = hosts.FirstOrDefault(h => h.Gateway);
            if (router != null && router.Open.Contains(80) && !router.Open.Contains(443))
                r.Add(Finding.Issue("NET_ROUTER_HTTP", Severity.Info, "Modem yönetim sayfası şifresiz (HTTP)", "Router admin page is unencrypted (HTTP)", "Modemin yönetim paneline yalnızca HTTP ile erişiliyor; varsayılan parolayı değiştirdiğinden emin ol.", "The router's admin panel is only reachable over HTTP; make sure its default password has been changed."));
            return r;
        }

        private static string Describe(Host h, bool tr)
        {
            string role = h.Self ? (tr ? " (bu bilgisayar)" : " (this PC)") : h.Gateway ? (tr ? " (modem/ağ geçidi)" : " (router/gateway)") : "";
            string ports = h.Open.Count == 0 ? "" : "  ·  " + string.Join(", ", h.Open.OrderBy(p => p).Select(PortName));
            return h.Ip + role + (string.IsNullOrEmpty(h.Name) ? "" : "  " + h.Name) + (h.Mac == null ? "" : "  [" + h.Mac + "]") + ports;
        }

        private static string PortName(int p)
        {
            switch (p)
            {
                case 22: return "22 SSH"; case 80: return "80 HTTP"; case 135: return "135 RPC"; case 139: return "139 NetBIOS";
                case 443: return "443 HTTPS"; case 445: return "445 SMB"; case 3389: return "3389 RDP"; case 8080: return "8080 HTTP-alt";
                default: return p.ToString();
            }
        }

        // The connected interface with a default gateway and a private (RFC 1918 / CGNAT) IPv4 address.
        private static bool LocalSubnet(out IPAddress self, out IPAddress mask, out IPAddress gateway)
        {
            self = mask = gateway = null;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces().OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Count))
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                IPInterfaceProperties props = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation u in props.UnicastAddresses)
                {
                    if (u.Address.AddressFamily != AddressFamily.InterNetwork || u.IPv4Mask == null || !IsPrivate(u.Address)) continue;
                    self = u.Address; mask = u.IPv4Mask;
                    GatewayIPAddressInformation g = props.GatewayAddresses.FirstOrDefault(x => x.Address.AddressFamily == AddressFamily.InterNetwork);
                    gateway = g == null ? null : g.Address;
                    return true;
                }
            }
            return false;
        }

        private static bool IsPrivate(IPAddress a)
        {
            byte[] b = a.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }

        private static async Task<bool> IsOpen(IPAddress ip, int port, int timeoutMs)
        {
            using (TcpClient c = new TcpClient())
            {
                Task connect = c.ConnectAsync(ip, port);
                if (await Task.WhenAny(connect, Task.Delay(timeoutMs)) != connect) return false;
                try { await connect; return c.Connected; } catch { return false; }
            }
        }

        private static string HostName(IPAddress ip)
        {
            try
            {
                Task<IPHostEntry> t = Dns.GetHostEntryAsync(ip);
                if (!t.Wait(1200)) return null;
                string name = t.Result.HostName;
                return name == ip.ToString() ? null : name;
            }
            catch { return null; }
        }

        private static uint ToUInt(byte[] b) { return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3]; }
        private static IPAddress FromUInt(uint v) { return new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }); }
    }
}
