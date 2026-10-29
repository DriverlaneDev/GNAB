using System.Drawing.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Gnab.Imaging;

/// <summary>
/// Pulls text out of a captured bitmap using the OCR engine built into Windows (Windows.Media.Ocr).
/// It runs fully offline, ships with Windows 10/11, and needs no API key or model download.
/// </summary>
public static class OcrService
{
    /// <param name="EngineAvailable">
    /// False when Windows has no OCR language pack installed, so no recognition could be attempted —
    /// distinct from a successful run that simply found no text (EngineAvailable true, Text empty).
    /// </param>
    public readonly record struct OcrResult(bool EngineAvailable, string Text);

    /// <summary>
    /// Runs on a background thread — RecognizeAsync has no UI-thread affinity, and keeping it off the UI
    /// thread lets a caller safely block on the returned task without deadlocking on a sync context.
    /// </summary>
    public static async Task<OcrResult> RecognizeAsync(Bitmap bitmap)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            return new OcrResult(EngineAvailable: false, Text: string.Empty);
        }

        using var raStream = new InMemoryRandomAccessStream();
        EncodeAsPng(bitmap, raStream);
        raStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(raStream);
        using var software = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var result = await engine.RecognizeAsync(software);
        return new OcrResult(EngineAvailable: true, Text: result.Text ?? string.Empty);
    }

    private static void EncodeAsPng(Bitmap bitmap, InMemoryRandomAccessStream raStream)
    {
        // The OCR engine rejects images larger than MaxImageDimension on a side, so shrink first,
        // preserving aspect ratio. Full-screen grabs on a large or high-DPI display can exceed it.
        var max = OcrEngine.MaxImageDimension;
        Bitmap? scaled = null;
        var source = bitmap;
        if (bitmap.Width > max || bitmap.Height > max)
        {
            var ratio = Math.Min((double)max / bitmap.Width, (double)max / bitmap.Height);
            var size = new Size(Math.Max(1, (int)(bitmap.Width * ratio)), Math.Max(1, (int)(bitmap.Height * ratio)));
            scaled = new Bitmap(bitmap, size);
            source = scaled;
        }

        try
        {
            using var managed = new MemoryStream();
            source.Save(managed, ImageFormat.Png);
            var bytes = managed.ToArray();

            using var writer = new DataWriter(raStream);
            writer.WriteBytes(bytes);
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.FlushAsync().AsTask().GetAwaiter().GetResult();
            // Release raStream from the writer so disposing the writer (end of scope) leaves it open.
            writer.DetachStream();
        }
        finally
        {
            scaled?.Dispose();
        }
    }
}
