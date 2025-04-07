using System.Diagnostics;

namespace GitGood
{
    public static class GitUtils
    {
        /// <summary>
        /// Gets the URL of the specified git remote
        /// </summary>
        /// <param name="remote">The remote name (defaults to "origin")</param>
        /// <returns>The remote URL or null if not found</returns>
        public static async Task<string?> GetRemoteUrlAsync(string remote = "origin")
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"remote get-url {remote}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = Process.Start(processInfo);
                if (process != null)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        return output.Trim();
                    }
                }
            }
            catch
            {
                // If any error occurs, return null
            }

            return null;
        }

        /// <summary>
        /// Finds the root directory of the git repository
        /// </summary>
        /// <returns>The path to the git repository root, or null if not in a repository</returns>
        public static async Task<string?> FindRepositoryRootAsync()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --show-toplevel",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = Process.Start(processInfo);
                if (process == null)
                {
                    return null;
                }

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return output.Trim();
                }
            }
            catch
            {
                // If any error occurs, assume we're not in a git repository
            }

            return null;
        }
    }
} 