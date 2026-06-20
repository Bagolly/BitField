using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Helpers;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;


namespace bagolly.BitField;

#pragma warning disable IDE0290 // Use primary constructor

/// <summary> Represents a bitfield backed by a fixed-size, unsigned integer <typeparamref name="T"/>. </summary>
public struct BitField<T> :
    IEquatable<BitField<T>>,
    IShiftOperators<BitField<T>, int, BitField<T>>,
    IBitwiseOperators<BitField<T>, BitField<T>, BitField<T>>,
    IEqualityOperators<BitField<T>, BitField<T>, bool>,
    ISpanFormattable,
    IUtf8SpanFormattable

    where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
{
    private static readonly int TWidth = int.CreateChecked(T.Log2(T.MaxValue)) + 1;
    private T bits;


    /// <summary> Returns the value of the bitfield. </summary>
    public readonly T Value => bits;


    /// <summary> Returns the capacity of the bitfield. </summary>
    public static int Capacity => TWidth;


    /// <summary> Creates a new instance using the provided value. </summary>
    public BitField(T initialData) => bits = initialData;


    /// <summary> Gets or sets the bit at <paramref name="index"/>. </summary>
    /// <exception cref="ArgumentOutOfRangeException"/>
    [IndexerName("Bits")]
    public bool this[int index]
    {
        readonly get
        {
            if ((uint)index >= (uint)TWidth)
                ThrowHelper.ThrowArgumentOutOfRangeException();

            return (bits & (T.One << index)) != T.Zero;
        }

        set
        {
            if ((uint)index >= (uint)TWidth)
                ThrowHelper.ThrowArgumentOutOfRangeException();

            if (typeof(T) == typeof(uint))
                bits = T.CreateChecked(BitHelper.SetFlag(Unsafe.As<T, uint>(ref bits), index, value));

            else if (typeof(T) == typeof(ulong))
                bits = T.CreateChecked(BitHelper.SetFlag(Unsafe.As<T, ulong>(ref bits), index, value));

            else
            {
                T mask = T.One << index;
                bits = value ? bits | mask : bits & ~mask;
            }
        }
    }


    /// <summary> Gets or sets the bit at <paramref name="index"/>. </summary>
    [IndexerName("Bits")]
    public bool this[Index index]
    {
        readonly get => this[index.GetOffset(TWidth)];
        set => this[index.GetOffset(TWidth)] = value;
    }


    /// <summary> Gets or sets a range of bits given by <paramref name="range"/>. </summary>
    /// <exception cref="ArgumentOutOfRangeException"/>
    [IndexerName("Bits")]
    public T this[Range range]
    {
        readonly get
        {
            var (offset, length) = range.GetOffsetAndLength(TWidth);

            if (length == TWidth)
                return bits;

            if (typeof(T) == typeof(uint) || (typeof(T) == typeof(nuint) && nuint.Size is 4))
            {
                var result = BitHelper.ExtractRange(uint.CreateChecked(Value), (byte)offset, (byte)length);
                return T.CreateChecked(result);
            }

            else if (typeof(T) == typeof(ulong) || (typeof(T) == typeof(nuint) && nuint.Size is 8))
            {
                var result = BitHelper.ExtractRange(ulong.CreateChecked(Value), (byte)offset, (byte)length);
                return T.CreateChecked(result);
            }

            else
            {
                T value = bits >>> offset;
                T mask = (T.One << length) - T.One;
                return value & mask;
            }
        }


        set
        {
            var (offset, length) = range.GetOffsetAndLength(TWidth);

            if (length == TWidth)
                bits = value;

            else if (typeof(T) == typeof(uint) || (typeof(T) == typeof(nuint) && nuint.Size is 4))
                BitHelper.SetRange(ref Unsafe.As<T, uint>(ref bits), (byte)offset, (byte)length, uint.CreateChecked(value));

            else if (typeof(T) == typeof(ulong) || (typeof(T) == typeof(nuint) && nuint.Size is 8))
                BitHelper.SetRange(ref Unsafe.As<T, ulong>(ref bits), (byte)offset, (byte)length, ulong.CreateChecked(value));

            else
            {
                T mask = (T.One << length) - T.One;
                T read = mask << offset;
                T write = (value & mask) << offset;

                bits = (~read & bits) | write;
            }
        }
    }


    /// <summary> Counts the number of bits set in the bitfield. </summary>
    public readonly T Count() => T.PopCount(bits);


    /// <summary> Returns a binary bitstring representing the values in this bitfield. </summary>
    public override readonly string ToString() => string.Create(TWidth, this, static (span, field) =>
    {
        for (int i = 0; i < TWidth; i++)
            span[TWidth - 1 - i] = field[i] ? '1' : '0';
    });


    /// <inheritdoc/>
    /// <remarks> 
    /// <para> The argument for <paramref name="formatProvider"/> is unused. </para>
    /// <para> The expected format is a character 'B', 'X', or 'O' specifying the radix,
    /// followed by a number between 0 and <see cref="BitField{T}.Capacity"/> specifying the size of the digit groups.</para>
    /// </remarks>
    /// <exception cref="FormatException"/>
    public readonly string ToString(string? format, IFormatProvider? formatProvider = null)
    {
        if (!string.IsNullOrEmpty(format))
        {
            var state = ParseFormatString(format);
            return string.Create(state.ExactSize, state, FormatCore);
        }

        return ToString();
    }


    /// <inheritdoc/>
    /// <remarks> The arguments for <paramref name="format"/> and <paramref name="provider"/> are unused. </remarks>
    public readonly bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider = null)
    {
        // Since we can guarantee that we only use the ASCII subset of UTF-8, we just reuse FormatCore via generics.
        bytesWritten = 0;

        // We always write the full bitfield, so this is a minimum requirement.
        if (utf8Destination.Length < TWidth)
            return false;

        if (format.Length >= 1)
        {
            var state = ParseFormatString(format);

            if (utf8Destination.Length < state.ExactSize)
                return false;

            FormatCore(utf8Destination, state);
            bytesWritten = state.ExactSize;
            return true;
        }

        // Default behavior is binary with no grouping.
        else
        {
            for (int i = 0; i < TWidth; i++)
                utf8Destination[TWidth - 1 - i] = this[i] ? (byte)'1' : (byte)'0';

            bytesWritten = TWidth;
            return true;
        }
    }


    /// <inheritdoc/>
    /// <remarks> 
    /// <para> The argument for <paramref name="provider"/> is unused. </para>
    /// <para> The expected format is a character 'B', 'X', or 'O' specifying the radix,
    /// followed by a number between 0 and <see cref="BitField{T}.Capacity"/> specifying the size of the digit groups.</para>
    /// </remarks>
    /// <exception cref="FormatException"/>
    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider = null)
    {
        charsWritten = 0;

        // We always write the full bitfield, so this is a minimum requirement.
        if (destination.Length < TWidth)
            return false;

        // Write formatted.
        if (format.Length >= 1)
        {
            var state = ParseFormatString(format);

            if (destination.Length < state.ExactSize)
                return false;

            FormatCore(destination, state);
            charsWritten = state.ExactSize;
            return true;
        }

        // Default behavior is binary with no grouping.
        else
        {
            for (int i = 0; i < TWidth; i++)
                destination[TWidth - 1 - i] = this[i] ? '1' : '0';

            charsWritten = TWidth;
            return true;
        }
    }


    /// <summary> Returns an enumerator for this instance. </summary>
    public readonly BitEnumerator GetEnumerator() => new(this);


    public override readonly int GetHashCode() => bits.GetHashCode();
    public override readonly bool Equals(object? obj) => obj is BitField<T> other && Equals(other);
    public readonly bool Equals(BitField<T> other) => bits.Equals(other.bits);

    public static bool operator ==(BitField<T> left, BitField<T> right) => left.Equals(right);
    public static bool operator !=(BitField<T> left, BitField<T> right) => !(left == right);

    public static BitField<T> operator |(BitField<T> left, BitField<T> right) => new(left.bits | right.bits);
    public static BitField<T> operator &(BitField<T> left, BitField<T> right) => new(left.bits & right.bits);
    public static BitField<T> operator ^(BitField<T> left, BitField<T> right) => new(left.bits ^ right.bits);
    public static BitField<T> operator ~(BitField<T> left) => new(~left.bits);

    public static BitField<T> operator <<(BitField<T> left, int n) => new(left.bits << n);
    public static BitField<T> operator >>(BitField<T> left, int n) => new(left.bits >> n);
    public static BitField<T> operator >>>(BitField<T> left, int n) => new(left.bits >>> n);

    public static explicit operator T(BitField<T> left) => left.Value;

    public static explicit operator BitField<T>(T value) => new(value);


    private readonly ToStringState ParseFormatString(ReadOnlySpan<char> format)
    {
        Debug.Assert(!format.IsEmpty, "Bug: format null or too short.");

        byte radix = (format[0] | 0x20) switch
        {
            'b' => 1,
            'x' => 4,
            'o' => 3,
            _ => ThrowHelper.ThrowFormatException<byte>($"Invalid radix specifier '{format[0]}'.")
        };

        int groupWidth;

        if (format.Length == 1)
            groupWidth = TWidth;

        else if (!int.TryParse(format[1..], CultureInfo.InvariantCulture, out groupWidth) || (uint)groupWidth > (uint)TWidth)
            ThrowHelper.ThrowFormatException("Invalid grouping number.");

        int bitsPerGroup = radix * groupWidth;
        var (groupCnt, lastGroupBits) = Math.DivRem(TWidth, bitsPerGroup);
        int outputSize = groupCnt * groupWidth + groupCnt - 1;

        if (lastGroupBits != 0)
            outputSize += 1 + ((lastGroupBits + (radix - 1)) / radix);

        // At this point the radix is already validated to be an ASCII letter, so we only need to test for lower case.
        bool useLowerCase = (format[0] & 0x20) != 0;

        return new(bits, outputSize, bitsPerGroup, groupCnt, groupWidth, radix, lastGroupBits != 0, useLowerCase);
    }


    private static void FormatCore<U>(Span<U> dst, ToStringState state) where U : struct, IBinaryInteger<U>, IUnsignedNumber<U>, IMinMaxValue<U>
    {
        var lookup = state.UseLowerCase ? "0123456789abcdef"u8 : "0123456789ABCDEF"u8;
        var data = state.Data;
        var groupMask = state.BitsPerGroup >= TWidth ? T.AllBitsSet : (T.One << state.BitsPerGroup) - T.One; // Avoid undefined shifts.
        var digitMask = (T.One << state.Radix) - T.One;
        int writeIdx = state.ExactSize - 1;

        // Write the main groups in big endian order.
        for (int i = 0; i < state.NumberOfGroups; i++, data >>>= state.BitsPerGroup)
        {
            // Mask out the next group of digits to write.
            T group = data & groupMask;

            // Write the digits. The output is in BE order, but it's more efficient to extract the values in LE order.
            // The call to byte::CreateChecked is safe because 0 < radix < 5  =>  0 < ((1 << radix) - 1) < 16.
            for (int j = 0; j < state.DigitsPerGroup; j++, group >>>= state.Radix, --writeIdx)
                dst[writeIdx] = U.CreateChecked(lookup[byte.CreateChecked(group & digitMask)]);

            // Don't write a trailing separator if this is the last group.
            if (writeIdx < 0)
                break;

            dst[writeIdx--] = U.CreateChecked(' ');
        }

        // Write the remainder group, if any.
        if (state.HasRemainderGroup)
        {
            T lastGroup = data & groupMask;

            for (; writeIdx >= 0; lastGroup >>>= state.Radix, --writeIdx)
                dst[writeIdx] = U.CreateChecked(lookup[byte.CreateChecked(lastGroup & digitMask)]);
        }
    }


    private readonly struct ToStringState
    {
        public readonly T Data;
        public readonly int ExactSize;
        public readonly int BitsPerGroup;
        public readonly int NumberOfGroups;
        public readonly int DigitsPerGroup;
        public readonly bool HasRemainderGroup;
        public readonly byte Radix;
        public readonly bool UseLowerCase;

        public ToStringState(T d, int oSize, int gBits, int gCnt, int g, byte r, bool hasRem, bool isLower)
        {
            Data = d;
            ExactSize = oSize;
            BitsPerGroup = gBits;
            NumberOfGroups = gCnt;
            DigitsPerGroup = g;
            Radix = r;
            HasRemainderGroup = hasRem;
            UseLowerCase = isLower;
        }
    }


    /// <summary> Enumerates the bits of a bitfield in big endian order. </summary>
    public ref struct BitEnumerator
    {
        private readonly BitField<T> value;
        private int position;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitEnumerator(BitField<T> field)
        {
            value = field;
            position = TWidth;
        }

        public readonly bool Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value[position];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            position -= 1;
            return position > -1;
        }
    }
}
