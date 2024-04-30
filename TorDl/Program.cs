using System.Net;
using Knapcode.TorSharp;
using Serilog;
using Serilog.Core;
using TorDl.Extensions;

namespace TorDl;

public static class Program
{
    #region Properties

    /// <summary>
    /// Logging service.
    /// </summary>
    public static Logger Log { get; } =
        new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
    
    /// <summary>
    /// Progress percentage tracker.
    /// </summary>
    private static readonly List<int> ProgressIndicators = [];
    
    /// <summary>
    /// TorSharp settings.
    /// </summary>
    private static readonly TorSharpSettings TorSettings = new()
    {
        PrivoxySettings =
        {
            Disable = true
        },
        WriteToConsole = false
    };
    
    #endregion
    
    /// <summary>
    /// Init all the things...
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    private static async Task Main(string[] args)
    {
        if (args.Length == 0 ||
            args.Any(n => n is "-h" or "--help"))
        {
            ShowProgramUsage();
            return;
        }

        if (!ParseCmdArgs(args, out var options) ||
            !await VerifyTor())
        {
            return;
        }

        await DownloadUrls(options);
    }
    
    /// <summary>
    /// Set up the proxy and download each of the given URLs.
    /// </summary>
    /// <param name="options">Parsed options.</param>
    private static async Task DownloadUrls(Options options)
    {
        HttpClientHandler handler;
        
        using var proxy = new TorSharpProxy(TorSettings);

        try
        {
            Log.Information("Setting up Tor proxy...");
            
            await proxy.ConfigureAndStartAsync();

            var proxyUri = new Uri($"socks5://localhost:{TorSettings.TorSettings.SocksPort}");

            handler = new HttpClientHandler
            {
                Proxy = new WebProxy(proxyUri)
            };
            
            Log.Information("Proxying through {proxyUri}", proxyUri);
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
            return;
        }

        using (handler)
        using (var client = new HttpClient(handler))
        {
            client.Timeout = options.RequestTimeout;

            var count = 0;

            foreach (var url in options.Urls)
            {
                try
                {
                    Log.Information(
                        "Downloading {count} of {total} - {url}",
                        ++count,
                        options.Urls.Count,
                        url);

                    lock (ProgressIndicators)
                    {
                        ProgressIndicators.Clear();
                    }

                    var progress = new Progress<float>(ReportProgress);

                    using var stream = new MemoryStream();
                    await client.DownloadAsync(url, stream, progress, CancellationToken.None);

                    var bytes = stream.ToArray();
                    var filename = GetFilename(url);
                    var path = Path.Combine(Directory.GetCurrentDirectory(), filename);
                    
                    Log.Information("File: {path}", path);

                    await File.WriteAllBytesAsync(path, bytes);
                }
                catch (TimeoutException ex)
                {
                    Log.Error(ex, "Timeout after {period}", options.RequestTimeout);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }
        }
        
        proxy.Stop();
    }

    /// <summary>
    /// Generate a filename based on a URL.
    /// </summary>
    /// <param name="uri">URL.</param>
    /// <returns>Filename.</returns>
    private static string GetFilename(Uri uri)
    {
        var name = uri.LocalPath;

        if (name is "" or "/")
        {
            return $"{uri.DnsSafeHost}-default.html";
        }

        if (name.EndsWith('/'))
        {
            name = name[..^1];
        }

        var index = name.LastIndexOf('/');

        if (index > -1)
        {
            name = name[(index + 1)..];
        }

        return name;
    }

    /// <summary>
    /// Parse command line arguments into options.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="options">Parsed options.</param>
    /// <returns>Success.</returns>
    private static bool ParseCmdArgs(IReadOnlyList<string> args, out Options options)
    {
        options = new();

        var skip = false;

        for (var i = 0; i < args.Count; i++)
        {
            if (skip)
            {
                skip = false;
                continue;
            }

            switch (args[i])
            {
                case "-t":
                case "--timeout":
                    if (i == args.Count - 1)
                    {
                        Log.Error("{argument} must be followed by a number of seconds.", args[i]);
                        return false;
                    }

                    if (!int.TryParse(args[i + 1], out var seconds))
                    {
                        Log.Error("Unable to parse {value} to number of seconds.", args[i + 1]);
                        return false;
                    }

                    options.RequestTimeout = seconds > 0
                        ? TimeSpan.FromSeconds(seconds)
                        : Timeout.InfiniteTimeSpan;

                    skip = true;
                    break;
                
                default:
                    if (!Uri.TryCreate(args[i], UriKind.Absolute, out var uri))
                    {
                        Log.Error("Unable to parse {argument} to a valid URL.", args[i]);
                        return false;
                    }

                    options.Urls.Add(uri);
                    break;
            }
        }

        if (options.Urls.Count > 0)
        {
            return true;
        }

        Log.Error("You must add at least one URL to download.");
        return false;
    }

    /// <summary>
    /// Log current progress, if new.
    /// </summary>
    /// <param name="value">Percentage in 0 (0%) to 1 (100%).</param>
    private static void ReportProgress(float value)
    {
        lock (ProgressIndicators)
        {
            var percentage = (int)(value * 100);

            if (ProgressIndicators.Contains(percentage))
            {
                return;
            }

            ProgressIndicators.Add(percentage);
        
            Log.Information("Progress: {percentage}", $"{percentage}%");
        }
    }

    /// <summary>
    /// Show program usage and options.
    /// </summary>
    private static void ShowProgramUsage()
    {
        var lines = new []
        {
            "TorDl v0.1-alpha",
            "Simple CLI to download files from the TOR network.",
            "",
            "Usage:",
            "  tordl <urls> [<options>]",
            "",
            "Options:",
            "  <url>                    URL to download. Can be repeated.",
            "  -t|--timeout <seconds>   Set request timeout, in seconds. 0 = no timeout. Defaults to 120 seconds.",
            "",
            "Source and documentation available at https://github.com/nagilum/tordl",
            "Uses Knapcode.TorSharp (https://www.nuget.org/packages/Knapcode.TorSharp) for Tor network proxying and Serilog (https://www.nuget.org/packages/Serilog) for logging.",
        };

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
    }

    /// <summary>
    /// Verify the local Tor install.
    /// </summary>
    /// <returns>Success.</returns>
    private static async Task<bool> VerifyTor()
    {
        try
        {
            Log.Information("Verifying local copy of Tor...");
            
            using var client = new HttpClient();
            
            var fetcher = new TorSharpToolFetcher(TorSettings, client);

            await fetcher.FetchAsync();

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, ex.Message);
            return false;
        }
    }
}