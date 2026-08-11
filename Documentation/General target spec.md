# Target development conventions

This document is intended to clarify application binary interface (API) when generating target representations.
This document is also meant to future-proof CommonIR development and usage so as to not cause confusion when swapping targets.

# Data
## Null
The famous "billion-dollar mistake".

I believe the foremost important issue to address is the choice behind completely banning `null`.
It should be up to the user to decide wether data is valid or not, and recieve an empty struct (or such) instead of a null pointer.

The IR should guarantee that data has *some* value, wether that is 0 or not.

This is solved by pre-allocating a section of memory to data without a value. The size of this section should be the size of the biggest compile-time data declared (such as a struct).
Since arrays gets the benefit of a fat pointer, a "Out of bounds" panic would occur if the user tried accessing data outside of its range.

## Pointers
All targets using linear memory should use fat pointers, meaning pointers should store both the pointer to data aswell as the size of given data.

Depending on the architecture, x86, for example, would produce 8-byte fat pointers (as UInt64), whilst x64 would produce a 16-byte fat pointer (as UInt128).

Below is a visual example of a fat pointer on a 32-bit system:
| Pointer (4 bytes) | Length (4 bytes) |
| ----------------- | ---------------- |
| 0x00000000        | 0x00000000       |

## Strings
Strings are always referenced by a fat pointer, meaning when strings are stored in memory, a 4-byte length is prefixed to the pointer itself.

Strings should always be coded with UTF-8.

Here is a visual example of how strings should be stored in memory:

| String data (5 bytes)            |
| -------------------------------- |
| { 0x48, 0x65, 0x6c, 0x6c, 0x6f } |

And the pointer to it:
```
  Pointer  Length
  vvvvvvvv vvvvvvvv
0x00000000_00000005
```

> Why not use null-terminated strings?
* Getting the length of a string takes O(1) time instead of O(n).
* Substrings are O(1) time instead of O(n).
* Strings referenced by fat pointers can contain null characters.
* Diverged buffer-overflow risk.