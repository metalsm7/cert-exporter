using CommandLine;

namespace CertExporter;

public class CommandOption
{
    [Option('d', "domain", Required = false)]
    public String domain { get; set; } = "";

    [Option("web.listen-address", Required = false)]
    public String listen { get; set; } = ":9972";
}