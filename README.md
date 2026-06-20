
![badge](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/Bagolly/d84251f0d4ab9946ba92c6f1ae25ef79/raw/bitfield-code-coverage.json)

# BitField

Contains a stateless, generic bitfield type `BitField<T>`. Can take advantage of hardware support through intrinsics, if available.

Currently, the following types are tested:
 - `byte`
 - `char`
 - `ushort`
 - `uint`
 - `ulong`
 - `nuint`
 - `UInt128`

Although any type implementing the required interfaces can be used. The bitfield itself will always equal the type's size, hence "stateless".

### Reading and writing
There are three ways to read and write bits:
 1. With `int`, where the first position is the LSB.
 2. With `Index`, from both ends.
 3. With `Range`, accessing a slice of type `T`.

Note that `Index` and `Range` are implemented to work like other BCL types, so the last element is `^1` and not `^0`, and so on.


### Printing and formatting

Formatting is implemented through `ISpanFormattable` and `IUtf8SpanFormattable`; this includes support for interpolated strings.

#### Formatting
The format string, if provided, must have the form `<radix><grouping-number>`. If no format string is specified, the bits are printed in binary with no grouping.

The `<radix>` specifier is a single character:
 - `B` or `b`: binary
 - `O` or `o`: octal
 - `X` or `x`: hexadecimal. The case also determines the case of the hexadecimal letters in the output.

The `<grouping-number>` specifies the number of digits to print before a whitespace. It must be a positive value that is less, than the type's capacity.
The group containing the MSB may have less digits, if the number of digits cannot evenly fit into the specified number of groups.


Formatting examples:
| Format | Type     | Number         | Result                |
| ------ | -------- |--------------- | --------------------- |
| x2     | `uint`   | 0xDEADBEEF     | `de ad be ef`         |
| O3     | `byte`   | 7              | `007`                 |
| B4     | `ushort` | 0xBEEF         | `1011 1110 1110 1111` |
| X4     | `ulong`  | 0xAAAAAAAAAAAA | `0000 AAAA AAAA AAAA` |
| b3     | `byte`   | 0xAA           | `10 101 010`          |

### Other common operations
 - The number of bits the bitfield can hold can be accessed statically as `BitField<T>.Capacity`.
 - The actual value in the bitfield can be read through `Value`.
 - The number of set bits in the bitfield can be computed through `Count()`. This will use `POPCNT` if the underlying type supports it.
 - Enumeration using `foreach` is supported.
 - All bitwise operators are implemented and work as expected (including the corresponding generic interfaces).
 - Equality (including `IEquatable`) is supported through the underlying type.
 - Explicit casts are supported to and from the underlying type.


### Additional bitwise operations defined in `BitFieldExtensions`
 
This class contains additional operations defined as extension methods. This is an overview, see the corresponding XML documentation for more details.

- `BlendWith<T>(T a, T b, T c)`: conditionally blend `a` and `b` using `c` as a mask. Similar to variable blends in AVX.
- `SwapRange<T>(T a, int i, int j, int length)`: swap two non-overlapping substrings of the same length within a bitfield.
- `Interleave`: interleave the bits of two bitfields __*__.

__*__ `Interleave` is implemented non-generically. This is because interleaving two values of width _n_ requires a type of width _2n_ bits.
This is not always possible (like interleaving two `UInt128` values), and is not expressible as a type constraint either.

The following overloads are implemented for `Interleave`:
 - `byte`, `byte` -> `ushort`
 - `ushort`, `ushort` -> `uint`
 - `uint`, `uint` -> `ulong`
 - `ulong`, `ulong` -> `UInt128`
