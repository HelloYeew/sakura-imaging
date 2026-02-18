// This code is part of the Sakura framework project. Licensed under the MIT License.
// See the LICENSE file for full license text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using ImageMagick;
using ImageMagick.Formats;
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

    private MagickImage? originalImage;

    public MagickImage? Image { get; private set; }
    public Texture? PreviewTexture { get; private set; }

    public GLMagickEditableImage(ITextureManager textureManager)
    {
        this.textureManager = textureManager;
    }

    public void Load(string path)
    {
        var settings = GetRawSettings(Path.GetExtension(path));
        originalImage = settings != null ? new MagickImage(path, settings) : new MagickImage(path);
        PrepareImage(originalImage);
        Image = new MagickImage(originalImage);
        SyncToGpu();
    }

    public void Load(Stream stream, string? formatHint = null)
    {
        var settings = GetRawSettings(formatHint);
        originalImage = settings != null ? new MagickImage(stream, settings) : new MagickImage(stream);
        PrepareImage(originalImage);
        Image = new MagickImage(originalImage);
        SyncToGpu();
    }

    private void PrepareImage(MagickImage? targetImage)
    {
        if (targetImage == null) return;
        targetImage.AutoOrient();
        targetImage.Alpha(AlphaOption.Off);
    }

    private MagickReadSettings? GetRawSettings(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return null;
        var ext = extension.TrimStart('.').ToLowerInvariant();

        // Map common extensions to their specific MagickFormats.
        MagickFormat format = ext switch
        {
            "nef" => MagickFormat.Nef,
            "cr2" => MagickFormat.Cr2,
            "dng" => MagickFormat.Dng,
            "arw" => MagickFormat.Arw,
            "raf" => MagickFormat.Raf,
            "orf" => MagickFormat.Orf,
            "rw2" => MagickFormat.Rw2,
            _ => MagickFormat.Unknown
        };

        if (format != MagickFormat.Unknown)
        {
            return new MagickReadSettings
            {
                Format = format,
                Defines = new DngReadDefines
                {
                    // The purple tint often happens when "CameraWhitebalance" fails to read the metadata
                    // multipliers correctly, resulting in Green=0 (Magenta).
                    // AutoWhitebalance uses the image data to find gray points.
                    UseAutoWhiteBalance = true,

                    // Ensure the output is in sRGB color space.
                    OutputColor = DngOutputColor.SRGB
                }
            };
        }

        return null;
    }

    public void Apply(Action<MagickImage> editOperation)
    {
        if (originalImage == null) return;
        Image?.Dispose();
        Image = new MagickImage(originalImage);
        editOperation(Image);
        SyncToGpu();
    }

    private void SyncToGpu()
    {
        if (Image == null) return;

        // Clone so we don't modify the original RAW data (which might be 16-bit)
        using var viewCopy = (MagickImage)Image.Clone();

        // Force sRGB Color Space
        if (viewCopy.ColorSpace != ColorSpace.sRGB)
            viewCopy.TransformColorSpace(ColorProfiles.SRGB);

        // Force 8-bit Depth
        viewCopy.Depth = 8;

        // 3. Ensure RGBA Layout
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
        originalImage?.Dispose();
        Image?.Dispose();
        if (PreviewTexture?.GlTexture != null)
            PreviewTexture.GlTexture.Dispose();
    }
}
