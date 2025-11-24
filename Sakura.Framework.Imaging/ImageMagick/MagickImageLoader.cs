// This code is part of the Sakura framework project. Licensed under the MIT License.
// See the LICENSE file for full license text.

using System.IO;
using ImageMagick;
using Sakura.Framework.Graphics.Textures;
using Sakura.Framework.Logging;

namespace Sakura.Framework.Imaging.ImageMagick;

/// <summary>
/// The image loader using ImageMagick that support accurate color and more image manipulations.
/// Also support a wide range of image formats.
/// </summary>
public class MagickImageLoader : IImageLoader
{
    public ImageRawData Load(Stream stream)
    {
        using var image = new MagickImage(stream);

        image.AutoOrient();

        Logger.Debug($"[Magick] Image Color Space: {image.ColorSpace}", LoggingTarget.Graphics);
        Logger.Debug($"[Magick] Image Format: {image.Format}", LoggingTarget.Graphics);

        // Fix color space since some images have ICC profiles or different color spaces
        image.TransformColorSpace(ColorProfiles.SRGB);

        // Ensure RGBA formats
        if (image.Format != MagickFormat.Rgba)
        {
            image.Format = MagickFormat.Rgba;
        }

        byte[] data = image.ToByteArray(MagickFormat.Rgba);

        return new ImageRawData((int)image.Width, (int)image.Height, data);
    }
}
