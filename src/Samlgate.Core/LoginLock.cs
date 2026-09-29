namespace Samlgate;

/// <summary>
/// Cross-process lock around sign-in. Tools often run several credential_process calls at once; without this
/// they would all start a browser on the same profile (only one can own it) and race on the credentials file.
/// Uses an exclusive file handle, which .NET maps to flock() on macOS/Linux.
/// </summary>
public static class LoginLock
{
    public static async Task<IDisposable> AcquireAsync(
        string lockFile, TimeSpan timeout, Action<string>? onWaiting = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lockFile))!);
        var deadline = DateTime.UtcNow + timeout;
        var announced = false;

        while (true)
        {
            try
            {
                return new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                if (!announced)
                {
                    onWaiting?.Invoke("Another samlgate sign-in is in progress; waiting for it to finish...");
                    announced = true;
                }

                await Task.Delay(250, cancellationToken);
            }
            catch (IOException e)
            {
                throw new SamlgateException("Timed out waiting for another samlgate sign-in to finish.", e);
            }
        }
    }
}
