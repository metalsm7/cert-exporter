using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
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
                // Console.WriteLine($"Proc {req.RequestUri?.Host}{((req.RequestUri?.Port == 443) ? "" : $":{req.RequestUri?.Port}")} - certificate:{certificate}");
                rtnVal.Append(
                    new StringBuilder()
                        .Append($"# HELP cert_expires_in Seconds certificate expires{Environment.NewLine}")
                        .Append($"# TYPE cert_expires_in counter{Environment.NewLine}")
                        .Append(
                            $"cert_expires_in{{domain=\"{req.RequestUri?.Host}{((req.RequestUri?.Port == 443) ? "" : $":{req.RequestUri?.Port}")}\",subject=\"{x509.Subject.Replace("CN=", "")}\",issuer=\"{x509.Issuer}\"}} {(Math.Floor(x509.NotAfter.Subtract(DateTime.UnixEpoch).TotalSeconds - DateTime.Now.Subtract(DateTime.UnixEpoch).TotalSeconds))}{Environment.NewLine}"
                        )
                        .ToString()
                );

                //
                return true;
            }
        });
    //
    var tasks = new List<Task>();
    foreach (var domain in domains) {
        try
        {
            tasks.Add(
                client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{domain}")))
                    .ContinueWith(
                        result => {
                            if (result.Exception != null) {
                                Console.Error.WriteLine($"Error domain: {domain} - message:{result.Exception.Message}");
                                //
                                rtnVal.Append(
                                    new StringBuilder()
                                        .Append($"# HELP cert_expires_in Seconds certificate expires{Environment.NewLine}")
                                        .Append($"# TYPE cert_expires_in counter{Environment.NewLine}")
                                        .Append(
                                            $"cert_expires_in{{domain=\"{domain}\",subject=\"\",issuer=\"\"}} 0{Environment.NewLine}"
                                        )
                                        .ToString()
                                );
                            }
                        }
                    )
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($" - {domain} / {ex}");
        }
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
        tasks.Add(
            client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{domain}"))
            )
            .ContinueWith(
                result => {
                    if (result.Exception != null) {
                        Console.Error.WriteLine($"Error domain: {domain} - message:{result.Exception.Message}");
                        //
                        list.Add(
                            new AZData()
                                .Add("Url", $"https://{domain}")
                                .Add("Domain", Regex.Replace(domain, ":[0-9]+$", ""))
                                .Add("Subject", "")
                                .Add("Issuer", "")
                                .Add("NotBefore", 0)
                                .Add("NotAfter", 0)
                                .Add("RemainSeconds", 0)
                        );
                    }
                }
            )
        );
    }
    await Task.WhenAll(tasks);
    //
    return list.ToJsonString();
});

// app.MapControllers();

//
app.Lifetime.ApplicationStarted.Register(() => {
    Console.WriteLine(@" _____         _   _____                 _           
|     |___ ___| |_|   __|_ _ ___ ___ ___| |_ ___ ___ 
|   --| -_|  _|  _|   __|_'_| . | . |  _|  _| -_|  _|
|_____|___|_| |_| |_____|_,_|  _|___|_| |_| |___|_|  
                            |_|       ");
});

//
app.Run();


// curl -v https://kpopfile.codewiz.kr
// curl -v https://img.kpoplive.net
// curl -v https://www.goldlive.co.kr
// curl -v https://www.jjinlive.com
// curl -v https://pangload.codewiz.kr:8091
// curl -v https://kpopload.codewiz.kr:8004
// curl -v https://pangchat1.codewiz.kr:8091
// curl -v https://kpopchat1.codewiz.kr:8004
// curl -v https://pangchat2.codewiz.kr:8091
// curl -v https://kpopchat2.codewiz.kr:8004
// curl -v https://live0.goldlive.co.kr
// curl -v https://live1.goldlive.co.kr
// curl -v https://live2.goldlive.co.kr
// curl -v https://live3.goldlive.co.kr
// curl -v https://live0.kpoplive.net
// curl -v https://uno-www.codewiz.kr
// curl -v https://uno-app.codewiz.kr
// curl -v https://kpoplive.www.codewiz.kr
// curl -v https://st1.codewiz.kr:8091
// curl -v https://st1.codewiz.kr:8004
// curl -v https://st2.codewiz.kr:8004
// curl -v https://st3.codewiz.kr:8004
// curl -v https://gitlab.codewiz.kr
// curl -v https://bbidc.codewiz.kr
// curl -v https://kpopvod.codewiz.kr
// curl -v https://pangvod.codewiz.kr
// curl -v https://unofile.codewiz.kr
// curl -v https://wapp.codewiz.kr
// curl -v https://api.zchat.codewiz.kr
// curl -v https://broadcast.pangpangtv.co.kr:8091
// curl -v https://papi.pangpangtv.co.kr
// curl -v https://img.kpoprnx.com
// curl -v https://www.kpoprnx.com
// curl -v https://message.codewiz.kr
// curl -v https://noti.codewiz.kr

