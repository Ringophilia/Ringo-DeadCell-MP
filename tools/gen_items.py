"""Lists every equippable item from the game's data.cdb, for tools\\items.ps1.

Usage: python -I tools/gen_items.py <res.pak> <out.txt>
Each line is `id<TAB>category`.
"""
import json
import struct
import sys

CATEGORIES = {0: "trap", 1: "grenade", 2: "sidekick", 3: "power", 4: "melee", 5: "ranged", 6: "shield"}
STAMP_END = 76


def read_cdb(path):
    with open(path, "rb") as f:
        head = f.read(8)
        header_size = struct.unpack("<i", head[4:8])[0]
        f.seek(0)
        hdr = f.read(header_size + 16)
        files = {}

        def node(pos, prefix):
            n = hdr[pos]
            name = hdr[pos + 1:pos + 1 + n].decode("utf-8", "replace")
            pos += 1 + n
            flags = hdr[pos]
            pos += 1
            full = prefix + "/" + name if prefix else name
            if flags & 1:
                count = struct.unpack_from("<i", hdr, pos)[0]
                pos += 4
                for _ in range(count):
                    pos = node(pos, full)
                return pos
            offset, size, _ = struct.unpack_from("<iii", hdr, pos)
            files[full] = (offset, size)
            return pos + 12

        node(STAMP_END, "")
        offset, size = files["data.cdb"]
        f.seek(header_size + offset)
        raw = f.read(size)
    return json.loads(raw[:raw.rfind(b"}") + 1])


def main():
    pak, out = sys.argv[1], sys.argv[2]
    sheets = {s["name"]: s for s in read_cdb(pak)["sheets"]}
    rows = [(l["id"], CATEGORIES[l.get("group")]) for l in sheets["item"]["lines"] if l.get("group") in CATEGORIES]
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        for item, category in rows:
            f.write(f"{item}\t{category}\n")
    print("wrote", len(rows), "items to", out)


if __name__ == "__main__":
    main()
