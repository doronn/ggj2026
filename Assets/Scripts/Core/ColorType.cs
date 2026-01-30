using System;
using UnityEngine;

namespace BreakingHue.Core
{
    /// <summary>
    /// Flags-based color representation using RYB (Red-Yellow-Blue) color model.
    /// Colors combine to create secondaries: Red|Yellow=Orange, Yellow|Blue=Green, Red|Blue=Purple.
    /// All three combined creates Black (R|Y|B = Black).
    /// </summary>
    [Flags]
    public enum ColorType
    {
        None   = 0,
        Red    = 1 << 0,  // 1
        Yellow = 1 << 1,  // 2
        Blue   = 1 << 2,  // 4
        
        // Secondary colors (RYB model)
        Orange = Red | Yellow,       // 3 (R+Y)
        Green  = Yellow | Blue,      // 6 (Y+B)
        Purple = Red | Blue,         // 5 (R+B)
        
        // Tertiary - all primaries combined
        Black  = Red | Yellow | Blue // 7 (R+Y+B)
    }

    public static class ColorTypeExtensions
    {
        /// <summary>
        /// Converts a Unity Color to ColorType flags using RYB interpretation.
        /// Maps visual RGB to RYB primaries based on color analysis.
        /// </summary>
        public static ColorType FromColor(Color color)
        {
            ColorType result = ColorType.None;
            
            // Check for black (all colors combined in RYB)
            if (color.r < 0.2f && color.g < 0.2f && color.b < 0.2f && color.a > 0.5f)
                return ColorType.Black;
            
            // Red detection (high red, low green, low blue)
            if (color.r > 0.6f && color.g < 0.5f && color.b < 0.5f)
                result |= ColorType.Red;
            
            // Yellow detection (high red AND high green, low blue)
            if (color.r > 0.6f && color.g > 0.6f && color.b < 0.5f)
            {
                result = ColorType.Yellow; // Pure yellow, not red
            }
            
            // Blue detection (low red, low green, high blue)
            if (color.r < 0.5f && color.g < 0.5f && color.b > 0.6f)
                result |= ColorType.Blue;
            
            // Orange detection (high red, medium green, low blue)
            if (color.r > 0.8f && color.g > 0.3f && color.g < 0.7f && color.b < 0.3f)
                return ColorType.Orange;
            
            // Green detection (low red, high green, low blue) - RYB green = Yellow + Blue visually
            if (color.r < 0.5f && color.g > 0.6f && color.b < 0.5f)
                return ColorType.Green;
            
            // Purple detection (high red, low green, high blue)
            if (color.r > 0.4f && color.g < 0.4f && color.b > 0.4f)
                return ColorType.Purple;
            
            return result;
        }

        /// <summary>
        /// Converts ColorType flags to a Unity Color for visual display.
        /// Maps RYB color model to RGB for rendering.
        /// </summary>
        public static Color ToColor(this ColorType colorType)
        {
            return colorType switch
            {
                ColorType.None => new Color(0.5f, 0.5f, 0.5f, 0.5f), // Transparent grey
                ColorType.Red => new Color(1f, 0f, 0f, 1f),          // Pure red
                ColorType.Yellow => new Color(1f, 1f, 0f, 1f),       // Pure yellow
                ColorType.Blue => new Color(0f, 0f, 1f, 1f),         // Pure blue
                ColorType.Orange => new Color(1f, 0.5f, 0f, 1f),     // Orange (#FF8000)
                ColorType.Green => new Color(0f, 1f, 0f, 1f),        // Green (#00FF00)
                ColorType.Purple => new Color(0.5f, 0f, 0.5f, 1f),   // Purple (#800080)
                ColorType.Black => new Color(0.06f, 0.06f, 0.06f, 1f), // Near-black (#101010)
                _ => GetCompositeColor(colorType)
            };
        }

        /// <summary>
        /// Handles composite colors that aren't predefined (e.g., Red|Yellow|Blue = Black).
        /// </summary>
        private static Color GetCompositeColor(ColorType colorType)
        {
            // If it contains all three primaries, it's black
            if ((colorType & ColorType.Black) == ColorType.Black)
                return new Color(0.06f, 0.06f, 0.06f, 1f);
            
            // Otherwise, blend the component colors
            Color result = Color.black;
            int count = 0;
            
            if ((colorType & ColorType.Red) != 0)
            {
                result += ColorType.Red.ToColor();
                count++;
            }
            if ((colorType & ColorType.Yellow) != 0)
            {
                result += ColorType.Yellow.ToColor();
                count++;
            }
            if ((colorType & ColorType.Blue) != 0)
            {
                result += ColorType.Blue.ToColor();
                count++;
            }
            
            if (count > 0)
            {
                result /= count;
                result.a = 1f;
            }
            
            return result;
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
        /// Adds flags to the color type (additive operation).
        /// </summary>
        public static ColorType Add(this ColorType source, ColorType toAdd)
        {
            return source | toAdd;
        }

        /// <summary>
        /// Checks if a combined mask color can pass through a barrier of the given color.
        /// The mask must contain all color flags required by the barrier.
        /// </summary>
        public static bool CanPassThrough(ColorType combinedMaskColor, ColorType barrierColor)
        {
            if (combinedMaskColor == ColorType.None || barrierColor == ColorType.None)
                return false;

            // The combined mask must contain all flags in the barrier color
            return (combinedMaskColor & barrierColor) == barrierColor;
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
                ColorType.Yellow => "Yellow",
                ColorType.Blue => "Blue",
                ColorType.Orange => "Orange",
                ColorType.Green => "Green",
                ColorType.Purple => "Purple",
                ColorType.Black => "Black",
                _ => GetCompositeDisplayName(colorType)
            };
        }

        /// <summary>
        /// Gets display name for non-standard composite colors.
        /// </summary>
        private static string GetCompositeDisplayName(ColorType colorType)
        {
            // If it's the full black combination
            if ((colorType & ColorType.Black) == ColorType.Black)
                return "Black";
            
            // Build name from components
            var parts = new System.Collections.Generic.List<string>();
            if ((colorType & ColorType.Red) != 0) parts.Add("Red");
            if ((colorType & ColorType.Yellow) != 0) parts.Add("Yellow");
            if ((colorType & ColorType.Blue) != 0) parts.Add("Blue");
            
            return parts.Count > 0 ? string.Join("+", parts) : "Unknown";
        }

        /// <summary>
        /// Gets all primary color flags present in a color type.
        /// </summary>
        public static ColorType GetPrimaryComponents(this ColorType colorType)
        {
            return colorType & (ColorType.Red | ColorType.Yellow | ColorType.Blue);
        }

        /// <summary>
        /// Counts how many primary colors are in the color type.
        /// </summary>
        public static int CountPrimaries(this ColorType colorType)
        {
            int count = 0;
            if ((colorType & ColorType.Red) != 0) count++;
            if ((colorType & ColorType.Yellow) != 0) count++;
            if ((colorType & ColorType.Blue) != 0) count++;
            return count;
        }

        /// <summary>
        /// Checks if the color type is a primary color (Red, Yellow, or Blue only).
        /// </summary>
        public static bool IsPrimary(this ColorType colorType)
        {
            return colorType == ColorType.Red || 
                   colorType == ColorType.Yellow || 
                   colorType == ColorType.Blue;
        }

        /// <summary>
        /// Checks if the color type is a secondary color (Orange, Green, or Purple).
        /// </summary>
        public static bool IsSecondary(this ColorType colorType)
        {
            return colorType == ColorType.Orange || 
                   colorType == ColorType.Green || 
                   colorType == ColorType.Purple;
        }

        /// <summary>
        /// Checks if the color type is Black (all primaries combined).
        /// </summary>
        public static bool IsBlack(this ColorType colorType)
        {
            return (colorType & ColorType.Black) == ColorType.Black;
        }
    }
}
