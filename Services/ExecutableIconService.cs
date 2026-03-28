using SkiaSharp;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

namespace ImvixPro.Services
{
    public static class ExecutableIconService
    {
        private const int ResourceDirectoryHeaderSize = 16;
        private const int ResourceDirectoryEntrySize = 8;
        private const int ResourceDataEntrySize = 16;
        private const int GroupIconResourceType = 14;
        private const int IconResourceType = 3;
        private const int IconDirectoryHeaderSize = 6;
        private const int GroupIconEntrySize = 14;
        private const int SingleIconDirectoryEntrySize = 16;

        public static bool IsExecutableIconSource(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPortableExecutableIconSource(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".dll", StringComparison.OrdinalIgnoreCase);
        }

        public static SKBitmap? TryExtractPrimaryIconBitmap(string filePath)
        {
            return TryExtractPrimaryIconBitmap(filePath, iconIndex: 0);
        }

        public static SKBitmap? TryExtractPrimaryIconBitmap(string filePath, int iconIndex)
        {
            if (!IsPortableExecutableIconSource(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var iconBytes = TryBuildPrimaryIconFile(filePath, iconIndex);
                if (iconBytes is null)
                {
                    return null;
                }

                using var memory = new MemoryStream(iconBytes, writable: false);
                return SKBitmap.Decode(memory);
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? TryBuildPrimaryIconFile(string filePath, int iconIndex)
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);

            var peHeader = peReader.PEHeaders.PEHeader;
            if (peHeader is null)
            {
                return null;
            }

            var resourceDirectory = peHeader.ResourceTableDirectory;
            if (resourceDirectory.RelativeVirtualAddress <= 0 || resourceDirectory.Size <= 0)
            {
                return null;
            }

            if (!TryReadResourceSection(stream, peReader.PEHeaders.SectionHeaders, resourceDirectory.RelativeVirtualAddress, out var resourceSection))
            {
                return null;
            }

            if (!TryGetTypeEntries(resourceSection.Bytes, GroupIconResourceType, out var groupIconEntries) || groupIconEntries.Count == 0)
            {
                return null;
            }

            var selectedGroupEntry = groupIconEntries[NormalizeIconIndex(iconIndex, groupIconEntries.Count)];
            if (!TryReadResourceDataAtEntry(resourceSection.Bytes, resourceSection.VirtualAddress, selectedGroupEntry, out var groupIconData))
            {
                return null;
            }

            if (!TrySelectLargestGroupIcon(groupIconData, out var selectedIcon))
            {
                return null;
            }

            if (!TryReadResourceData(resourceSection.Bytes, resourceSection.VirtualAddress, IconResourceType, selectedIcon.ResourceId, out var iconData))
            {
                return null;
            }

            return BuildSingleIconFile(selectedIcon, iconData);
        }

        private static bool TryReadResourceSection(
            Stream stream,
            IReadOnlyList<SectionHeader> sectionHeaders,
            int resourceRva,
            out ResourceSection resourceSection)
        {
            for (var index = 0; index < sectionHeaders.Count; index++)
            {
                var section = sectionHeaders[index];
                var rawSize = Math.Max(0, section.SizeOfRawData);
                var mappedSize = Math.Max(rawSize, section.VirtualSize);
                if (resourceRva < section.VirtualAddress || resourceRva >= section.VirtualAddress + mappedSize || rawSize <= 0)
                {
                    continue;
                }

                var bytes = new byte[rawSize];
                stream.Position = section.PointerToRawData;
                stream.ReadExactly(bytes);
                resourceSection = new ResourceSection(bytes, section.VirtualAddress);
                return true;
            }

            resourceSection = default;
            return false;
        }

        private static bool TryReadResourceData(
            byte[] resourceSection,
            int resourceSectionRva,
            int typeId,
            int resourceId,
            out byte[] data)
        {
            data = [];

            if (!TryGetTypeEntries(resourceSection, typeId, out var entries))
            {
                return false;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.Id != resourceId)
                {
                    continue;
                }

                return TryReadResourceDataAtEntry(resourceSection, resourceSectionRva, entry, out data);
            }

            return false;
        }

        private static bool TryGetTypeEntries(byte[] resourceSection, int typeId, out List<ResourceDirectoryEntry> entries)
        {
            entries = [];

            if (!TryGetDirectoryEntries(resourceSection, 0, out var rootEntries))
            {
                return false;
            }

            for (var index = 0; index < rootEntries.Count; index++)
            {
                var entry = rootEntries[index];
                if (!entry.IsDirectory || entry.Id != typeId)
                {
                    continue;
                }

                return TryGetDirectoryEntries(resourceSection, entry.ChildOffset, out entries);
            }

            return false;
        }

        private static bool TryReadResourceDataAtEntry(
            byte[] resourceSection,
            int resourceSectionRva,
            ResourceDirectoryEntry entry,
            out byte[] data)
        {
            data = [];

            if (!entry.IsDirectory || !TryGetDirectoryEntries(resourceSection, entry.ChildOffset, out var languageEntries) || languageEntries.Count == 0)
            {
                return false;
            }

            for (var index = 0; index < languageEntries.Count; index++)
            {
                var languageEntry = languageEntries[index];
                if (languageEntry.IsDirectory)
                {
                    continue;
                }

                return TryReadResourceDataEntry(resourceSection, resourceSectionRva, languageEntry.ChildOffset, out data);
            }

            return false;
        }

        private static bool TryReadResourceDataEntry(
            byte[] resourceSection,
            int resourceSectionRva,
            int dataEntryOffset,
            out byte[] data)
        {
            data = [];

            if (!HasBytes(resourceSection, dataEntryOffset, ResourceDataEntrySize))
            {
                return false;
            }

            var dataRva = (int)ReadUInt32(resourceSection, dataEntryOffset);
            var dataSize = (int)ReadUInt32(resourceSection, dataEntryOffset + 4);
            var dataOffset = dataRva - resourceSectionRva;
            if (dataOffset < 0 || dataSize <= 0 || dataOffset > resourceSection.Length - dataSize)
            {
                return false;
            }

            data = resourceSection.AsSpan(dataOffset, dataSize).ToArray();
            return true;
        }

        private static bool TryGetDirectoryEntries(byte[] resourceSection, int directoryOffset, out List<ResourceDirectoryEntry> entries)
        {
            entries = [];

            if (!HasBytes(resourceSection, directoryOffset, ResourceDirectoryHeaderSize))
            {
                return false;
            }

            var entryCount =
                ReadUInt16(resourceSection, directoryOffset + 12) +
                ReadUInt16(resourceSection, directoryOffset + 14);
            if (entryCount <= 0)
            {
                return false;
            }

            var entriesOffset = directoryOffset + ResourceDirectoryHeaderSize;
            for (var index = 0; index < entryCount; index++)
            {
                var entryOffset = entriesOffset + (index * ResourceDirectoryEntrySize);
                if (!HasBytes(resourceSection, entryOffset, ResourceDirectoryEntrySize))
                {
                    return false;
                }

                var name = ReadUInt32(resourceSection, entryOffset);
                var data = ReadUInt32(resourceSection, entryOffset + 4);
                var isNamedEntry = (name & 0x80000000u) != 0;
                var id = isNamedEntry ? (int?)null : (int)(name & 0xFFFFu);

                entries.Add(new ResourceDirectoryEntry(
                    id,
                    (int)(data & 0x7FFFFFFFu),
                    (data & 0x80000000u) != 0,
                    isNamedEntry));
            }

            return entries.Count > 0;
        }

        private static bool TrySelectLargestGroupIcon(byte[] groupIconData, out GroupIconEntry entry)
        {
            entry = default;

            if (!HasBytes(groupIconData, 0, IconDirectoryHeaderSize))
            {
                return false;
            }

            var reserved = ReadUInt16(groupIconData, 0);
            var type = ReadUInt16(groupIconData, 2);
            var count = ReadUInt16(groupIconData, 4);
            if (reserved != 0 || type != 1 || count <= 0)
            {
                return false;
            }

            var bestArea = -1;
            var bestBitCount = -1;
            uint bestResourceSize = 0;

            for (var index = 0; index < count; index++)
            {
                var offset = IconDirectoryHeaderSize + (index * GroupIconEntrySize);
                if (!HasBytes(groupIconData, offset, GroupIconEntrySize))
                {
                    return false;
                }

                var widthByte = groupIconData[offset];
                var heightByte = groupIconData[offset + 1];
                var colorCount = groupIconData[offset + 2];
                var width = widthByte == 0 ? 256 : widthByte;
                var height = heightByte == 0 ? 256 : heightByte;
                var area = width * height;
                var planes = ReadUInt16(groupIconData, offset + 4);
                var bitCount = ReadUInt16(groupIconData, offset + 6);
                var resourceSize = ReadUInt32(groupIconData, offset + 8);
                var resourceId = ReadUInt16(groupIconData, offset + 12);

                if (resourceId == 0)
                {
                    continue;
                }

                if (area < bestArea ||
                    (area == bestArea && bitCount < bestBitCount) ||
                    (area == bestArea && bitCount == bestBitCount && resourceSize <= bestResourceSize))
                {
                    continue;
                }

                bestArea = area;
                bestBitCount = bitCount;
                bestResourceSize = resourceSize;
                entry = new GroupIconEntry(widthByte, heightByte, colorCount, planes, bitCount, resourceId);
            }

            return bestArea > 0;
        }

        private static byte[] BuildSingleIconFile(GroupIconEntry entry, byte[] iconData)
        {
            var buffer = new byte[IconDirectoryHeaderSize + SingleIconDirectoryEntrySize + iconData.Length];
            WriteUInt16(buffer, 0, 0);
            WriteUInt16(buffer, 2, 1);
            WriteUInt16(buffer, 4, 1);

            buffer[6] = entry.WidthByte;
            buffer[7] = entry.HeightByte;
            buffer[8] = entry.ColorCount;
            buffer[9] = 0;
            WriteUInt16(buffer, 10, entry.Planes);
            WriteUInt16(buffer, 12, entry.BitCount);
            WriteUInt32(buffer, 14, (uint)iconData.Length);
            WriteUInt32(buffer, 18, IconDirectoryHeaderSize + SingleIconDirectoryEntrySize);

            iconData.CopyTo(buffer.AsSpan(IconDirectoryHeaderSize + SingleIconDirectoryEntrySize));
            return buffer;
        }

        private static int NormalizeIconIndex(int iconIndex, int entryCount)
        {
            if (entryCount <= 0)
            {
                return 0;
            }

            return iconIndex >= 0 && iconIndex < entryCount
                ? iconIndex
                : 0;
        }

        private static bool HasBytes(byte[] bytes, int offset, int length)
        {
            return offset >= 0 && length >= 0 && offset <= bytes.Length - length;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));
        }

        private static void WriteUInt16(byte[] bytes, int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)), value);
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);
        }

        private readonly record struct ResourceSection(byte[] Bytes, int VirtualAddress);

        private readonly record struct ResourceDirectoryEntry(int? Id, int ChildOffset, bool IsDirectory, bool IsNamed);

        private readonly record struct GroupIconEntry(
            byte WidthByte,
            byte HeightByte,
            byte ColorCount,
            ushort Planes,
            ushort BitCount,
            ushort ResourceId);
    }
}
