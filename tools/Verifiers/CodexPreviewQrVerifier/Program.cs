using ImvixPro.Services;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

Console.OutputEncoding = Encoding.UTF8;

Bitmap CreateQrBitmap(string payload)
{
    var writer = new BarcodeWriterPixelData
    {
        Format = BarcodeFormat.QR_CODE,
        Options = new QrCodeEncodingOptions
        {
            Width = 420,
            Height = 420,
            Margin = 2
        }
    };

    var pixelData = writer.Write(payload);
    var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppArgb);
    var rect = new Rectangle(0, 0, pixelData.Width, pixelData.Height);
    var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

    try
    {
        Marshal.Copy(pixelData.Pixels, 0, data.Scan0, pixelData.Pixels.Length);
    }
    finally
    {
        bitmap.UnlockBits(data);
    }

    return bitmap;
}

byte[] CreateQrImage(string payload)
{
    using var bitmap = CreateQrBitmap(payload);
    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    return stream.ToArray();
}

byte[] CreateMultiQrImage(params string[] payloads)
{
    var bitmaps = new List<Bitmap>(payloads.Length);

    try
    {
        foreach (var payload in payloads)
        {
            bitmaps.Add(CreateQrBitmap(payload));
        }

        const int spacing = 60;
        const int padding = 24;
        var canvasWidth = (padding * 2) + (spacing * Math.Max(0, bitmaps.Count - 1));
        var canvasHeight = padding * 2;

        foreach (var bitmap in bitmaps)
        {
            canvasWidth += bitmap.Width;
            canvasHeight = Math.Max(canvasHeight, bitmap.Height + (padding * 2));
        }

        using var canvas = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(canvas);
        graphics.Clear(Color.White);

        var x = padding;
        foreach (var bitmap in bitmaps)
        {
            var y = (canvasHeight - bitmap.Height) / 2;
            graphics.DrawImage(bitmap, x, y, bitmap.Width, bitmap.Height);
            x += bitmap.Width + spacing;
        }

        using var stream = new MemoryStream();
        canvas.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
    finally
    {
        foreach (var bitmap in bitmaps)
        {
            bitmap.Dispose();
        }
    }
}

async Task<int> VerifyDecodeAsync(PreviewQrService service, string name, string input, bool expectUrl)
{
    var result = await service.RecognizeAsync(CreateQrImage(input));
    var decoded = result.Text.Trim();
    var matches = result.HasText && string.Equals(decoded, input, StringComparison.Ordinal);
    var urlMatch = PreviewQrService.TryGetSingleUrl(decoded, out var url);
    var urlPassed = expectUrl
        ? urlMatch && string.Equals(url, input, StringComparison.Ordinal)
        : !urlMatch;

    Console.WriteLine($"== {name} ==");
    Console.WriteLine($"Decoded: {decoded}");
    Console.WriteLine($"Exact Match: {matches}");
    Console.WriteLine($"URL Match: {urlPassed}");
    Console.WriteLine();

    return matches && urlPassed ? 0 : 1;
}

async Task<int> VerifyMultipleDecodeAsync(PreviewQrService service, string name, params string[] inputs)
{
    var result = await service.RecognizeAllAsync(CreateMultiQrImage(inputs));
    var matches = result.Results.Count == inputs.Length;

    if (matches)
    {
        for (var index = 0; index < inputs.Length; index++)
        {
            if (!string.Equals(result.Results[index].Content, inputs[index], StringComparison.Ordinal))
            {
                matches = false;
                break;
            }
        }
    }

    Console.WriteLine($"== {name} ==");
    Console.WriteLine($"Detected Count: {result.Results.Count}");

    for (var index = 0; index < result.Results.Count; index++)
    {
        Console.WriteLine($"[{index + 1}] {result.Results[index].Content}");
    }

    Console.WriteLine($"Ordered Match: {matches}");
    Console.WriteLine();

    return matches ? 0 : 1;
}

var service = new PreviewQrService();
var failures = 0;

failures += await VerifyDecodeAsync(service, "PlainText", "IMVIX-QR-HELLO", expectUrl: false);
failures += await VerifyDecodeAsync(service, "SingleUrl", "https://example.com/imvix", expectUrl: true);
failures += await VerifyMultipleDecodeAsync(
    service,
    "MultipleQrCodes",
    "https://a.imvix.test",
    "HELLO-MULTI-QR");

var multipleUrlHandled = !PreviewQrService.TryGetSingleUrl("https://example.com https://openai.com", out _);
Console.WriteLine("== MultipleUrls ==");
Console.WriteLine($"URL button disabled expectation: {multipleUrlHandled}");
Console.WriteLine();
failures += multipleUrlHandled ? 0 : 1;

Console.WriteLine($"Failures={failures}");
return failures == 0 ? 0 : 1;
