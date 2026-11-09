using Microsoft.Extensions.Logging;
using ShortP2P.Client.ChatMedia.Vocabulary;

namespace TorgLink.Maui;

/// <summary>SMT-3: copy packaged vocabulary zips into AppData and hash once at startup.</summary>
public static class VocabularyBootstrap
{
    public static async Task InitializeAsync(ILogger? logger = null)
    {
        var destDir = Path.Combine(FileSystem.AppDataDirectory, "vocabularies");
        Directory.CreateDirectory(destDir);

        foreach (var name in new[] { "RU_vocabulary_v1.zip", "EN_vocabulary_v1.zip" })
        {
            var dest = Path.Combine(destDir, name);
            try
            {
                await using var src = await FileSystem.OpenAppPackageFileAsync($"vocabularies/{name}")
                    .ConfigureAwait(false);
                await using var dst = File.Create(dest);
                await src.CopyToAsync(dst).ConfigureAwait(false);
            }
            catch
            {
                // Fallback: LogicalName may be flat filename only.
                try
                {
                    await using var src = await FileSystem.OpenAppPackageFileAsync(name).ConfigureAwait(false);
                    await using var dst = File.Create(dest);
                    await src.CopyToAsync(dst).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Vocabulary pack {Name} not found in app package", name);
                }
            }
        }

        VocabularyCatalog.Initialize(destDir, logger);
    }
}
