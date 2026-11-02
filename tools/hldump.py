"""Parse the HashLink bytecode embedded in deadcells.exe and dump it to text.

Usage: python -I tools/hldump.py <deadcells.exe> <out_dir>

Output:
  classes.txt   every class: super, fields, methods (name -> findex)
  natives.txt   every native function (lib, name, signature)
  functions.txt one line per function: findex, class.method, signature, file:line
  strings.txt   the string table
  files.txt     source file names from the debug info
  code.pickle   the full parsed module, opcodes included, for ad-hoc queries
"""
import json
import os
import pickle
import struct
import sys

KINDS = ["void", "u8", "u16", "i32", "i64", "f32", "f64", "bool", "bytes", "dyn", "fun", "obj", "array",
         "type", "ref", "virtual", "dynobj", "abstract", "enum", "null", "method", "struct", "packed", "guid"]
HFUN, HOBJ, HREF, HVIRTUAL, HABSTRACT, HENUM, HNULL, HMETHOD, HSTRUCT, HPACKED = 10, 11, 14, 15, 17, 18, 19, 20, 21, 22

OPS = [("Mov", 2), ("Int", 2), ("Float", 2), ("Bool", 2), ("Bytes", 2), ("String", 2), ("Null", 1),
       ("Add", 3), ("Sub", 3), ("Mul", 3), ("SDiv", 3), ("UDiv", 3), ("SMod", 3), ("UMod", 3),
       ("Shl", 3), ("SShr", 3), ("UShr", 3), ("And", 3), ("Or", 3), ("Xor", 3),
       ("Neg", 2), ("Not", 2), ("Incr", 1), ("Decr", 1),
       ("Call0", 2), ("Call1", 3), ("Call2", 4), ("Call3", 5), ("Call4", 6),
       ("CallN", -1), ("CallMethod", -1), ("CallThis", -1), ("CallClosure", -1),
       ("StaticClosure", 2), ("InstanceClosure", 3), ("VirtualClosure", 3),
       ("GetGlobal", 2), ("SetGlobal", 2), ("Field", 3), ("SetField", 3), ("GetThis", 2), ("SetThis", 2),
       ("DynGet", 3), ("DynSet", 3),
       ("JTrue", 2), ("JFalse", 2), ("JNull", 2), ("JNotNull", 2), ("JSLt", 3), ("JSGte", 3), ("JSGt", 3),
       ("JSLte", 3), ("JULt", 3), ("JUGte", 3), ("JNotLt", 3), ("JNotGte", 3), ("JEq", 3), ("JNotEq", 3),
       ("JAlways", 1),
       ("ToDyn", 2), ("ToSFloat", 2), ("ToUFloat", 2), ("ToInt", 2), ("SafeCast", 2), ("UnsafeCast", 2),
       ("ToVirtual", 2),
       ("Label", 0), ("Ret", 1), ("Throw", 1), ("Rethrow", 1), ("Switch", -1), ("NullCheck", 1), ("Trap", 2),
       ("EndTrap", 1),
       ("GetI8", 3), ("GetI16", 3), ("GetMem", 3), ("GetArray", 3), ("SetI8", 3), ("SetI16", 3), ("SetMem", 3),
       ("SetArray", 3),
       ("New", 1), ("ArraySize", 2), ("Type", 2), ("GetType", 2), ("GetTID", 2), ("Ref", 2), ("Unref", 2),
       ("Setref", 2), ("MakeEnum", -1), ("EnumAlloc", 2), ("EnumIndex", 2), ("EnumField", 4), ("SetEnumField", 3),
       ("Assert", 0), ("RefData", 2), ("RefOffset", 3), ("Nop", 0), ("Prefetch", 3), ("Asm", 3)]


class R:
    def __init__(self, b, p):
        self.b, self.p = b, p

    def u8(self):
        v = self.b[self.p]
        self.p += 1
        return v

    def i32(self):
        v = struct.unpack_from("<i", self.b, self.p)[0]
        self.p += 4
        return v

    def idx(self):
        b = self.u8()
        if not b & 0x80:
            return b & 0x7F
        if not b & 0x40:
            v = self.u8() | ((b & 31) << 8)
        else:
            c, d, e = self.u8(), self.u8(), self.u8()
            v = ((b & 31) << 24) | (c << 16) | (d << 8) | e
        return -v if b & 0x20 else v

    def strings(self, n):
        size = self.i32()
        start = self.p
        self.p += size
        out, off = [], start
        for _ in range(n):
            sz = self.idx()
            out.append(self.b[off:off + sz].decode("utf-8", "replace"))
            off += sz + 1
        return out


class Code:
    pass


def parse(b, start, with_ops=True):
    r = R(b, start)
    assert b[start:start + 3] == b"HLB"
    r.p += 3
    c = Code()
    c.version = r.u8()
    flags = r.idx()
    c.hasdebug = flags & 1
    nints, nfloats, nstrings = r.idx(), r.idx(), r.idx()
    nbytes = r.idx() if c.version >= 5 else 0
    ntypes, nglobals, nnatives, nfunctions = r.idx(), r.idx(), r.idx(), r.idx()
    nconstants = r.idx() if c.version >= 4 else 0
    c.entry = r.idx()
    print("version", c.version, "ints", nints, "floats", nfloats, "strings", nstrings, "types", ntypes, "globals",
          nglobals, "natives", nnatives, "functions", nfunctions, "constants", nconstants, "entry", c.entry)
    c.ints = [r.i32() for _ in range(nints)]
    c.floats = list(struct.unpack_from("<%dd" % nfloats, b, r.p))
    r.p += 8 * nfloats
    c.strings = r.strings(nstrings)
    if c.version >= 5:
        size = r.i32()
        r.p += size
        for _ in range(nbytes):
            r.idx()
    c.files = []
    if c.hasdebug:
        n = r.idx()
        c.files = r.strings(n)
    c.types = []
    for i in range(ntypes):
        k = r.u8()
        t = {"k": k}
        if k in (HFUN, HMETHOD):
            n = r.u8()
            t["args"] = [r.idx() for _ in range(n)]
            t["ret"] = r.idx()
        elif k in (HOBJ, HSTRUCT):
            t["name"] = c.strings[r.idx()]
            t["super"] = r.idx()
            t["global"] = r.idx()
            nf, np, nb = r.idx(), r.idx(), r.idx()
            t["fields"] = [(c.strings[r.idx()], r.idx()) for _ in range(nf)]
            t["proto"] = [(c.strings[r.idx()], r.idx(), r.idx()) for _ in range(np)]
            t["bind"] = [(r.idx(), r.idx()) for _ in range(nb)]
        elif k in (HREF, HNULL, HPACKED):
            t["of"] = r.idx()
        elif k == HVIRTUAL:
            nf = r.idx()
            t["fields"] = [(c.strings[r.idx()], r.idx()) for _ in range(nf)]
        elif k == HABSTRACT:
            t["name"] = c.strings[r.idx()]
        elif k == HENUM:
            t["name"] = c.strings[r.idx()]
            t["global"] = r.idx()
            nc = r.idx()
            ctors = []
            for _ in range(nc):
                name = c.strings[r.idx()]
                np = r.idx()
                ctors.append((name, [r.idx() for _ in range(np)]))
            t["ctors"] = ctors
        c.types.append(t)
    c.globals = [r.idx() for _ in range(nglobals)]
    c.natives = []
    for _ in range(nnatives):
        lib, name, t, fi = c.strings[r.idx()], c.strings[r.idx()], r.idx(), r.idx()
        c.natives.append((lib, name, t, fi))
    c.funcs = []
    for _ in range(nfunctions):
        f = {"t": r.idx(), "fi": r.idx()}
        nregs, nops = r.idx(), r.idx()
        f["regs"] = [r.idx() for _ in range(nregs)]
        ops = []
        for _ in range(nops):
            o = r.idx()
            name, na = OPS[o]
            if na == 0:
                a = ()
            elif 1 <= na <= 3:
                a = tuple(r.idx() for _ in range(na))
            elif na == -1:
                if name == "Switch":
                    p1, n = r.idx(), r.idx()
                    ex = tuple(r.idx() for _ in range(n))
                    a = (p1, ex, r.idx())
                else:
                    p1, p2, n = r.idx(), r.idx(), r.u8()
                    a = (p1, p2) + (tuple(r.idx() for _ in range(n)),)
            else:
                a = tuple(r.idx() for _ in range(3)) + (tuple(r.idx() for _ in range(na - 3)),)
            ops.append((name, a))
        f["ops"] = ops if with_ops else None
        f["nops"] = nops
        if c.hasdebug:
            dbg = []
            i, cf, cl = 0, -1, 0
            while i < nops:
                x = r.u8()
                if x & 1:
                    x >>= 1
                    cf = (x << 8) | r.u8()
                elif x & 2:
                    delta, count = x >> 6, (x >> 2) & 15
                    for _ in range(count):
                        dbg.append((cf, cl))
                        i += 1
                    cl += delta
                elif x & 4:
                    cl += x >> 3
                    dbg.append((cf, cl))
                    i += 1
                else:
                    b2, b3 = r.u8(), r.u8()
                    cl = (x >> 3) | (b2 << 5) | (b3 << 13)
                    dbg.append((cf, cl))
                    i += 1
            f["dbg"] = dbg if with_ops else dbg[:1]
            if c.version >= 3:
                for _ in range(r.idx()):
                    r.idx()
                    r.idx()
        c.funcs.append(f)
    for _ in range(nconstants):
        r.idx()
        for _ in range(r.idx()):
            r.idx()
    c.end = r.p
    print("parsed bytes", r.p - start, "end offset", hex(r.p))
    return c


def tname(c, i, depth=0):
    t = c.types[i]
    k = t["k"]
    if "name" in t:
        return t["name"]
    if k in (HNULL, HREF, HPACKED) and depth < 4:
        return KINDS[k] + "<" + tname(c, t["of"], depth + 1) + ">"
    if k in (HFUN, HMETHOD) and depth < 3:
        return "(" + ",".join(tname(c, a, depth + 1) for a in t["args"]) + ")->" + tname(c, t["ret"], depth + 1)
    if k == HVIRTUAL and depth < 3:
        return "virtual{" + ",".join(n for n, _ in t["fields"]) + "}"
    return KINDS[k]


def fnames(c):
    names = {}
    for t in c.types:
        if t["k"] in (HOBJ, HSTRUCT):
            for name, mi, _ in t["proto"]:
                names[mi] = t["name"] + "." + name
            for fid, mi in t["bind"]:
                names.setdefault(mi, t["name"] + ".#bind%d" % fid)
    return names


def main():
    exe, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    b = open(exe, "rb").read()
    start = b.find(b"HLB\x04")
    assert start >= 0
    c = parse(b, start)
    names = fnames(c)
    with open(os.path.join(out, "classes.txt"), "w", encoding="utf-8") as f:
        for i, t in enumerate(c.types):
            if t["k"] in (HOBJ, HSTRUCT):
                sup = tname(c, t["super"]) if t["super"] >= 0 else "-"
                f.write("class %s extends %s  #t%d\n" % (t["name"], sup, i))
                for n, ti in t["fields"]:
                    f.write("  .%s : %s\n" % (n, tname(c, ti)))
                for n, mi, pi in t["proto"]:
                    f.write("  %s() f%d\n" % (n, mi))
            elif t["k"] == HENUM:
                f.write("enum %s #t%d\n" % (t["name"], i))
                for n, ps in t["ctors"]:
                    f.write("  %s(%s)\n" % (n, ",".join(tname(c, p) for p in ps)))
    with open(os.path.join(out, "natives.txt"), "w", encoding="utf-8") as f:
        for lib, name, t, fi in c.natives:
            f.write("%s.%s  f%d  %s\n" % (lib, name, fi, tname(c, t)))
    with open(os.path.join(out, "functions.txt"), "w", encoding="utf-8") as f:
        for fn in c.funcs:
            d = fn.get("dbg") or [(-1, 0)]
            src = "%s:%d" % (c.files[d[0][0]], d[0][1]) if d[0][0] >= 0 else "?"
            f.write("f%d\t%s\t%s\t%d\t%s\n" % (fn["fi"], names.get(fn["fi"], "?"), tname(c, fn["t"]), fn["nops"], src))
    with open(os.path.join(out, "strings.txt"), "w", encoding="utf-8") as f:
        for i, s in enumerate(c.strings):
            f.write("%d\t%s\n" % (i, json.dumps(s, ensure_ascii=False)))
    with open(os.path.join(out, "files.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(c.files))
    with open(os.path.join(out, "code.pickle"), "wb") as f:
        pickle.dump(c.__dict__, f)


if __name__ == "__main__":
    main()
