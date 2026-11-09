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
        logger ??= Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var destDir = Path.Combine(FileSystem.AppDataDirectory, "vocabularies");
        Directory.CreateDirectory(destDir);
        logger.LogInformation("VocabularyBootstrap: AppData cache {Dir}", destDir);

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
                    logger.LogInformation(
                        "VocabularyBootstrap: extracted package asset {Asset} → {Dest} ({Bytes} bytes)",
                        asset, dest, new FileInfo(dest).Length);
                    break;
                }
                catch
                {
                    // try next logical name
                }
            }

            if (!copied)
                logger.LogWarning(
                    "VocabularyBootstrap: pack {Name} not found in app package (ShortP2P.Vocabulary)", name);
        }

        if (Directory.EnumerateFiles(destDir, "*_vocabulary_v*.zip").Any())
        {
            logger.LogInformation("VocabularyBootstrap: initializing catalog from AppData packs");
            VocabularyCatalog.Initialize(destDir, logger);
            return;
        }

        // Dev / unpackaged: fall back to ShortP2P.Vocabulary submodule next to the repo root.
        logger.LogInformation("VocabularyBootstrap: no packaged zips; trying ShortP2P.Vocabulary near app");
        if (!VocabularyHost.TryInitializeNearApp(destDir, logger))
            logger.LogWarning("VocabularyBootstrap: no vocabulary packs available (package or ShortP2P.Vocabulary)");
    }
}
