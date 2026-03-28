using ImvixPro.Services;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

Console.OutputEncoding = Encoding.UTF8;

Bitmap CreateCodeBitmap(string payload, BarcodeFormat format)
{
    var options = format == BarcodeFormat.QR_CODE
        ? new QrCodeEncodingOptions
        {
            Width = 280,
            Height = 280,
            Margin = 2
        }
        : new EncodingOptions
        {
            Width = 520,
            Height = 180,
            Margin = 10,
            PureBarcode = true
        };

    var writer = new BarcodeWriterPixelData
    {
        Format = format,
        Options = options
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

byte[] CreateCodeImage(string payload, BarcodeFormat format)
{
    using var bitmap = CreateCodeBitmap(payload, format);
    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    return stream.ToArray();
}

byte[] CreateMixedImage(params (string Payload, BarcodeFormat Format)[] items)
{
    var bitmaps = new List<Bitmap>(items.Length);

    try
    {
        foreach (var item in items)
        {
            bitmaps.Add(CreateCodeBitmap(item.Payload, item.Format));
        }

        const int spacing = 48;
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

byte[] CreateStackedImage(params (string Payload, BarcodeFormat Format)[] items)
{
    var bitmaps = new List<Bitmap>(items.Length);

    try
    {
        foreach (var item in items)
        {
            bitmaps.Add(CreateCodeBitmap(item.Payload, item.Format));
        }

        const int spacing = 64;
        const int padding = 24;
        var canvasWidth = padding * 2;
        var canvasHeight = (padding * 2) + (spacing * Math.Max(0, bitmaps.Count - 1));

        foreach (var bitmap in bitmaps)
        {
            canvasWidth = Math.Max(canvasWidth, bitmap.Width + (padding * 2));
            canvasHeight += bitmap.Height;
        }

        using var canvas = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(canvas);
        graphics.Clear(Color.White);

        var y = padding;
        foreach (var bitmap in bitmaps)
        {
            var x = (canvasWidth - bitmap.Width) / 2;
            graphics.DrawImage(bitmap, x, y, bitmap.Width, bitmap.Height);
            y += bitmap.Height + spacing;
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

async Task<int> VerifySingleAsync(
    PreviewBarcodeService service,
    string name,
    string payload,
    BarcodeFormat format,
    string expectedDisplayFormat)
{
    var result = await service.RecognizeAllAsync(CreateCodeImage(payload, format));
    var first = result.Results.Count > 0 ? result.Results[0] : null;
    var matches = result.Results.Count == 1 &&
                  first is not null &&
                  string.Equals(first.Content, payload, StringComparison.Ordinal) &&
                  string.Equals(first.Format, expectedDisplayFormat, StringComparison.Ordinal);

    Console.WriteLine($"== {name} ==");
    Console.WriteLine($"Detected Count: {result.Results.Count}");
    Console.WriteLine($"First Content: {first?.Content}");
    Console.WriteLine($"First Format: {first?.Format}");
    Console.WriteLine($"Match: {matches}");
    Console.WriteLine();

    return matches ? 0 : 1;
}

async Task<int> VerifyMultipleAsync(
    PreviewBarcodeService service,
    string name,
    params (string Payload, BarcodeFormat Format, string ExpectedDisplayFormat)[] inputs)
{
    var scanInputs = new (string Payload, BarcodeFormat Format)[inputs.Length];
    for (var index = 0; index < inputs.Length; index++)
    {
        scanInputs[index] = (inputs[index].Payload, inputs[index].Format);
    }

    var result = await service.RecognizeAllAsync(CreateStackedImage(scanInputs));
    var matches = result.Results.Count == inputs.Length;

    if (matches)
    {
        for (var index = 0; index < inputs.Length; index++)
        {
            if (!string.Equals(result.Results[index].Content, inputs[index].Payload, StringComparison.Ordinal) ||
                !string.Equals(result.Results[index].Format, inputs[index].ExpectedDisplayFormat, StringComparison.Ordinal))
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
        Console.WriteLine($"[{index + 1}] {result.Results[index].Format}: {result.Results[index].Content}");
    }

    Console.WriteLine($"Ordered Match: {matches}");
    Console.WriteLine();

    return matches ? 0 : 1;
}

async Task<int> VerifyMixedQrBarcodeAsync(PreviewBarcodeService service)
{
    const string barcodePayload = "IMVIX-BARCODE-128";
    const string qrPayload = "https://example.com/mixed";
    var result = await service.RecognizeAllAsync(CreateMixedImage(
        (barcodePayload, BarcodeFormat.CODE_128),
        (qrPayload, BarcodeFormat.QR_CODE)));

    var hasBarcode = false;
    var hasQr = false;

    foreach (var item in result.Results)
    {
        hasBarcode |= string.Equals(item.Content, barcodePayload, StringComparison.Ordinal) &&
                      string.Equals(item.Format, "Code128", StringComparison.Ordinal) &&
                      !item.IsQrCode;
        hasQr |= string.Equals(item.Content, qrPayload, StringComparison.Ordinal) &&
                 string.Equals(item.Format, "QR", StringComparison.Ordinal) &&
                 item.IsQrCode;
    }

    var matches = result.Results.Count >= 2 && hasBarcode && hasQr;

    Console.WriteLine("== MixedQrAndBarcode ==");
    Console.WriteLine($"Detected Count: {result.Results.Count}");

    for (var index = 0; index < result.Results.Count; index++)
    {
        Console.WriteLine($"[{index + 1}] {result.Results[index].Format}: {result.Results[index].Content} (IsQr={result.Results[index].IsQrCode})");
    }

    Console.WriteLine($"Mixed Match: {matches}");
    Console.WriteLine();

    return matches ? 0 : 1;
}

var service = new PreviewBarcodeService();
var failures = 0;

failures += await VerifySingleAsync(
    service,
    "SingleCode128",
    "IMVIX-BARCODE-HELLO",
    BarcodeFormat.CODE_128,
    "Code128");

failures += await VerifySingleAsync(
    service,
    "SingleEan13",
    "5901234123457",
    BarcodeFormat.EAN_13,
    "EAN-13");

failures += await VerifyMultipleAsync(
    service,
    "MultipleLinearBarcodes",
    ("IMVIX-LINEAR-1", BarcodeFormat.CODE_128, "Code128"),
    ("5901234123457", BarcodeFormat.EAN_13, "EAN-13"));

failures += await VerifyMixedQrBarcodeAsync(service);

Console.WriteLine($"Failures={failures}");
return failures == 0 ? 0 : 1;

