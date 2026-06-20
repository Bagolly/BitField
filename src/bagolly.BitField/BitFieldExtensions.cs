using CommunityToolkit.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;


namespace bagolly.BitField;


/// <summary> Provides additional operations that are commonly performed over a <see cref="BitField{T}"/>. </summary>
public static class BitFieldExtensions
{
    /// <summary> Interleaves the bits of this bitfield with the bits of <paramref name="b"/>. </summary>
    /// <returns>A new bitfield consisting of 16 bits laid out as [<i>a0 b0 a1 b1 ... a7 b7</i>].</returns>
    public static BitField<ushort> InterleaveWith(this BitField<byte> a, BitField<byte> b)
    {
        if (Bmi2.IsSupported)
        {
            uint value = Bmi2.ParallelBitDeposit(a.Value, 0xAAAA) | Bmi2.ParallelBitDeposit(b.Value, 0x5555);
            return new BitField<ushort>((ushort)value);
        }

        else
            return Interleave16Fallback(a, b);
    }


    /// <summary> Interleaves the bits of this bitfield with the bits of <paramref name="b"/>. </summary>
    /// <returns>A new bitfield consisting of 32 bits laid out as [<i>a0 b0 a1 b1 ... a15 b15</i>].</returns>
    public static BitField<uint> InterleaveWith(this BitField<ushort> a, BitField<ushort> b)
    {
        if (Bmi2.IsSupported)
        {
            uint value = Bmi2.ParallelBitDeposit(a.Value, 0xAAAAAAAA) | Bmi2.ParallelBitDeposit(b.Value, 0x55555555);
            return new BitField<uint>(value);
        }

        else
            return Interleave32Fallback(a, b);
    }


    /// <summary> Interleaves the bits of this bitfield with the bits of <paramref name="b"/>. </summary>
    /// <returns>A new bitfield consisting of 64 bits laid out as [<i>a0 b0 a1 b1 ... a31 b31</i>].</returns>
    public static BitField<ulong> InterleaveWith(this BitField<uint> a, BitField<uint> b)
    {
        if (Bmi2.X64.IsSupported)
        {
            ulong value = Bmi2.X64.ParallelBitDeposit(a.Value, 0xAAAAAAAAAAAAAAAA) | Bmi2.X64.ParallelBitDeposit(b.Value, 0x5555555555555555);
            return new BitField<ulong>(value);
        }

        else
            return Interleave64Fallback(a, b);
    }


    /// <summary> Interleaves the bits of this bitfield with the bits of <paramref name="b"/>. </summary>
    /// <returns>A new bitfield consisting of 128 bits laid out as [<i>a0 b0 a1 b1 ... a63 b63</i>].</returns>
    public static BitField<UInt128> InterleaveWith(this BitField<ulong> a, BitField<ulong> b)
    {
        // PCLMULQDQ is another good candidate, especially below Zen3 where PDEP is slow.
        if (Bmi2.X64.IsSupported)
        {
            var upperL = Bmi2.X64.ParallelBitExtract(a.Value, 0xFFFFFFFF00000000);
            var upperR = Bmi2.X64.ParallelBitExtract(b.Value, 0xFFFFFFFF00000000);
            var lowerL = a.Value & 0xFFFFFFFF;
            var lowerR = b.Value & 0xFFFFFFFF;
            var upper = Bmi2.X64.ParallelBitDeposit(upperL, 0xAAAAAAAAAAAAAAAA) | Bmi2.X64.ParallelBitDeposit(upperR, 0x5555555555555555);
            var lower = Bmi2.X64.ParallelBitDeposit(lowerL, 0xAAAAAAAAAAAAAAAA) | Bmi2.X64.ParallelBitDeposit(lowerR, 0x5555555555555555);

            return new(new(upper, lower));
        }

        else
            return Interleave128Fallback(a, b);
    }


    /// <summary>
    /// Swaps <paramref name="length"/> bits starting at <paramref name="i"/> with the ones starting at <paramref name="j"/>.
    /// </summary>
    /// <remarks> The result of swapping overlapping sections is undefined. </remarks>
    /// <param name="value"></param>
    /// <param name="i">The zero-based index of the first section.</param>
    /// <param name="j">The zero-based index of the second section.</param>
    /// <param name="length">The length of the sections.</param>
    public static BitField<T> SwapRange<T>(this BitField<T> field, int i, int j, int length)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // Based on https://graphics.stanford.edu/~seander/bithacks.html#SwappingBitsXOR
        uint capacity = (uint)BitField<T>.Capacity;

        if ((uint)i > capacity || (uint)j > capacity || (uint)length > capacity)
            ThrowHelper.ThrowArgumentException($"Values must be nonnegative and less then than {capacity}.");

        if (i + length > capacity || j + length > capacity)
            ThrowHelper.ThrowArgumentOutOfRangeException("Subsections must be within the bounds of the bitfield.");

        T bits = field.Value;
        T tmp = ((bits >>> i) ^ (bits >>> j)) & ((T.One << length) - T.One);
        return new(bits ^ ((tmp << i) | (tmp << j)));
    }


    /// <summary>
    /// Conditionally blend the bits in this field with <paramref name="other"/>, based on <paramref name="control"/>.
    /// </summary>
    /// <param name="other">The bitfield to blend this instance with.</param>
    /// <param name="control">The bitfield to use as a mask.</param>
    /// <returns> A new <see cref="BitField{T}"/>, where
    /// <list type="bullet">
    /// <item>
    /// for every set bit in <paramref name="control"/>, the value is taken from <paramref name="other"/>,
    /// </item>
    /// <item>
    /// for every unset bit in <paramref name="control"/>, the value is taken from <see langword="this"/>.
    /// </item>
    /// </list>
    /// </returns>
    public static BitField<T> BlendWith<T>(in this BitField<T> field, in BitField<T> other, in BitField<T> control)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // Based on https://graphics.stanford.edu/~seander/bithacks.html#MaskedMerge
        // Although the described version isn't really an improvement anymore with BM1 andnot, (4 instructions for both),
        // it's still a useful operation.
        return new(field.Value ^ (field.Value ^ other.Value) & control.Value);
    }


    #region Fallbacks  
    // The fallbacks use methods described in https://graphics.stanford.edu/~seander/bithacks.html.

    internal static BitField<ushort> Interleave16Fallback(BitField<byte> a, BitField<byte> b)
    {
        ulong l = ((b.Value * 0x0101010101010101UL & 0x8040201008040201UL) * 0x0102040810204081UL >> 49) & 0x5555;
        ulong r = ((a.Value * 0x0101010101010101UL & 0x8040201008040201UL) * 0x0102040810204081UL >> 48) & 0xAAAA;
        return new((ushort)(l | r));
    }


    internal static BitField<uint> Interleave32Fallback(BitField<ushort> a, BitField<ushort> b)
    {
        uint x = b.Value;
        uint y = a.Value;

        x = (x | (x << 8)) & 0x00FF00FF;
        x = (x | (x << 4)) & 0x0F0F0F0F;
        x = (x | (x << 2)) & 0x33333333;
        x = (x | (x << 1)) & 0x55555555;

        y = (y | (y << 8)) & 0x00FF00FF;
        y = (y | (y << 4)) & 0x0F0F0F0F;
        y = (y | (y << 2)) & 0x33333333;
        y = (y | (y << 1)) & 0x55555555;

        return new(x | (y << 1));
    }


    internal static BitField<ulong> Interleave64Fallback(BitField<uint> a, BitField<uint> b)
    {
        ulong x = b.Value;
        ulong y = a.Value;

        x = (x | (x << 16)) & 0x0000FFFF0000FFFF;
        x = (x | (x << 08)) & 0x00FF00FF00FF00FF;
        x = (x | (x << 04)) & 0x0F0F0F0F0F0F0F0F;
        x = (x | (x << 02)) & 0x3333333333333333;
        x = (x | (x << 01)) & 0x5555555555555555;

        y = (y | (y << 16)) & 0x0000FFFF0000FFFF;
        y = (y | (y << 08)) & 0x00FF00FF00FF00FF;
        y = (y | (y << 04)) & 0x0F0F0F0F0F0F0F0F;
        y = (y | (y << 02)) & 0x3333333333333333;
        y = (y | (y << 01)) & 0x5555555555555555;

        return new(x | (y << 1));
    }


    internal static BitField<UInt128> Interleave128Fallback(BitField<ulong> a, BitField<ulong> b)
    {
        var m0 = new UInt128(0x00000000FFFFFFFF, 0x00000000FFFFFFFF);
        var m1 = new UInt128(0x0000FFFF0000FFFF, 0x0000FFFF0000FFFF);
        var m2 = new UInt128(0x00FF00FF00FF00FF, 0x00FF00FF00FF00FF);
        var m3 = new UInt128(0x0F0F0F0F0F0F0F0F, 0x0F0F0F0F0F0F0F0F);
        var m4 = new UInt128(0x3333333333333333, 0x3333333333333333);
        var m5 = new UInt128(0x5555555555555555, 0x5555555555555555);

        UInt128 x = b.Value;
        UInt128 y = a.Value;

        x = (x | (x << 32)) & m0;
        y = (y | (y << 32)) & m0;
        x = (x | (x << 16)) & m1;
        y = (y | (y << 16)) & m1;
        x = (x | (x << 08)) & m2;
        y = (y | (y << 08)) & m2;
        x = (x | (x << 04)) & m3;
        y = (y | (y << 04)) & m3;
        x = (x | (x << 02)) & m4;
        y = (y | (y << 02)) & m4;
        x = (x | (x << 01)) & m5;
        y = (y | (y << 01)) & m5;

        return new(x | (y << 1));
    }
    #endregion
}