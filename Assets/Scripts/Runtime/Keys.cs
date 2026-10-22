using UnityEngine.InputSystem;

namespace Handline
{
    public static class Keys
    {
        public static bool Pressed(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        public static bool Held(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }
    }
}
