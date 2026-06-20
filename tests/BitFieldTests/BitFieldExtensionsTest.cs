using bagolly.BitField;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using static bagolly.BitField.BitFieldExtensions;

namespace BitFieldTests;


[TestFixture]
[TestOf(nameof(BitFieldExtensionsTest))]
internal class BitFieldExtensionsTest
{
    private static IEnumerable<TestCaseData<UInt128>> TestCase_U128_BlendWith =>
    [
        new(new(0, 0x3), new(0xF000000000000000, 0), new(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAA)) { ExpectedResult = new UInt128(0xA000000000000000, 0x1) }
    ];


    [TestCase([(byte)0x3, (byte)0xF0, (byte)0xAA], TypeArgs = [typeof(byte)], ExpectedResult = (byte)0xA1)]
    [TestCase([(byte)0x9B, (byte)0x74, (byte)0xF0], TypeArgs = [typeof(byte)], ExpectedResult = (byte)0x7B)]
    [TestCase([(ushort)0x3, (ushort)0xF000, (ushort)0xAAAA], TypeArgs = [typeof(ushort)], ExpectedResult = (ushort)0xA001)]
    [TestCase([0x3u, 0xF0000000u, 0xAAAAAAAAu], TypeArgs = [typeof(uint)], ExpectedResult = 0xA0000001u)]
    [TestCase([0x3ul, 0xF000000000000000ul, 0xAAAAAAAAAAAAAAAAul], TypeArgs = [typeof(ulong)], ExpectedResult = 0xA000000000000001ul)]
    [TestCaseSource(nameof(TestCase_U128_BlendWith))]
    public static T BlendWith_FixedData_ReturnsExpected<T>(T a, T b, T c) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        return ((BitField<T>)a).BlendWith((BitField<T>)b, (BitField<T>)c).Value;
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void SwapRange_SwapUpperHalfWithZero_EqualsLowerHalf<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        int halfWidth = BitField<T>.Capacity / 2;

        var lower = new BitField<T>((T.One << halfWidth) - T.One);
        var upper = lower << halfWidth;

        Assert.That(upper.SwapRange(0, halfWidth, halfWidth), Is.EqualTo(lower));
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void SwapRange_BadRange_Throws<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> stub = new(default);
        int max = BitField<T>.Capacity;
        int bad1 = max + 1;
        int bad2 = -1;

        // Test offset i
        Assert.Throws<ArgumentException>(() => new BitField<T>().SwapRange(bad1, default, default));
        Assert.Throws<ArgumentException>(() => new BitField<T>().SwapRange(bad2, default, default));

        // Test offset j
        Assert.Throws<ArgumentException>(() => new BitField<T>().SwapRange(default, bad1, default));
        Assert.Throws<ArgumentException>(() => new BitField<T>().SwapRange(default, bad2, default));

        // Test length
        Assert.Throws<ArgumentException>(() => new BitField<T>().SwapRange(default, default, bad1));
        Assert.Throws<ArgumentException>(() => new BitField<T>().SwapRange(default, default, bad2));

        // Test offset + length
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitField<T>().SwapRange(max - 1, 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitField<T>().SwapRange(0, max - 1, 2));
    }


    [Test]
    public static void Interleave_8To16_ProducesExpectedResult()
    {
        BitField<byte> none = new(0);
        BitField<byte> all = new(0xFF);
        BitField<byte> altEven = new(0x55);
        BitField<byte> altOdd = ~altEven;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(none.InterleaveWith(all).Value, Is.EqualTo(0x5555));
            Assert.That(all.InterleaveWith(none).Value, Is.EqualTo(0xAAAA));
            Assert.That(altOdd.InterleaveWith(altEven).Value, Is.EqualTo(0x9999));
            Assert.That(altEven.InterleaveWith(altOdd).Value, Is.EqualTo(0x6666));
        }
       
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Interleave16Fallback(none, all).Value, Is.EqualTo(0x5555));
            Assert.That(Interleave16Fallback(all, none).Value, Is.EqualTo(0xAAAA));
            Assert.That(Interleave16Fallback(altOdd, altEven).Value, Is.EqualTo(0x9999));
            Assert.That(Interleave16Fallback(altEven, altOdd).Value, Is.EqualTo(0x6666));
        }
    }


    [Test]
    public static void Interleave_16To32_ProducesExpectedResult()
    {
        BitField<ushort> none = new(0);
        BitField<ushort> all = new(0xFFFF);
        BitField<ushort> altEven = new(0x5555);
        BitField<ushort> altOdd = ~altEven;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(none.InterleaveWith(all).Value, Is.EqualTo(0x5555_5555u));
            Assert.That(all.InterleaveWith(none).Value, Is.EqualTo(0xAAAA_AAAAu));
            Assert.That(altOdd.InterleaveWith(altEven).Value, Is.EqualTo(0x9999_9999u));
            Assert.That(altEven.InterleaveWith(altOdd).Value, Is.EqualTo(0x6666_6666u));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Interleave32Fallback(none, all).Value, Is.EqualTo(0x5555_5555u));
            Assert.That(Interleave32Fallback(all, none).Value, Is.EqualTo(0xAAAA_AAAAu));
            Assert.That(Interleave32Fallback(altOdd, altEven).Value, Is.EqualTo(0x9999_9999u));
            Assert.That(Interleave32Fallback(altEven, altOdd).Value, Is.EqualTo(0x6666_6666u));
        }
    }


    [Test]
    public static void Interleave_32To64_ProducesExpectedResult()
    {
        BitField<uint> none = new(0);
        BitField<uint> all = new(0xFFFF_FFFF);
        BitField<uint> altEven = new(0x5555_5555);
        BitField<uint> altOdd = ~altEven;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(none.InterleaveWith(all).Value, Is.EqualTo(0x5555_5555_5555_5555ul));
            Assert.That(all.InterleaveWith(none).Value, Is.EqualTo(0xAAAA_AAAA_AAAA_AAAAul));
            Assert.That(altOdd.InterleaveWith(altEven).Value, Is.EqualTo(0x9999_9999_9999_9999ul));
            Assert.That(altEven.InterleaveWith(altOdd).Value, Is.EqualTo(0x6666_6666_6666_6666ul));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Interleave64Fallback(none, all).Value, Is.EqualTo(0x5555_5555_5555_5555ul));
            Assert.That(Interleave64Fallback(all, none).Value, Is.EqualTo(0xAAAA_AAAA_AAAA_AAAAul));
            Assert.That(Interleave64Fallback(altOdd, altEven).Value, Is.EqualTo(0x9999_9999_9999_9999ul));
            Assert.That(Interleave64Fallback(altEven, altOdd).Value, Is.EqualTo(0x6666_6666_6666_6666ul));
        }
    }


    [Test]
    public static void Interleave_64To128_ProducesExpectedResult()
    {
        BitField<ulong> none = new(0);
        BitField<ulong> all = new(0xFFFF_FFFF_FFFF_FFFFul);
        BitField<ulong> altEven = new(0x5555_5555_5555_5555ul);
        BitField<ulong> altOdd = ~altEven;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(none.InterleaveWith(all).Value, Is.EqualTo(new UInt128(0x5555555555555555, 0x5555555555555555)));
            Assert.That(all.InterleaveWith(none).Value, Is.EqualTo(new UInt128(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAA)));
            Assert.That(altOdd.InterleaveWith(altEven).Value, Is.EqualTo(new UInt128(0x9999_9999_9999_9999ul, 0x9999_9999_9999_9999ul)));
            Assert.That(altEven.InterleaveWith(altOdd).Value, Is.EqualTo(new UInt128(0x6666_6666_6666_6666ul, 0x6666_6666_6666_6666ul)));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Interleave128Fallback(none, all).Value, Is.EqualTo(new UInt128(0x5555555555555555, 0x5555555555555555)));
            Assert.That(Interleave128Fallback(all, none).Value, Is.EqualTo(new UInt128(0xAAAAAAAAAAAAAAAA, 0xAAAAAAAAAAAAAAAA)));
            Assert.That(Interleave128Fallback(altOdd, altEven).Value, Is.EqualTo(new UInt128(0x9999_9999_9999_9999ul, 0x9999_9999_9999_9999ul)));
            Assert.That(Interleave128Fallback(altEven, altOdd).Value, Is.EqualTo(new UInt128(0x6666_6666_6666_6666ul, 0x6666_6666_6666_6666ul)));
        }
    }
}
