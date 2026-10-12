using System.Diagnostics;
using System.Text;

namespace SwitchNet.Network;

internal static class ProcessRunner
{
    public static async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        int timeoutMs = 30_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return new CommandResult(-1, stdout.ToString(), stderr + "\nTimed out.");
        }

        return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    public static CommandResult Run(string fileName, string arguments, int timeoutMs = 30_000)
        => RunAsync(fileName, arguments, timeoutMs).GetAwaiter().GetResult();
}

internal readonly record struct CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;

    public string CombinedOutput
    {
        get
        {
            var parts = new[] { StdOut.Trim(), StdErr.Trim() }.Where(s => s.Length > 0);
            return string.Join(Environment.NewLine, parts);
        }
    }
}
