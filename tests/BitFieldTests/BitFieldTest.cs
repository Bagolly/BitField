using bagolly.BitField;
using CommunityToolkit.HighPerformance;
using Newtonsoft.Json.Linq;
using NUnit.Framework.Constraints;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace BitFieldTests;


// Making the test fixture itself generic makes it not discover any tests...
[TestFixture]
[TestOf(nameof(BitField<>))]
internal class BitFieldTests
{
    private static IEnumerable<TestCaseData<UInt128>> TestCase_U128_MaxValue => [new(UInt128.MaxValue)];

    private static IEnumerable<TestCaseData<UInt128>> TestCase_U128_MaxValueOfU64 => [new(ulong.MaxValue)];

    private static IEnumerable<TestCaseData<UInt128>> TestCase_U128_Alternating => [new TestCaseData<UInt128>(new UInt128(0x5555555555555555, 0x5555555555555555))];

    private static IEnumerable<TestCaseData<UInt128>> TestCase_U128_Roundtrip => [new(new UInt128(0x8000000000000001, 1))];

    private static IEnumerable<TestCaseData<nuint>> TestCase_NUint_Roundtrip =>
    [
        nuint.Size is 8 ? new TestCaseData<nuint>(unchecked((nuint)0x8000000100000001)) : new TestCaseData<nuint>(0x80010001)
    ];


    [TestCase([byte.MaxValue], TypeArgs = [typeof(byte)])]
    [TestCase([ushort.MaxValue], TypeArgs = [typeof(ushort)])]
    [TestCase([uint.MaxValue], TypeArgs = [typeof(uint)])]
    [TestCase([ulong.MaxValue], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_U128_MaxValue))]
    public static void Ctor_MaxValue_SetsMaxValue<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // NUnit refuses to run tests with char arguments in the surrogate range, char.MaxValue, and a bunch of C0 controls (seriously who is testing NUnit itself?).
        // Most likely a serialization issue, as they are using XML (of all things) as an intermediate protocol between their layers.
        // My workaround is to pass ushort.MaxValue for the argument, and use char separately as a type argument. This probably stops the serializer from trying
        // to write the char as \uFFFF and will write 65535 instead (they really could've done this themselves, but oh well). 
        Assert.That(new BitField<T>(T.AllBitsSet).Value, Is.EqualTo(value));
    }


    [TestCase([(byte)0x91], TypeArgs = [typeof(byte)])]
    [TestCase([(ushort)0x8101], TypeArgs = [typeof(ushort)])]
    [TestCase([0x80010001], TypeArgs = [typeof(uint)])]
    [TestCase([0x8000000100000001], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_NUint_Roundtrip))]
    [TestCaseSource(nameof(TestCase_U128_Roundtrip))]
    public static void GetSetInt_Roundtrip_Passes<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // The value has the bits at the 3 specified positions set high, all others are low.
        BitField<T> field = new(value);

        int lsb = 0;
        int mid = BitField<T>.Capacity / 2;
        int msb = BitField<T>.Capacity - 1;

        // 1. Check reads.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(field[lsb], Is.True);
            Assert.That(field[mid], Is.True);
            Assert.That(field[msb], Is.True);
        }

        // 2. Check unsets.
        field[lsb] = false;
        field[mid] = false;
        field[msb] = false;

        Assert.That(field.Value, Is.EqualTo(T.Zero));

        // 3. Check writes.
        field[lsb] = true;
        field[mid] = true;
        field[msb] = true;

        Assert.That(field.Value, Is.EqualTo(value));
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(nuint)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void GetSetInt_BadArg_Throws<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {   
        // Get
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BitField<T>(default)[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BitField<T>(default)[BitField<T>.Capacity]);

        // Set
        var stub = new BitField<T>(default);
        Assert.Throws<ArgumentOutOfRangeException>(() => stub[-1] = default);
        Assert.Throws<ArgumentOutOfRangeException>(() => stub[BitField<T>.Capacity] = default);
    }


    [TestCase([(byte)0x91], TypeArgs = [typeof(byte)])]
    [TestCase([(ushort)0x8101], TypeArgs = [typeof(ushort)])]
    [TestCase([0x80010001], TypeArgs = [typeof(uint)])]
    [TestCase([0x8000000100000001], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_NUint_Roundtrip))]
    [TestCaseSource(nameof(TestCase_U128_Roundtrip))]
    public static void GetSetIndex_Roundtrip_Passes<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // The value has the bits at the 3 specified positions set high, all others are low.
        BitField<T> field = new(value);
        int mid = BitField<T>.Capacity / 2;

        // 1. Check reads.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(field[^1], Is.EqualTo(field[BitField<T>.Capacity - 1]));
            Assert.That(field[^mid], Is.EqualTo(field[BitField<T>.Capacity - mid]));
            Assert.That(field[^BitField<T>.Capacity], Is.EqualTo(field[BitField<T>.Capacity - BitField<T>.Capacity]));
        }

        // 2. Check unsets.
        field[^1] = false;
        field[^mid] = false;
        field[^BitField<T>.Capacity] = false;

        Assert.That(field.Value, Is.EqualTo(T.Zero));

        // 3. Check writes.
        field[^1] = true;
        field[^mid] = true;
        field[^BitField<T>.Capacity] = true;

        Assert.That(field.Value, Is.EqualTo(value));
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void GetSetIndex_BadArg_Throws<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {   
        // Get
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BitField<T>(default)[(Index)(-1)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new BitField<T>(default)[Index.End]);

        // Set
        var stub = new BitField<T>(default);
        Assert.Throws<ArgumentOutOfRangeException>(() => stub[(Index)(-1)] = default);
        Assert.Throws<ArgumentOutOfRangeException>(() => stub[(Index)BitField<T>.Capacity] = default);
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(nuint)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void GetSetRange_Roundtrip_Passes<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // We do a similar Read->Unset->Write->Read roundtrip like for the others.
        int fst = 0;
        int mid = BitField<T>.Capacity / 2;
        int last = BitField<T>.Capacity;

        var lower = (T.One << mid) - T.One;
        var upper = lower << mid;

        var field1 = new BitField<T>(lower);
        var field2 = new BitField<T>(upper);

        // Check exctractions. We test against lower because all extracted ranges are aligned to the LSB.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(field1[fst..mid], Is.EqualTo(lower));
            Assert.That(field1[mid..last], Is.EqualTo(T.Zero));

            Assert.That(field2[mid..last], Is.EqualTo(lower));
            Assert.That(field2[fst..mid], Is.EqualTo(T.Zero));
        }

        field1[fst..mid] = T.Zero;
        field2[mid..last] = T.Zero;

        // Check unsets.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(field1.Value, Is.EqualTo(T.Zero));
            Assert.That(field2.Value, Is.EqualTo(T.Zero));
        }

        field1[fst..mid] = lower;
        field2[mid..last] = lower;

        // Check writes.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(field1.Value, Is.EqualTo(lower));
            Assert.That(field2.Value, Is.EqualTo(upper));
        }
    }


    [TestCase([(byte)0x91], TypeArgs = [typeof(byte)])]
    [TestCase([(ushort)0x8101], TypeArgs = [typeof(ushort)])]
    [TestCase([0x80010001], TypeArgs = [typeof(uint)])]
    [TestCase([0x8000000100000001], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_U128_Roundtrip))]
    public static void GetSetRange_EdgeCase_Passes<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> all1 = new(T.Zero);
        BitField<T> all2 = new(T.Zero);

        BitField<T> none1 = new(value);
        BitField<T> none2 = new(value);
        BitField<T> none3 = new(value);

        // 1. Write the full range of the type instead of ctor.
        all1[0..] = T.AllBitsSet;
        all2[..BitField<T>.Capacity] = T.AllBitsSet;
        _ = all1[0..];
        _ = all2[..BitField<T>.Capacity];

        // 2. Empty range, no change.
        none1[0..0] = T.AllBitsSet;
        none2[(BitField<T>.Capacity - 1)..(BitField<T>.Capacity - 1)] = T.AllBitsSet;
        none3[6..6] = T.AllBitsSet;

        // 1. Write the full range of the type instead of ctor.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(all1.Value, Is.EqualTo(T.AllBitsSet));
            Assert.That(all2.Value, Is.EqualTo(T.AllBitsSet));
            Assert.That(none1.Value, Is.EqualTo(value));
            Assert.That(none2.Value, Is.EqualTo(value));
            Assert.That(none3.Value, Is.EqualTo(value));
        }

        // Check unsets.
        all1[0..] = T.Zero;
        all2[..BitField<T>.Capacity] = T.Zero;

        none1[0..0] = T.Zero;
        none2[(BitField<T>.Capacity - 1)..(BitField<T>.Capacity - 1)] = T.Zero;
        none3[6..6] = T.Zero;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all1.Value, Is.EqualTo(T.Zero));
            Assert.That(all2.Value, Is.EqualTo(T.Zero));
            Assert.That(none1.Value, Is.EqualTo(value));
            Assert.That(none2.Value, Is.EqualTo(value));
            Assert.That(none3.Value, Is.EqualTo(value));
        }
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void GetSetRange_BadRange_Throws<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> stub = new(); // The value doesn't matter, just testing for a throw.

        Range[] badRanges =
        [
            +0..(BitField<T>.Capacity + 1), // out of bounds
            ^1..+0,                   // descending 1
            ^1..^5,                   // descending 2
            +5..+2,                   // descending 3
        ];

        // Get
        foreach (var range in badRanges)
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = stub[range]);

        //Set
        foreach (var range in badRanges)
            Assert.Throws<ArgumentOutOfRangeException>(() => stub[range] = default);
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void Count_ValidData_Passes<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> empty = new();
        BitField<T> full = new(T.AllBitsSet);
        BitField<T> six = new((T.One << 6) - T.One);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(empty.Count(), Is.EqualTo(T.Zero));
            Assert.That(full.Count(), Is.EqualTo(T.CreateChecked(BitField<T>.Capacity)));
            Assert.That(six.Count(), Is.EqualTo(T.CreateChecked(6)));
        }
    }


    [TestCase([4], TypeArgs = [typeof(byte)])]
    [TestCase([12], TypeArgs = [typeof(ushort)])]
    [TestCase([22], TypeArgs = [typeof(uint)])]
    [TestCase([45], TypeArgs = [typeof(ulong)])]
    [TestCase([121], TypeArgs = [typeof(UInt128)])]
    public static void ToString_FixedData_Passes<T>(int customLength) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // customLength is just an arbitrary valid index to test at least one non-edge case as well.   
        BitField<T> empty = new();
        BitField<T> full = new(T.AllBitsSet);
        BitField<T> rand = new((T.One << customLength) - T.One);

        int expected = BitField<T>.Capacity;
        string strEmpty = empty.ToString();
        string strFull = full.ToString();
        string strRand = rand.ToString();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strEmpty, Has.Length.EqualTo(expected));
            Assert.That(strFull, Has.Length.EqualTo(expected));
            Assert.That(strRand, Has.Length.EqualTo(expected));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(strEmpty, Is.EqualTo(new string('0', expected)));
            Assert.That(strFull, Is.EqualTo(new string('1', expected)));
            Assert.That(strRand, Is.EqualTo(string.Concat(new string('0', expected - customLength), new string('1', customLength))));
        }
    }


    [TestCase([0, 1 << 08], TypeArgs = [typeof(byte)])]
    [TestCase([0, 1 << 16], TypeArgs = [typeof(ushort)])]
    [TestCase([0, 1 << 32], TypeArgs = [typeof(uint)])]
    [TestCase([0, long.MaxValue], TypeArgs = [typeof(ulong)])]
    [TestCase([0, long.MaxValue], TypeArgs = [typeof(UInt128)])]
    public static void ToString_RandomData_EqualsBCLResult<T>(long iMin, long eMax) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        const int seed = 0xBAD_FACE;
        var random = new Random(seed);
        var field = new BitField<T>(T.CreateChecked(random.NextInt64(iMin, eMax)));

        Assert.That(field.Value.ToString($"B{BitField<T>.Capacity}", CultureInfo.InvariantCulture), Is.EqualTo(field.ToString()));
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void Operators_FixedData_InvariantsHold<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // Not much to test here, the operators just defer to the underlying type's operator. GetHashCode and Equals is also tested here,
        // but only to make sure they behave as expected.
        BitField<T> all = new(T.AllBitsSet);
        BitField<T> none = new(T.Zero);
        BitField<T> one = new(T.One);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all | none, Is.EqualTo(all));
            Assert.That(all & none, Is.EqualTo(none));
            Assert.That(all ^ all, Is.EqualTo(none));
            Assert.That(~none, Is.EqualTo(all));
            Assert.That((one << 7).Value, Is.EqualTo(T.One << 7));
            Assert.That((all >> 7).Count(), Is.EqualTo(T.CreateChecked(BitField<T>.Capacity - 7)));
            Assert.That(all >>> 3, Is.EqualTo(all >> 3));
            Assert.That(all == none, Is.EqualTo(all.Equals(none)));
            Assert.That(all != none, Is.EqualTo(!(all == none)));
            Assert.That(all.Value, Is.EqualTo((T)all));
            Assert.That((BitField<T>)all.Value, Is.EqualTo(all));
            Assert.That(all == none, Is.EqualTo(all.Equals(none)));
            Assert.That(all.GetHashCode(), Is.EqualTo(all.Value.GetHashCode()));
            Assert.That(all.Equals(obj: null), Is.False);
            Assert.That(all.Equals((object)all), Is.True);
        }
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void ToStringFormat_EdgeCaseGroupings_MatchesBCLResult<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        Span<BitField<T>> testCases = [new(T.AllBitsSet), new(T.Zero)];

        // There are no UInt128/Int128 overloads in Convert.
        if (typeof(T) == typeof(UInt128))
            testCases[0] = new BitField<T>(T.CreateChecked(ulong.MaxValue));

        foreach (var testCase in testCases)
        {
            using (Assert.EnterMultipleScope())
            {
                var strBinMax = testCase.ToString($"B{BitField<T>.Capacity}");
                var strOctMax = testCase.ToString($"O{BitField<T>.Capacity}");
                var strHexMax = testCase.ToString($"X{BitField<T>.Capacity}");

                var valBinMax = Convert.ToUInt64(RemoveWhiteSpace(strBinMax), 02);
                var valOctMax = Convert.ToUInt64(RemoveWhiteSpace(strOctMax), 08);
                var valHexMax = Convert.ToUInt64(RemoveWhiteSpace(strHexMax), 16);

                Assert.That(T.CreateChecked(valBinMax), Is.EqualTo(testCase.Value));
                Assert.That(T.CreateChecked(valOctMax), Is.EqualTo(testCase.Value));
                Assert.That(T.CreateChecked(valHexMax), Is.EqualTo(testCase.Value));

                var strBinMin = testCase.ToString("B1");
                var strOctMin = testCase.ToString("O1");
                var strHexMin = testCase.ToString("X1");

                var valBinMin = Convert.ToUInt64(RemoveWhiteSpace(strBinMin), 02);
                var valOctMin = Convert.ToUInt64(RemoveWhiteSpace(strOctMin), 08);
                var valHexMin = Convert.ToUInt64(RemoveWhiteSpace(strHexMin), 16);

                Assert.That(T.CreateChecked(valBinMin), Is.EqualTo(testCase.Value));
                Assert.That(T.CreateChecked(valOctMin), Is.EqualTo(testCase.Value));
                Assert.That(T.CreateChecked(valHexMin), Is.EqualTo(testCase.Value));
            }
        }

        static string RemoveWhiteSpace(string str) => str
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(new StringBuilder(), (sb, str) => sb.Append(str))
            .ToString();
    }


    [TestCase([byte.MaxValue], TypeArgs = [typeof(byte)])]
    [TestCase([ushort.MaxValue], TypeArgs = [typeof(ushort)])]
    [TestCase([uint.MaxValue], TypeArgs = [typeof(uint)])]
    [TestCase([ulong.MaxValue], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_U128_MaxValueOfU64))]
    public static void ToStringFormat_ValidRadix_MatchesBCLResult<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // There is no octal specifier in .NET, so we test it by roundtripping with Convert::ToUint64.
        BitField<T> field = new(value);

        // Upper case must output upper case hex digits.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.ToString("B"), Is.EqualTo(field.Value.ToString($"B{BitField<T>.Capacity}", CultureInfo.InvariantCulture)));
            Assert.That(field.ToString("X"), Is.EqualTo(field.Value.ToString($"X{BitField<T>.Capacity / 4}", CultureInfo.InvariantCulture)));

            ulong octal = Convert.ToUInt64(field.ToString("O"), 8);
            Assert.That(octal, Is.EqualTo(ulong.CreateChecked(field.Value)));
        }

        // Lower case must output lower case hex digits.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.ToString("b"), Is.EqualTo(field.Value.ToString($"b{BitField<T>.Capacity}", CultureInfo.InvariantCulture)));
            Assert.That(field.ToString("x"), Is.EqualTo(field.Value.ToString($"x{BitField<T>.Capacity / 4}", CultureInfo.InvariantCulture)));

            ulong octal = Convert.ToUInt64(field.ToString("o"), 8);
            Assert.That(octal, Is.EqualTo(ulong.CreateChecked(field.Value)));
        }
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void ToStringFormat_BadRadix_Throws<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> field = default;

        using (Assert.EnterMultipleScope())
        { 
            // No radix specified.
            Assert.Throws<FormatException>(() => field.ToString("012"));
            // Grouping number has trailing characters.
            Assert.Throws<FormatException>(() => field.ToString("B13k"));
            // Grouping number contains characters.
            Assert.Throws<FormatException>(() => field.ToString("X1k3"));
            // Grouping number too large.
            Assert.Throws<FormatException>(() => field.ToString($"G{BitField<T>.Capacity + 1}"));
            // Grouping number negative.
            Assert.Throws<FormatException>(() => field.ToString("B-12"));
            // Invalid radix.
            Assert.Throws<FormatException>(() => field.ToString("D"));
            // Valid BCL specifier, but not for this type.
            Assert.Throws<FormatException>(() => field.ToString("G4"));
            // Not the same as string.Empty.
            Assert.Throws<FormatException>(() => field.ToString("\0"));
        }
    }


    [TestCase([byte.MaxValue], TypeArgs = [typeof(byte)])]
    [TestCase([ushort.MaxValue], TypeArgs = [typeof(ushort)])]
    [TestCase([uint.MaxValue], TypeArgs = [typeof(uint)])]
    [TestCase([ulong.MaxValue], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_U128_MaxValue))]
    public static void ToStringFormat_NoFormat_MatchesBCLResults<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // The expected default behavior is binary with no grouping. This goes for both the overloaded object::ToString() and also the one
        // from ISpanFormattable, when the format string is null or empty.
        // Both null(string) and string.Empty should work equivalently.
        BitField<T> field = new(value);

        using (Assert.EnterMultipleScope())
        {
            string expected = field.Value.ToString($"B{BitField<T>.Capacity}", CultureInfo.InvariantCulture);
            Assert.That(field.ToString(null), Is.EqualTo(expected));
            Assert.That(field.ToString(string.Empty), Is.EqualTo(expected));
            Assert.That(field.ToString(), Is.EqualTo(expected));
        }
    }


    [TestCase([(byte)0x55], TypeArgs = [typeof(byte)])]
    [TestCase([(ushort)0x5555], TypeArgs = [typeof(ushort)])]
    [TestCase([0x55555555u], TypeArgs = [typeof(uint)])]
    [TestCase([0x5555555555555555ul], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_U128_Alternating))]
    public static void ToStringFormat_BinaryNoRemGroup_Passes<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // Once again, NUNit dies if you dare to have type parameters along with concrete types, so we have to build the results programmatically.
        int width = BitField<T>.Capacity;
        List<int> groupings = [];

        int v = 1;
        do
        {
            groupings.Add(v);
            v <<= 1;
        } while (v <= width);

        string[] expectedResults = new string[groupings.Count];

        for (int i = 0; i < groupings.Count; i++)
        {
            int g = groupings[i];
            StringBuilder sb = new();

            foreach (var c in value.ToString($"B{width}", CultureInfo.InvariantCulture).Chunk(g))
                sb.Append(c).Append(' ');

            expectedResults[i] = sb.Remove(sb.Length - 1, 1).ToString();
        }

        for (int i = 0; i < groupings.Count; i++)
            Assert.That(new BitField<T>(value).ToString($"B{groupings[i]}"), Is.EqualTo(expectedResults[i]));
    }


    [TestCase([(byte)0x55], TypeArgs = [typeof(byte)])]
    [TestCase([(ushort)0x5555], TypeArgs = [typeof(ushort)])]
    [TestCase([0x55555555u], TypeArgs = [typeof(uint)])]
    [TestCase([0x5555555555555555ul], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_U128_Alternating))]
    public static void ToStringFormat_HexNoRemGroup_Passes<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        // Once again, NUNit dies if you dare to have type parameters along with concrete types, so we have to build the results programmatically.
        int width = BitField<T>.Capacity;
        List<int> groupings = [];

        int v = 1;
        do
        {
            groupings.Add(v);
            v <<= 1;
        } while (v <= width);

        string[] expectedResults = new string[groupings.Count];

        for (int i = 0; i < groupings.Count; i++)
        {
            int g = groupings[i];
            StringBuilder sb = new();

            foreach (var c in value.ToString($"X{width / 4}", CultureInfo.InvariantCulture).Chunk(g))
                sb.Append(c).Append(' ');

            expectedResults[i] = sb.Remove(sb.Length - 1, 1).ToString();
        }

        for (int i = 0; i < groupings.Count; i++)
            Assert.That(new BitField<T>(value).ToString($"X{groupings[i]}"), Is.EqualTo(expectedResults[i]));
    }


    [TestCase<int>(1, ExpectedResult = "1 2 5")]
    [TestCase<int>(2, ExpectedResult = "1 25")]
    [TestCase<int>(3, ExpectedResult = "125")]
    public static string ToStringFormatU8_Oct_Passes(int g) => new BitField<byte>(0x55).ToString($"O{g}");


    [TestCase<int>(1, ExpectedResult = "0 5 2 5 2 5")]
    [TestCase<int>(2, ExpectedResult = "05 25 25")]
    [TestCase<int>(3, ExpectedResult = "052 525")]
    [TestCase<int>(4, ExpectedResult = "05 2525")]
    [TestCase<int>(5, ExpectedResult = "0 52525")]
    [TestCase<int>(6, ExpectedResult = "052525")]
    public static string ToStringFormatU16_Oct_Passes(int g) => new BitField<ushort>(0x5555).ToString($"O{g}");


    [TestCase<int>(01, ExpectedResult = "0 0 0 2 5 2 5 2 5 2 5")]
    [TestCase<int>(07, ExpectedResult = "0002 5252525")]
    [TestCase<int>(08, ExpectedResult = "000 25252525")]
    [TestCase<int>(09, ExpectedResult = "00 025252525")]
    [TestCase<int>(10, ExpectedResult = "0 0025252525")]
    [TestCase<int>(11, ExpectedResult = "00025252525")]
    public static string ToStringFormatU32_Oct_Passes(int g) => new BitField<uint>(0x555555).ToString($"O{g}");

    [TestCase<int>(01, ExpectedResult = "0 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5")]
    [TestCase<int>(12, ExpectedResult = "0525252525 252525252525")]
    [TestCase<int>(13, ExpectedResult = "052525252 5252525252525")]
    [TestCase<int>(14, ExpectedResult = "05252525 25252525252525")]
    [TestCase<int>(15, ExpectedResult = "0525252 525252525252525")]
    [TestCase<int>(16, ExpectedResult = "052525 2525252525252525")]
    [TestCase<int>(17, ExpectedResult = "05252 52525252525252525")]
    [TestCase<int>(18, ExpectedResult = "0525 252525252525252525")]
    [TestCase<int>(19, ExpectedResult = "052 5252525252525252525")]
    [TestCase<int>(20, ExpectedResult = "05 25252525252525252525")]
    [TestCase<int>(21, ExpectedResult = "0 525252525252525252525")]
    public static string ToStringFormatU64_Oct_Passes(int g) => new BitField<ulong>(0x5555555555555555).ToString($"O{g}");


    [TestCase<int>(1, ExpectedResult = "1 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5 2 5")]
    [TestCase<int>(22, ExpectedResult = "125252525252525252525 2525252525252525252525")]
    [TestCase<int>(23, ExpectedResult = "12525252525252525252 52525252525252525252525")]
    [TestCase<int>(24, ExpectedResult = "1252525252525252525 252525252525252525252525")]
    [TestCase<int>(25, ExpectedResult = "125252525252525252 5252525252525252525252525")]
    [TestCase<int>(26, ExpectedResult = "12525252525252525 25252525252525252525252525")]
    [TestCase<int>(27, ExpectedResult = "1252525252525252 525252525252525252525252525")]
    [TestCase<int>(28, ExpectedResult = "125252525252525 2525252525252525252525252525")]
    [TestCase<int>(29, ExpectedResult = "12525252525252 52525252525252525252525252525")]
    [TestCase<int>(30, ExpectedResult = "1252525252525 252525252525252525252525252525")]
    [TestCase<int>(31, ExpectedResult = "125252525252 5252525252525252525252525252525")]
    [TestCase<int>(32, ExpectedResult = "12525252525 25252525252525252525252525252525")]
    [TestCase<int>(33, ExpectedResult = "1252525252 525252525252525252525252525252525")]
    [TestCase<int>(34, ExpectedResult = "125252525 2525252525252525252525252525252525")]
    [TestCase<int>(35, ExpectedResult = "12525252 52525252525252525252525252525252525")]
    [TestCase<int>(36, ExpectedResult = "1252525 252525252525252525252525252525252525")]
    [TestCase<int>(37, ExpectedResult = "125252 5252525252525252525252525252525252525")]
    [TestCase<int>(38, ExpectedResult = "12525 25252525252525252525252525252525252525")]
    [TestCase<int>(39, ExpectedResult = "1252 525252525252525252525252525252525252525")]
    [TestCase<int>(40, ExpectedResult = "125 2525252525252525252525252525252525252525")]
    [TestCase<int>(41, ExpectedResult = "12 52525252525252525252525252525252525252525")]
    [TestCase<int>(42, ExpectedResult = "1 252525252525252525252525252525252525252525")]
    public static string ToStringFormatU128_Oct_Passes(int g) => new BitField<UInt128>(new(0x5555555555555555, 0x5555555555555555)).ToString($"O{g}");



    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void TryFormatChar_BadBuffer_ReturnsFalse<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> field = new(default);

        using (Assert.EnterMultipleScope())
        {
            // Large enough initially, but not for the given format.
            Assert.That(field.TryFormat(stackalloc char[BitField<T>.Capacity], out _, "B1"), Is.False);         
            Assert.That(field.TryFormat(default(Span<char>), out _, default), Is.False);
            Assert.That(field.TryFormat(Span<char>.Empty, out _, default), Is.False);
            Assert.That(field.TryFormat(stackalloc char[7], out _, default), Is.False);
        }
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void TryFormatChar_ValidBuffer_ReturnsTrueAndCorrectWritten<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> field = new(default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.TryFormat(stackalloc char[BitField<T>.Capacity], out int written, default), Is.True);
            Assert.That(written, Is.EqualTo(BitField<T>.Capacity));
        }
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void TryFormatUtf8_BadBuffer_ReturnsFalse<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> field = new(default);

        using (Assert.EnterMultipleScope())
        {
            // Large enough initially, but not for the given format.
            Assert.That(field.TryFormat(stackalloc byte[BitField<T>.Capacity], out _, "B1"), Is.False);
            Assert.That(field.TryFormat(default(Span<byte>), out _, default), Is.False);
            Assert.That(field.TryFormat(Span<byte>.Empty, out _, default), Is.False);
            Assert.That(field.TryFormat(stackalloc byte[7], out _, default), Is.False);
        }
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void TryFormatUtf8_ValidBuffer_ReturnsTrueAndCorrectWritten<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> field = new(default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(field.TryFormat(stackalloc byte[BitField<T>.Capacity], out int written, default), Is.True);
            Assert.That(written, Is.EqualTo(BitField<T>.Capacity));
        }
    }


    [TestCase([byte.MaxValue], TypeArgs = [typeof(byte)])]
    [TestCase([ushort.MaxValue], TypeArgs = [typeof(ushort)])]
    [TestCase([uint.MaxValue], TypeArgs = [typeof(uint)])]
    [TestCase([ulong.MaxValue], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_U128_MaxValue))]
    [SuppressMessage("Assertion", "NUnit2010", Justification = "We are testing the values for equality, and the 'expected' result is 'True'. This is semantically different.")]
    public static void TryFormatBoth_SameData_ReturnEquivalentResults<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        ReadOnlySpan<string?> formats = ["B", "X", "O", "", null];
        Span<char> result1 = stackalloc char[BitField<T>.Capacity];
        Span<byte> result2 = stackalloc byte[BitField<T>.Capacity];
        foreach (var fmt in formats)
        {
            BitField<T> field = new(value);

            if (!field.TryFormat(result1, out int charsWritten, fmt))
                Assert.Fail("Could not all chars.");

            if (!field.TryFormat(result2, out int bytesWritten, fmt))
                Assert.Fail("Could not write all bytes.");

            Assert.That(charsWritten == bytesWritten, Is.True);

            string str1 = new(result1[..charsWritten]);
            string str2 = Encoding.UTF8.GetString(result2[..bytesWritten]);
            string str3 = field.ToString(fmt);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(str1 == str2, Is.True);
                Assert.That(str1 == str3, Is.True);
            }
        }
    }


    [TestCase(TypeArgs = [typeof(byte)])]
    [TestCase(TypeArgs = [typeof(ushort)])]
    [TestCase(TypeArgs = [typeof(uint)])]
    [TestCase(TypeArgs = [typeof(ulong)])]
    [TestCase(TypeArgs = [typeof(UInt128)])]
    public static void Enumerator_Empty_TestInvariants<T>() where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> field = new();

        int count = 0;
        bool state = false;

        foreach (var bit in field)
        {
            state |= bit;
            count += 1;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state, Is.False);
            Assert.That(count, Is.EqualTo(BitField<T>.Capacity));
        }
    }


    [TestCase([(byte)0x55], TypeArgs = [typeof(byte)])]
    [TestCase([(ushort)0x5555], TypeArgs = [typeof(ushort)])]
    [TestCase([0x55555555u], TypeArgs = [typeof(uint)])]
    [TestCase([0x5555555555555555ul], TypeArgs = [typeof(ulong)])]
    [TestCaseSource(nameof(TestCase_U128_Alternating))]
    public static void Enumerator_FixedData_Passes<T>(T value) where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        BitField<T> field = new(value);

        List<bool> results = new(BitField<T>.Capacity);

        foreach (var bit in field)
            results.Add(bit);

        using (Assert.EnterMultipleScope())
        {
            int trueCount = int.CreateChecked(field.Count());
            Assert.That(results.Count(bit => bit is true), Is.EqualTo(trueCount));
            Assert.That(results.Count(bit => bit is false), Is.EqualTo(BitField<T>.Capacity - trueCount));
        }
    }
}