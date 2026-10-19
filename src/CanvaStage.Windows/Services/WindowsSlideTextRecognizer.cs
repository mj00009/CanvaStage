using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace CanvaStage.Windows.Services;

/// <summary>Local Windows OCR; no cloud service, uploaded image or presentation text in logs.</summary>
public static class WindowsSlideTextRecognizer
{
    public static async Task<string?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        OcrEngine? engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null) return null;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        StorageFile file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)).AsTask(timeout.Token).ConfigureAwait(false);
        using var stream = await file.OpenReadAsync().AsTask(timeout.Token).ConfigureAwait(false);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask(timeout.Token).ConfigureAwait(false);
        if (decoder.PixelWidth > OcrEngine.MaxImageDimension || decoder.PixelHeight > OcrEngine.MaxImageDimension
            || (long)decoder.PixelWidth * decoder.PixelHeight > 8_000_000) return null;
        using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
            .AsTask(timeout.Token).ConfigureAwait(false);
        OcrResult result = await engine.RecognizeAsync(bitmap).AsTask(timeout.Token).ConfigureAwait(false);
        return result.Text;
    }
}
