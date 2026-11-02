"""Write the bytecode embedded in deadcells.exe to a file, optionally editing a string.

Usage: python -I tools/extract_boot.py <deadcells.exe> <out.hlb> [OLD=NEW ...]

OLD and NEW must have the same UTF-8 length: this edits the string table in place.
It is a smoke test for the loader, not the patcher.
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from hldump import R


def string_table(b, start):
    """Returns (blob_offset, sizes) by walking the header up to the string table."""
    r = R(b, start + 3)
    version = r.u8()
    r.idx()
    nints, nfloats, nstrings = r.idx(), r.idx(), r.idx()
    if version >= 5:
        r.idx()
    for _ in range(4):
        r.idx()
    if version >= 4:
        r.idx()
    r.idx()
    r.p += 4 * nints + 8 * nfloats
    size = r.i32()
    blob = r.p
    r.p += size
    return blob, [r.idx() for _ in range(nstrings)]


def main():
    exe, out, *edits = sys.argv[1:]
    b = open(exe, "rb").read()
    start = b.find(b"HLB\x04")
    # The resource directory gives the size, but the bytecode is the last thing in
    # .rsrc and hl_code_read stops by itself, so the section end is a safe bound.
    pe = struct.unpack_from("<I", b, 0x3C)[0]
    nsec = struct.unpack_from("<H", b, pe + 6)[0]
    optsz = struct.unpack_from("<H", b, pe + 20)[0]
    end = None
    for i in range(nsec):
        s = pe + 24 + optsz + i * 40
        if b[s:s + 5] == b".rsrc":
            raw_size, raw_ptr = struct.unpack_from("<II", b, s + 16)
            end = raw_ptr + raw_size
    data = bytearray(b[start:end])
    blob, sizes = string_table(bytes(data), 0)
    for edit in edits:
        old, new = (x.encode() for x in edit.split("=", 1))
        assert len(old) == len(new), "OLD and NEW must have the same byte length"
        off, hits = blob, 0
        for sz in sizes:
            if bytes(data[off:off + sz]) == old:
                data[off:off + sz] = new
                hits += 1
            off += sz + 1
        print("edit", edit, "hits", hits)
        assert hits, "string not found"
    open(out, "wb").write(data)
    print("wrote", out, len(data))


if __name__ == "__main__":
    main()
