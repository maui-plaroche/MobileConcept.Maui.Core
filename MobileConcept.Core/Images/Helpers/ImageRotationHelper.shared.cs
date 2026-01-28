using System;
using System.IO;
using SkiaSharp;

namespace MobileConcept.Core.Images.Helpers;

/// <summary>
/// Image rotation helper.
/// </summary>
public static class ImageRotationHelper
{
    /// <summary>
    /// Resizes an image to exact dimensions with EXIF rotation handling.
    /// Use for profile images (crops to target size).
    /// </summary>
    public static Stream ResizeWithExifRotation(Stream inputStream, int targetWidth, int targetHeight, int quality = 90)
    {
        using var inputSkiaStream = new SKManagedStream(inputStream);
        using var codec = SKCodec.Create(inputSkiaStream);

        var origin = codec.EncodedOrigin;
        using var bitmap = SKBitmap.Decode(codec);
        using var rotatedBitmap = ApplyExifOrientation(bitmap, origin);
        using var resizedBitmap =
            rotatedBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);

        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        var outputStream = new MemoryStream();
        data.SaveTo(outputStream);
        outputStream.Position = 0;

        return outputStream;
    }

    /// <summary>
    /// Resizes an image proportionally within max bounds with EXIF rotation handling.
    /// Use for pin images (maintains aspect ratio).
    /// </summary>
    public static Stream ResizeProportionalWithExifRotation(Stream inputStream, int maxWidth = 1200,
        int maxHeight = 1200, int quality = 90)
    {
        using var inputSkiaStream = new SKManagedStream(inputStream);
        using var codec = SKCodec.Create(inputSkiaStream);

        var origin = codec.EncodedOrigin;
        using var bitmap = SKBitmap.Decode(codec);
        using var rotatedBitmap = ApplyExifOrientation(bitmap, origin);

        // Calculate proportional size
        var aspectRatio = (float)rotatedBitmap.Width / rotatedBitmap.Height;
        int newWidth, newHeight;

        if (rotatedBitmap.Width > rotatedBitmap.Height)
        {
            newWidth = Math.Min(rotatedBitmap.Width, maxWidth);
            newHeight = (int)(newWidth / aspectRatio);
            if (newHeight > maxHeight)
            {
                newHeight = maxHeight;
                newWidth = (int)(newHeight * aspectRatio);
            }
        }
        else
        {
            newHeight = Math.Min(rotatedBitmap.Height, maxHeight);
            newWidth = (int)(newHeight * aspectRatio);
            if (newWidth > maxWidth)
            {
                newWidth = maxWidth;
                newHeight = (int)(newWidth / aspectRatio);
            }
        }

        // Only resize if needed
        SKBitmap finalBitmap;
        if (rotatedBitmap.Width <= newWidth && rotatedBitmap.Height <= newHeight)
        {
            finalBitmap = rotatedBitmap;
        }
        else
        {
            finalBitmap = rotatedBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default);
        }

        using var image = SKImage.FromBitmap(finalBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        if (finalBitmap != rotatedBitmap)
            finalBitmap.Dispose();

        var outputStream = new MemoryStream();
        data.SaveTo(outputStream);
        outputStream.Position = 0;

        return outputStream;
    }

    private static Stream ResizeImage(Stream inputStream, int targetWidth, int targetHeight)
    {
        using var inputSkiaStream = new SKManagedStream(inputStream);
        using var codec = SKCodec.Create(inputSkiaStream);

        // Get EXIF orientation and decode with correct orientation
        var origin = codec.EncodedOrigin;
        using var bitmap = SKBitmap.Decode(codec);

        // Apply EXIF rotation/flip
        using var rotatedBitmap = ApplyExifOrientation(bitmap, origin);

        // Resize after rotation
        using var resizedBitmap =
            rotatedBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);

        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

        var outputStream = new MemoryStream();
        data.SaveTo(outputStream);
        outputStream.Position = 0;

        return outputStream;
    }

    private static SKBitmap ApplyExifOrientation(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopLeft:
                return bitmap.Copy();

            case SKEncodedOrigin.TopRight: // Mirror horizontal
                return FlipBitmap(bitmap, horizontal: true);

            case SKEncodedOrigin.BottomRight: // Rotate 180
                return RotateBitmap(bitmap, 180);

            case SKEncodedOrigin.BottomLeft: // Mirror vertical
                return FlipBitmap(bitmap, horizontal: false);

            case SKEncodedOrigin.LeftTop: // Rotate 90 + mirror horizontal
                var rotated90H = RotateBitmap(bitmap, 90);
                var flipped = FlipBitmap(rotated90H, horizontal: true);
                rotated90H.Dispose();
                return flipped;

            case SKEncodedOrigin.RightTop: // Rotate 90
                return RotateBitmap(bitmap, 90);

            case SKEncodedOrigin.RightBottom: // Rotate 270 + mirror horizontal
                var rotated270H = RotateBitmap(bitmap, 270);
                var flipped2 = FlipBitmap(rotated270H, horizontal: true);
                rotated270H.Dispose();
                return flipped2;

            case SKEncodedOrigin.LeftBottom: // Rotate 270
                return RotateBitmap(bitmap, 270);

            default:
                return bitmap.Copy();
        }
    }

    private static SKBitmap RotateBitmap(SKBitmap bitmap, int degrees)
    {
        var isRotate90Or270 = degrees == 90 || degrees == 270;
        var rotatedWidth = isRotate90Or270 ? bitmap.Height : bitmap.Width;
        var rotatedHeight = isRotate90Or270 ? bitmap.Width : bitmap.Height;

        var rotatedBitmap = new SKBitmap(rotatedWidth, rotatedHeight);

        using var canvas = new SKCanvas(rotatedBitmap);
        canvas.Translate(rotatedWidth / 2f, rotatedHeight / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
        canvas.DrawBitmap(bitmap, 0, 0);

        return rotatedBitmap;
    }

    private static SKBitmap FlipBitmap(SKBitmap bitmap, bool horizontal)
    {
        var flippedBitmap = new SKBitmap(bitmap.Width, bitmap.Height);

        using var canvas = new SKCanvas(flippedBitmap);
        canvas.Scale(horizontal ? -1 : 1, horizontal ? 1 : -1);
        canvas.Translate(horizontal ? -bitmap.Width : 0, horizontal ? 0 : -bitmap.Height);
        canvas.DrawBitmap(bitmap, 0, 0);

        return flippedBitmap;
    }
}