using Microsoft.Extensions.Logging;
using ShortP2P.Client.ChatMedia.Vocabulary;

namespace TorgLink.Maui;

/// <summary>
///     SMT-3: copy vocabulary zips from the ShortP2P.Vocabulary submodule (packaged as MauiAsset)
///     into AppData and BLAKE3-hash them once at startup.
/// </summary>
public static class VocabularyBootstrap
{
    public static async Task InitializeAsync(ILogger? logger = null)
    {
        var destDir = Path.Combine(FileSystem.AppDataDirectory, "vocabularies");
        Directory.CreateDirectory(destDir);

        // Prefer packages from ShortP2P.Vocabulary (https://github.com/TorgLink/ShortP2P.Vocabulary, branch dev/0.2).
        foreach (var name in new[] { "RU_vocabulary_v1.zip", "EN_vocabulary_v1.zip" })
        {
            var dest = Path.Combine(destDir, name);
            var copied = false;
            foreach (var asset in new[] { $"vocabularies/{name}", $"vocabularies\\{name}", name })
            {
                try
                {
                    await using var src = await FileSystem.OpenAppPackageFileAsync(asset).ConfigureAwait(false);
                    await using var dst = File.Create(dest);
                    await src.CopyToAsync(dst).ConfigureAwait(false);
                    copied = true;
                    break;
                }
                catch
                {
                    // try next logical name
                }
            }

            if (!copied)
                logger?.LogWarning("Vocabulary pack {Name} not found in app package (ShortP2P.Vocabulary)", name);
        }

        VocabularyCatalog.Initialize(destDir, logger);
    }
}
