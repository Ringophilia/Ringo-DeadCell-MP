using dc;
using dc.hl.types;
using dc.hxd;
using dc.pr;

namespace RingoMP
{
    /// What the player at this machine is holding, read through the game's own
    /// binding tables, so rebinding and gamepads work without extra code.
    public static unsafe class RealInput
    {
        const int NoBinding = -1;

        public static ulong Read()
        {
            var isMenu = false;
            if (Game.Class.ME?.hero is not { } hero || hero.controlsLocked(ref isMenu))
            {
                return 0;
            }
            var controller = Boot.Class.ME.controller;
            var bindings = controller.get_bindings();
            var mask = 0UL;
            for (var i = 0; i < VirtualController.ActionCount; i++)
            {
                if (KeyDown(bindings.primary, i) || KeyDown(bindings.secondary, i) || KeyDown(bindings.third, i)
                    || PadDown(controller, bindings.padA, i) || PadDown(controller, bindings.padB, i) || PadDown(controller, bindings.padC, i))
                {
                    mask |= 1UL << i;
                }
            }
            return mask;
        }

        static int Binding(ArrayBytes_Int table, int action) =>
            action < table.length ? ((int*)table.bytes)[action] : NoBinding;

        static bool KeyDown(ArrayBytes_Int table, int action)
        {
            var key = Binding(table, action);
            return key > NoBinding && Key.Class.isDown.Invoke(key);
        }

        static bool PadDown(dc.tool.Controller controller, ArrayBytes_Int table, int action)
        {
            var button = Binding(table, action);
            return button > NoBinding && controller.padIsDown(button);
        }
    }
}
