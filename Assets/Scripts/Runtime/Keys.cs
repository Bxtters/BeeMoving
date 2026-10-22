using UnityEngine.InputSystem;

namespace Handline
{
    /// <summary>
    /// Keyboard reads for the camera stage.
    /// Added so both displays share one input path instead of calling the Input System in every file.
    /// A missing keyboard returns false, which adds a safe no-op on devices with no keys.
    /// </summary>
    public static class Keys
    {
        /// <summary>
        /// True only on the frame the key goes down. Adds one-shot actions such as mirror, rotate, and camera cycle.
        /// </summary>
        public static bool Pressed(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        /// <summary>
        /// True while the key stays down. Added for controls that stay active until the key is released.
        /// </summary>
        public static bool Held(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].isPressed;
        }
    }
}
