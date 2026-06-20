using bagolly.BitField;
using CommunityToolkit.HighPerformance;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;


namespace bagolly.BitFieldDemo;


class Program
{
    static void Main()
    {
        
    }
    
    public static void Test()
    {
        BitField<uint> a = new(123);
        BitField<ulong> b = new(456);

        if(a.Value < b.Value)
            Console.WriteLine("a");

        else
            Console.WriteLine($"{a:B8}");
    }



}
