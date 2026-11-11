using SkiaSharp;

namespace MobileConcept.Core.GameEngine;

/// <summary>Limites de démarrage prudentes, à mesurer sur appareils réels.</summary>
public sealed record TextureLimits(int MaxDimension = 1024, long BudgetBytes = 24 * 1024 * 1024,
    int MaxEncodedBytes = 4 * 1024 * 1024)
{
    public long Validate(int width, int height, long residentBytes)
    {
        if (MaxDimension < 1 || BudgetBytes < 1 || MaxEncodedBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(TextureLimits));
        if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension)
            throw new InvalidDataException($"Texture {width}×{height} hors limite {MaxDimension}. Fournir un atlas découpé.");
        long bytes = checked((long)width * height * 4);
        // Réserve 2 × les pixels pour le décodage/transfert ; les surfaces système restent hors budget.
        if (residentBytes + bytes * 2 > BudgetBytes)
            throw new InvalidDataException("Budget de textures dépassé. Décharger le monde précédent.");
        return bytes;
    }
}

/// <summary>Cache par scène. Bitmaps empruntés : seul ce cache les libère. Pas de cache statique.</summary>
public sealed class TextureCache : IDisposable
{
    private readonly Dictionary<string, SKBitmap> textures = new(StringComparer.Ordinal);
    private readonly TextureLimits limits;
    public long ResidentBytes { get; private set; }
    public TextureCache(TextureLimits? limits = null) => this.limits = limits ?? new();
    public SKBitmap Load(string key, Stream source)
    {
        if (textures.TryGetValue(key, out var cached)) return cached;
        // Lecture bornée, y compris pour les streams Android non seekables.
        using var encoded = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (encoded.Length + read > limits.MaxEncodedBytes) throw new InvalidDataException("Fichier image trop volumineux.");
            encoded.Write(buffer, 0, read);
        }
        encoded.Position = 0;
        using var codec = SKCodec.Create(encoded) ?? throw new InvalidDataException("Image illisible.");
        long bytes = limits.Validate(codec.Info.Width, codec.Info.Height, ResidentBytes);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap();
        try
        {
            if (!bitmap.TryAllocPixels(info)) throw new InvalidDataException("Allocation de texture refusée.");
            if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
                throw new InvalidDataException("Décodage incomplet ou invalide.");
            bitmap.SetImmutable();
            textures.Add(key, bitmap); ResidentBytes += bytes;
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }
    public void Clear()
    {
        foreach (var texture in textures.Values) texture.Dispose();
        textures.Clear(); ResidentBytes = 0;
    }
    public void Dispose() => Clear();
}
