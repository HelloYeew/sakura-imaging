// This code is part of the Sakura framework project. Licensed under the MIT License.
// See the LICENSE file for full license text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using ImageMagick;
using Sakura.Framework.Graphics.Textures;
using Silk.NET.OpenGL;
using Texture = Sakura.Framework.Graphics.Textures.Texture;

namespace Sakura.Framework.Imaging.ImageMagick;

/// <summary>
/// An object representing an ImageMagick object that can be edited, manipulated, and use OpenGL to render.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class GLMagickEditableImage : IDisposable
{
    private readonly GL gl;

    /// <summary>
    /// The underlying ImageMagick image
    /// use for storing the high-precision (Q16) and original image data (color space etc.)
    /// </summary>
    public MagickImage? Image { get; private set; }

    /// <summary>
    /// Sakura's <see cref="Texture"/> for using in drawable objects and use for rendering.
    /// </summary>
    public Texture? PreviewTexture { get; private set; }

    public GLMagickEditableImage(GL gl)
    {
        this.gl = gl;
    }

    public void Load(Stream stream)
    {
        Image = new MagickImage(stream);
        Image.AutoOrient();
        SyncToGpu();
    }

    /// <summary>
    /// Applies an edit operation and updates the GPU texture.
    /// </summary>
    public void Apply(Action<MagickImage> editOperation)
    {
        if (Image == null) return;
        editOperation(Image);
        SyncToGpu();
    }

    /// <summary>
    /// Takes the High-Res CPU image, converts a copy to sRGB, and uploads to GPU.
    /// </summary>
    private void SyncToGpu()
    {
        if (Image == null) return;

        // Clone the image so we don't mess up the Master data
        using var viewCopy = (MagickImage)Image.Clone();

        // Convert the copy data to sRGB for the monitor
        // This ensures the user sees "Correct" colors on screen,
        // but the master image (Image object above) retains the original color profile.
        viewCopy.TransformColorSpace(ColorProfiles.SRGB);

        // Downsample to 8-bit RGBA for OpenGL
        if (viewCopy.Format != MagickFormat.Rgba)
            viewCopy.Format = MagickFormat.Rgba;

        byte[] rawBytes = viewCopy.ToByteArray(MagickFormat.Rgba);

        // Update the Texture
        // Dispose the old texture and create a new one
        var oldTexture = PreviewTexture;

        var glTexture = new GLTexture(gl, (int)viewCopy.Width, (int)viewCopy.Height, rawBytes);
        PreviewTexture = new Texture(glTexture);

        if (oldTexture != null && oldTexture.GlTexture != null)
        {
            oldTexture.GlTexture.Dispose();
        }
    }

    public void Dispose()
    {
        Image?.Dispose();
        if (PreviewTexture?.GlTexture != null)
            PreviewTexture.GlTexture.Dispose();
    }
}
