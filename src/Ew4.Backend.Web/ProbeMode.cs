using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Ew4.Interface.Common;

namespace Ew4.Backend.Web;

/// <summary>
/// <c>--probe</c>：逐阶段自检并立刻打印，用来在拿不到日志的设备上（Android 壳）定位启动崩溃。
/// 崩溃时最后一行就是出事的阶段。全部通过会打印 <c>probe: done</c>。
/// </summary>
internal static class ProbeMode
{
    public static async Task RunAsync()
    {
        void Say(string text)
        {
            Console.Out.WriteLine(text);
            Console.Out.Flush();
        }

        Say("probe: runtime " + RuntimeInformation.FrameworkDescription + " / " + RuntimeInformation.RuntimeIdentifier
            + " / " + RuntimeInformation.ProcessArchitecture);

        Say("probe: io");
        string temp = Path.Combine(Path.GetTempPath(), "ew4-probe-" + Environment.ProcessId);
        File.WriteAllText(temp, "x");
        File.Delete(temp);

        Say("probe: sha256");
        Say("probe: sha256 system=" + Sha256Hash.UsesSystemImplementation + " "
            + Convert.ToHexStringLower(Sha256Hash.HashData("abc"u8.ToArray()))[..16]);

        Say("probe: socket");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        listener.Stop();

        Say("probe: aspnet-build");
        WebApplicationBuilder builder = WebApplication.CreateBuilder([]);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        WebApplication app = builder.Build();
        app.MapGet("/", () => "hi");

        Say("probe: aspnet-start");
        await app.StartAsync();
        string address = app.Urls.First();

        Say("probe: aspnet-request " + address);
        using var client = new HttpClient();
        string body = await client.GetStringAsync(address);
        await app.StopAsync();
        Say("probe: aspnet-ok " + body);
        Say("probe: done");
    }
}
