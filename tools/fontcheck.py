# Reads the game's bitmap fonts out of res.pak and checks every phrase of
# Translations.cs against the font of its language.
import re, struct, sys

pak_path, translations = sys.argv[1], sys.argv[2]
f = open(pak_path, "rb")
assert f.read(3) == b"PAK"
version = f.read(1)[0]
header_size, data_size = struct.unpack("<ii", f.read(8))
f.read(64)  # this game keeps a hash of the pak here
files = {}

def entry(prefix):
    name = f.read(f.read(1)[0]).decode("utf-8")
    path = f"{prefix}/{name}" if prefix else name
    flags = f.read(1)[0]
    if flags & 1:
        for _ in range(struct.unpack("<i", f.read(4))[0]):
            entry(path)
    else:
        if True:  # int32 offsets in this game, whatever the version
            pos = struct.unpack("<i", f.read(4))[0]
        else:
            pos = int(struct.unpack("<d", f.read(8))[0])
        size, _ = struct.unpack("<ii", f.read(8))
        files[path] = (pos, size)

entry("")

def read(path):
    pos, size = files[path]
    f.seek(header_size + pos)
    return f.read(size)

def chars(data):
    # Heaps' BFNT: name, size, texture, line height and base line, the default
    # letter, then 22 bytes a glyph (kerning pairs never used) up to a zero id.
    assert data[:4] == b"BFNT"
    i = 6
    i += 2 + struct.unpack_from("<H", data, i)[0]
    i += 2
    i += 2 + struct.unpack_from("<H", data, i)[0]
    i += 8
    found = set()
    while (letter := struct.unpack_from("<i", data, i)[0]) != 0:
        found.add(letter)
        i += 22
    return found

fonts = {}
for charset in ["latin", "japanese", "korean", "cyrillic", "schinese", "tchinese"]:
    candidates = [p for p in files if p.endswith(f"{charset}_18.fnt")]
    fonts[charset] = chars(read(candidates[0]))
    print(charset, candidates[0], len(fonts[charset]), "chars")

languages = ["en", "de", "es", "fr", "it", "ja", "ko", "pl", "pt", "ru", "tr", "zh", "zh-tw"]
charset_of = {"ja": "japanese", "ko": "korean", "ru": "cyrillic", "zh": "schinese", "zh-tw": "tchinese"}
source = open(translations, encoding="utf-8").read()
table = source[source.index("Table = new()"):source.index("static string Current")]
total = 0
for phrase, body in re.findall(r"\[Phrase\.(\w+)\] = \[(.*?)\],\n", table, re.S):
    texts = re.findall(r'"((?:[^"\\]|\\.)*)"', body)
    assert len(texts) == len(languages), (phrase, len(texts))
    for language, text in zip(languages, texts):
        font = fonts[charset_of.get(language, "latin")]
        missing = sorted({c for c in text if c not in "{}" and ord(c) not in font})
        if missing:
            total += len(missing)
            print("missing", language, phrase, "".join(missing))
print("letters missing in all:", total)
