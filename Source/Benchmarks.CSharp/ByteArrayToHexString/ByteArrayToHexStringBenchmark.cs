﻿using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.CSharp.ByteArrayToHexString
{
    /// <summary>
    /// byte配列を16進数stringに変換
    /// cf. https://stackoverflow.com/q/311165
    /// </summary>
    [Config(typeof(BenchmarkConfig))]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "ベンチマーク")]
    public class ByteArrayToHexStringBenchmark
    {
        // HMAC-SHA256は256bit
        const int ArraySize = 32;
        static readonly byte[] SourceBytes;

        [SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline", Justification = "ベンチマーク")]
        static ByteArrayToHexStringBenchmark()
        {
            SourceBytes = new byte[ArraySize];
            var random = new Random();
            random.NextBytes(SourceBytes);
        }

        [Benchmark]
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "小文字が必要")]
        public string BitConverterToString()
            => BitConverter.ToString(SourceBytes).ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal);

        [Benchmark]
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "小文字が必要")]
        public string XmlSerializationWriterFromByteArrayHex()
            => ByteArrayHelperXmlSerializationWriter.ToHexString(SourceBytes).ToLowerInvariant();

        [Benchmark]
        public string ArrayConvertAll()
            => string.Concat(Array.ConvertAll(SourceBytes, b => b.ToString("x2")));

        [Benchmark]
        public string LinqSelect()
            => string.Concat(SourceBytes.Select(b => b.ToString("x2")));

        [Benchmark]
        public string ForEach()
        {
            var length = SourceBytes.Length * 2;
            var result = new string(default, length);
            var span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(result.AsSpan()), length);
            var i = 0;
            foreach (var sourceByte in SourceBytes)
            {
                sourceByte.TryFormat(span[i..], out _, "x2");
                i += 2;
            }

            return result;
        }

        [Benchmark]
        public string UnsafeAsLong()
        {
            var length = SourceBytes.Length * 2;
            var result = new string(default, length);
            var resultSpan = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(result.AsSpan()), length);
            ref var sourceStart = ref Unsafe.As<byte, long>(ref SourceBytes[0]);

            const int Size = sizeof(long);
            const string Format = "x16";

            BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref sourceStart, 0))
                .TryFormat(resultSpan, out _, Format);
            BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref sourceStart, 1))
                .TryFormat(resultSpan[(Size * 2 * 1)..], out _, Format);
            BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref sourceStart, 2))
                .TryFormat(resultSpan[(Size * 2 * 2)..], out _, Format);
            BinaryPrimitives.ReverseEndianness(Unsafe.Add(ref sourceStart, 3))
                .TryFormat(resultSpan[(Size * 2 * 3)..], out _, Format);

            return result;
        }

        [Benchmark]
        public string UnsafeReadUnalignedLong()
        {
            var length = SourceBytes.Length * 2;
            var result = new string(default, length);
            var resultSpan = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(result.AsSpan()), length);
            ref var sourceStart = ref SourceBytes[0];

            const int Size = sizeof(long);
            const string Format = "x16";

            // ReSharper disable once CommentTypo
            // BinaryPrimitives.ReadInt64BigEndianやBitConverter.ToInt64、MemoryMarshal.Read内部では、
            // Unsafe.ReadUnalignedを使用している。
            // cf. https://github.com/dotnet/corefx/blob/b0f6ef48cca9ae70b0e8d81ffa640cbdd1b26f55/src/Common/src/CoreLib/System/Buffers/Binary/ReaderBigEndian.cs#L46
            // cf. https://github.com/dotnet/corefx/blob/v2.2.0/src/Common/src/CoreLib/System/BitConverter.cs#L293
            // cf. https://github.com/dotnet/corefx/blob/v2.2.0/src/Common/src/CoreLib/System/Runtime/InteropServices/MemoryMarshal.cs#L165
            BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref sourceStart, Size * 0)))
                .TryFormat(resultSpan, out _, Format);
            BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref sourceStart, Size * 1)))
                .TryFormat(resultSpan[(Size * 2 * 1)..], out _, Format);
            BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref sourceStart, Size * 2)))
                .TryFormat(resultSpan[(Size * 2 * 2)..], out _, Format);
            BinaryPrimitives.ReverseEndianness(Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref sourceStart, Size * 3)))
                .TryFormat(resultSpan[(Size * 2 * 3)..], out _, Format);

            return result;
        }

        [Benchmark]
        public string BinaryReader()
        {
            var length = SourceBytes.Length * 2;
            var result = new string(default, length);
            var resultSpan = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(result.AsSpan()), length);

            using var ms = new MemoryStream(SourceBytes);
            using var br = new BinaryReader(ms);

            const int Size = sizeof(long);
            const string Format = "x16";

            BinaryPrimitives.ReverseEndianness(br.ReadInt64())
                .TryFormat(resultSpan, out _, Format);
            BinaryPrimitives.ReverseEndianness(br.ReadInt64())
                .TryFormat(resultSpan[(Size * 2 * 1)..], out _, Format);
            BinaryPrimitives.ReverseEndianness(br.ReadInt64())
                .TryFormat(resultSpan[(Size * 2 * 2)..], out _, Format);
            BinaryPrimitives.ReverseEndianness(br.ReadInt64())
                .TryFormat(resultSpan[(Size * 2 * 3)..], out _, Format);

            return result;
        }

        [Benchmark]
        public string LookupShift1()
        {
            ReadOnlySpan<char> table = "0123456789abcdef";
            ref var tableStart = ref MemoryMarshal.GetReference(table);
            var result = new string(default, SourceBytes.Length * 2);
            ref var resultStart = ref MemoryMarshal.GetReference(result.AsSpan());
            var i = 0;
            foreach (var sourceByte in SourceBytes)
            {
                Unsafe.Add(ref resultStart, i++) = Unsafe.Add(ref tableStart, sourceByte >> 0b0100);
                Unsafe.Add(ref resultStart, i++) = Unsafe.Add(ref tableStart, sourceByte & 0b1111);
            }

            return result;
        }

        [Benchmark]
        public string LookupShift2()
        {
            ReadOnlySpan<char> table = "0123456789abcdef";
            ref var tableStart = ref MemoryMarshal.GetReference(table);
            var result = new string(default, SourceBytes.Length * 2);
            ref var resultStart = ref MemoryMarshal.GetReference(result.AsSpan());
            var i = -1;
            foreach (var sourceByte in SourceBytes)
            {
                Unsafe.Add(ref resultStart, ++i) = Unsafe.Add(ref tableStart, sourceByte >> 0b0100);
                Unsafe.Add(ref resultStart, ++i) = Unsafe.Add(ref tableStart, sourceByte & 0b1111);
            }

            return result;
        }

        [Benchmark]
        public unsafe string LookupShiftUnsafe()
        {
            const string Table = "0123456789abcdef";
            var result = new string(default, SourceBytes.Length * 2);
            fixed (char* resultPointer = result, tablePointer = Table)
            {
                var pointer = resultPointer;
                foreach (var sourceByte in SourceBytes)
                {
                    *pointer++ = tablePointer[sourceByte >> 0b0100];
                    *pointer++ = tablePointer[sourceByte & 0b1111];
                }
            }

            return result;
        }

        [Benchmark]
        public string Manipulation1()
        {
            var result = new string(default, SourceBytes.Length * 2);
            ref var resultStart = ref MemoryMarshal.GetReference(result.AsSpan());
            var i = 0;
            foreach (var sourceByte in SourceBytes)
            {
                var b = (byte)(sourceByte >> 0b0100);
                Unsafe.Add(ref resultStart, i++) = (char)(b > 9 ? 'a' - 10 + b : b + '0');
                b = (byte)(sourceByte & 0b1111);
                Unsafe.Add(ref resultStart, i++) = (char)(b > 9 ? 'a' - 10 + b : b + '0');
            }

            return result;
        }

        [Benchmark]
        public unsafe string Manipulation1Unsafe()
        {
            var result = new string(default, SourceBytes.Length * 2);
            fixed (char* resultPointer = result)
            {
                var pointer = resultPointer;
                foreach (var sourceByte in SourceBytes)
                {
                    var b = (byte)(sourceByte >> 0b0100);
                    *pointer++ = (char)(b > 9 ? 'a' - 10 + b : b + '0');
                    b = (byte)(sourceByte & 0b1111);
                    *pointer++ = (char)(b > 9 ? 'a' - 10 + b : b + '0');
                }
            }

            return result;
        }

        [Benchmark]
        public string Manipulation2()
        {
            var result = new string(default, SourceBytes.Length * 2);
            ref var resultStart = ref MemoryMarshal.GetReference(result.AsSpan());
            var i = 0;
            foreach (var sourceByte in SourceBytes)
            {
                var b = sourceByte >> 0b0100;
                Unsafe.Add(ref resultStart, i++) =
                    (char)('a' - 10 + b + (((b - 10) >> ((sizeof(int) * 8) - 1)) & -('a' - 10 - '0')));
                b = sourceByte & 0b1111;
                Unsafe.Add(ref resultStart, i++) =
                    (char)('a' - 10 + b + (((b - 10) >> ((sizeof(int) * 8) - 1)) & -('a' - 10 - '0')));
            }

            return result;
        }

        [Benchmark]
        public unsafe string Manipulation2Unsafe()
        {
            var result = new string(default, SourceBytes.Length * 2);
            fixed (char* resultPointer = result)
            {
                var pointer = resultPointer;
                foreach (var sourceByte in SourceBytes)
                {
                    var b = sourceByte >> 0b0100;
                    *pointer++ = (char)('a' - 10 + b + (((b - 10) >> ((sizeof(int) * 8) - 1)) & -('a' - 10 - '0')));
                    b = sourceByte & 0b1111;
                    *pointer++ = (char)('a' - 10 + b + (((b - 10) >> ((sizeof(int) * 8) - 1)) & -('a' - 10 - '0')));
                }
            }

            return result;
        }

        [Benchmark]
        public string Lookup() => ByteArrayHelperLookup.ToHexString(SourceBytes);

        [Benchmark]
        public string LookupUnsafe() => ByteArrayHelperLookup.ToHexStringUnsafe(SourceBytes);
    }
}