using System;
using SkiaSharp;

namespace ESCPOS_NET.Extensions
{
	public static class SkiaImageExtensions
	{
		/// <summary>
		/// Decodes an image, scales it to fit the given bounds, converts it to grayscale,
		/// applies Stucki dithering and packs it into 1 bit per pixel (1 = black).
		/// </summary>
		public static byte[] ToSingleBitPixelByteArray(byte[] imageBytes, out int width, out int height, int? maxWidth = null, int? maxHeight = null, float threshold = 0.5F)
		{
			using (var source = SKBitmap.Decode(imageBytes))
			{
				if (source == null)
				{
					throw new ArgumentException("The image data could not be decoded.", nameof(imageBytes));
				}

				width = source.Width;
				height = source.Height;

				if (maxWidth.HasValue || maxHeight.HasValue)
				{
					double scale = Math.Min(
							(maxWidth ?? int.MaxValue) / (double)source.Width,
							(maxHeight ?? int.MaxValue) / (double)source.Height);
					width = Math.Max(1, (int)Math.Round(source.Width * scale));
					height = Math.Max(1, (int)Math.Round(source.Height * scale));
				}

				// Draw onto a white background (replaces transparency) at the target size.
				using (var target = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
				{
					using (var canvas = new SKCanvas(target))
					using (var image = SKImage.FromBitmap(source))
					using (var paint = new SKPaint { IsAntialias = true })
					{
						var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

						canvas.Clear(SKColors.White);
						canvas.DrawImage(
							image,
							new SKRect(0, 0, source.Width, source.Height),
							new SKRect(0, 0, width, height),
							sampling,
							paint);
					}

					return Pack(target, threshold);
				}
			}
		}

		private static byte[] Pack(SKBitmap bitmap, float threshold)
		{
			int w = bitmap.Width;
			int h = bitmap.Height;
			SKColor[] pixels = bitmap.Pixels;

			// Grayscale (0..1)
			var gray = new float[w * h];
			for (int i = 0; i < gray.Length; i++)
			{
				SKColor c = pixels[i];
				gray[i] = ((0.299f * c.Red) + (0.587f * c.Green) + (0.114f * c.Blue)) / 255f;
			}

			// Stucki dithering kernel (divisor 42)
			int[] dx = { 1, 2, -2, -1, 0, 1, 2, -2, -1, 0, 1, 2 };
			int[] dy = { 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2 };
			int[] weight = { 8, 4, 2, 4, 8, 4, 2, 1, 2, 4, 2, 1 };

			int bytesPerRow = (w + 7 & -8) / 8;
			var result = new byte[bytesPerRow * h];

			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					float oldValue = gray[(y * w) + x];
					float newValue = oldValue < threshold ? 0f : 1f;
					float error = oldValue - newValue;

					if (newValue == 0f)
					{
						result[(y * bytesPerRow) + (x / 8)] |= (byte)(0x01 << (7 - (x % 8)));
					}

					for (int k = 0; k < weight.Length; k++)
					{
						int nx = x + dx[k];
						int ny = y + dy[k];
						if (nx >= 0 && nx < w && ny < h)
						{
							gray[(ny * w) + nx] += error * weight[k] / 42f;
						}
					}
				}
			}

			return result;
		}
	}
}