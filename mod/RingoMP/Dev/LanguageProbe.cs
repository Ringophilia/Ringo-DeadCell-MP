using dc;
using dc.h2d;
using dc.hxd;
using dc.hxd.res;
using ModCore.Utilities;
using Serilog;

namespace RingoMP.Dev
{
    /// Puts every phrase of every language through the game's own font for that
    /// language. A letter a bitmap font lacks just vanishes on screen, and the game's
    /// fonts may hold little more than the letters of its own text.
    public static class LanguageProbe
    {
        const int Size = 18;

        public static void Report(ILogger log)
        {
            log.Information("RINGO language now={l}", Lang.Class.LANG?.ToString());
            var languages = Lang.Class.LANGS;
            var total = 0;
            for (var i = 0; i < languages.length; i++)
            {
                string description = languages.getDyn(i).ToString();
                var id = Field(description, "id");
                var charset = Field(description, "chars");
                var font = Load($"fonts/exported/{charset}_{Size}.fnt");
                var missing = new SortedDictionary<char, List<Phrase>>();
                foreach (var phrase in Translations.Phrases)
                {
                    foreach (var letter in Translations.In(id == "es-es" ? "es" : id, phrase).Where(c => c != '{' && c != '}' && !font.hasChar(c)))
                    {
                        if (!missing.TryGetValue(letter, out var phrases))
                        {
                            missing[letter] = phrases = [];
                        }
                        phrases.Add(phrase);
                    }
                }
                total += missing.Count;
                log.Information("RINGO font check {id} ({set}): {n} letters missing: {m}", id, charset, missing.Count,
                    string.Join(' ', missing.Select(m => $"{m.Key}({string.Join(',', m.Value.Distinct())})")));
            }
            log.Information("RINGO font check done: {n} letters missing in all", total);
        }

        static Font Load(string path) => new BitmapFont(Res.Class.get_loader().fs.get(path.AsHaxeString())).toFont();

        /// The game prints its language records as "{id : de, chars : latin, ...}".
        static string Field(string description, string name)
        {
            var start = description.IndexOf($"{name} : ", StringComparison.Ordinal) + name.Length + 3;
            return description[start..description.IndexOf(',', start)];
        }
    }
}
