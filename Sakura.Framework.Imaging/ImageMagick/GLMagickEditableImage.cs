// This code is part of the Sakura framework project. Licensed under the MIT License.
// See the LICENSE file for full license text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using ImageMagick;
using Sakura.Framework.Graphics.Textures;
using Texture = Sakura.Framework.Graphics.Textures.Texture;

namespace Sakura.Framework.Imaging.ImageMagick;

/// <summary>
/// An object representing an ImageMagick object that can be edited, manipulated, and use OpenGL to render.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class GLMagickEditableImage : IDisposable
{
    private readonly ITextureManager textureManager;

    /// <summary>
    /// The underlying ImageMagick image
    /// use for storing the high-precision (Q16) and original image data (color space etc.)
    /// </summary>
    public MagickImage? Image { get; private set; }

    /// <summary>
    /// Sakura's <see cref="Texture"/> for using in drawable objects and use for rendering.
    /// </summary>
    public Texture? PreviewTexture { get; private set; }

    public GLMagickEditableImage(ITextureManager textureManager)
    {
        this.textureManager = textureManager;
    }

    /// <summary>
    /// Load a document directly from file path.
    /// Will auto-detect RAW formats and apply proper settings based on file extension.
    /// </summary>
    /// <param name="path">The file path to load from.</param>
    public void Load(string path)
    {
        Image = new MagickImage(path);
        Image.AutoOrient();
        SyncToGpu();
    }

    /// <summary>
    /// Load a document from a stream.
    /// </summary>
    /// <param name="stream">The source of <see cref="Stream"/></param>
    /// <param name="formatHint">
    /// Optional file extension hint for RAW format detection (e.g., ".nef", ".cr2").
    /// Required for RAW formats to make proper adjustments.
    /// </param>
    public void Load(Stream stream, string? formatHint = null)
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

        using var viewCopy = (MagickImage)Image.Clone();
        if (viewCopy.ColorSpace != ColorSpace.sRGB)
            viewCopy.TransformColorSpace(ColorProfiles.SRGB); //

        if (viewCopy.Format != MagickFormat.Rgba)
            viewCopy.Format = MagickFormat.Rgba;

        byte[] rawBytes = viewCopy.ToByteArray(MagickFormat.Rgba);

        if (PreviewTexture != null)
        {
            PreviewTexture.GlTexture?.Dispose();
        }

        PreviewTexture = textureManager.FromPixelData((int)viewCopy.Width, (int)viewCopy.Height, rawBytes);
    }

    public void Dispose()
    {
        Image?.Dispose();
        if (PreviewTexture?.GlTexture != null)
            PreviewTexture.GlTexture.Dispose();
    }
}
