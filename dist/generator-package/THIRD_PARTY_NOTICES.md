# PokéFinder RNG integration

`plugin/UltimateEditor/PokeFinderRng.cs` adapts the IV-to-PID calculation in
[Admiral-Fish/PokeFinder](https://github.com/Admiral-Fish/PokeFinder), pinned to
commit `3624cb7737da247f22d3afa5265a0698f8be8fc1`.

Copyright (C) 2017–2024 Admiral_Fish, bumba, and EzPzStreamz.
Licensed under GNU GPL version 3 or any later version. The complete license is
included as `LICENSE-PokeFinder.txt`. The pinned upstream source is downloaded
under `vendor/pokefinder/`, with its original copyright notices and test data.
It can also be obtained from
https://github.com/Admiral-Fish/PokeFinder/archive/3624cb7737da247f22d3afa5265a0698f8be8fc1.zip.

The port preserves Method 1, reverse Method 1, Method 2, Method 4, XD/Colosseum,
and the two Cute Charm calculations. IV state recovery uses bounded enumeration
of the unknown low 16 bits and bit 31 instead of the upstream lattice shortcut.
Channel bonus-disc calculations are outside the current encounter catalog and
are not ported. Output is checked against upstream IV-to-PID test vectors for
the included methods. `SuggestedSID` is diagnostic data from upstream's
calculation; generation never replaces the source trainer's actual SID with it.

The original PokeFinder GUI executable is not launched or automated. This is an
in-process source integration inside the isolated generation worker. Generation
for other scenarios uses PKHeX's native encounter converter. A timeout is not a
proof that the specified combination is mathematically impossible.

# PKHeX

The plugin and worker link to the existing pinned PKHeX.Core source, commit
`bfe89c7` / release 26.10.18, under `vendor/pkhex/kwsch-PKHeX-bfe89c7`.
Its license and corresponding source are in that directory and available from
https://github.com/kwsch/PKHeX. The worker package includes PKHeX.Core.dll and
the upstream license. The editor's C# source and project files are in `plugin/`.
