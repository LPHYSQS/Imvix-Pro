using ImageMagick;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ImvixPro.Services.PsdModule
{
    public sealed class PsdDetailService
    {
        private readonly PsdRenderService _psdRenderService = new();

        public PsdDetailInfo Read(string filePath, int fallbackWidth = 0, int fallbackHeight = 0, bool fallbackTransparency = false)
        {
            PsdDocumentInfo? renderInfo = null;
            if (_psdRenderService.TryReadDocumentInfo(filePath, out var cachedInfo, out _) && cachedInfo is not null)
            {
                renderInfo = cachedInfo;
            }

            var snapshot = TryReadSnapshot(filePath, out var parsedSnapshot)
                ? parsedSnapshot
                : default;

            var width = renderInfo?.Width ?? snapshot.Width ?? Math.Max(0, fallbackWidth);
            var height = renderInfo?.Height ?? snapshot.Height ?? Math.Max(0, fallbackHeight);
            var bitDepth = renderInfo?.BitDepth ?? snapshot.BitDepth;
            var colorMode = snapshot.ColorMode ?? renderInfo?.ColorMode;
            var compression = snapshot.CompositeCompression ?? snapshot.LayerCompression;

            return new PsdDetailInfo(
                width,
                height,
                bitDepth,
                colorMode,
                snapshot.DpiX,
                snapshot.DpiY,
                snapshot.ChannelCount,
                snapshot.HasAlphaChannel,
                renderInfo?.HasTransparency ?? snapshot.HasTransparencyChannel ?? fallbackTransparency,
                snapshot.LayerCount,
                snapshot.VisibleLayerCount,
                snapshot.HiddenLayerCount,
                snapshot.LayerGroupCount,
                snapshot.MaxLayerWidth,
                snapshot.MaxLayerHeight,
                snapshot.HasTransparencyChannel,
                snapshot.LockedLayerCount,
                snapshot.TextLayerCount,
                snapshot.ImageLayerCount,
                snapshot.ShapeLayerCount,
                snapshot.LayerNameSamples,
                snapshot.IccProfileName,
                snapshot.ColorSpace ?? ResolveColorSpace(colorMode),
                snapshot.HasEmbeddedColorProfile,
                snapshot.HasMergedImage,
                snapshot.CompatibilityVersion,
                compression switch
                {
                    null => null,
                    "Raw" => false,
                    _ => true
                },
                compression,
                snapshot.HasTextLayers,
                snapshot.HasSmartObjects,
                snapshot.HasVectorPaths);
        }

        private static bool TryReadSnapshot(string filePath, out PsdBinarySnapshot snapshot)
        {
            snapshot = default;

            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new BigEndianReader(stream);

                if (reader.ReadAscii(4) != "8BPS")
                {
                    return false;
                }

                var version = reader.ReadUInt16();
                if (version is not 1 and not 2)
                {
                    return false;
                }

                reader.Skip(6);

                var channelCount = reader.ReadUInt16();
                var height = checked((int)reader.ReadUInt32());
                var width = checked((int)reader.ReadUInt32());
                var bitDepth = (int)reader.ReadUInt16();
                var colorModeCode = reader.ReadUInt16();
                var colorMode = ResolveColorMode(colorModeCode);

                var builder = new SnapshotBuilder
                {
                    Width = width,
                    Height = height,
                    BitDepth = bitDepth,
                    ChannelCount = channelCount,
                    ColorMode = colorMode,
                    ColorSpace = ResolveColorSpace(colorMode),
                    HasAlphaChannel = TryResolveAlphaChannel(channelCount, colorModeCode)
                };

                reader.Skip(reader.ReadUInt32());
                ReadImageResources(reader, ref builder);
                ReadLayerAndMaskInfo(reader, version, ref builder);

                if (reader.Position + 2 <= reader.Length)
                {
                    builder.CompositeCompression = ResolveCompression(reader.ReadUInt16());
                    builder.HasMergedImage ??= true;
                }

                if (builder.HasEmbeddedColorProfile is null)
                {
                    builder.HasEmbeddedColorProfile = false;
                }

                snapshot = builder.Build();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void ReadImageResources(BigEndianReader reader, ref SnapshotBuilder builder)
        {
            var resourceSectionLength = reader.ReadUInt32();
            var resourceSectionEnd = reader.Position + resourceSectionLength;
            if (resourceSectionEnd > reader.Length)
            {
                throw new EndOfStreamException();
            }

            while (reader.Position < resourceSectionEnd)
            {
                if (resourceSectionEnd - reader.Position < 12)
                {
                    reader.Position = resourceSectionEnd;
                    break;
                }

                var signature = reader.ReadAscii(4);
                if (signature is not "8BIM" and not "MeSa")
                {
                    reader.Position = resourceSectionEnd;
                    break;
                }

                var resourceId = reader.ReadUInt16();
                SkipPascalString(reader, 2);
                var dataLength = reader.ReadUInt32();
                if (reader.Position + dataLength > resourceSectionEnd)
                {
                    reader.Position = resourceSectionEnd;
                    break;
                }

                var data = reader.ReadBytes(checked((int)dataLength));
                if ((dataLength & 1) == 1 && reader.Position < resourceSectionEnd)
                {
                    reader.Skip(1);
                }

                switch (resourceId)
                {
                    case 1005:
                        TryReadResolutionInfo(data, ref builder);
                        break;
                    case 1039:
                        builder.HasEmbeddedColorProfile = true;
                        TryReadIccProfile(data, ref builder);
                        break;
                    case 1057:
                        TryReadVersionInfo(data, ref builder);
                        break;
                }
            }
        }

        private static void ReadLayerAndMaskInfo(BigEndianReader reader, int version, ref SnapshotBuilder builder)
        {
            var layerAndMaskLength = version == 2
                ? checked((long)reader.ReadUInt64())
                : reader.ReadUInt32();
            var layerAndMaskEnd = reader.Position + layerAndMaskLength;
            if (layerAndMaskEnd > reader.Length)
            {
                throw new EndOfStreamException();
            }

            if (layerAndMaskLength <= 0)
            {
                return;
            }

            var layerInfoLength = version == 2
                ? checked((long)reader.ReadUInt64())
                : reader.ReadUInt32();
            var layerInfoEnd = reader.Position + layerInfoLength;
            if (layerInfoEnd > layerAndMaskEnd)
            {
                reader.Position = layerAndMaskEnd;
                return;
            }

            if (layerInfoLength <= 0 || reader.Position >= layerInfoEnd)
            {
                reader.Position = layerAndMaskEnd;
                return;
            }

            var rawLayerCount = reader.ReadInt16();
            builder.HasTransparencyChannel = rawLayerCount < 0 || builder.HasAlphaChannel == true;
            var layerRecordCount = Math.Abs(rawLayerCount);
            var records = new List<LayerRecord>(layerRecordCount);

            for (var index = 0; index < layerRecordCount; index++)
            {
                if (reader.Position >= layerInfoEnd)
                {
                    break;
                }

                records.Add(ReadLayerRecord(reader, version));
            }

            var layerCompressionMethods = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                foreach (var channel in record.Channels)
                {
                    if (channel.Length <= 0 || reader.Position >= layerInfoEnd)
                    {
                        continue;
                    }

                    var bytesAvailable = Math.Min(channel.Length, layerInfoEnd - reader.Position);
                    if (bytesAvailable >= 2)
                    {
                        var compression = ResolveCompression(reader.ReadUInt16());
                        if (!string.IsNullOrWhiteSpace(compression))
                        {
                            layerCompressionMethods.Add(compression);
                        }

                        bytesAvailable -= 2;
                    }

                    if (bytesAvailable > 0)
                    {
                        reader.Skip(bytesAvailable);
                    }
                }
            }

            SummarizeLayers(records, ref builder);
            builder.LayerCompression = layerCompressionMethods.Count switch
            {
                0 => null,
                1 => layerCompressionMethods.First(),
                _ => string.Join(", ", layerCompressionMethods.OrderBy(static value => value, StringComparer.Ordinal))
            };

            reader.Position = layerAndMaskEnd;
        }

        private static void SummarizeLayers(IReadOnlyList<LayerRecord> records, ref SnapshotBuilder builder)
        {
            var layerCount = 0;
            var visibleLayerCount = 0;
            var hiddenLayerCount = 0;
            var layerGroupCount = 0;
            var maxLayerWidth = 0;
            var maxLayerHeight = 0;
            var lockedLayerCount = 0;
            var textLayerCount = 0;
            var shapeLayerCount = 0;
            var imageLayerCount = 0;
            var sampleNames = new List<string>(5);
            var hasTextLayers = false;
            var hasSmartObjects = false;
            var hasVectorPaths = false;

            foreach (var record in records)
            {
                if (record.IsGroupEnd)
                {
                    continue;
                }

                layerCount++;
                if (record.IsHidden)
                {
                    hiddenLayerCount++;
                }
                else
                {
                    visibleLayerCount++;
                }

                maxLayerWidth = Math.Max(maxLayerWidth, Math.Max(0, record.Width));
                maxLayerHeight = Math.Max(maxLayerHeight, Math.Max(0, record.Height));

                if (record.IsGroupStart)
                {
                    layerGroupCount++;
                }
                else if (record.IsTextLayer)
                {
                    textLayerCount++;
                    hasTextLayers = true;
                }
                else if (record.IsShapeLayer)
                {
                    shapeLayerCount++;
                    hasVectorPaths = true;
                }
                else
                {
                    imageLayerCount++;
                }

                if (record.IsLocked)
                {
                    lockedLayerCount++;
                }

                if (record.IsSmartObject)
                {
                    hasSmartObjects = true;
                }

                if (record.HasVectorPath)
                {
                    hasVectorPaths = true;
                }

                if (sampleNames.Count < 5 && !string.IsNullOrWhiteSpace(record.Name))
                {
                    sampleNames.Add(record.Name);
                }
            }

            builder.LayerCount = layerCount;
            builder.VisibleLayerCount = visibleLayerCount;
            builder.HiddenLayerCount = hiddenLayerCount;
            builder.LayerGroupCount = layerGroupCount;
            builder.MaxLayerWidth = maxLayerWidth > 0 ? maxLayerWidth : null;
            builder.MaxLayerHeight = maxLayerHeight > 0 ? maxLayerHeight : null;
            builder.LockedLayerCount = lockedLayerCount;
            builder.TextLayerCount = textLayerCount;
            builder.ImageLayerCount = imageLayerCount;
            builder.ShapeLayerCount = shapeLayerCount;
            builder.LayerNameSamples = sampleNames;
            builder.HasTextLayers = hasTextLayers;
            builder.HasSmartObjects = hasSmartObjects;
            builder.HasVectorPaths = hasVectorPaths;
        }

        private static LayerRecord ReadLayerRecord(BigEndianReader reader, int version)
        {
            var top = reader.ReadInt32();
            var left = reader.ReadInt32();
            var bottom = reader.ReadInt32();
            var right = reader.ReadInt32();
            var channelCount = reader.ReadUInt16();

            var channels = new List<LayerChannel>(channelCount);
            for (var channelIndex = 0; channelIndex < channelCount; channelIndex++)
            {
                var channelId = reader.ReadInt16();
                var channelLength = version == 2
                    ? checked((long)reader.ReadUInt64())
                    : reader.ReadUInt32();
                channels.Add(new LayerChannel(channelId, channelLength));
            }

            reader.Skip(4);
            reader.Skip(4);
            reader.Skip(1);
            reader.Skip(1);
            var flags = reader.ReadByte();
            reader.Skip(1);

            var extraLength = reader.ReadUInt32();
            var extraEnd = reader.Position + extraLength;

            reader.Skip(reader.ReadUInt32());
            reader.Skip(reader.ReadUInt32());

            var layerName = ReadPascalString(reader, 4);
            var unicodeLayerName = layerName;
            var isGroupStart = false;
            var isGroupEnd = false;
            var isTextLayer = false;
            var isShapeLayer = false;
            var isSmartObject = false;
            var hasVectorPath = false;
            var isLocked = false;

            while (reader.Position < extraEnd)
            {
                if (extraEnd - reader.Position < 12)
                {
                    reader.Position = extraEnd;
                    break;
                }

                var signature = reader.ReadAscii(4);
                if (signature is not "8BIM" and not "8B64")
                {
                    reader.Position = extraEnd;
                    break;
                }

                var key = reader.ReadAscii(4);
                var dataLength = signature == "8B64"
                    ? checked((long)reader.ReadUInt64())
                    : reader.ReadUInt32();
                var dataEnd = reader.Position + dataLength;

                if (dataEnd > extraEnd)
                {
                    reader.Position = extraEnd;
                    break;
                }

                switch (key)
                {
                    case "luni":
                        unicodeLayerName = ReadUnicodeString(reader, dataLength) ?? unicodeLayerName;
                        break;
                    case "lsct":
                    case "lsdk":
                        ReadSectionDivider(reader, dataLength, out isGroupStart, out isGroupEnd);
                        break;
                    case "TySh":
                        isTextLayer = true;
                        break;
                    case "SoLd":
                    case "PlLd":
                        isSmartObject = true;
                        break;
                    case "vmsk":
                    case "vsms":
                    case "vscg":
                    case "vogk":
                        isShapeLayer = true;
                        hasVectorPath = true;
                        break;
                    case "lspf":
                        isLocked = ReadLockedFlag(reader, dataLength);
                        break;
                }

                reader.Position = dataEnd;
                if ((dataLength & 1) == 1 && reader.Position < extraEnd)
                {
                    reader.Skip(1);
                }
            }

            reader.Position = extraEnd;

            return new LayerRecord(
                unicodeLayerName,
                right - left,
                bottom - top,
                (flags & 0x02) != 0,
                isGroupStart,
                isGroupEnd,
                isTextLayer,
                isShapeLayer,
                isSmartObject,
                hasVectorPath,
                isLocked,
                channels);
        }

        private static bool ReadLockedFlag(BigEndianReader reader, long dataLength)
        {
            if (dataLength < 4)
            {
                return false;
            }

            return reader.ReadUInt32() != 0;
        }

        private static void ReadSectionDivider(BigEndianReader reader, long dataLength, out bool isGroupStart, out bool isGroupEnd)
        {
            isGroupStart = false;
            isGroupEnd = false;

            if (dataLength < 4)
            {
                return;
            }

            var dividerType = reader.ReadInt32();
            isGroupStart = dividerType is 1 or 2;
            isGroupEnd = dividerType == 3;
        }

        private static void TryReadResolutionInfo(byte[] data, ref SnapshotBuilder builder)
        {
            try
            {
                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BigEndianReader(stream);

                var horizontalResolution = ReadFixedPoint1616(reader);
                var horizontalUnit = reader.ReadInt16();
                reader.Skip(2);
                var verticalResolution = ReadFixedPoint1616(reader);
                var verticalUnit = reader.ReadInt16();

                builder.DpiX = ConvertToDpi(horizontalResolution, horizontalUnit);
                builder.DpiY = ConvertToDpi(verticalResolution, verticalUnit);
            }
            catch (EndOfStreamException)
            {
            }
        }

        private static void TryReadIccProfile(byte[] data, ref SnapshotBuilder builder)
        {
            try
            {
                var colorProfile = new ColorProfile(data);
                builder.IccProfileName =
                    NormalizeText(colorProfile.Description) ??
                    NormalizeText(colorProfile.Model) ??
                    NormalizeText(colorProfile.Manufacturer);

                builder.ColorSpace ??= NormalizeText(colorProfile.ColorSpace.ToString());
            }
            catch (MagickException)
            {
                builder.IccProfileName = null;
            }
        }

        private static void TryReadVersionInfo(byte[] data, ref SnapshotBuilder builder)
        {
            try
            {
                using var stream = new MemoryStream(data, writable: false);
                using var reader = new BigEndianReader(stream);

                reader.ReadUInt32();
                builder.HasMergedImage = reader.ReadByte() != 0;
                var writerName = ReadUnicodeString(reader);
                var readerName = ReadUnicodeString(reader);
                var fileVersion = stream.Position + 4 <= stream.Length ? reader.ReadUInt32() : 0U;

                var preferredName = NormalizeText(writerName) ?? NormalizeText(readerName);
                builder.CompatibilityVersion = preferredName switch
                {
                    null => fileVersion > 0 ? string.Create(CultureInfo.InvariantCulture, $"Version {fileVersion}") : null,
                    _ when fileVersion > 0 => string.Create(CultureInfo.InvariantCulture, $"{preferredName} {fileVersion}"),
                    _ => preferredName
                };
            }
            catch (EndOfStreamException)
            {
            }
        }

        private static string? ReadUnicodeString(BigEndianReader reader, long? maxLength = null)
        {
            if (maxLength is not null && maxLength.Value < 4)
            {
                return null;
            }

            var characterCount = reader.ReadUInt32();
            var byteCount = checked((long)characterCount * 2L);
            if (maxLength is not null && 4 + byteCount > maxLength.Value)
            {
                return null;
            }

            if (byteCount == 0)
            {
                return null;
            }

            var bytes = reader.ReadBytes(checked((int)byteCount));
            return NormalizeText(Encoding.BigEndianUnicode.GetString(bytes));
        }

        private static string ReadPascalString(BigEndianReader reader, int alignment)
        {
            var length = reader.ReadByte();
            var bytes = length == 0 ? [] : reader.ReadBytes(length);
            var consumed = 1 + length;
            var padding = (alignment - (consumed % alignment)) % alignment;
            if (padding > 0)
            {
                reader.Skip(padding);
            }

            return NormalizeText(Encoding.Latin1.GetString(bytes)) ?? string.Empty;
        }

        private static void SkipPascalString(BigEndianReader reader, int alignment)
        {
            var length = reader.ReadByte();
            var consumed = 1 + length;
            if (length > 0)
            {
                reader.Skip(length);
            }

            var padding = (alignment - (consumed % alignment)) % alignment;
            if (padding > 0)
            {
                reader.Skip(padding);
            }
        }

        private static double ReadFixedPoint1616(BigEndianReader reader)
        {
            return reader.ReadInt32() / 65536d;
        }

        private static double? ConvertToDpi(double value, int unit)
        {
            return unit switch
            {
                1 => value,
                2 => value * 2.54d,
                _ => null
            };
        }

        private static bool? TryResolveAlphaChannel(int channelCount, ushort colorModeCode)
        {
            var colorChannelCount = colorModeCode switch
            {
                0 => 1,
                1 => 1,
                2 => 1,
                3 => 3,
                4 => 4,
                8 => 1,
                9 => 3,
                _ => (int?)null
            };

            return colorChannelCount is int count ? channelCount > count : null;
        }

        private static string? ResolveColorMode(ushort colorMode)
        {
            return colorMode switch
            {
                0 => "Bitmap",
                1 => "Grayscale",
                2 => "Indexed",
                3 => "RGB",
                4 => "CMYK",
                7 => "Multichannel",
                8 => "Duotone",
                9 => "Lab",
                _ => null
            };
        }

        private static string? ResolveColorSpace(string? colorMode)
        {
            return colorMode switch
            {
                "Bitmap" => "Bitmap",
                "Grayscale" => "Grayscale",
                "Indexed" => "Indexed",
                "RGB" => "RGB",
                "CMYK" => "CMYK",
                "Multichannel" => "Multichannel",
                "Duotone" => "Duotone",
                "Lab" => "Lab",
                _ => null
            };
        }

        private static string? ResolveCompression(ushort compression)
        {
            return compression switch
            {
                0 => "Raw",
                1 => "RLE",
                2 => "ZIP",
                3 => "ZIP Prediction",
                _ => null
            };
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
        }

        private sealed class BigEndianReader : IDisposable
        {
            private readonly Stream _stream;

            public BigEndianReader(Stream stream)
            {
                _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            }

            public long Position
            {
                get => _stream.Position;
                set => _stream.Position = value;
            }

            public long Length => _stream.Length;

            public byte ReadByte()
            {
                var value = _stream.ReadByte();
                if (value < 0)
                {
                    throw new EndOfStreamException();
                }

                return (byte)value;
            }

            public short ReadInt16()
            {
                Span<byte> buffer = stackalloc byte[2];
                FillBuffer(buffer);
                return BinaryPrimitives.ReadInt16BigEndian(buffer);
            }

            public ushort ReadUInt16()
            {
                Span<byte> buffer = stackalloc byte[2];
                FillBuffer(buffer);
                return BinaryPrimitives.ReadUInt16BigEndian(buffer);
            }

            public int ReadInt32()
            {
                Span<byte> buffer = stackalloc byte[4];
                FillBuffer(buffer);
                return BinaryPrimitives.ReadInt32BigEndian(buffer);
            }

            public uint ReadUInt32()
            {
                Span<byte> buffer = stackalloc byte[4];
                FillBuffer(buffer);
                return BinaryPrimitives.ReadUInt32BigEndian(buffer);
            }

            public ulong ReadUInt64()
            {
                Span<byte> buffer = stackalloc byte[8];
                FillBuffer(buffer);
                return BinaryPrimitives.ReadUInt64BigEndian(buffer);
            }

            public string ReadAscii(int count)
            {
                return Encoding.ASCII.GetString(ReadBytes(count));
            }

            public byte[] ReadBytes(int count)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(count);

                var buffer = new byte[count];
                if (count == 0)
                {
                    return buffer;
                }

                FillBuffer(buffer);
                return buffer;
            }

            public void Skip(long count)
            {
                if (count < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                if (count == 0)
                {
                    return;
                }

                if (_stream.Position + count > _stream.Length)
                {
                    throw new EndOfStreamException();
                }

                _stream.Position += count;
            }

            public void Dispose()
            {
                _stream.Dispose();
            }

            private void FillBuffer(Span<byte> buffer)
            {
                var totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    var bytesRead = _stream.Read(buffer[totalRead..]);
                    if (bytesRead <= 0)
                    {
                        throw new EndOfStreamException();
                    }

                    totalRead += bytesRead;
                }
            }
        }

        private sealed record LayerChannel(short Id, long Length);

        private sealed record LayerRecord(
            string Name,
            int Width,
            int Height,
            bool IsHidden,
            bool IsGroupStart,
            bool IsGroupEnd,
            bool IsTextLayer,
            bool IsShapeLayer,
            bool IsSmartObject,
            bool HasVectorPath,
            bool IsLocked,
            IReadOnlyList<LayerChannel> Channels);

        private readonly record struct PsdBinarySnapshot(
            int? Width,
            int? Height,
            int? BitDepth,
            string? ColorMode,
            double? DpiX,
            double? DpiY,
            int? ChannelCount,
            bool? HasAlphaChannel,
            bool? HasTransparencyChannel,
            int? LayerCount,
            int? VisibleLayerCount,
            int? HiddenLayerCount,
            int? LayerGroupCount,
            int? MaxLayerWidth,
            int? MaxLayerHeight,
            int LockedLayerCount,
            int TextLayerCount,
            int ImageLayerCount,
            int ShapeLayerCount,
            IReadOnlyList<string> LayerNameSamples,
            string? IccProfileName,
            string? ColorSpace,
            bool? HasEmbeddedColorProfile,
            bool? HasMergedImage,
            string? CompatibilityVersion,
            string? CompositeCompression,
            string? LayerCompression,
            bool? HasTextLayers,
            bool? HasSmartObjects,
            bool? HasVectorPaths);

        private struct SnapshotBuilder
        {
            public int? Width { get; set; }
            public int? Height { get; set; }
            public int? BitDepth { get; set; }
            public string? ColorMode { get; set; }
            public double? DpiX { get; set; }
            public double? DpiY { get; set; }
            public int? ChannelCount { get; set; }
            public bool? HasAlphaChannel { get; set; }
            public bool? HasTransparencyChannel { get; set; }
            public int? LayerCount { get; set; }
            public int? VisibleLayerCount { get; set; }
            public int? HiddenLayerCount { get; set; }
            public int? LayerGroupCount { get; set; }
            public int? MaxLayerWidth { get; set; }
            public int? MaxLayerHeight { get; set; }
            public int LockedLayerCount { get; set; }
            public int TextLayerCount { get; set; }
            public int ImageLayerCount { get; set; }
            public int ShapeLayerCount { get; set; }
            public IReadOnlyList<string>? LayerNameSamples { get; set; }
            public string? IccProfileName { get; set; }
            public string? ColorSpace { get; set; }
            public bool? HasEmbeddedColorProfile { get; set; }
            public bool? HasMergedImage { get; set; }
            public string? CompatibilityVersion { get; set; }
            public string? CompositeCompression { get; set; }
            public string? LayerCompression { get; set; }
            public bool? HasTextLayers { get; set; }
            public bool? HasSmartObjects { get; set; }
            public bool? HasVectorPaths { get; set; }

            public PsdBinarySnapshot Build()
            {
                return new PsdBinarySnapshot(
                    Width,
                    Height,
                    BitDepth,
                    ColorMode,
                    DpiX,
                    DpiY,
                    ChannelCount,
                    HasAlphaChannel,
                    HasTransparencyChannel,
                    LayerCount,
                    VisibleLayerCount,
                    HiddenLayerCount,
                    LayerGroupCount,
                    MaxLayerWidth,
                    MaxLayerHeight,
                    LockedLayerCount,
                    TextLayerCount,
                    ImageLayerCount,
                    ShapeLayerCount,
                    LayerNameSamples ?? [],
                    IccProfileName,
                    ColorSpace,
                    HasEmbeddedColorProfile,
                    HasMergedImage,
                    CompatibilityVersion,
                    CompositeCompression,
                    LayerCompression,
                    HasTextLayers,
                    HasSmartObjects,
                    HasVectorPaths);
            }
        }
    }
}
