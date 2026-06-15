using System.Diagnostics;

namespace OpenGitBase.Api.Services;

internal static class FleetSshKeyGenerator
{
    public static (string PublicKey, string PrivateKey) GenerateEd25519KeyPair()
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "opengitbase-fleet-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(tempDir);
        var keyPath = Path.Combine(tempDir, "dispatcher");

        try
        {
            var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "ssh-keygen",
                    Arguments =
                        $"-t ed25519 -N \"\" -f \"{keyPath}\" -C opengitbase-dispatcher-storage",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            );
            process?.WaitForExit(10_000);
            if (process is null || process.ExitCode != 0 || !File.Exists(keyPath))
            {
                throw new InvalidOperationException(
                    "ssh-keygen failed to generate fleet SSH keys."
                );
            }

            return (File.ReadAllText(keyPath + ".pub").Trim(), File.ReadAllText(keyPath).Trim());
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup of temp key material.
            }
        }
    }
}
