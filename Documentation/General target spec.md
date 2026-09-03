# Target development conventions

This document is intended to clarify application binary interface (API) when generating target representations.
This document is also meant to future-proof CommonIR development and usage so as to not cause confusion when swapping targets.

# Data
## Null
The famous "billion-dollar mistake".

We believe the foremost important issue to address is the choice behind completely banning `null`.
It should be up to the user to decide wether data is valid or not, and recieve an empty struct (or such) instead of a null pointer.

The IR should guarantee that data has *some* value, wether that is 0 or not.

This is solved by pre-allocating a section of memory to data without a value. The size of this section should be the size of the biggest compile-time data declared (such as a struct).
Since arrays gets the benefit of a fat pointer, a "Out of bounds" panic would occur if the user tried accessing data outside of its range.

Say you create a struct in the language of your choice which utilizes CommonIR with the given pseudocode;

```rust
struct Person {
    str Name,
    i32 Age,
    str Email
}

fn main {
    Person bruce = Person {
        Name: "Bruce",
        Age: 43
    }

    print(bruce.Email) // Result: ""
}
```

Even though the email property was not assigned, a pointer with size 0 was still created at ``Person.Email``.

## Pointers
All targets using linear memory should use fat pointers for aggregates, meaning pointers should store both the pointer to data aswell as the size of given data.

Depending on the architecture, x86, for example, would an 8-byte value tuple (or UInt64), whilst x64 would produce a 16-byte value tuple (or UInt128).

Below is a visual example of a fat pointer on a 32-bit system:

<table>
  <tr>
    <th colspan="2">Fat pointer (8 bytes)</th>
  </tr>
  <tr>
    <td>Pointer (4 bytes)</td>
    <td>Length (4 bytes)</td>
  </tr>
  <tr>
    <td>0x00000000</td>
    <td>0x00000000</td>
  </tr>
</table>

## Strings
Strings are always referenced by a fat pointer, meaning when strings are stored in memory, a 4-byte length indicator is postfixed to the pointer itself.

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

### Why not use null-terminated strings?
* Getting the length of a string spends O(1) time instead of O(n).
* Substrings are O(1) time instead of O(n).
* Strings referenced by fat pointers can contain null characters.
* Diverged buffer-overflow risk.