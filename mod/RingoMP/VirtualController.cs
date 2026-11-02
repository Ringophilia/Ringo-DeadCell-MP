using dc;
using dc.hl.types;
using dc.hxd;
using dc.libs.heaps;
using dc.tool;
using ModCore.Utilities;

namespace RingoMP
{
    /// Gives a hero its own input, separate from the keyboard and gamepads.
    ///
    /// The game inlines its input checks into every reader. They only look at
    /// `controller.parent`: `mode`, `gc.device`, `gc.toggles[button] > 0` for held
    /// and `== 1` for just pressed. So a private tool.Controller whose GamePad is
    /// off the global update list is fully ours to write, and no hook is needed.
    public sealed unsafe class VirtualController
    {
        public const int ActionCount = 38;
        const int NoButton = -1;
        const int ReleasedToggle = 0;

        readonly Controller controller;
        readonly GamePad pad;
        readonly bool[] held = new bool[ActionCount];

        public ControllerAccess Access { get; }

        public VirtualController()
        {
            var scene = Boot.Class.ME.s2d;
            controller = new Controller(scene);
            pad = new GamePad(null, null);
            Detach(controller);
            GamePad.Class.ALL.remove(pad);
            Controller.Class.ALL.remove(controller);

            pad.device = new Pad();
            controller.gc = pad;
            controller.mode = Controller.Class.ENABLE_PAD;

            for (var i = 0; i < ActionCount; i++)
            {
                pad.toggles.push(ReleasedToggle);
            }
            var bindings = controller.normalBindings;
            Fill(bindings.padA, i => i);
            Fill(bindings.padB, _ => NoButton);
            Fill(bindings.padC, _ => NoButton);

            Access = controller.createAccess("remote-hero".AsHaxeString(), null);
        }

        /// The constructor grabs the real gamepads; they must stay with the local player.
        static void Detach(Controller c)
        {
            foreach (var list in new[] { c.allGamepads, c.activeGamepads })
            {
                for (var i = list.length - 1; i >= 0; i--)
                {
                    GamePad.Class.ALL.remove(list.getDyn(i));
                    list.remove(list.getDyn(i));
                }
            }
        }

        static void Fill(ArrayBytes_Int array, Func<int, int> value)
        {
            while (array.length < ActionCount)
            {
                array.push(NoButton);
            }
            var data = (int*)array.bytes;
            for (var i = 0; i < ActionCount; i++)
            {
                data[i] = value(i);
            }
        }

        public void SetHeld(int action, bool down) => held[action] = down;

        public void ReleaseAll() => System.Array.Clear(held);

        /// Held actions as bits, the form they travel over the network in.
        public ulong HeldMask
        {
            get
            {
                var mask = 0UL;
                for (var i = 0; i < ActionCount; i++)
                {
                    if (held[i])
                    {
                        mask |= 1UL << i;
                    }
                }
                return mask;
            }
            set
            {
                for (var i = 0; i < ActionCount; i++)
                {
                    held[i] = (value >> i & 1) != 0;
                }
            }
        }

        /// Runs once per game frame, before the hero reads its input.
        public void Update()
        {
            var toggles = (int*)pad.toggles.bytes;
            for (var i = 0; i < ActionCount; i++)
            {
                toggles[i] = held[i] ? toggles[i] + 1 : ReleasedToggle;
            }
        }
    }
}
