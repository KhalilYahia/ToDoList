using System.ComponentModel;
using System.Diagnostics;

namespace OpsManager.Repository.IntegrationTests.Infrastructure;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerAvailability.IsAvailable.Value)
        {
            Skip = "Docker is required for PostgreSQL integration tests.";
        }
    }
}

internal static class DockerAvailability
{
    public static Lazy<bool> IsAvailable { get; } = new(CheckDocker);

    private static bool CheckDocker()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info --format {{.ServerVersion}}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            return process is not null && process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
