// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace EvolveOS_ShellEnhancer.Utilities.Helpers
{
    public static class IconTintHelper
    {
        public static async Task<SoftwareBitmapSource> GetTintedImageAsync(IRandomAccessStream stream, Color tintColor)
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                new BitmapTransform(),
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb);

            byte[] pixels = pixelData.DetachPixelData();

            bool isMonochrome = true;
            long totalBrightness = 0;
            int visiblePixels = 0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte pa = pixels[i + 3];
                if (pa > 20)
                {
                    byte pb = pixels[i];
                    byte pg = pixels[i + 1];
                    byte pr = pixels[i + 2];

                    float r = pr * 255f / pa;
                    float g = pg * 255f / pa;
                    float b = pb * 255f / pa;

                    float max = Math.Max(r, Math.Max(g, b));
                    float min = Math.Min(r, Math.Min(g, b));

                    if (max - min > 35)
                    {
                        isMonochrome = false;
                    }

                    totalBrightness += (long)max;
                    visiblePixels++;
                }
            }

            float averageBrightness = visiblePixels > 0 ? (totalBrightness / (float)visiblePixels) / 255f : 0f;
            float tintIntensity = tintColor.A / 255f;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte pb = pixels[i];
                byte pg = pixels[i + 1];
                byte pr = pixels[i + 2];
                byte pa = pixels[i + 3];

                if (pa > 0)
                {
                    float r = pr * 255f / pa;
                    float g = pg * 255f / pa;
                    float b = pb * 255f / pa;

                    float brightness = Math.Max(r, Math.Max(g, b)) / 255f;
                    float tintedR, tintedG, tintedB;

                    if (isMonochrome)
                    {
                        if (averageBrightness < 0.4f)
                        {
                            brightness = 1.0f - (Math.Min(r, Math.Min(g, b)) / 255f);
                        }

                        tintedR = tintColor.R * brightness;
                        tintedG = tintColor.G * brightness;
                        tintedB = tintColor.B * brightness;
                    }
                    else
                    {
                        tintedR = tintColor.R * brightness;
                        tintedG = tintColor.G * brightness;
                        tintedB = tintColor.B * brightness;
                    }

                    float finalR = (r * (1f - tintIntensity)) + (tintedR * tintIntensity);
                    float finalG = (g * (1f - tintIntensity)) + (tintedG * tintIntensity);
                    float finalB = (b * (1f - tintIntensity)) + (tintedB * tintIntensity);

                    pixels[i] = (byte)Math.Clamp(finalB * pa / 255f, 0, 255); // B
                    pixels[i + 1] = (byte)Math.Clamp(finalG * pa / 255f, 0, 255); // G
                    pixels[i + 2] = (byte)Math.Clamp(finalR * pa / 255f, 0, 255); // R
                }
            }

            SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, (int)decoder.PixelWidth, (int)decoder.PixelHeight, BitmapAlphaMode.Premultiplied);
            softwareBitmap.CopyFromBuffer(pixels.AsBuffer());

            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap);
            return source;
        }
    }
}