using Ew4.Backend.Web;
using Ew4.Interface.Host;
using Ew4.Service.Host;
using Microsoft.AspNetCore.Http.Features;

if (args.Contains("--probe"))
{
    await ProbeMode.RunAsync();
    return;
}

Ew4HostLaunchOptions launch = Ew4HostLaunch.Resolve(args);

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddEw4GameHost(launch);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024L * 1024 * 1024);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 1024L * 1024 * 1024);

WebApplication app = builder.Build();
// Android 壳在本机起后端：设置了 EW4_ACCESS_TOKEN 就要求 cookie ew4_token 匹配，防止本机其它应用访问。
string? accessToken = Environment.GetEnvironmentVariable("EW4_ACCESS_TOKEN");
if (!string.IsNullOrEmpty(accessToken))
{
    byte[] expected = System.Text.Encoding.UTF8.GetBytes(accessToken);
    app.Use(async (context, next) =>
    {
        byte[] actual = System.Text.Encoding.UTF8.GetBytes(context.Request.Cookies["ew4_token"] ?? "");
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next();
    });
}

app.UsePlayFrontend();
app.MapPlayModApi();
app.MapPlayApi();
app.MapEditorApi();
app.Run();
