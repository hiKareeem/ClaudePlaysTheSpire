using System;
using System.IO;

namespace HermesBridge.HermesBridgeCode;

internal static class AtomicFileWriter
{
    public static void WriteText(string destinationPath, string content)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException($"Destination path has no directory: {destinationPath}");
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(destinationPath)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, content);

            if (File.Exists(destinationPath))
            {
                File.Replace(tempPath, destinationPath, null, true);
            }
            else
            {
                File.Move(tempPath, destinationPath);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }
}
