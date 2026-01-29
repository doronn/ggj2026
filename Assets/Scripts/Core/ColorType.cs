using System;
using UnityEngine;

namespace BreakingHue.Core
{
    /// <summary>
    /// Flags-based color representation for the subtractive color mechanic.
    /// Colors can be combined: Red | Green = Yellow, Red | Blue = Magenta, etc.
    /// </summary>
    [Flags]
    public enum ColorType
    {
        None  = 0,
        Red   = 1 << 0,  // 1
        Green = 1 << 1,  // 2
        Blue  = 1 << 2,  // 4
        
        // Composite colors for convenience
        Yellow  = Red | Green,      // 3
        Cyan    = Green | Blue,     // 6
        Magenta = Red | Blue,       // 5
        White   = Red | Green | Blue // 7
    }

    public static class ColorTypeExtensions
    {
        /// <summary>
        /// Converts a Unity Color to ColorType flags.
        /// Each channel is considered "on" if its value > 0.5f.
        /// </summary>
        public static ColorType FromColor(Color color)
        {
            ColorType result = ColorType.None;
            
            if (color.r > 0.5f)
                result |= ColorType.Red;
            if (color.g > 0.5f)
                result |= ColorType.Green;
            if (color.b > 0.5f)
                result |= ColorType.Blue;
            
            return result;
        }

        /// <summary>
        /// Converts ColorType flags back to a Unity Color.
        /// </summary>
        public static Color ToColor(this ColorType colorType)
        {
            float r = (colorType & ColorType.Red) != 0 ? 1f : 0f;
            float g = (colorType & ColorType.Green) != 0 ? 1f : 0f;
            float b = (colorType & ColorType.Blue) != 0 ? 1f : 0f;
            
            return new Color(r, g, b, 1f);
        }

        /// <summary>
        /// Checks if the color type contains all the required flags.
        /// </summary>
        public static bool Contains(this ColorType inventory, ColorType required)
        {
            return (inventory & required) == required;
        }

        /// <summary>
        /// Removes flags from the color type (subtractive operation).
        /// </summary>
        public static ColorType Subtract(this ColorType source, ColorType toRemove)
        {
            return source & ~toRemove;
        }

        /// <summary>
        /// Checks if an equipped mask can pass through a door of the given color.
        /// Supports both exact matches (Purple mask -> Purple door) and
        /// composite matches (Red|Blue equipped -> Purple door).
        /// </summary>
        public static bool CanPassThrough(ColorType equippedMask, ColorType doorColor)
        {
            if (equippedMask == ColorType.None || doorColor == ColorType.None)
                return false;

            // Exact match (e.g., Purple mask -> Purple door)
            if (equippedMask == doorColor)
                return true;

            // Composite match (e.g., mask contains all flags required by door)
            // This allows Red|Blue mask to pass through Magenta door
            return (equippedMask & doorColor) == doorColor;
        }

        /// <summary>
        /// Gets a human-readable name for the color type.
        /// </summary>
        public static string GetDisplayName(this ColorType colorType)
        {
            return colorType switch
            {
                ColorType.None => "Empty",
                ColorType.Red => "Red",
                ColorType.Green => "Green",
                ColorType.Blue => "Blue",
                ColorType.Yellow => "Yellow",
                ColorType.Cyan => "Cyan",
                ColorType.Magenta => "Purple",
                ColorType.White => "White",
                _ => colorType.ToString()
            };
        }
    }
}
