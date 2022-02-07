# About
This code is based on decompiled code of "ABK_insert.exe" by id-daemon.
Some variables and labels are given names or where renamed, but here is the code mostly generated by decompiler.
Original decompiled code is in orig.cs

# Usage
Same as the original:
- ABK_insert [ABK file] [wav file] [sample № to replace]
- Use mono 16-bit files only.
- SX.exe must be in the same folder.

# What's new
**2.0**
- Insert new sound in the end of BNK istead of end of ABK.
- Size of inserted sound included in ABK total size.
- BNK total size changing.
- Added compatibility with NFS Underground (front-end SFX)

**2.1**
- Some fixes in ABK_insert (16 bytes align, looplenght fix, deleting xa.raw after using)
- Fixed SX crashes in cases when there was file with >19 characters name in same folder.

# Compatibility
With adding compatibility with NFS Underground, I think it should be compatible with any ABKs between Underground and Carbon (which is using BNKl fromat).
If you have found any compatibility issues, let me know.
TODO: S10A fromat support.

# Sound eXchange
This program is used in conjuction with Sound eXchange. 
Copyright (c) 2004 - 2022 Electronic Arts
