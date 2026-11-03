using dc;
using dc.h2d;
using dc.h3d.mat;
using dc.hl.types;
using HaxeProxy.Runtime;
using Serilog;

namespace RingoMP.Dev
{
    /// A picture of what the game draws, for a test game on a hidden desktop that
    /// nobody can look at. It is taken while the game renders, the one time the
    /// engine can draw somewhere else.
    public static class Snapshot
    {
        static string? pending;
        static ILogger? logger;
        static bool hooked;

        public static void Request(string path, ILogger log)
        {
            pending = path;
            logger = log;
            if (!hooked)
            {
                hooked = true;
                Hook_Scene.render += OnRender;
            }
        }

        static void OnRender(Hook_Scene.orig_render orig, Scene self, dc.h3d.Engine engine)
        {
            orig(self, engine);
            if (pending is not { } path || !ReferenceEquals(self, Boot.Class.ME.s2d))
            {
                return;
            }
            pending = null;
            // An array made from here has no storage yet, so the game makes this one.
            var flags = (ArrayObj)self.children.slice(0, 0);
            flags.push(new TextureFlags.Target());
            var texture = new Texture(engine.width, engine.height, flags, null, null);
            engine.pushTarget(texture, Ref<int>.Null, Ref<int>.Null);
            engine.clear(0, 1, null);
            orig(self, engine);
            engine.popTarget();
            var png = texture.capturePixels(Ref<int>.Null, Ref<int>.Null).toPNG(null);
            var bytes = new byte[png.length];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)png.get(i);
            }
            texture.dispose();
            File.WriteAllBytes(path, bytes);
            logger?.Information("RINGO snapshot {w}x{h} saved to {p}", engine.width, engine.height, path);
        }
    }
}
