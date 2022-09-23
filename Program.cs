using System.Security.Cryptography.X509Certificates;
using System.Text;
using CertExporter;
using Com.Mparang.AZLib;
using CommandLine;

string[]? domains = null;
string listen = "";

// argument parse
var cmd = CommandLine.Parser.Default.ParseArguments<CommandOption>(args);
if (cmd.Tag == ParserResultType.Parsed) {
    //
    if (cmd.Value.domain.Trim().Length > 0) {
        domains = cmd.Value.domain.Split(new char[] {',', ';'});
    }
    //
    if (cmd.Value.domain.Trim().Length > 0) {
        listen = cmd.Value.listen;
    }
}

// env
if (domains == null || domains.Length < 1) {
    var envDomain = Environment.GetEnvironmentVariable("DOMAIN");
    if (envDomain?.Trim().Length > 0) {
        domains = envDomain.Split(new char[] { ',', ';' });
    }
}
if (domains == null || domains.Length < 1) Environment.Exit(0);
if (listen.Length < 1) {
    //
    var envListen = Environment.GetEnvironmentVariable("LISTEN");
    if (envListen?.Trim().Length > 0) {
        listen = envListen;
    }
}
if (listen.Length < 1) listen = ":9972";
if (listen.StartsWith(":")) listen = $"0.0.0.0{listen}";

//
var builder = WebApplication.CreateBuilder(args);
// builder.Services.AddControllers();
builder.WebHost.UseKestrel();
builder.WebHost.UseUrls($"http://{listen}");
var app = builder.Build();

app.MapGet("/", () => {
    return new StringBuilder()
        .Append("<html>")
        .Append("<head><title>Node Exporter</title></head>")
        .Append("<body>")
        .Append("<h1>Node Exporter</h1>")
        .Append("<p><a href=\"/metrics\">Metrics</a></p>")
        .Append("<p><a href=\"/metrics/json\">Json</a></p>")
        .Append("</body>")
        .Append("</html>")
        .ToString();
});

app.MapGet("/metrics", async (HttpContext context) => {
    //
    context.Response.ContentType = "application/json";
    //
    var rtnVal = new StringBuilder();
    //
    var client = new HttpClient(
        new HttpClientHandler {
            UseDefaultCredentials = true,
            ServerCertificateCustomValidationCallback = (req, certificate, _, _) => {
                var x509 = new X509Certificate2(certificate);
                // var notAfter = x509.NotAfter;
                // var totalSeconds = notAfter.Subtract(DateTime.UnixEpoch).TotalSeconds;
                // Console.WriteLine($"{req.RequestUri} - x509:{x509}");
                // Console.WriteLine($"{req.RequestUri} - NotAfter:{notAfter} / {totalSeconds} / {Math.Floor(totalSeconds - DateTime.Now.Subtract(DateTime.UnixEpoch).TotalSeconds)}");
                // 
                // # HELP node_cpu_seconds_total Seconds the cpus spent in each mode.
                // # TYPE node_cpu_seconds_total counter
                // node_cpu_seconds_total{cpu="0",mode="idle"} 8.71366944e+07
                try {
                    rtnVal.Append(
                        new StringBuilder()
                            .Append($"# HELP cert_expires_in Seconds certificate expires{Environment.NewLine}")
                            .Append($"# TYPE cert_expires_in counter{Environment.NewLine}")
                            .Append(
                                $"cert_expires_in{{domain=\"{req.RequestUri?.Host}{((req.RequestUri?.Port == 443) ? "" : $":{req.RequestUri?.Port}")}\",subject=\"{x509.Subject.Replace("CN=", "")}\",issuer=\"{x509.Issuer}\"}} {(Math.Floor(x509.NotAfter.Subtract(DateTime.UnixEpoch).TotalSeconds - DateTime.Now.Subtract(DateTime.UnixEpoch).TotalSeconds))}{Environment.NewLine}"
                            )
                            .ToString()
                    );
                }
                catch (Exception ex) {
                    Console.WriteLine(ex);
                }

                //
                return true;
            }
        });
    //
    var tasks = new List<Task>();
    foreach (var domain in domains) {
        tasks.Add(client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{domain}"))));
    }
    await Task.WhenAll(tasks);
    //
    return rtnVal.ToString();
});

app.MapGet("/metrics/json", async (HttpContext context) => {
    //
    context.Response.ContentType = "application/json";
    //
    var list = new AZList();
    //
    var client = new HttpClient(
        new HttpClientHandler {
            UseDefaultCredentials = true,
            ServerCertificateCustomValidationCallback = (req, certificate, _, _) => {
                var x509 = new X509Certificate2(certificate);
                // var notAfter = x509.NotAfter;
                // var totalSeconds = notAfter.Subtract(DateTime.UnixEpoch).TotalSeconds;
                // Console.WriteLine($"{req.RequestUri} - x509:{x509}");
                // Console.WriteLine($"{req.RequestUri} - NotAfter:{notAfter} / {totalSeconds} / {Math.Floor(totalSeconds - DateTime.Now.Subtract(DateTime.UnixEpoch).TotalSeconds)}");
                //
                list.Add(
                    new AZData()
                        .Add("Url", req.RequestUri)
                        .Add("Domain", req.RequestUri?.Host ?? "")
                        .Add("Subject", x509.Subject)
                        .Add("Issuer", x509.Issuer)
                        .Add("NotBefore", x509.NotBefore.Subtract(DateTime.UnixEpoch).TotalSeconds)
                        .Add("NotAfter", x509.NotAfter.Subtract(DateTime.UnixEpoch).TotalSeconds)
                        .Add("RemainSeconds", Math.Floor(x509.NotAfter.Subtract(DateTime.UnixEpoch).TotalSeconds - DateTime.Now.Subtract(DateTime.UnixEpoch).TotalSeconds))
                );
                //
                return true;
            }
        });
    //
    var tasks = new List<Task>();
    foreach (var domain in domains) {
        tasks.Add(client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{domain}"))));
    }
    await Task.WhenAll(tasks);
    //
    return list.ToJsonString();
});

// app.MapControllers();

app.Run();