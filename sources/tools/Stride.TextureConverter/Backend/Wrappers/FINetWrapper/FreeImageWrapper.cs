// ==========================================================
// FreeImage 3 .NET wrapper
// Original FreeImage 3 functions and .NET compatible derived functions
//
// Design and implementation by
// - Jean-Philippe Goerke (jpgoerke@users.sourceforge.net)
// - Carsten Klein (cklein05@users.sourceforge.net)
//
// Contributors:
// - David Boland (davidboland@vodafone.ie)
//
// Main reference : MSDN Knowlede Base
//
// This file is part of FreeImage 3
//
// COVERED CODE IS PROVIDED UNDER THIS LICENSE ON AN "AS IS" BASIS, WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING, WITHOUT LIMITATION, WARRANTIES
// THAT THE COVERED CODE IS FREE OF DEFECTS, MERCHANTABLE, FIT FOR A PARTICULAR PURPOSE
// OR NON-INFRINGING. THE ENTIRE RISK AS TO THE QUALITY AND PERFORMANCE OF THE COVERED
// CODE IS WITH YOU. SHOULD ANY COVERED CODE PROVE DEFECTIVE IN ANY RESPECT, YOU (NOT
// THE INITIAL DEVELOPER OR ANY OTHER CONTRIBUTOR) ASSUME THE COST OF ANY NECESSARY
// SERVICING, REPAIR OR CORRECTION. THIS DISCLAIMER OF WARRANTY CONSTITUTES AN ESSENTIAL
// PART OF THIS LICENSE. NO USE OF ANY COVERED CODE IS AUTHORIZED HEREUNDER EXCEPT UNDER
// THIS DISCLAIMER.
//
// Use at your own risk!
// ==========================================================

// ==========================================================
// CVS
// $Revision: 1.19 $
// $Date: 2011/10/02 13:00:45 $
// $Id: FreeImageWrapper.cs,v 1.19 2011/10/02 13:00:45 drolon Exp $
// ==========================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FreeImageAPI.IO;
using FreeImageAPI.Metadata;

using StridePixelFormat = Stride.Graphics.PixelFormat;

namespace FreeImageAPI
{
	/// <summary>
	/// Static class importing functions from the FreeImage library
	/// and providing additional functions.
	/// </summary>
	internal static partial class FreeImage
	{
		#region Constants

		/// <summary>
		/// Array containing all 'FREE_IMAGE_MDMODEL's.
		/// </summary>
		public static readonly FREE_IMAGE_MDMODEL[] FREE_IMAGE_MDMODELS =
			(FREE_IMAGE_MDMODEL[])Enum.GetValues(typeof(FREE_IMAGE_MDMODEL));

		/// <summary>
		/// Stores handles used to read from streams.
		/// </summary>
		private static Dictionary<FIMULTIBITMAP, fi_handle> streamHandles =
			new Dictionary<FIMULTIBITMAP, fi_handle>();

		/// <summary>
		/// Version of the wrapper library.
		/// </summary>
		private static Version WrapperVersion;

		private const int DIB_RGB_COLORS = 0;
		private const int DIB_PAL_COLORS = 1;
		private const int CBM_INIT = 0x4;

		/// <summary>
		/// An uncompressed format.
		/// </summary>
		public const int BI_RGB = 0;

		/// <summary>
		/// A run-length encoded (RLE) format for bitmaps with 8 bpp. The compression format is a 2-byte
		/// format consisting of a count byte followed by a byte containing a color index.
		/// </summary>
		public const int BI_RLE8 = 1;

		/// <summary>
		/// An RLE format for bitmaps with 4 bpp. The compression format is a 2-byte format consisting
		/// of a count byte followed by two word-length color indexes.
		/// </summary>
		public const int BI_RLE4 = 2;

		/// <summary>
		/// Specifies that the bitmap is not compressed and that the color table consists of three
		/// <b>DWORD</b> color masks that specify the red, green, and blue components, respectively,
		/// of each pixel. This is valid when used with 16- and 32-bpp bitmaps.
		/// </summary>
		public const int BI_BITFIELDS = 3;

		/// <summary>
		/// <b>Windows 98/Me, Windows 2000/XP:</b> Indicates that the image is a JPEG image.
		/// </summary>
		public const int BI_JPEG = 4;

		/// <summary>
		/// <b>Windows 98/Me, Windows 2000/XP:</b> Indicates that the image is a PNG image.
		/// </summary>
		public const int BI_PNG = 5;

		#endregion

		#region General functions

		/// <summary>
		/// Returns the internal version of this FreeImage .NET wrapper.
		/// </summary>
		/// <returns>The internal version of this FreeImage .NET wrapper.</returns>
		public static Version GetWrapperVersion()
		{
			if (WrapperVersion == null)
			{
				try
				{
					object[] attributes = Assembly.GetAssembly(typeof(FreeImage))?
					.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
					if ((attributes != null) && (attributes.Length != 0))
					{
						if (attributes[0] is AssemblyFileVersionAttribute attribute)
						{
							return (WrapperVersion = new Version(attribute.Version));
						}
					}
				}
				catch
				{

				}

				WrapperVersion = new Version();
			}

			return WrapperVersion;
		}

		/// <summary>
		/// Returns the version of the native FreeImage library.
		/// </summary>
		/// <returns>The version of the native FreeImage library.</returns>
		public static Version GetNativeVersion()
		{
			return new Version(GetVersion());
		}

		/// <summary>
		/// Returns a value indicating if the FreeImage library is available or not.
		/// See remarks for further details.
		/// </summary>
		/// <returns><c>false</c> if the file is not available or out of date;
		/// <c>true</c>, otherwise.</returns>
		/// <remarks>
		/// The FreeImage.NET library is a wrapper for the native C++ library
		/// (FreeImage.dll ... dont mix ist up with this library FreeImageNet.dll).
		/// The native library <b>must</b> be either in the same folder as the program's
		/// executable or in a folder contained in the envirent variable <i>PATH</i>
		/// (for example %WINDIR%\System32).<para/>
		/// Further more must both libraries, including the program itself,
		/// be the same architecture (x86 or x64).
		/// </remarks>
		public static bool IsAvailable()
		{
			try
			{
				// Call a static fast executing function
				Version nativeVersion = new Version(GetVersion());
				Version wrapperVersion = GetWrapperVersion();
				// No exception thrown, the library seems to be present
				return
					(nativeVersion.Major > wrapperVersion.Major) ||
					((nativeVersion.Major == wrapperVersion.Major) && (nativeVersion.Minor > wrapperVersion.Minor)) ||
					((nativeVersion.Major == wrapperVersion.Major) && (nativeVersion.Minor == wrapperVersion.Minor) && (nativeVersion.Build >= wrapperVersion.Build));
			}
			catch (DllNotFoundException)
			{
				return false;
			}
			catch (EntryPointNotFoundException)
			{
				return false;
			}
			catch (BadImageFormatException)
			{
				return false;
			}
		}

		#endregion

		#region Bitmap management functions

		/// <summary>
		/// Creates a new bitmap in memory.
		/// </summary>
		/// <param name="width">Width of the new bitmap.</param>
		/// <param name="height">Height of the new bitmap.</param>
		/// <param name="bpp">Bit depth of the new Bitmap.
		/// Supported pixel depth: 1-, 4-, 8-, 16-, 24-, 32-bit per pixel for standard bitmap</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		public static FIBITMAP Allocate(int width, int height, int bpp)
		{
			return Allocate(width, height, bpp, 0, 0, 0);
		}

		/// <summary>
		/// Creates a new bitmap in memory.
		/// </summary>
		/// <param name="type">Type of the image.</param>
		/// <param name="width">Width of the new bitmap.</param>
		/// <param name="height">Height of the new bitmap.</param>
		/// <param name="bpp">Bit depth of the new Bitmap.
		/// Supported pixel depth: 1-, 4-, 8-, 16-, 24-, 32-bit per pixel for standard bitmap</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		public static FIBITMAP AllocateT(FREE_IMAGE_TYPE type, int width, int height, int bpp)
		{
			return AllocateT(type, width, height, bpp, 0, 0, 0);
		}

		/// <summary>
		/// Allocates a new image of the specified width, height and bit depth and optionally
		/// fills it with the specified color. See remarks for further details.
		/// </summary>
		/// <param name="width">Width of the new bitmap.</param>
		/// <param name="height">Height of the new bitmap.</param>
		/// <param name="bpp">Bit depth of the new bitmap.
		/// Supported pixel depth: 1-, 4-, 8-, 16-, 24-, 32-bit per pixel for standard bitmaps.</param>
		/// <param name="color">The color to fill the bitmap with or <c>null</c>.</param>
		/// <param name="options">Options to enable or disable function-features.</param>
		/// <param name="palette">The palette of the bitmap or <c>null</c>.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <remarks>
		/// This function is an extension to <see cref="Allocate"/>, which additionally supports
		/// specifying a palette to be set for the newly create image, as well as specifying a
		/// background color, the newly created image should initially be filled with.
		/// <para/>
		/// Basically, this function internally relies on function <see cref="Allocate"/>, followed by a
		/// call to <see cref="FillBackground&lt;T&gt;"/>. This is why both parameters
		/// <paramref name="color"/> and <paramref name="options"/> behave the same as it is
		/// documented for function <see cref="FillBackground&lt;T&gt;"/>.
		/// So, please refer to the documentation of <see cref="FillBackground&lt;T&gt;"/> to
		/// learn more about parameters <paramref name="color"/> and <paramref name="options"/>.
		/// <para/>
		/// The palette specified through parameter <paramref name="palette"/> is only copied to the
		/// newly created image, if the desired bit depth is smaller than or equal to 8 bits per pixel.
		/// In other words, the <paramref name="palette"/> parameter is only taken into account for
		/// palletized images. So, for an 8-bit image, the length is 256, for an 4-bit image it is 16
		/// and it is 2 for a 1-bit image. In other words, this function does not support partial palettes.
		/// <para/>
		/// However, specifying a palette is not necesarily needed, even for palletized images. This
		/// function is capable of implicitly creating a palette, if <paramref name="palette"/> is <c>null</c>.
		/// If the specified background color is a greyscale value (red = green = blue) or if option
		/// <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/> is specified, a greyscale palette
		/// is created. For a 1-bit image, only if the specified background color is either black or white,
		/// a monochrome palette, consisting of black and white only is created. In any case, the darker
		/// colors are stored at the smaller palette indices.
		/// <para/>
		/// If the specified background color is not a greyscale value, or is neither black nor white
		/// for a 1-bit image, solely this specified color is injected into the otherwise black-initialized
		/// palette. For this operation, option <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/>
		/// is implicit, so the specified <paramref name="color"/> is applied to the palette entry,
		/// specified by the background color's <see cref="RGBQUAD.rgbReserved"/> field.
		/// The image is then filled with this palette index.
		/// <para/>
		/// This function returns a newly created image as function <see cref="Allocate"/> does, if both
		/// parameters <paramref name="color"/> and <paramref name="palette"/> are <c>null</c>.
		/// If only <paramref name="color"/> is <c>null</c>, the palette pointed to by
		/// parameter <paramref name="palette"/> is initially set for the new image, if a palletized
		/// image of type <see cref="FREE_IMAGE_TYPE.FIT_BITMAP"/> is created.
		/// However, in the latter case, this function returns an image, whose
		/// pixels are all initialized with zeros so, the image will be filled with the color of the
		/// first palette entry.
		/// </remarks>
		public static FIBITMAP AllocateEx(int width, int height, int bpp,
			RGBQUAD? color, FREE_IMAGE_COLOR_OPTIONS options, RGBQUAD[] palette)
		{
			return AllocateEx(width, height, bpp, color, options, palette, 0, 0, 0);
		}

		/// <summary>
		/// Allocates a new image of the specified width, height and bit depth and optionally
		/// fills it with the specified color. See remarks for further details.
		/// </summary>
		/// <param name="width">Width of the new bitmap.</param>
		/// <param name="height">Height of the new bitmap.</param>
		/// <param name="bpp">Bit depth of the new bitmap.
		/// Supported pixel depth: 1-, 4-, 8-, 16-, 24-, 32-bit per pixel for standard bitmaps.</param>
		/// <param name="color">The color to fill the bitmap with or <c>null</c>.</param>
		/// <param name="options">Options to enable or disable function-features.</param>
		/// <param name="palette">The palette of the bitmap or <c>null</c>.</param>
		/// <param name="red_mask">Red part of the color layout.
		/// eg: 0xFF0000</param>
		/// <param name="green_mask">Green part of the color layout.
		/// eg: 0x00FF00</param>
		/// <param name="blue_mask">Blue part of the color layout.
		/// eg: 0x0000FF</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <remarks>
		/// This function is an extension to <see cref="Allocate"/>, which additionally supports
		/// specifying a palette to be set for the newly create image, as well as specifying a
		/// background color, the newly created image should initially be filled with.
		/// <para/>
		/// Basically, this function internally relies on function <see cref="Allocate"/>, followed by a
		/// call to <see cref="FillBackground&lt;T&gt;"/>. This is why both parameters
		/// <paramref name="color"/> and <paramref name="options"/> behave the same as it is
		/// documented for function <see cref="FillBackground&lt;T&gt;"/>.
		/// So, please refer to the documentation of <see cref="FillBackground&lt;T&gt;"/> to
		/// learn more about parameters <paramref name="color"/> and <paramref name="options"/>.
		/// <para/>
		/// The palette specified through parameter <paramref name="palette"/> is only copied to the
		/// newly created image, if the desired bit depth is smaller than or equal to 8 bits per pixel.
		/// In other words, the <paramref name="palette"/> parameter is only taken into account for
		/// palletized images. So, for an 8-bit image, the length is 256, for an 4-bit image it is 16
		/// and it is 2 for a 1-bit image. In other words, this function does not support partial palettes.
		/// <para/>
		/// However, specifying a palette is not necesarily needed, even for palletized images. This
		/// function is capable of implicitly creating a palette, if <paramref name="palette"/> is <c>null</c>.
		/// If the specified background color is a greyscale value (red = green = blue) or if option
		/// <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/> is specified, a greyscale palette
		/// is created. For a 1-bit image, only if the specified background color is either black or white,
		/// a monochrome palette, consisting of black and white only is created. In any case, the darker
		/// colors are stored at the smaller palette indices.
		/// <para/>
		/// If the specified background color is not a greyscale value, or is neither black nor white
		/// for a 1-bit image, solely this specified color is injected into the otherwise black-initialized
		/// palette. For this operation, option <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/>
		/// is implicit, so the specified <paramref name="color"/> is applied to the palette entry,
		/// specified by the background color's <see cref="RGBQUAD.rgbReserved"/> field.
		/// The image is then filled with this palette index.
		/// <para/>
		/// This function returns a newly created image as function <see cref="Allocate"/> does, if both
		/// parameters <paramref name="color"/> and <paramref name="palette"/> are <c>null</c>.
		/// If only <paramref name="color"/> is <c>null</c>, the palette pointed to by
		/// parameter <paramref name="palette"/> is initially set for the new image, if a palletized
		/// image of type <see cref="FREE_IMAGE_TYPE.FIT_BITMAP"/> is created.
		/// However, in the latter case, this function returns an image, whose
		/// pixels are all initialized with zeros so, the image will be filled with the color of the
		/// first palette entry.
		/// </remarks>
		public static FIBITMAP AllocateEx(int width, int height, int bpp,
			RGBQUAD? color, FREE_IMAGE_COLOR_OPTIONS options, RGBQUAD[] palette,
			uint red_mask, uint green_mask, uint blue_mask)
		{
			if ((palette != null) && (bpp <= 8) && (palette.Length < (1 << bpp)))
				return FIBITMAP.Zero;

			if (color.HasValue)
			{
				GCHandle handle = new GCHandle();
				try
				{
					RGBQUAD[] buffer = [color.Value];
					handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
					return AllocateEx(width, height, bpp, handle.AddrOfPinnedObject(),
						options, palette, red_mask, green_mask, blue_mask);
				}
				finally
				{
					if (handle.IsAllocated)
						handle.Free();
				}
			}

			return AllocateEx(width, height, bpp, IntPtr.Zero,
				options, palette, red_mask, green_mask, blue_mask);
		}

		/// <summary>
		/// Allocates a new image of the specified type, width, height and bit depth and optionally
		/// fills it with the specified color. See remarks for further details.
		/// </summary>
		/// <typeparam name="T">The type of the specified color.</typeparam>
		/// <param name="type">Type of the image.</param>
		/// <param name="width">Width of the new bitmap.</param>
		/// <param name="height">Height of the new bitmap.</param>
		/// <param name="bpp">Bit depth of the new bitmap.
		/// Supported pixel depth: 1-, 4-, 8-, 16-, 24-, 32-bit per pixel for standard bitmap</param>
		/// <param name="color">The color to fill the bitmap with or <c>null</c>.</param>
		/// <param name="options">Options to enable or disable function-features.</param>
		/// <param name="palette">The palette of the bitmap or <c>null</c>.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <remarks>
		/// This function is an extension to <see cref="AllocateT"/>, which additionally supports
		/// specifying a palette to be set for the newly create image, as well as specifying a
		/// background color, the newly created image should initially be filled with.
		/// <para/>
		/// Basically, this function internally relies on function <see cref="AllocateT"/>, followed by a
		/// call to <see cref="FillBackground&lt;T&gt;"/>. This is why both parameters
		/// <paramref name="color"/> and <paramref name="options"/> behave the same as it is
		/// documented for function <see cref="FillBackground&lt;T&gt;"/>. So, please refer to the
		/// documentation of <see cref="FillBackground&lt;T&gt;"/> to learn more about parameters color and options.
		/// <para/>
		/// The palette specified through parameter palette is only copied to the newly created
		/// image, if its image type is <see cref="FREE_IMAGE_TYPE.FIT_BITMAP"/> and the desired bit
		/// depth is smaller than or equal to 8 bits per pixel. In other words, the <paramref name="palette"/>
		/// palette is only taken into account for palletized images. However, if the preceding conditions
		/// match and if <paramref name="palette"/> is not <c>null</c>, the palette is assumed to be at
		/// least as large as the size of a fully populated palette for the desired bit depth.
		/// So, for an 8-bit image, this length is 256, for an 4-bit image it is 16 and it is
		/// 2 for a 1-bit image. In other words, this function does not support partial palettes.
		/// <para/>
		/// However, specifying a palette is not necesarily needed, even for palletized images. This
		/// function is capable of implicitly creating a palette, if <paramref name="palette"/> is <c>null</c>.
		/// If the specified background color is a greyscale value (red = green = blue) or if option
		/// <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/> is specified, a greyscale palette
		/// is created. For a 1-bit image, only if the specified background color is either black or white,
		/// a monochrome palette, consisting of black and white only is created. In any case, the darker
		/// colors are stored at the smaller palette indices.
		/// <para/>
		/// If the specified background color is not a greyscale value, or is neither black nor white
		/// for a 1-bit image, solely this specified color is injected into the otherwise black-initialized
		/// palette. For this operation, option <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/>
		/// is implicit, so the specified color is applied to the palette entry, specified by the
		/// background color's <see cref="RGBQUAD.rgbReserved"/> field. The image is then filled with
		/// this palette index.
		/// <para/>
		/// This function returns a newly created image as function <see cref="AllocateT"/> does, if both
		/// parameters <paramref name="color"/> and <paramref name="palette"/> are <c>null</c>.
		/// If only <paramref name="color"/> is <c>null</c>, the palette pointed to by
		/// parameter <paramref name="palette"/> is initially set for the new image, if a palletized
		/// image of type <see cref="FREE_IMAGE_TYPE.FIT_BITMAP"/> is created.
		/// However, in the latter case, this function returns an image, whose
		/// pixels are all initialized with zeros so, the image will be filled with the color of the
		/// first palette entry.
		/// </remarks>
		public static FIBITMAP AllocateExT<T>(FREE_IMAGE_TYPE type, int width, int height, int bpp,
			T? color, FREE_IMAGE_COLOR_OPTIONS options, RGBQUAD[] palette) where T : struct
		{
			return AllocateExT(type, width, height, bpp, color, options, palette, 0, 0, 0);
		}

		/// <summary>
		/// Allocates a new image of the specified type, width, height and bit depth and optionally
		/// fills it with the specified color. See remarks for further details.
		/// </summary>
		/// <typeparam name="T">The type of the specified color.</typeparam>
		/// <param name="type">Type of the image.</param>
		/// <param name="width">Width of the new bitmap.</param>
		/// <param name="height">Height of the new bitmap.</param>
		/// <param name="bpp">Bit depth of the new bitmap.
		/// Supported pixel depth: 1-, 4-, 8-, 16-, 24-, 32-bit per pixel for standard bitmap</param>
		/// <param name="color">The color to fill the bitmap with or <c>null</c>.</param>
		/// <param name="options">Options to enable or disable function-features.</param>
		/// <param name="palette">The palette of the bitmap or <c>null</c>.</param>
		/// <param name="red_mask">Red part of the color layout.
		/// eg: 0xFF0000</param>
		/// <param name="green_mask">Green part of the color layout.
		/// eg: 0x00FF00</param>
		/// <param name="blue_mask">Blue part of the color layout.
		/// eg: 0x0000FF</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <remarks>
		/// This function is an extension to <see cref="AllocateT"/>, which additionally supports
		/// specifying a palette to be set for the newly create image, as well as specifying a
		/// background color, the newly created image should initially be filled with.
		/// <para/>
		/// Basically, this function internally relies on function <see cref="AllocateT"/>, followed by a
		/// call to <see cref="FillBackground&lt;T&gt;"/>. This is why both parameters
		/// <paramref name="color"/> and <paramref name="options"/> behave the same as it is
		/// documented for function <see cref="FillBackground&lt;T&gt;"/>. So, please refer to the
		/// documentation of <see cref="FillBackground&lt;T&gt;"/> to learn more about parameters color and options.
		/// <para/>
		/// The palette specified through parameter palette is only copied to the newly created
		/// image, if its image type is <see cref="FREE_IMAGE_TYPE.FIT_BITMAP"/> and the desired bit
		/// depth is smaller than or equal to 8 bits per pixel. In other words, the <paramref name="palette"/>
		/// palette is only taken into account for palletized images. However, if the preceding conditions
		/// match and if <paramref name="palette"/> is not <c>null</c>, the palette is assumed to be at
		/// least as large as the size of a fully populated palette for the desired bit depth.
		/// So, for an 8-bit image, this length is 256, for an 4-bit image it is 16 and it is
		/// 2 for a 1-bit image. In other words, this function does not support partial palettes.
		/// <para/>
		/// However, specifying a palette is not necesarily needed, even for palletized images. This
		/// function is capable of implicitly creating a palette, if <paramref name="palette"/> is <c>null</c>.
		/// If the specified background color is a greyscale value (red = green = blue) or if option
		/// <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/> is specified, a greyscale palette
		/// is created. For a 1-bit image, only if the specified background color is either black or white,
		/// a monochrome palette, consisting of black and white only is created. In any case, the darker
		/// colors are stored at the smaller palette indices.
		/// <para/>
		/// If the specified background color is not a greyscale value, or is neither black nor white
		/// for a 1-bit image, solely this specified color is injected into the otherwise black-initialized
		/// palette. For this operation, option <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/>
		/// is implicit, so the specified color is applied to the palette entry, specified by the
		/// background color's <see cref="RGBQUAD.rgbReserved"/> field. The image is then filled with
		/// this palette index.
		/// <para/>
		/// This function returns a newly created image as function <see cref="AllocateT"/> does, if both
		/// parameters <paramref name="color"/> and <paramref name="palette"/> are <c>null</c>.
		/// If only <paramref name="color"/> is <c>null</c>, the palette pointed to by
		/// parameter <paramref name="palette"/> is initially set for the new image, if a palletized
		/// image of type <see cref="FREE_IMAGE_TYPE.FIT_BITMAP"/> is created.
		/// However, in the latter case, this function returns an image, whose
		/// pixels are all initialized with zeros so, the image will be filled with the color of the
		/// first palette entry.
		/// </remarks>
		public static FIBITMAP AllocateExT<T>(FREE_IMAGE_TYPE type, int width, int height, int bpp,
			T? color, FREE_IMAGE_COLOR_OPTIONS options, RGBQUAD[] palette,
			uint red_mask, uint green_mask, uint blue_mask) where T : struct
		{
			if ((palette != null) && (bpp <= 8) && (palette.Length < (1 << bpp)))
				return FIBITMAP.Zero;

			if (color.HasValue)
			{
				if (!CheckColorType(type, color.Value))
					return FIBITMAP.Zero;

				GCHandle handle = new GCHandle();
				try
				{
					T[] buffer = { color.Value };
					handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
					return AllocateExT(type, width, height, bpp, handle.AddrOfPinnedObject(),
						options, palette, red_mask, green_mask, blue_mask);
				}
				finally
				{
					if (handle.IsAllocated)
						handle.Free();
				}
			}

			return AllocateExT(type, width, height, bpp, IntPtr.Zero,
				options, palette, red_mask, green_mask, blue_mask);
		}

		/// <summary>
		/// Converts a raw bitmap to a FreeImage bitmap.
		/// </summary>
		/// <param name="bits">Array of bytes containing the raw bitmap.</param>
		/// <param name="type">The type of the raw bitmap.</param>
		/// <param name="width">The width in pixels of the raw bitmap.</param>
		/// <param name="height">The height in pixels of the raw bitmap.</param>
		/// <param name="pitch">Defines the total width of a scanline in the raw bitmap,
		/// including padding bytes.</param>
		/// <param name="bpp">The bit depth (bits per pixel) of the raw bitmap.</param>
		/// <param name="redMask">The bit mask describing the bits used to store a single
		/// pixel's red component in the raw bitmap. This is only applied to 16-bpp raw bitmaps.</param>
		/// <param name="greenMask">The bit mask describing the bits used to store a single
		/// pixel's green component in the raw bitmap. This is only applied to 16-bpp raw bitmaps.</param>
		/// <param name="blueMask">The bit mask describing the bits used to store a single
		/// pixel's blue component in the raw bitmap. This is only applied to 16-bpp raw bitmaps.</param>
		/// <param name="topdown">If true, the raw bitmap is stored in top-down order (top-left pixel first)
		/// and in bottom-up order (bottom-left pixel first) otherwise.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		public static unsafe FIBITMAP ConvertFromRawBits(
			byte[] bits,
			FREE_IMAGE_TYPE type,
			int width,
			int height,
			int pitch,
			uint bpp,
			uint redMask,
			uint greenMask,
			uint blueMask,
			bool topdown)
		{
			fixed (byte* ptr = bits)
			{
				return ConvertFromRawBits(
					(IntPtr)ptr,
					type,
					width,
					height,
					pitch,
					bpp,
					redMask,
					greenMask,
					blueMask,
					topdown);
			}
		}

		/// <summary>
		/// Converts a raw bitmap to a FreeImage bitmap.
		/// </summary>
		/// <param name="bits">Pointer to the memory block containing the raw bitmap.</param>
		/// <param name="type">The type of the raw bitmap.</param>
		/// <param name="width">The width in pixels of the raw bitmap.</param>
		/// <param name="height">The height in pixels of the raw bitmap.</param>
		/// <param name="pitch">Defines the total width of a scanline in the raw bitmap,
		/// including padding bytes.</param>
		/// <param name="bpp">The bit depth (bits per pixel) of the raw bitmap.</param>
		/// <param name="redMask">The bit mask describing the bits used to store a single
		/// pixel's red component in the raw bitmap. This is only applied to 16-bpp raw bitmaps.</param>
		/// <param name="greenMask">The bit mask describing the bits used to store a single
		/// pixel's green component in the raw bitmap. This is only applied to 16-bpp raw bitmaps.</param>
		/// <param name="blueMask">The bit mask describing the bits used to store a single
		/// pixel's blue component in the raw bitmap. This is only applied to 16-bpp raw bitmaps.</param>
		/// <param name="topdown">If true, the raw bitmap is stored in top-down order (top-left pixel first)
		/// and in bottom-up order (bottom-left pixel first) otherwise.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		public static unsafe FIBITMAP ConvertFromRawBits(
			IntPtr bits,
			FREE_IMAGE_TYPE type,
			int width,
			int height,
			int pitch,
			uint bpp,
			uint redMask,
			uint greenMask,
			uint blueMask,
			bool topdown)
		{
			byte* addr = (byte*)bits;
			if ((addr == null) || (width <= 0) || (height <= 0))
			{
				return FIBITMAP.Zero;
			}

			FIBITMAP dib = AllocateT(type, width, height, (int)bpp, redMask, greenMask, blueMask);
			if (dib != FIBITMAP.Zero)
			{
				if (topdown)
				{
					for (int i = height - 1; i >= 0; --i)
					{
						ref byte dst = ref Unsafe.AsRef<byte>((byte*) GetScanLine(dib, i));
						ref byte src = ref Unsafe.AsRef<byte>(addr);

						Unsafe.CopyBlockUnaligned(ref dst, ref src, GetLine(dib));

						addr += pitch;
					}
				}
				else
				{
					for (int i = 0; i < height; ++i)
					{
						ref byte dst = ref Unsafe.AsRef<byte>((byte*) GetScanLine(dib, i));
						ref byte src = ref Unsafe.AsRef<byte>(addr);

						Unsafe.CopyBlockUnaligned(ref dst, ref src, GetLine(dib));

						addr += pitch;
					}
				}
			}
			return dib;
		}

		/// <summary>
		/// Loads a FreeImage bitmap.
		/// The file will be loaded with default loading flags.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists.</exception>
		public static FIBITMAP LoadEx(string filename)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return LoadEx(filename, FREE_IMAGE_LOAD_FLAGS.DEFAULT, ref format);
		}

		/// <summary>
		/// Loads a FreeImage bitmap.
		/// Load flags can be provided by the flags parameter.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists.</exception>
		public static FIBITMAP LoadEx(string filename, FREE_IMAGE_LOAD_FLAGS flags)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return LoadEx(filename, flags, ref format);
		}

		/// <summary>
		/// Loads a FreeImage bitmap.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> the files
		/// real format is being analysed. If no plugin can read the file, format remains
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> and 0 is returned.
		/// The file will be loaded with default loading flags.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <param name="format">Format of the image. If the format is unknown use
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.
		/// In case a suitable format was found by LoadEx it will be returned in format.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists.</exception>
		public static FIBITMAP LoadEx(string filename, ref FREE_IMAGE_FORMAT format)
		{
			return LoadEx(filename, FREE_IMAGE_LOAD_FLAGS.DEFAULT, ref format);
		}

		/// <summary>
		/// Loads a FreeImage bitmap.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> the files
		/// real format is being analysed. If no plugin can read the file, format remains
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> and 0 is returned.
		/// Load flags can be provided by the flags parameter.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="format">Format of the image. If the format is unknown use
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.
		/// In case a suitable format was found by LoadEx it will be returned in format.
		/// </param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists.</exception>
		public static FIBITMAP LoadEx(string filename, FREE_IMAGE_LOAD_FLAGS flags, ref FREE_IMAGE_FORMAT format)
		{
			// check that the file exists
			if (!File.Exists(filename))
			{
				throw new FileNotFoundException(filename + " could not be found.");
			}

			// try to find the file format by querying FreeImage plugins
			if (format == FREE_IMAGE_FORMAT.FIF_UNKNOWN)
			{
				format = GetFileType(filename, 0);
			}
			if (format == FREE_IMAGE_FORMAT.FIF_UNKNOWN) // fall-back try to identify the file format using the extension if still unknown
			{
				format = GetFileTypeFromExtension(filename);
			}

			// load the file if the format is supported
			var dib = new FIBITMAP();
			if (FIFSupportsReading(format))
			{
				dib = Load(format, filename, flags);
			}
			return dib;
		}

		private static FREE_IMAGE_FORMAT GetFileTypeFromExtension(string filename)
		{
			if(string.IsNullOrEmpty(filename) || !filename.Contains("."))
				return FREE_IMAGE_FORMAT.FIF_UNKNOWN;

			var extention = filename.Substring(filename.LastIndexOf('.'));
			return extention switch
			{
				".tga" => FREE_IMAGE_FORMAT.FIF_TARGA,
				".png" => FREE_IMAGE_FORMAT.FIF_PNG,
				".bmp" => FREE_IMAGE_FORMAT.FIF_BMP,
				".tiff" or ".tif" => FREE_IMAGE_FORMAT.FIF_TIFF,
				".jpg" or ".jpeg" => FREE_IMAGE_FORMAT.FIF_JPEG,
				_ => FREE_IMAGE_FORMAT.FIF_UNKNOWN,// Note: other format met so far seems to be properly handled by "GetFileType".
												   //  -> no need to add the extension/format association here.
			};
		}

		/// <summary>
		/// Deletes a previously loaded FreeImage bitmap from memory and resets the handle to 0.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		public static void UnloadEx(ref FIBITMAP dib)
		{
			if (!dib.IsNull)
			{
				Unload(dib);
				dib.SetNull();
			}
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a file.
		/// The format is taken off the filename.
		/// If no suitable format was found false will be returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="filename">The complete name of the file to save to.
		/// The extension will be corrected if it is no valid extension for the
		/// selected format or if no extension was specified.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="filename"/> is null.</exception>
		public static bool SaveEx(FIBITMAP dib, string filename)
		{
			return SaveEx(
				ref dib,
				filename,
				FREE_IMAGE_FORMAT.FIF_UNKNOWN,
				FREE_IMAGE_SAVE_FLAGS.DEFAULT,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				false);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a file.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>
		/// the format is taken off the filename.
		/// If no suitable format was found false will be returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="filename">The complete name of the file to save to.
		/// The extension will be corrected if it is no valid extension for the
		/// selected format or if no extension was specified.</param>
		/// <param name="format">Format of the image. If the format should be taken from the
		/// filename use <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="filename"/> is null.</exception>
		public static bool SaveEx(
			FIBITMAP dib,
			string filename,
			FREE_IMAGE_FORMAT format)
		{
			return SaveEx(
				ref dib,
				filename,
				format,
				FREE_IMAGE_SAVE_FLAGS.DEFAULT,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				false);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a file.
		/// The format is taken off the filename.
		/// If no suitable format was found false will be returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="filename">The complete name of the file to save to.
		/// The extension will be corrected if it is no valid extension for the
		/// selected format or if no extension was specified.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.
		/// If the function failed and returned false, the bitmap was not unloaded.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="filename"/> is null.</exception>
		public static bool SaveEx(
			ref FIBITMAP dib,
			string filename,
			bool unloadSource)
		{
			return SaveEx(
				ref dib,
				filename,
				FREE_IMAGE_FORMAT.FIF_UNKNOWN,
				FREE_IMAGE_SAVE_FLAGS.DEFAULT,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				unloadSource);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a file.
		/// The format is taken off the filename.
		/// If no suitable format was found false will be returned.
		/// Save flags can be provided by the flags parameter.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="filename">The complete name of the file to save to.
		/// The extension will be corrected if it is no valid extension for the
		/// selected format or if no extension was specified</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="filename"/> is null.</exception>
		public static bool SaveEx(
			FIBITMAP dib,
			string filename,
			FREE_IMAGE_SAVE_FLAGS flags)
		{
			return SaveEx(
				ref dib,
				filename,
				FREE_IMAGE_FORMAT.FIF_UNKNOWN,
				flags,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				false);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a file.
		/// The format is taken off the filename.
		/// If no suitable format was found false will be returned.
		/// Save flags can be provided by the flags parameter.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="filename">The complete name of the file to save to.
		/// The extension will be corrected if it is no valid extension for the
		/// selected format or if no extension was specified.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.
		/// If the function failed and returned false, the bitmap was not unloaded.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="filename"/> is null.</exception>
		public static bool SaveEx(
			ref FIBITMAP dib,
			string filename,
			FREE_IMAGE_SAVE_FLAGS flags,
			bool unloadSource)
		{
			return SaveEx(
				ref dib,
				filename,
				FREE_IMAGE_FORMAT.FIF_UNKNOWN,
				flags,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				unloadSource);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a file.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>
		/// the format is taken off the filename.
		/// If no suitable format was found false will be returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="filename">The complete name of the file to save to.
		/// The extension will be corrected if it is no valid extension for the
		/// selected format or if no extension was specified.</param>
		/// <param name="format">Format of the image. If the format should be taken from the
		/// filename use <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.
		/// If the function failed and returned false, the bitmap was not unloaded.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="filename"/> is null.</exception>
		public static bool SaveEx(
			ref FIBITMAP dib,
			string filename,
			FREE_IMAGE_FORMAT format,
			bool unloadSource)
		{
			return SaveEx(
				ref dib,
				filename,
				format,
				FREE_IMAGE_SAVE_FLAGS.DEFAULT,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				unloadSource);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a file.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>
		/// the format is taken off the filename.
		/// If no suitable format was found false will be returned.
		/// Save flags can be provided by the flags parameter.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="filename">The complete name of the file to save to.
		/// The extension will be corrected if it is no valid extension for the
		/// selected format or if no extension was specified.</param>
		/// <param name="format">Format of the image. If the format should be taken from the
		/// filename use <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="filename"/> is null.</exception>
		public static bool SaveEx(
			FIBITMAP dib,
			string filename,
			FREE_IMAGE_FORMAT format,
			FREE_IMAGE_SAVE_FLAGS flags)
		{
			return SaveEx(
				ref dib,
				filename,
				format,
				flags,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				false);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a file.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>
		/// the format is taken off the filename.
		/// If no suitable format was found false will be returned.
		/// Save flags can be provided by the flags parameter.
		/// The bitmaps color depth can be set by 'colorDepth'.
		/// If set to <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_AUTO"/> a suitable color depth
		/// will be taken if available.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="filename">The complete name of the file to save to.
		/// The extension will be corrected if it is no valid extension for the
		/// selected format or if no extension was specified.</param>
		/// <param name="format">Format of the image. If the format should be taken from the
		/// filename use <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="colorDepth">The new color depth of the bitmap.
		/// Set to <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_AUTO"/> if Save should take the
		/// best suitable color depth.
		/// If a color depth is selected that the provided format cannot write an
		/// error-message will be thrown.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.
		/// If the function failed and returned false, the bitmap was not unloaded.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentException">
		/// A direct color conversion failed.</exception>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="filename"/> is null.</exception>
		public static bool SaveEx(
			ref FIBITMAP dib,
			string filename,
			FREE_IMAGE_FORMAT format,
			FREE_IMAGE_SAVE_FLAGS flags,
			FREE_IMAGE_COLOR_DEPTH colorDepth,
			bool unloadSource)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			if (filename == null)
			{
				throw new ArgumentNullException("filename");
			}
			bool result = false;
			// Gets format from filename if the format is unknown
			if (format == FREE_IMAGE_FORMAT.FIF_UNKNOWN)
			{
				format = GetFIFFromFilename(filename);
			}
			if (format != FREE_IMAGE_FORMAT.FIF_UNKNOWN)
			{
				// Checks writing support
				if (FIFSupportsWriting(format) && FIFSupportsExportType(format, GetImageType(dib)))
				{
					// Check valid filename and correct it if needed
					if (!IsFilenameValidForFIF(format, filename))
					{
						string extension = GetPrimaryExtensionFromFIF(format);
						filename = Path.ChangeExtension(filename, extension);
					}

					FIBITMAP dibToSave = PrepareBitmapColorDepth(dib, format, colorDepth);
					try
					{
						result = Save(format, dibToSave, filename, flags);
					}
					finally
					{
						// Always unload a temporary created bitmap.
						if (dibToSave != dib)
						{
							UnloadEx(ref dibToSave);
						}
						// On success unload the bitmap
						if (result && unloadSource)
						{
							UnloadEx(ref dib);
						}
					}
				}
			}
			return result;
		}

		/// <summary>
		/// Loads a FreeImage bitmap.
		/// The stream must be set to the correct position before calling LoadFromStream.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> is not capable of reading.</exception>
		public static FIBITMAP LoadFromStream(Stream stream)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return LoadFromStream(stream, FREE_IMAGE_LOAD_FLAGS.DEFAULT, ref format);
		}

		/// <summary>
		/// Loads a FreeImage bitmap.
		/// The stream must be set to the correct position before calling LoadFromStream.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> is not capable of reading.</exception>
		public static FIBITMAP LoadFromStream(Stream stream, FREE_IMAGE_LOAD_FLAGS flags)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return LoadFromStream(stream, flags, ref format);
		}

		/// <summary>
		/// Loads a FreeImage bitmap.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> the
		/// bitmaps real format is being analysed.
		/// The stream must be set to the correct position before calling LoadFromStream.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="format">Format of the image. If the format is unknown use
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.
		/// In case a suitable format was found by LoadFromStream it will be returned in format.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> is not capable of reading.</exception>
		public static FIBITMAP LoadFromStream(Stream stream, ref FREE_IMAGE_FORMAT format)
		{
			return LoadFromStream(stream, FREE_IMAGE_LOAD_FLAGS.DEFAULT, ref format);
		}

		/// <summary>
		/// Loads a FreeImage bitmap.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>
		/// the bitmaps real format is being analysed.
		/// The stream must be set to the correct position before calling LoadFromStream.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="format">Format of the image. If the format is unknown use
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.
		/// In case a suitable format was found by LoadFromStream it will be returned in format.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> is not capable of reading.</exception>
		public static FIBITMAP LoadFromStream(
			Stream stream,
			FREE_IMAGE_LOAD_FLAGS flags,
			ref FREE_IMAGE_FORMAT format)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			if (!stream.CanRead)
			{
				throw new ArgumentException("stream is not capable of reading.");
			}
			// Wrap the source stream if it is unable to seek (which is required by FreeImage)
			stream = (stream.CanSeek) ? stream : new StreamWrapper(stream, true);

			stream.Position = 0L;
			if (format == FREE_IMAGE_FORMAT.FIF_UNKNOWN)
			{
				// Get the format of the bitmap
				format = GetFileTypeFromStream(stream);
				// Restore the streams position
				stream.Position = 0L;
			}
			if (!FIFSupportsReading(format))
			{
				return FIBITMAP.Zero;
			}
			// Create a 'FreeImageIO' structure for calling 'LoadFromHandle'
			// using the internal structure 'FreeImageStreamIO'.
			FreeImageIO io = FreeImageStreamIO.io;
			using fi_handle handle = new fi_handle(stream);
			return LoadFromHandle(format, ref io, handle, flags);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a stream.
		/// The stream must be set to the correct position before calling SaveToStream.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="format">Format of the image.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> cannot write.</exception>
		public static bool SaveToStream(
			FIBITMAP dib,
			Stream stream,
			FREE_IMAGE_FORMAT format)
		{
			return SaveToStream(
				ref dib,
				stream,
				format,
				FREE_IMAGE_SAVE_FLAGS.DEFAULT,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				false);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a stream.
		/// The stream must be set to the correct position before calling SaveToStream.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="format">Format of the image.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> cannot write.</exception>
		public static bool SaveToStream(
			ref FIBITMAP dib,
			Stream stream,
			FREE_IMAGE_FORMAT format,
			bool unloadSource)
		{
			return SaveToStream(
				ref dib,
				stream,
				format,
				FREE_IMAGE_SAVE_FLAGS.DEFAULT,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				unloadSource);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a stream.
		/// The stream must be set to the correct position before calling SaveToStream.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="format">Format of the image.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> cannot write.</exception>
		public static bool SaveToStream(
			FIBITMAP dib,
			Stream stream,
			FREE_IMAGE_FORMAT format,
			FREE_IMAGE_SAVE_FLAGS flags)
		{
			return SaveToStream(
				ref dib,
				stream,
				format,
				flags,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				false);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a stream.
		/// The stream must be set to the correct position before calling SaveToStream.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="format">Format of the image.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> cannot write.</exception>
		public static bool SaveToStream(
			ref FIBITMAP dib,
			Stream stream,
			FREE_IMAGE_FORMAT format,
			FREE_IMAGE_SAVE_FLAGS flags,
			bool unloadSource)
		{
			return SaveToStream(
				ref dib, stream,
				format,
				flags,
				FREE_IMAGE_COLOR_DEPTH.FICD_AUTO,
				unloadSource);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a stream.
		/// The stream must be set to the correct position before calling SaveToStream.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="format">Format of the image.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="colorDepth">The new color depth of the bitmap.
		/// Set to <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_AUTO"/> if SaveToStream should
		/// take the best suitable color depth.
		/// If a color depth is selected that the provided format cannot write an
		/// error-message will be thrown.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> cannot write.</exception>
		public static bool SaveToStream(
			FIBITMAP dib,
			Stream stream,
			FREE_IMAGE_FORMAT format,
			FREE_IMAGE_SAVE_FLAGS flags,
			FREE_IMAGE_COLOR_DEPTH colorDepth)
		{
			return SaveToStream(
				ref dib,
				stream,
				format,
				flags,
				colorDepth,
				false);
		}

		/// <summary>
		/// Saves a previously loaded FreeImage bitmap to a stream.
		/// The stream must be set to the correct position before calling SaveToStream.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="format">Format of the image.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="colorDepth">The new color depth of the bitmap.
		/// Set to <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_AUTO"/> if SaveToStream should
		/// take the best suitable color depth.
		/// If a color depth is selected that the provided format cannot write an
		/// error-message will be thrown.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> cannot write.</exception>
		public static bool SaveToStream(
			ref FIBITMAP dib,
			Stream stream,
			FREE_IMAGE_FORMAT format,
			FREE_IMAGE_SAVE_FLAGS flags,
			FREE_IMAGE_COLOR_DEPTH colorDepth,
			bool unloadSource)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			if (!stream.CanWrite)
			{
				throw new ArgumentException("stream is not capable of writing.");
			}
			if ((!FIFSupportsWriting(format)) || (!FIFSupportsExportType(format, GetImageType(dib))))
			{
				return false;
			}

			FIBITMAP dibToSave = PrepareBitmapColorDepth(dib, format, colorDepth);
			bool result = false;

			try
			{
				// Create a 'FreeImageIO' structure for calling 'SaveToHandle'
				FreeImageIO io = FreeImageStreamIO.io;

				using fi_handle handle = new fi_handle(stream);
				result = SaveToHandle(format, dibToSave, ref io, handle, flags);
			}
			finally
			{
				// Always unload a temporary created bitmap.
				if (dibToSave != dib)
				{
					UnloadEx(ref dibToSave);
				}
				// On success unload the bitmap
				if (result && unloadSource)
				{
					UnloadEx(ref dib);
				}
			}

			return result;
		}

		#endregion

		#region Plugin functions

		/// <summary>
		/// Checks if an extension is valid for a certain format.
		/// </summary>
		/// <param name="fif">The desired format.</param>
		/// <param name="extension">The desired extension.</param>
		/// <returns>True if the extension is valid for the given format, false otherwise.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="extension"/> is null.</exception>
		public static bool IsExtensionValidForFIF(FREE_IMAGE_FORMAT fif, string extension)
		{
			return IsExtensionValidForFIF(fif, extension, StringComparison.CurrentCultureIgnoreCase);
		}

		/// <summary>
		/// Checks if an extension is valid for a certain format.
		/// </summary>
		/// <param name="fif">The desired format.</param>
		/// <param name="extension">The desired extension.</param>
		/// <param name="comparisonType">The string comparison type.</param>
		/// <returns>True if the extension is valid for the given format, false otherwise.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="extension"/> is null.</exception>
		public static bool IsExtensionValidForFIF(FREE_IMAGE_FORMAT fif, string extension, StringComparison comparisonType)
		{
			if (extension == null)
			{
				throw new ArgumentNullException("extension");
			}
			bool result = false;
			// Split up the string and compare each with the given extension
			string tempList = GetFIFExtensionList(fif);
			if (tempList != null)
			{
				string[] extensionList = tempList.Split(',');
				foreach (string ext in extensionList)
				{
					if (extension.Equals(ext, comparisonType))
					{
						result = true;
						break;
					}
				}
			}
			return result;
		}

		/// <summary>
		/// Checks if a filename is valid for a certain format.
		/// </summary>
		/// <param name="fif">The desired format.</param>
		/// <param name="filename">The desired filename.</param>
		/// <returns>True if the filename is valid for the given format, false otherwise.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="filename"/> is null.</exception>
		public static bool IsFilenameValidForFIF(FREE_IMAGE_FORMAT fif, string filename)
		{
			return IsFilenameValidForFIF(fif, filename, StringComparison.CurrentCultureIgnoreCase);
		}

		/// <summary>
		/// Checks if a filename is valid for a certain format.
		/// </summary>
		/// <param name="fif">The desired format.</param>
		/// <param name="filename">The desired filename.</param>
		/// <param name="comparisonType">The string comparison type.</param>
		/// <returns>True if the filename is valid for the given format, false otherwise.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="filename"/> is null.</exception>
		public static bool IsFilenameValidForFIF(FREE_IMAGE_FORMAT fif, string filename, StringComparison comparisonType)
		{
			if (filename == null)
			{
				throw new ArgumentNullException("filename");
			}
			bool result = false;
			// Extract the filenames extension if it exists
			string extension = Path.GetExtension(filename);
			if (extension.Length != 0)
			{
				extension = extension.Remove(0, 1);
				result = IsExtensionValidForFIF(fif, extension, comparisonType);
			}
			return result;
		}

		/// <summary>
		/// This function returns the primary (main or most commonly used?) extension of a certain
		/// image format (fif). This is done by returning the first of all possible extensions
		/// returned by GetFIFExtensionList().
		/// That assumes, that the plugin returns the extensions in ordered form.</summary>
		/// <param name="fif">The image format to obtain the primary extension for.</param>
		/// <returns>The primary extension of the specified image format.</returns>
		public static string GetPrimaryExtensionFromFIF(FREE_IMAGE_FORMAT fif)
		{
			string result = null;
			string extensions = GetFIFExtensionList(fif);
			if (extensions != null)
			{
				int position = extensions.IndexOf(',');
				if (position < 0)
				{
					result = extensions;
				}
				else
				{
					result = extensions.Substring(0, position);
				}
			}
			return result;
		}

		#endregion

		#region Multipage functions

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists while opening.</exception>
		public static FIMULTIBITMAP OpenMultiBitmapEx(string filename)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return OpenMultiBitmapEx(
				filename,
				ref format,
				FREE_IMAGE_LOAD_FLAGS.DEFAULT,
				false,
				false,
				false);
		}

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <param name="keep_cache_in_memory">When true performance is increased at the cost of memory.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists while opening.</exception>
		public static FIMULTIBITMAP OpenMultiBitmapEx(string filename, bool keep_cache_in_memory)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return OpenMultiBitmapEx(
				filename,
				ref format,
				FREE_IMAGE_LOAD_FLAGS.DEFAULT,
				false,
				false,
				keep_cache_in_memory);
		}

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <param name="read_only">When true the bitmap will be loaded read only.</param>
		/// <param name="keep_cache_in_memory">When true performance is increased at the cost of memory.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists while opening.</exception>
		public static FIMULTIBITMAP OpenMultiBitmapEx(
			string filename,
			bool read_only,
			bool keep_cache_in_memory)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return OpenMultiBitmapEx(
				filename,
				ref format,
				FREE_IMAGE_LOAD_FLAGS.DEFAULT,
				false,
				read_only,
				keep_cache_in_memory);
		}

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <param name="create_new">When true a new bitmap is created.</param>
		/// <param name="read_only">When true the bitmap will be loaded read only.</param>
		/// <param name="keep_cache_in_memory">When true performance is increased at the cost of memory.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists while opening.</exception>
		public static FIMULTIBITMAP OpenMultiBitmapEx(
			string filename,
			bool create_new,
			bool read_only,
			bool keep_cache_in_memory)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return OpenMultiBitmapEx(
				filename,
				ref format,
				FREE_IMAGE_LOAD_FLAGS.DEFAULT,
				create_new,
				read_only,
				keep_cache_in_memory);
		}

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> the files real
		/// format is being analysed. If no plugin can read the file, format remains
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> and 0 is returned.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <param name="format">Format of the image. If the format is unknown use
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.
		/// In case a suitable format was found by LoadEx it will be returned in format.</param>
		/// <param name="create_new">When true a new bitmap is created.</param>
		/// <param name="read_only">When true the bitmap will be loaded read only.</param>
		/// <param name="keep_cache_in_memory">When true performance is increased at the cost of memory.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists while opening.</exception>
		public static FIMULTIBITMAP OpenMultiBitmapEx(
			string filename,
			ref FREE_IMAGE_FORMAT format,
			bool create_new,
			bool read_only,
			bool keep_cache_in_memory)
		{
			return OpenMultiBitmapEx(
				filename,
				ref format,
				FREE_IMAGE_LOAD_FLAGS.DEFAULT,
				create_new,
				read_only,
				keep_cache_in_memory);
		}

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> the files
		/// real format is being analysed. If no plugin can read the file, format remains
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> and 0 is returned.
		/// Load flags can be provided by the flags parameter.
		/// </summary>
		/// <param name="filename">The complete name of the file to load.</param>
		/// <param name="format">Format of the image. If the format is unknown use
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/>.
		/// In case a suitable format was found by LoadEx it will be returned in format.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="create_new">When true a new bitmap is created.</param>
		/// <param name="read_only">When true the bitmap will be loaded read only.</param>
		/// <param name="keep_cache_in_memory">When true performance is increased at the cost of memory.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		/// <exception cref="FileNotFoundException">
		/// <paramref name="filename"/> does not exists while opening.</exception>
		public static FIMULTIBITMAP OpenMultiBitmapEx(
			string filename,
			ref FREE_IMAGE_FORMAT format,
			FREE_IMAGE_LOAD_FLAGS flags,
			bool create_new,
			bool read_only,
			bool keep_cache_in_memory)
		{
			if (!File.Exists(filename) && !create_new)
			{
				throw new FileNotFoundException(filename + " could not be found.");
			}
			if (format == FREE_IMAGE_FORMAT.FIF_UNKNOWN)
			{
				// Check if a plugin can read the data
				format = GetFileType(filename, 0);
			}
			FIMULTIBITMAP dib = new FIMULTIBITMAP();
			if (FIFSupportsReading(format))
			{
				dib = OpenMultiBitmap(format, filename, create_new, read_only, keep_cache_in_memory, flags);
			}
			return dib;
		}

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap.
		/// </summary>
		/// <param name="stream">The stream to load the bitmap from.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		public static FIMULTIBITMAP OpenMultiBitmapFromStream(Stream stream)
		{
			FREE_IMAGE_FORMAT format = FREE_IMAGE_FORMAT.FIF_UNKNOWN;
			return OpenMultiBitmapFromStream(stream, ref format, FREE_IMAGE_LOAD_FLAGS.DEFAULT);
		}

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap.
		/// In case the loading format is <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> the files
		/// real format is being analysed. If no plugin can read the file, format remains
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/> and 0 is returned.
		/// Load flags can be provided by the flags parameter.
		/// </summary>
		/// <param name="stream">The stream to load the bitmap from.</param>
		/// <param name="format">Format of the image. If the format is unknown use
		/// <see cref="FREE_IMAGE_FORMAT.FIF_UNKNOWN"/></param>.
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		public static FIMULTIBITMAP OpenMultiBitmapFromStream(Stream stream, ref FREE_IMAGE_FORMAT format, FREE_IMAGE_LOAD_FLAGS flags)
		{
			if (stream == null)
				return FIMULTIBITMAP.Zero;

			if (!stream.CanSeek)
				stream = new StreamWrapper(stream, true);

			FIMULTIBITMAP mdib = FIMULTIBITMAP.Zero;
			FreeImageIO io = FreeImageStreamIO.io;
			fi_handle handle = new fi_handle(stream);

			try
			{
				if (format == FREE_IMAGE_FORMAT.FIF_UNKNOWN)
				{
					format = GetFileTypeFromHandle(ref io, handle, checked((int)stream.Length));
				}

				mdib = OpenMultiBitmapFromHandle(format, ref io, handle, flags);

				if (mdib.IsNull)
				{
					handle.Dispose();
				}
				else
				{
					lock (streamHandles)
					{
						streamHandles.Add(mdib, handle);
					}
				}

				return mdib;
			}
			catch
			{
				if (!mdib.IsNull)
					CloseMultiBitmap(mdib, FREE_IMAGE_SAVE_FLAGS.DEFAULT);

				if (handle.IsNull == false)
					handle.Dispose();

				throw;
			}
		}

		/// <summary>
		/// Closes a previously opened multi-page bitmap and, when the bitmap was not opened read-only, applies any changes made to it.
		/// </summary>
		/// <param name="bitmap">Handle to a FreeImage multi-paged bitmap.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		public static bool CloseMultiBitmap(FIMULTIBITMAP bitmap, FREE_IMAGE_SAVE_FLAGS flags)
		{
			if (CloseMultiBitmap_(bitmap, flags))
			{
                lock (streamHandles)
                {
                    if (streamHandles.TryGetValue(bitmap, out var handle))
                    {
                        streamHandles.Remove(bitmap);
                        handle.Dispose();
                    }
                }
                return true;
			}
			return false;
		}

		/// <summary>
		/// Closes a previously opened multi-page bitmap and, when the bitmap was not opened read-only,
		/// applies any changes made to it.
		/// On success the handle will be reset to null.
		/// </summary>
		/// <param name="bitmap">Handle to a FreeImage multi-paged bitmap.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		public static bool CloseMultiBitmapEx(ref FIMULTIBITMAP bitmap)
		{
			return CloseMultiBitmapEx(ref bitmap, FREE_IMAGE_SAVE_FLAGS.DEFAULT);
		}

		/// <summary>
		/// Closes a previously opened multi-page bitmap and, when the bitmap was not opened read-only,
		/// applies any changes made to it.
		/// On success the handle will be reset to null.
		/// </summary>
		/// <param name="bitmap">Handle to a FreeImage multi-paged bitmap.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		public static bool CloseMultiBitmapEx(ref FIMULTIBITMAP bitmap, FREE_IMAGE_SAVE_FLAGS flags)
		{
			bool result = false;
			if (!bitmap.IsNull)
			{
				if (CloseMultiBitmap(bitmap, flags))
				{
					bitmap.SetNull();
					result = true;
				}
			}
			return result;
		}

		/// <summary>
		/// Retrieves the number of pages that are locked in a multi-paged bitmap.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage multi-paged bitmap.</param>
		/// <returns>Number of locked pages.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static int GetLockedPageCount(FIMULTIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			int result = 0;
			GetLockedPageNumbers(dib, null, ref result);
			return result;
		}

		/// <summary>
		/// Retrieves a list locked pages of a multi-paged bitmap.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage multi-paged bitmap.</param>
		/// <returns>List containing the indexes of the locked pages.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static int[] GetLockedPages(FIMULTIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			// Get the number of pages and create an array to save the information
			int count = 0;
			int[] result = null;
			// Get count
			if (GetLockedPageNumbers(dib, result, ref count))
			{
				result = new int[count];
				// Fill array
				if (!GetLockedPageNumbers(dib, result, ref count))
				{
					result = null;
				}
			}
			return result;
		}

		/// <summary>
		/// Loads a FreeImage multi-paged bitmap from a stream and returns the
		/// FreeImage memory stream used as temporary buffer.
		/// The bitmap can not be modified by calling
		/// <see cref="FreeImage.AppendPage(FIMULTIBITMAP,FIBITMAP)"/>,
		/// <see cref="FreeImage.InsertPage(FIMULTIBITMAP,Int32,FIBITMAP)"/>,
		/// <see cref="FreeImage.MovePage(FIMULTIBITMAP,Int32,Int32)"/> or
		/// <see cref="FreeImage.DeletePage(FIMULTIBITMAP,Int32)"/>.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="format">Format of the image.</param>
		/// <param name="flags">Flags to enable or disable plugin-features.</param>
		/// <param name="memory">The temporary memory buffer used to load the bitmap.</param>
		/// <returns>Handle to a FreeImage multi-paged bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> can not read.</exception>
		public static FIMULTIBITMAP LoadMultiBitmapFromStream(
			Stream stream,
			FREE_IMAGE_FORMAT format,
			FREE_IMAGE_LOAD_FLAGS flags,
			out FIMEMORY memory)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			if (!stream.CanRead)
			{
				throw new ArgumentException("stream");
			}
			const int blockSize = 1024;
			int bytesRead;
			byte[] buffer = new byte[blockSize];

			stream = stream.CanSeek ? stream : new StreamWrapper(stream, true);
			memory = OpenMemory(IntPtr.Zero, 0);

			do
			{
				bytesRead = stream.Read(buffer, 0, blockSize);
                _ = WriteMemory(buffer, blockSize, 1, memory);
			}
			while (bytesRead == blockSize);

			return LoadMultiBitmapFromMemory(format, memory, flags);
		}

		#endregion

		#region Filetype functions

		/// <summary>
		/// Orders FreeImage to analyze the bitmap signature.
		/// In case the stream is not seekable, the stream will have been used
		/// and must be recreated for loading.
		/// </summary>
		/// <param name="stream">Name of the stream to analyze.</param>
		/// <returns>Type of the bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="stream"/> is null.</exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="stream"/> can not read.</exception>
		public static FREE_IMAGE_FORMAT GetFileTypeFromStream(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			if (!stream.CanRead)
			{
				throw new ArgumentException("stream is not capable of reading.");
			}
			// Wrap the stream if it cannot seek
			stream = (stream.CanSeek) ? stream : new StreamWrapper(stream, true);
			// Create a 'FreeImageIO' structure for the stream
			FreeImageIO io = FreeImageStreamIO.io;
			using fi_handle handle = new fi_handle(stream);
			return GetFileTypeFromHandle(ref io, handle, 0);
		}

		#endregion

		#region Pixel access functions

		/// <summary>
		/// Retrieves an hBitmap for a FreeImage bitmap.
		/// Call FreeHbitmap(IntPtr) to free the handle.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="hdc">A reference device context.
		/// Use IntPtr.Zero if no reference is available.</param>
		/// <param name="unload">When true dib will be unloaded if the function succeeded.</param>
		/// <returns>The hBitmap for the FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static unsafe IntPtr GetHbitmap(FIBITMAP dib, IntPtr hdc, bool unload)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			IntPtr hBitmap = IntPtr.Zero;
			bool release = false;
			IntPtr ppvBits = IntPtr.Zero;
			// Check if we have destination
			if (release = (hdc == IntPtr.Zero))
			{
				// We don't so request dc
				hdc = GetDC(IntPtr.Zero);
			}
			if (hdc != IntPtr.Zero)
			{
				// Get pointer to the infoheader of the bitmap
				IntPtr info = GetInfo(dib);
				// Create a bitmap in the dc
				hBitmap = CreateDIBSection(hdc, info, DIB_RGB_COLORS, out ppvBits, IntPtr.Zero, 0);
				if (hBitmap != IntPtr.Zero && ppvBits != IntPtr.Zero)
				{
					// Copy the data into the dc
					ref byte dst = ref Unsafe.AsRef<byte>((void*) ppvBits);
					ref byte src = ref Unsafe.AsRef<byte>((void*) GetBits(dib));
					Unsafe.CopyBlockUnaligned(ref dst, ref src, GetHeight(dib) * GetPitch(dib));

					// Success: we unload the bitmap
					if (unload)
					{
						Unload(dib);
					}
				}
				// We have to release the dc
				if (release)
				{
					ReleaseDC(IntPtr.Zero, hdc);
				}
			}
			return hBitmap;
		}

		/// <summary>
		/// Returns an HBITMAP created by the <c>CreateDIBitmap()</c> function which in turn
		/// has always the same color depth as the reference DC, which may be provided
		/// through <paramref name="hdc"/>. The desktop DC will be used,
		/// if <c>IntPtr.Zero</c> DC is specified.
		/// Call <see cref="FreeImage.FreeHbitmap(IntPtr)"/> to free the handle.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="hdc">Handle to a device context.</param>
		/// <param name="unload">When true the structure will be unloaded on success.
		/// If the function failed and returned false, the bitmap was not unloaded.</param>
		/// <returns>If the function succeeds, the return value is a handle to the
		/// compatible bitmap. If the function fails, the return value is <see cref="IntPtr.Zero"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static IntPtr GetBitmapForDevice(FIBITMAP dib, IntPtr hdc, bool unload)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			IntPtr hbitmap = IntPtr.Zero;
			bool release = false;
			if (release = (hdc == IntPtr.Zero))
			{
				hdc = GetDC(IntPtr.Zero);
			}
			if (hdc != IntPtr.Zero)
			{
				hbitmap = CreateDIBitmap(
					hdc,
					GetInfoHeader(dib),
					CBM_INIT,
					GetBits(dib),
					GetInfo(dib),
					DIB_RGB_COLORS);
				if (unload)
				{
					Unload(dib);
				}
				if (release)
				{
					ReleaseDC(IntPtr.Zero, hdc);
				}
			}
			return hbitmap;
		}

		/// <summary>
		/// Creates a FreeImage DIB from a Device Context/Compatible Bitmap.
		/// </summary>
		/// <param name="hbitmap">Handle to the bitmap.</param>
		/// <param name="hdc">Handle to a device context.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="hbitmap"/> is null.</exception>
		public unsafe static FIBITMAP CreateFromHbitmap(IntPtr hbitmap, IntPtr hdc)
		{
			if (hbitmap == IntPtr.Zero)
			{
				throw new ArgumentNullException("hbitmap");
			}

			FIBITMAP dib = new FIBITMAP();
			BITMAP bm;
			uint colors;
			bool release;

			if (GetObject(hbitmap, sizeof(BITMAP), (IntPtr)(&bm)) != 0)
			{
				dib = Allocate(bm.bmWidth, bm.bmHeight, bm.bmBitsPixel, 0, 0, 0);
				if (!dib.IsNull)
				{
					colors = GetColorsUsed(dib);
					if (release = (hdc == IntPtr.Zero))
					{
						hdc = GetDC(IntPtr.Zero);
					}
					if (GetDIBits(
						hdc,
						hbitmap,
						0,
						(uint)bm.bmHeight,
						GetBits(dib),
						GetInfo(dib),
						DIB_RGB_COLORS) != 0)
					{
						if (colors != 0)
						{
							BITMAPINFOHEADER* bmih = (BITMAPINFOHEADER*)GetInfo(dib);
							bmih[0].biClrImportant = bmih[0].biClrUsed = colors;
						}
					}
					else
					{
						UnloadEx(ref dib);
					}
					if (release)
					{
						ReleaseDC(IntPtr.Zero, hdc);
					}
				}
			}

			return dib;
		}

		/// <summary>
		/// Frees a bitmap handle.
		/// </summary>
		/// <param name="hbitmap">Handle to a bitmap.</param>
		/// <returns>True on success, false on failure.</returns>
		public static bool FreeHbitmap(IntPtr hbitmap)
		{
			return DeleteObject(hbitmap);
		}

		#endregion

		#region Bitmap information functions

		/// <summary>
		/// Retrieves a DIB's resolution in X-direction measured in 'dots per inch' (DPI) and not in
		/// 'dots per meter'.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns>The resolution in 'dots per inch'.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static uint GetResolutionX(FIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			return (uint)(0.5d + 0.0254d * GetDotsPerMeterX(dib));
		}

		/// <summary>
		/// Retrieves a DIB's resolution in Y-direction measured in 'dots per inch' (DPI) and not in
		/// 'dots per meter'.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns>The resolution in 'dots per inch'.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static uint GetResolutionY(FIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			return (uint)(0.5d + 0.0254d * GetDotsPerMeterY(dib));
		}

		/// <summary>
		/// Sets a DIB's resolution in X-direction measured in 'dots per inch' (DPI) and not in
		/// 'dots per meter'.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="res">The new resolution in 'dots per inch'.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static void SetResolutionX(FIBITMAP dib, uint res)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			SetDotsPerMeterX(dib, (uint)((double)res / 0.0254d + 0.5d));
		}

		/// <summary>
		/// Sets a DIB's resolution in Y-direction measured in 'dots per inch' (DPI) and not in
		/// 'dots per meter'.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="res">The new resolution in 'dots per inch'.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static void SetResolutionY(FIBITMAP dib, uint res)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			SetDotsPerMeterY(dib, (uint)((double)res / 0.0254d + 0.5d));
		}

		/// <summary>
		/// Returns whether the image is a greyscale image or not.
		/// The function scans all colors in the bitmaps palette for entries where
		/// red, green and blue are not all the same (not a grey color).
		/// Supports 1-, 4- and 8-bit bitmaps.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns>True if the image is a greyscale image, else false.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static unsafe bool IsGreyscaleImage(FIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			bool result = true;
			uint bpp = GetBPP(dib);
			switch (bpp)
			{
				case 1:
				case 4:
				case 8:
					RGBQUAD* palette = (RGBQUAD*)GetPalette(dib);
					uint paletteLength = GetColorsUsed(dib);
					for (int i = 0; i < paletteLength; i++)
					{
						if (palette[i].rgbRed != palette[i].rgbGreen ||
							palette[i].rgbRed != palette[i].rgbBlue)
						{
							result = false;
							break;
						}
					}
					break;
				default:
					result = false;
					break;
			}
			return result;
		}

		/// <summary>
		/// Returns a structure that represents the palette of a FreeImage bitmap.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns>A structure representing the bitmaps palette.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static Palette GetPaletteEx(FIBITMAP dib)
		{
			return new Palette(dib);
		}

		/// <summary>
		/// Returns the <see cref="BITMAPINFOHEADER"/> structure of a FreeImage bitmap.
		/// The structure is a copy, so changes will have no effect on
		/// the bitmap itself.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns><see cref="BITMAPINFOHEADER"/> structure of the bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static unsafe BITMAPINFOHEADER GetInfoHeaderEx(FIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			return *(BITMAPINFOHEADER*)GetInfoHeader(dib);
		}

		/// <summary>
		/// Returns the <see cref="BITMAPINFO"/> structure of a FreeImage bitmap.
		/// The structure is a copy, so changes will have no effect on
		/// the bitmap itself.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns><see cref="BITMAPINFO"/> structure of the bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static BITMAPINFO GetInfoEx(FIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			BITMAPINFO result = new BITMAPINFO();
			result.bmiHeader = GetInfoHeaderEx(dib);
			IntPtr ptr = GetPalette(dib);
			if (ptr == IntPtr.Zero)
			{
				result.bmiColors = [];
			}
			else
			{
				result.bmiColors = new MemoryArray<RGBQUAD>(ptr, (int)result.bmiHeader.biClrUsed).Data;
			}
			return result;
		}

		/// <summary>
		/// Returns the pixelformat of the bitmap.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns><see cref="System.Drawing.Imaging.PixelFormat"/> of the bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static PixelFormat GetPixelFormat(FIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}

			PixelFormat result = PixelFormat.Undefined;

			if (GetImageType(dib) == FREE_IMAGE_TYPE.FIT_BITMAP)
			{
				switch (GetBPP(dib))
				{
					case 1:
						result = PixelFormat.Format1bppIndexed;
						break;
					case 4:
						result = PixelFormat.Format4bppIndexed;
						break;
					case 8:
						result = PixelFormat.Format8bppIndexed;
						break;
					case 16:
						if ((GetBlueMask(dib) == FI16_565_BLUE_MASK) &&
							(GetGreenMask(dib) == FI16_565_GREEN_MASK) &&
							(GetRedMask(dib) == FI16_565_RED_MASK))
						{
							result = PixelFormat.Format16bppRgb565;
						}
						if ((GetBlueMask(dib) == FI16_555_BLUE_MASK) &&
							(GetGreenMask(dib) == FI16_555_GREEN_MASK) &&
							(GetRedMask(dib) == FI16_555_RED_MASK))
						{
							result = PixelFormat.Format16bppRgb555;
						}
						break;
					case 24:
						result = PixelFormat.Format24bppRgb;
						break;
					case 32:
						result = PixelFormat.Format32bppArgb;
						break;
				}
			}
			return result;
		}

		/// <summary>
		/// Retrieves all parameters needed to create a new FreeImage bitmap from
		/// the format of a .NET <see cref="System.Drawing.Image"/>.
		/// </summary>
		/// <param name="format">The <see cref="System.Drawing.Imaging.PixelFormat"/>
		/// of the .NET <see cref="System.Drawing.Image"/>.</param>
		/// <param name="type">Returns the type used for the new bitmap.</param>
		/// <param name="bpp">Returns the color depth for the new bitmap.</param>
		/// <param name="redMask">Returns the red_mask for the new bitmap.</param>
		/// <param name="greenMask">Returns the green_mask for the new bitmap.</param>
		/// <param name="blueMask">Returns the blue_mask for the new bitmap.</param>
		/// <returns>True in case a matching conversion exists; else false.
		/// </returns>
		public static bool GetFormatParameters(
			PixelFormat format,
			out FREE_IMAGE_TYPE type,
			out uint bpp,
			out uint redMask,
			out uint greenMask,
			out uint blueMask)
		{
			bool result = false;
			type = FREE_IMAGE_TYPE.FIT_UNKNOWN;
			bpp = 0;
			redMask = 0;
			greenMask = 0;
			blueMask = 0;
			switch (format)
			{
				case PixelFormat.Format1bppIndexed:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 1;
					result = true;
					break;
				case PixelFormat.Format4bppIndexed:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 4;
					result = true;
					break;
				case PixelFormat.Format8bppIndexed:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 8;
					result = true;
					break;
				case PixelFormat.Format16bppRgb565:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 16;
					redMask = FI16_565_RED_MASK;
					greenMask = FI16_565_GREEN_MASK;
					blueMask = FI16_565_BLUE_MASK;
					result = true;
					break;
				case PixelFormat.Format16bppRgb555:
				case PixelFormat.Format16bppArgb1555:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 16;
					redMask = FI16_555_RED_MASK;
					greenMask = FI16_555_GREEN_MASK;
					blueMask = FI16_555_BLUE_MASK;
					result = true;
					break;
				case PixelFormat.Format24bppRgb:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 24;
					redMask = FI_RGBA_RED_MASK;
					greenMask = FI_RGBA_GREEN_MASK;
					blueMask = FI_RGBA_BLUE_MASK;
					result = true;
					break;
				case PixelFormat.Format32bppRgb:
				case PixelFormat.Format32bppArgb:
				case PixelFormat.Format32bppPArgb:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 32;
					redMask = FI_RGBA_RED_MASK;
					greenMask = FI_RGBA_GREEN_MASK;
					blueMask = FI_RGBA_BLUE_MASK;
					result = true;
					break;
				case PixelFormat.Format16bppGrayScale:
					type = FREE_IMAGE_TYPE.FIT_UINT16;
					bpp = 16;
					result = true;
					break;
				case PixelFormat.Format48bppRgb:
					type = FREE_IMAGE_TYPE.FIT_RGB16;
					bpp = 48;
					result = true;
					break;
				case PixelFormat.Format64bppArgb:
				case PixelFormat.Format64bppPArgb:
					type = FREE_IMAGE_TYPE.FIT_RGBA16;
					bpp = 64;
					result = true;
					break;
			}
			return result;
		}

		/// <summary>
		/// Retrieves all parameters needed to create a new FreeImage bitmap from the pixel format.
		/// </summary>
		/// <param name="format">The <see cref="Stride.Graphics.PixelFormat"/> of the image.</param>
		/// <param name="type">Returns the type used for the new bitmap.</param>
		/// <param name="bpp">Returns the color depth for the new bitmap.</param>
		/// <param name="redMask">Returns the red_mask for the new bitmap.</param>
		/// <param name="greenMask">Returns the green_mask for the new bitmap.</param>
		/// <param name="blueMask">Returns the blue_mask for the new bitmap.</param>
		/// <returns>True in case a matching conversion exists; else false.
		/// </returns>
		public static bool GetFormatParameters(
			StridePixelFormat format,
			out FREE_IMAGE_TYPE type,
			out uint bpp,
			out uint redMask,
			out uint greenMask,
			out uint blueMask)
		{
			var result = true;
			type = FREE_IMAGE_TYPE.FIT_UNKNOWN;
			bpp = 0;
			redMask = 0;
			greenMask = 0;
			blueMask = 0;

			switch (format)
			{
				case StridePixelFormat.R32G32B32A32_Float:
					type = FREE_IMAGE_TYPE.FIT_RGBAF;
					bpp = 128;
					break;
				case StridePixelFormat.R32G32B32_Float:
					type = FREE_IMAGE_TYPE.FIT_RGBF;
					bpp = 96;
					break;
				case StridePixelFormat.R16G16B16A16_Typeless:
				case StridePixelFormat.R16G16B16A16_Float:
				case StridePixelFormat.R16G16B16A16_UNorm:
				case StridePixelFormat.R16G16B16A16_UInt:
				case StridePixelFormat.R16G16B16A16_SNorm:
				case StridePixelFormat.R16G16B16A16_SInt:
					type = FREE_IMAGE_TYPE.FIT_RGBA16;
					bpp = 64;
					break;
				case StridePixelFormat.D32_Float:
				case StridePixelFormat.R32_Float:
					type = FREE_IMAGE_TYPE.FIT_FLOAT;
					bpp = 32;
					break;
				case StridePixelFormat.R32_SInt:
					type = FREE_IMAGE_TYPE.FIT_INT32;
					bpp = 32;
					break;
				case StridePixelFormat.R32_UInt:
					type = FREE_IMAGE_TYPE.FIT_UINT32;
					bpp = 32;
					break;
				case StridePixelFormat.R16_SInt:
					type = FREE_IMAGE_TYPE.FIT_INT16;
					bpp = 16;
					break;
				case StridePixelFormat.R16_UInt:
					type = FREE_IMAGE_TYPE.FIT_UINT16;
					bpp = 16;
					break;
				case StridePixelFormat.R32_Typeless:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 32;
					break;
				case StridePixelFormat.R8G8B8A8_Typeless:
				case StridePixelFormat.R8G8B8A8_UNorm:
				case StridePixelFormat.R8G8B8A8_UNorm_SRgb:
				case StridePixelFormat.R8G8B8A8_UInt:
				case StridePixelFormat.R8G8B8A8_SNorm:
				case StridePixelFormat.R8G8B8A8_SInt:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 32;
					redMask = FI_RGBA_RED_MASK;
					greenMask = FI_RGBA_GREEN_MASK;
					blueMask = FI_RGBA_BLUE_MASK;
					break;
				case StridePixelFormat.R16G16_Typeless:
				case StridePixelFormat.R16G16_Float:
				case StridePixelFormat.R16G16_UNorm:
				case StridePixelFormat.R16G16_UInt:
				case StridePixelFormat.R16G16_SNorm:
				case StridePixelFormat.R16G16_SInt:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 32;
					redMask = 0xFFFF0000;
					greenMask = 0x0000FFFF;
					break;
				case StridePixelFormat.B8G8R8A8_Typeless:
				case StridePixelFormat.B8G8R8A8_UNorm_SRgb:
				case StridePixelFormat.B8G8R8X8_Typeless:
				case StridePixelFormat.B8G8R8X8_UNorm_SRgb:
				case StridePixelFormat.B8G8R8A8_UNorm:
				case StridePixelFormat.B8G8R8X8_UNorm:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 32;
					redMask = FI_RGBA_BLUE_MASK;
					greenMask = FI_RGBA_GREEN_MASK;
					blueMask = FI_RGBA_RED_MASK;
					break;

				case StridePixelFormat.B5G6R5_UNorm:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 16;
					redMask = FI16_565_RED_MASK;
					greenMask = FI16_565_GREEN_MASK;
					blueMask = FI16_565_BLUE_MASK;
					break;
				case StridePixelFormat.B5G5R5A1_UNorm:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 16;
					redMask = FI16_555_RED_MASK;
					greenMask = FI16_555_GREEN_MASK;
					blueMask = FI16_555_BLUE_MASK;
					break;
				case StridePixelFormat.R16_Typeless:
				case StridePixelFormat.R16_Float:
				case StridePixelFormat.D16_UNorm:
				case StridePixelFormat.R16_UNorm:
				case StridePixelFormat.R16_SNorm:
					type = FREE_IMAGE_TYPE.FIT_UINT16;
					bpp = 16;
					break;
				case StridePixelFormat.R8G8_Typeless:
				case StridePixelFormat.R8G8_UNorm:
				case StridePixelFormat.R8G8_UInt:
				case StridePixelFormat.R8G8_SNorm:
				case StridePixelFormat.R8G8_SInt:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 16;
					redMask = 0xFF00;
					greenMask= 0x00FF;
					break;
				case StridePixelFormat.R8_Typeless:
				case StridePixelFormat.R8_UNorm:
				case StridePixelFormat.R8_UInt:
				case StridePixelFormat.R8_SNorm:
				case StridePixelFormat.R8_SInt:
				case StridePixelFormat.A8_UNorm:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 8;
					break;
				case StridePixelFormat.R1_UNorm:
					type = FREE_IMAGE_TYPE.FIT_BITMAP;
					bpp = 1;
					break;
				default:
					result = false;
					break;
			}

			return result;
		}

		/// <summary>
		/// Retrieves all parameters needed to create a new FreeImage bitmap from
		/// raw bits <see cref="System.Drawing.Image"/>.
		/// </summary>
		/// <param name="type">The <see cref="FREE_IMAGE_TYPE"/>
		/// of the data in memory.</param>
		/// <param name="bpp">The color depth for the data.</param>
		/// <param name="red_mask">Returns the red_mask for the data.</param>
		/// <param name="green_mask">Returns the green_mask for the data.</param>
		/// <param name="blue_mask">Returns the blue_mask for the data.</param>
		/// <returns>True in case a matching conversion exists; else false.
		/// </returns>
		public static bool GetTypeParameters(
			FREE_IMAGE_TYPE type,
			int bpp,
			out uint red_mask,
			out uint green_mask,
			out uint blue_mask)
		{
			bool result = false;
			red_mask = 0;
			green_mask = 0;
			blue_mask = 0;
			switch (type)
			{
				case FREE_IMAGE_TYPE.FIT_BITMAP:
					switch (bpp)
					{
						case 1:
						case 4:
						case 8:
							result = true;
							break;
						case 16:
							result = true;
							red_mask = FI16_555_RED_MASK;
							green_mask = FI16_555_GREEN_MASK;
							blue_mask = FI16_555_BLUE_MASK;
							break;
						case 24:
						case 32:
							result = true;
							red_mask = FI_RGBA_RED_MASK;
							green_mask = FI_RGBA_GREEN_MASK;
							blue_mask = FI_RGBA_BLUE_MASK;
							break;
					}
					break;
				case FREE_IMAGE_TYPE.FIT_UNKNOWN:
					break;
				default:
					result = true;
					break;
			}
			return result;
		}

		/// <summary>
		/// Compares two FreeImage bitmaps.
		/// </summary>
		/// <param name="dib1">The first bitmap to compare.</param>
		/// <param name="dib2">The second bitmap to compare.</param>
		/// <param name="flags">Determines which components of the bitmaps will be compared.</param>
		/// <returns>True in case both bitmaps match the compare conditions, false otherwise.</returns>
		public static bool Compare(FIBITMAP dib1, FIBITMAP dib2, FREE_IMAGE_COMPARE_FLAGS flags)
		{
			// Check whether one bitmap is null
			if (dib1.IsNull ^ dib2.IsNull)
			{
				return false;
			}
			// Check whether both pointers are the same
			if (dib1 == dib2)
			{
				return true;
			}
			if (((flags & FREE_IMAGE_COMPARE_FLAGS.HEADER) > 0) && (!CompareHeader(dib1, dib2)))
			{
				return false;
			}
			if (((flags & FREE_IMAGE_COMPARE_FLAGS.PALETTE) > 0) && (!ComparePalette(dib1, dib2)))
			{
				return false;
			}
			if (((flags & FREE_IMAGE_COMPARE_FLAGS.DATA) > 0) && (!CompareData(dib1, dib2)))
			{
				return false;
			}
			if (((flags & FREE_IMAGE_COMPARE_FLAGS.METADATA) > 0) && (!CompareMetadata(dib1, dib2)))
			{
				return false;
			}
			return true;
		}

		private static unsafe bool CompareHeader(FIBITMAP dib1, FIBITMAP dib2)
		{
			IntPtr i1 = GetInfoHeader(dib1);
			IntPtr i2 = GetInfoHeader(dib2);
			return CompareMemory((void*)i1, (void*)i2, sizeof(BITMAPINFOHEADER));
		}

		private static unsafe bool ComparePalette(FIBITMAP dib1, FIBITMAP dib2)
		{
			IntPtr pal1 = GetPalette(dib1), pal2 = GetPalette(dib2);
			bool hasPalette1 = pal1 != IntPtr.Zero;
			bool hasPalette2 = pal2 != IntPtr.Zero;
			if (hasPalette1 ^ hasPalette2)
			{
				return false;
			}
			if (!hasPalette1)
			{
				return true;
			}
			uint colors = GetColorsUsed(dib1);
			if (colors != GetColorsUsed(dib2))
			{
				return false;
			}
			return CompareMemory((void*)pal1, (void*)pal2, sizeof(RGBQUAD) * colors);
		}

		private static unsafe bool CompareData(FIBITMAP dib1, FIBITMAP dib2)
		{
			uint width = GetWidth(dib1);
			if (width != GetWidth(dib2))
			{
				return false;
			}
			uint height = GetHeight(dib1);
			if (height != GetHeight(dib2))
			{
				return false;
			}
			uint bpp = GetBPP(dib1);
			if (bpp != GetBPP(dib2))
			{
				return false;
			}
			if (GetColorType(dib1) != GetColorType(dib2))
			{
				return false;
			}
			FREE_IMAGE_TYPE type = GetImageType(dib1);
			if (type != GetImageType(dib2))
			{
				return false;
			}
			if (GetRedMask(dib1) != GetRedMask(dib2))
			{
				return false;
			}
			if (GetGreenMask(dib1) != GetGreenMask(dib2))
			{
				return false;
			}
			if (GetBlueMask(dib1) != GetBlueMask(dib2))
			{
				return false;
			}

			byte* ptr1, ptr2;
			int fullBytes;
			int shift;
			uint line = GetLine(dib1);

			if (type == FREE_IMAGE_TYPE.FIT_BITMAP)
			{
				switch (bpp)
				{
					case 32:
						for (int i = 0; i < height; i++)
						{
							ptr1 = (byte*)GetScanLine(dib1, i);
							ptr2 = (byte*)GetScanLine(dib2, i);
							if (!CompareMemory(ptr1, ptr2, line))
							{
								return false;
							}
						}
						break;
					case 24:
						for (int i = 0; i < height; i++)
						{
							ptr1 = (byte*)GetScanLine(dib1, i);
							ptr2 = (byte*)GetScanLine(dib2, i);
							if (!CompareMemory(ptr1, ptr2, line))
							{
								return false;
							}
						}
						break;
					case 16:
						short* sPtr1, sPtr2;
						short mask = (short)(GetRedMask(dib1) | GetGreenMask(dib1) | GetBlueMask(dib1));
						if (mask == -1)
						{
							for (int i = 0; i < height; i++)
							{
								sPtr1 = (short*)GetScanLine(dib1, i);
								sPtr2 = (short*)GetScanLine(dib2, i);
								if (!CompareMemory(sPtr1, sPtr1, line))
								{
									return false;
								}
							}
						}
						else
						{
							for (int i = 0; i < height; i++)
							{
								sPtr1 = (short*)GetScanLine(dib1, i);
								sPtr2 = (short*)GetScanLine(dib2, i);
								for (int x = 0; x < width; x++)
								{
									if ((sPtr1[x] & mask) != (sPtr2[x] & mask))
									{
										return false;
									}
								}
							}
						}
						break;
					case 8:
						for (int i = 0; i < height; i++)
						{
							ptr1 = (byte*)GetScanLine(dib1, i);
							ptr2 = (byte*)GetScanLine(dib2, i);
							if (!CompareMemory(ptr1, ptr2, line))
							{
								return false;
							}
						}
						break;
					case 4:
						fullBytes = (int)width / 2;
						shift = (width % 2) == 0 ? 8 : 4;
						for (int i = 0; i < height; i++)
						{
							ptr1 = (byte*)GetScanLine(dib1, i);
							ptr2 = (byte*)GetScanLine(dib2, i);
							if (fullBytes != 0)
							{
								if (!CompareMemory(ptr1, ptr2, fullBytes))
								{
									return false;
								}
								ptr1 += fullBytes;
								ptr2 += fullBytes;
							}
							if (shift != 8)
							{
								if ((ptr1[0] >> shift) != (ptr2[0] >> shift))
								{
									return false;
								}
							}
						}
						break;
					case 1:
						fullBytes = (int)width / 8;
						shift = 8 - ((int)width % 8);
						for (int i = 0; i < height; i++)
						{
							ptr1 = (byte*)GetScanLine(dib1, i);
							ptr2 = (byte*)GetScanLine(dib2, i);
							if (fullBytes != 0)
							{
								if (!CompareMemory(ptr1, ptr2, fullBytes))
								{
									return false;
								}
								ptr1 += fullBytes;
								ptr2 += fullBytes;
							}
							if (shift != 8)
							{
								if ((ptr1[0] >> shift) != (ptr2[0] >> shift))
								{
									return false;
								}
							}
						}
						break;
					default:
						throw new NotSupportedException("Only 1, 4, 8, 16, 24 and 32 bpp bitmaps are supported.");
				}
			}
			else
			{
				for (int i = 0; i < height; i++)
				{
					ptr1 = (byte*)GetScanLine(dib1, i);
					ptr2 = (byte*)GetScanLine(dib2, i);
					if (!CompareMemory(ptr1, ptr2, line))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool CompareMetadata(FIBITMAP dib1, FIBITMAP dib2)
		{
			MetadataTag tag1, tag2;

			foreach (FREE_IMAGE_MDMODEL metadataModel in FREE_IMAGE_MDMODELS)
			{
				if (GetMetadataCount(metadataModel, dib1) !=
					GetMetadataCount(metadataModel, dib2))
				{
					return false;
				}
				if (GetMetadataCount(metadataModel, dib1) == 0)
				{
					continue;
				}

				FIMETADATA mdHandle = FindFirstMetadata(metadataModel, dib1, out tag1);
				if (mdHandle.IsNull)
				{
					continue;
				}
				do
				{
					if ((!GetMetadata(metadataModel, dib2, tag1.Key, out tag2)) || (tag1 != tag2))
					{
						FindCloseMetadata(mdHandle);
						return false;
					}
				}
				while (FindNextMetadata(mdHandle, out tag1));
				FindCloseMetadata(mdHandle);
			}

			return true;
		}

		/// <summary>
		/// Returns the FreeImage bitmap's transparency table.
		/// The array is empty in case the bitmap has no transparency table.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns>The FreeImage bitmap's transparency table.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static unsafe byte[] GetTransparencyTableEx(FIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			uint count = GetTransparencyCount(dib);
			byte[] result = new byte[count];

			ref byte dst = ref result[0];
			ref byte src = ref Unsafe.AsRef<byte>((byte*) GetTransparencyTable(dib));
			Unsafe.CopyBlockUnaligned(ref dst, ref src, count);

			return result;
		}

		/// <summary>
		/// Set the FreeImage bitmap's transparency table. Only affects palletised bitmaps.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="table">The FreeImage bitmap's new transparency table.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> or <paramref name="table"/> is null.</exception>
		public static void SetTransparencyTable(FIBITMAP dib, byte[] table)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			if (table == null)
			{
				throw new ArgumentNullException("table");
			}
			SetTransparencyTable(dib, table, table.Length);
		}

		/// <summary>
		/// This function returns the number of unique colors actually used by the
		/// specified 1-, 4-, 8-, 16-, 24- or 32-bit image. This might be different from
		/// what function FreeImage_GetColorsUsed() returns, which actually returns the
		/// palette size for palletised images. Works for
		/// <see cref="FREE_IMAGE_TYPE.FIT_BITMAP"/> type images only.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns>Returns the number of unique colors used by the image specified or
		/// zero, if the image type cannot be handled.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static unsafe int GetUniqueColors(FIBITMAP dib)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}

			int result = 0;

			if (GetImageType(dib) == FREE_IMAGE_TYPE.FIT_BITMAP)
			{
				BitArray bitArray;
				int uniquePalEnts;
				int hashcode;
				byte[] lut;
				int width = (int)GetWidth(dib);
				int height = (int)GetHeight(dib);

				switch (GetBPP(dib))
				{
					case 1:

						result = 1;
						lut = CreateShrunkenPaletteLUT(dib, out uniquePalEnts);
						if (uniquePalEnts == 1)
						{
							break;
						}

						if ((*(byte*)GetScanLine(dib, 0) & 0x80) == 0)
						{
							for (int y = 0; y < height; y++)
							{
								byte* scanline = (byte*)GetScanLine(dib, y);
								int mask = 0x80;
								for (int x = 0; x < width; x++)
								{
									if ((scanline[x / 8] & mask) > 0)
									{
										return 2;
									}
									mask = (mask == 0x1) ? 0x80 : (mask >> 1);
								}
							}
						}
						else
						{
							for (int y = 0; y < height; y++)
							{
								byte* scanline = (byte*)GetScanLine(dib, y);
								int mask = 0x80;
								for (int x = 0; x < width; x++)
								{
									if ((scanline[x / 8] & mask) == 0)
									{
										return 2;
									}
									mask = (mask == 0x1) ? 0x80 : (mask >> 1);
								}
							}
						}
						break;

					case 4:

						bitArray = new BitArray(0x10);
						lut = CreateShrunkenPaletteLUT(dib, out uniquePalEnts);
						if (uniquePalEnts == 1)
						{
							result = 1;
							break;
						}

						for (int y = 0; (y < height) && (result < uniquePalEnts); y++)
						{
							byte* scanline = (byte*)GetScanLine(dib, y);
							bool top = true;
							for (int x = 0; (x < width) && (result < uniquePalEnts); x++)
							{
								if (top)
								{
									hashcode = lut[scanline[x / 2] >> 4];
								}
								else
								{
									hashcode = lut[scanline[x / 2] & 0xF];
								}
								top = !top;
								if (!bitArray[hashcode])
								{
									bitArray[hashcode] = true;
									result++;
								}
							}
						}
						break;

					case 8:

						bitArray = new BitArray(0x100);
						lut = CreateShrunkenPaletteLUT(dib, out uniquePalEnts);
						if (uniquePalEnts == 1)
						{
							result = 1;
							break;
						}

						for (int y = 0; (y < height) && (result < uniquePalEnts); y++)
						{
							byte* scanline = (byte*)GetScanLine(dib, y);
							for (int x = 0; (x < width) && (result < uniquePalEnts); x++)
							{
								hashcode = lut[scanline[x]];
								if (!bitArray[hashcode])
								{
									bitArray[hashcode] = true;
									result++;
								}
							}
						}
						break;

					case 16:

						bitArray = new BitArray(0x10000);

						for (int y = 0; y < height; y++)
						{
							short* scanline = (short*)GetScanLine(dib, y);
							for (int x = 0; x < width; x++, scanline++)
							{
								hashcode = *scanline;
								if (!bitArray[hashcode])
								{
									bitArray[hashcode] = true;
									result++;
								}
							}
						}
						break;

					case 24:

						bitArray = new BitArray(0x1000000);

						for (int y = 0; y < height; y++)
						{
							byte* scanline = (byte*)GetScanLine(dib, y);
							for (int x = 0; x < width; x++, scanline += 3)
							{
								hashcode = *((int*)scanline) & 0x00FFFFFF;
								if (!bitArray[hashcode])
								{
									bitArray[hashcode] = true;
									result++;
								}
							}
						}
						break;

					case 32:

						bitArray = new BitArray(0x1000000);

						for (int y = 0; y < height; y++)
						{
							int* scanline = (int*)GetScanLine(dib, y);
							for (int x = 0; x < width; x++, scanline++)
							{
								hashcode = *scanline & 0x00FFFFFF;
								if (!bitArray[hashcode])
								{
									bitArray[hashcode] = true;
									result++;
								}
							}
						}
						break;
				}
			}
			return result;
		}

		/// <summary>
		/// Verifies whether the FreeImage bitmap is 16bit 555.
		/// </summary>
		/// <param name="dib">The FreeImage bitmap to verify.</param>
		/// <returns><b>true</b> if the bitmap is RGB16-555; otherwise <b>false</b>.</returns>
		public static bool ISRgb555(FIBITMAP dib)
		{
			return ((GetRedMask(dib) == FI16_555_RED_MASK) &&
				(GetGreenMask(dib) == FI16_555_GREEN_MASK) &&
				(GetBlueMask(dib) == FI16_555_BLUE_MASK));
		}

		/// <summary>
		/// Verifies whether the FreeImage bitmap is 16bit 565.
		/// </summary>
		/// <param name="dib">The FreeImage bitmap to verify.</param>
		/// <returns><b>true</b> if the bitmap is RGB16-565; otherwise <b>false</b>.</returns>
		public static bool ISRgb565(FIBITMAP dib)
		{
			return ((GetRedMask(dib) == FI16_565_RED_MASK) &&
				(GetGreenMask(dib) == FI16_565_GREEN_MASK) &&
				(GetBlueMask(dib) == FI16_565_BLUE_MASK));
		}

		#endregion

		#region ICC profile functions

		/// <summary>
		/// Creates a new ICC-Profile for a FreeImage bitmap.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="data">The data of the new ICC-Profile.</param>
		/// <returns>The new ICC-Profile of the bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIICCPROFILE CreateICCProfileEx(FIBITMAP dib, byte[] data)
		{
			return new FIICCPROFILE(dib, data);
		}

		/// <summary>
		/// Creates a new ICC-Profile for a FreeImage bitmap.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="data">The data of the new ICC-Profile.</param>
		/// <param name="size">The number of bytes of <paramref name="data"/> to use.</param>
		/// <returns>The new ICC-Profile of the FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIICCPROFILE CreateICCProfileEx(FIBITMAP dib, byte[] data, int size)
		{
			return new FIICCPROFILE(dib, data, size);
		}

		#endregion

		#region Conversion functions

		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion)
		{
			return ConvertColorDepth(
				dib,
				conversion,
				128,
				FREE_IMAGE_DITHER.FID_FS,
				FREE_IMAGE_QUANTIZE.FIQ_WUQUANT,
				false);
		}

		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion,
			bool unloadSource)
		{
			return ConvertColorDepth(
				dib,
				conversion,
				128,
				FREE_IMAGE_DITHER.FID_FS,
				FREE_IMAGE_QUANTIZE.FIQ_WUQUANT,
				unloadSource);
		}

		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <param name="threshold">Threshold value when converting with
		/// <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_01_BPP_THRESHOLD"/>.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion,
			byte threshold)
		{
			return ConvertColorDepth(
				dib,
				conversion,
				threshold,
				FREE_IMAGE_DITHER.FID_FS,
				FREE_IMAGE_QUANTIZE.FIQ_WUQUANT,
				false);
		}

		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <param name="ditherMethod">Dither algorithm when converting
		/// with <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_01_BPP_DITHER"/>.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion,
			FREE_IMAGE_DITHER ditherMethod)
		{
			return ConvertColorDepth(
				dib,
				conversion,
				128,
				ditherMethod,
				FREE_IMAGE_QUANTIZE.FIQ_WUQUANT,
				false);
		}


		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <param name="quantizationMethod">The quantization algorithm for conversion to 8-bit color depth.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion,
			FREE_IMAGE_QUANTIZE quantizationMethod)
		{
			return ConvertColorDepth(
				dib,
				conversion,
				128,
				FREE_IMAGE_DITHER.FID_FS,
				quantizationMethod,
				false);
		}

		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <param name="threshold">Threshold value when converting with
		/// <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_01_BPP_THRESHOLD"/>.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion,
			byte threshold,
			bool unloadSource)
		{
			return ConvertColorDepth(
				dib,
				conversion,
				threshold,
				FREE_IMAGE_DITHER.FID_FS,
				FREE_IMAGE_QUANTIZE.FIQ_WUQUANT,
				unloadSource);
		}

		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <param name="ditherMethod">Dither algorithm when converting with
		/// <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_01_BPP_DITHER"/>.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion,
			FREE_IMAGE_DITHER ditherMethod,
			bool unloadSource)
		{
			return ConvertColorDepth(
				dib,
				conversion,
				128,
				ditherMethod,
				FREE_IMAGE_QUANTIZE.FIQ_WUQUANT,
				unloadSource);
		}


		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <param name="quantizationMethod">The quantization algorithm for conversion to 8-bit color depth.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion,
			FREE_IMAGE_QUANTIZE quantizationMethod,
			bool unloadSource)
		{
			return ConvertColorDepth(
				dib,
				conversion,
				128,
				FREE_IMAGE_DITHER.FID_FS,
				quantizationMethod,
				unloadSource);
		}

		/// <summary>
		/// Converts a FreeImage bitmap from one color depth to another.
		/// If the conversion fails the original FreeImage bitmap is returned.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="conversion">The desired output format.</param>
		/// <param name="threshold">Threshold value when converting with
		/// <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_01_BPP_THRESHOLD"/>.</param>
		/// <param name="ditherMethod">Dither algorithm when converting with
		/// <see cref="FREE_IMAGE_COLOR_DEPTH.FICD_01_BPP_DITHER"/>.</param>
		/// <param name="quantizationMethod">The quantization algorithm for conversion to 8-bit color depth.</param>
		/// <param name="unloadSource">When true the structure will be unloaded on success.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		internal static FIBITMAP ConvertColorDepth(
			FIBITMAP dib,
			FREE_IMAGE_COLOR_DEPTH conversion,
			byte threshold,
			FREE_IMAGE_DITHER ditherMethod,
			FREE_IMAGE_QUANTIZE quantizationMethod,
			bool unloadSource)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}

			FIBITMAP result = new FIBITMAP();
			FIBITMAP dibTemp = new FIBITMAP();
			uint bpp = GetBPP(dib);
			bool reorderPalette = ((conversion & FREE_IMAGE_COLOR_DEPTH.FICD_REORDER_PALETTE) > 0);
			bool forceGreyscale = ((conversion & FREE_IMAGE_COLOR_DEPTH.FICD_FORCE_GREYSCALE) > 0);

			if (GetImageType(dib) == FREE_IMAGE_TYPE.FIT_BITMAP)
			{
				switch (conversion & (FREE_IMAGE_COLOR_DEPTH)0xFF)
				{
					case FREE_IMAGE_COLOR_DEPTH.FICD_01_BPP_THRESHOLD:

						if (bpp != 1)
						{
							if (forceGreyscale)
							{
								result = Threshold(dib, threshold);
							}
							else
							{
								dibTemp = ConvertTo24Bits(dib);
								result = ColorQuantizeEx(dibTemp, quantizationMethod, 2, null, 1);
								Unload(dibTemp);
							}
						}
						else
						{
							bool isGreyscale = IsGreyscaleImage(dib);
							if ((forceGreyscale && (!isGreyscale)) ||
								(reorderPalette && isGreyscale))
							{
								result = Threshold(dib, threshold);
							}
						}
						break;

					case FREE_IMAGE_COLOR_DEPTH.FICD_01_BPP_DITHER:

						if (bpp != 1)
						{
							if (forceGreyscale)
							{
								result = Dither(dib, ditherMethod);
							}
							else
							{
								dibTemp = ConvertTo24Bits(dib);
								result = ColorQuantizeEx(dibTemp, quantizationMethod, 2, null, 1);
								Unload(dibTemp);
							}
						}
						else
						{
							bool isGreyscale = IsGreyscaleImage(dib);
							if ((forceGreyscale && (!isGreyscale)) ||
								(reorderPalette && isGreyscale))
							{
								result = Dither(dib, ditherMethod);
							}
						}
						break;

					case FREE_IMAGE_COLOR_DEPTH.FICD_04_BPP:

						if (bpp != 4)
						{
							// Special case when 1bpp and FIC_PALETTE
							if (forceGreyscale ||
								((bpp == 1) && (GetColorType(dib) == FREE_IMAGE_COLOR_TYPE.FIC_PALETTE)))
							{
								dibTemp = ConvertToGreyscale(dib);
								result = ConvertTo4Bits(dibTemp);
								Unload(dibTemp);
							}
							else
							{
								dibTemp = ConvertTo24Bits(dib);
								result = ColorQuantizeEx(dibTemp, quantizationMethod, 16, null, 4);
								Unload(dibTemp);
							}
						}
						else
						{
							bool isGreyscale = IsGreyscaleImage(dib);
							if ((forceGreyscale && (!isGreyscale)) ||
								(reorderPalette && isGreyscale))
							{
								dibTemp = ConvertToGreyscale(dib);
								result = ConvertTo4Bits(dibTemp);
								Unload(dibTemp);
							}
						}

						break;

					case FREE_IMAGE_COLOR_DEPTH.FICD_08_BPP:

						if (bpp != 8)
						{
							if (forceGreyscale)
							{
								result = ConvertToGreyscale(dib);
							}
							else
							{
								dibTemp = ConvertTo24Bits(dib);
								result = ColorQuantize(dibTemp, quantizationMethod);
								Unload(dibTemp);
							}
						}
						else
						{
							bool isGreyscale = IsGreyscaleImage(dib);
							if ((forceGreyscale && (!isGreyscale)) || (reorderPalette && isGreyscale))
							{
								result = ConvertToGreyscale(dib);
							}
						}
						break;

					case FREE_IMAGE_COLOR_DEPTH.FICD_16_BPP_555:

						if (forceGreyscale)
						{
							dibTemp = ConvertToGreyscale(dib);
							result = ConvertTo16Bits555(dibTemp);
							Unload(dibTemp);
						}
						else if (bpp != 16 || GetRedMask(dib) != FI16_555_RED_MASK || GetGreenMask(dib) != FI16_555_GREEN_MASK || GetBlueMask(dib) != FI16_555_BLUE_MASK)
						{
							result = ConvertTo16Bits555(dib);
						}
						break;

					case FREE_IMAGE_COLOR_DEPTH.FICD_16_BPP:

						if (forceGreyscale)
						{
							dibTemp = ConvertToGreyscale(dib);
							result = ConvertTo16Bits565(dibTemp);
							Unload(dibTemp);
						}
						else if (bpp != 16 || GetRedMask(dib) != FI16_565_RED_MASK || GetGreenMask(dib) != FI16_565_GREEN_MASK || GetBlueMask(dib) != FI16_565_BLUE_MASK)
						{
							result = ConvertTo16Bits565(dib);
						}
						break;

					case FREE_IMAGE_COLOR_DEPTH.FICD_24_BPP:

						if (forceGreyscale)
						{
							dibTemp = ConvertToGreyscale(dib);
							result = ConvertTo24Bits(dibTemp);
							Unload(dibTemp);
						}
						else if (bpp != 24)
						{
							result = ConvertTo24Bits(dib);
						}
						break;

					case FREE_IMAGE_COLOR_DEPTH.FICD_32_BPP:

						if (forceGreyscale)
						{
							dibTemp = ConvertToGreyscale(dib);
							result = ConvertTo32Bits(dibTemp);
							Unload(dibTemp);
						}
						else if (bpp != 32)
						{
							result = ConvertTo32Bits(dib);
						}
						break;
				}
			}

			if (result.IsNull)
			{
				return dib;
			}
			if (unloadSource)
			{
				Unload(dib);
			}

			return result;
		}

		/// <summary>
		/// ColorQuantizeEx is an extension to the <see cref="ColorQuantize(FIBITMAP, FREE_IMAGE_QUANTIZE)"/>
		/// method that provides additional options used to quantize a 24-bit image to any
		/// number of colors (up to 256), as well as quantize a 24-bit image using a
		/// provided palette.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="quantize">Specifies the color reduction algorithm to be used.</param>
		/// <param name="PaletteSize">Size of the desired output palette.</param>
		/// <param name="ReservePalette">The provided palette.</param>
		/// <param name="minColorDepth"><b>true</b> to create a bitmap with the smallest possible
		/// color depth for the specified <paramref name="PaletteSize"/>.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		public static FIBITMAP ColorQuantizeEx(FIBITMAP dib, FREE_IMAGE_QUANTIZE quantize, int PaletteSize, RGBQUAD[] ReservePalette, bool minColorDepth)
		{
			FIBITMAP result;
			if (minColorDepth)
			{
				int bpp;
				if (PaletteSize >= 256)
					bpp = 8;
				else if (PaletteSize > 2)
					bpp = 4;
				else
					bpp = 1;
				result = ColorQuantizeEx(dib, quantize, PaletteSize, ReservePalette, bpp);
			}
			else
			{
				result = ColorQuantizeEx(dib, quantize, PaletteSize, ReservePalette, 8);
			}
			return result;
		}

		/// <summary>
		/// ColorQuantizeEx is an extension to the <see cref="ColorQuantize(FIBITMAP, FREE_IMAGE_QUANTIZE)"/>
		/// method that provides additional options used to quantize a 24-bit image to any
		/// number of colors (up to 256), as well as quantize a 24-bit image using a
		/// partial or full provided palette.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="quantize">Specifies the color reduction algorithm to be used.</param>
		/// <param name="PaletteSize">Size of the desired output palette.</param>
		/// <param name="ReservePalette">The provided palette.</param>
		/// <param name="bpp">The desired color depth of the created image.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		public static unsafe FIBITMAP ColorQuantizeEx(FIBITMAP dib, FREE_IMAGE_QUANTIZE quantize, int PaletteSize, RGBQUAD[] ReservePalette, int bpp)
		{
			FIBITMAP result = FIBITMAP.Zero;
			FIBITMAP temp = FIBITMAP.Zero;
			int reservedSize = ReservePalette?.Length ?? 0;

			if (bpp == 8)
			{
				result = ColorQuantizeEx(dib, quantize, PaletteSize, reservedSize, ReservePalette);
			}
			else if (bpp == 4)
			{
				temp = ColorQuantizeEx(dib, quantize, Math.Min(16, PaletteSize), reservedSize, ReservePalette);
				if (!temp.IsNull)
				{
					result = Allocate((int)GetWidth(temp), (int)GetHeight(temp), 4, 0, 0, 0);
					CloneMetadata(result, temp);
					CopyMemory(GetPalette(result), GetPalette(temp), paletteColors: 16);

					for (int y = (int)GetHeight(temp) - 1; y >= 0; y--)
					{
						Scanline<byte> srcScanline = new Scanline<byte>(temp, y);
						Scanline<FI4BIT> dstScanline = new Scanline<FI4BIT>(result, y);

						for (int x = (int)GetWidth(temp) - 1; x >= 0; x--)
						{
							dstScanline[x] = srcScanline[x];
						}
					}
				}
			}
			else if (bpp == 1)
			{
				temp = ColorQuantizeEx(dib, quantize, 2, reservedSize, ReservePalette);
				if (!temp.IsNull)
				{
					result = Allocate((int)GetWidth(temp), (int)GetHeight(temp), 1, 0, 0, 0);
					CloneMetadata(result, temp);
					CopyMemory(GetPalette(result), GetPalette(temp), paletteColors: 2);

					for (int y = (int)GetHeight(temp) - 1; y >= 0; y--)
					{
						Scanline<byte> srcScanline = new Scanline<byte>(temp, y);
						Scanline<FI1BIT> dstScanline = new Scanline<FI1BIT>(result, y);

						for (int x = (int)GetWidth(temp) - 1; x >= 0; x--)
						{
							dstScanline[x] = srcScanline[x];
						}
					}
				}
			}

			UnloadEx(ref temp);
			return result;

			static void CopyMemory(IntPtr dest, IntPtr src, int paletteColors)
			{
				ref byte dstMemory = ref Unsafe.AsRef<byte>((void*) dest);
				ref byte srcMemory = ref Unsafe.AsRef<byte>((void*) src);

				Unsafe.CopyBlockUnaligned(ref dstMemory, ref srcMemory, (uint) (paletteColors * sizeof(RGBQUAD)));
			}
		}

		#endregion

		#region Metadata

		/// <summary>
		/// Copies metadata from one FreeImage bitmap to another.
		/// </summary>
		/// <param name="src">Source FreeImage bitmap containing the metadata.</param>
		/// <param name="dst">FreeImage bitmap to copy the metadata to.</param>
		/// <param name="flags">Flags to switch different copy modes.</param>
		/// <returns>Returns -1 on failure else the number of copied tags.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="src"/> or <paramref name="dst"/> is null.</exception>
		public static int CloneMetadataEx(FIBITMAP src, FIBITMAP dst, FREE_IMAGE_METADATA_COPY flags)
		{
			if (src.IsNull)
			{
				throw new ArgumentNullException("src");
			}
			if (dst.IsNull)
			{
				throw new ArgumentNullException("dst");
			}

			FITAG tag = new FITAG(), tag2 = new FITAG();
			int copied = 0;

			// Clear all existing metadata
			if ((flags & FREE_IMAGE_METADATA_COPY.CLEAR_EXISTING) > 0)
			{
				foreach (FREE_IMAGE_MDMODEL model in FREE_IMAGE_MDMODELS)
				{
					if (!SetMetadata(model, dst, null, tag))
					{
						return -1;
					}
				}
			}

			bool keep = !((flags & FREE_IMAGE_METADATA_COPY.REPLACE_EXISTING) > 0);

			foreach (FREE_IMAGE_MDMODEL model in FREE_IMAGE_MDMODELS)
			{
				FIMETADATA mData = FindFirstMetadata(model, src, out tag);
				if (mData.IsNull) continue;
				do
				{
					string key = GetTagKey(tag);
					if (!(keep && GetMetadata(model, dst, key, out tag2)))
					{
						if (SetMetadata(model, dst, key, tag))
						{
							copied++;
						}
					}
				}
				while (FindNextMetadata(mData, out tag));
				FindCloseMetadata(mData);
			}

			return copied;
		}

		/// <summary>
		/// Returns the comment of a JPEG, PNG or GIF image.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <returns>Comment of the FreeImage bitmp, or null in case no comment exists.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static string GetImageComment(FIBITMAP dib)
		{
			string result = null;
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			FITAG tag;
			if (GetMetadata(FREE_IMAGE_MDMODEL.FIMD_COMMENTS, dib, "Comment", out tag))
			{
				MetadataTag metadataTag = new MetadataTag(tag, FREE_IMAGE_MDMODEL.FIMD_COMMENTS);
				result = metadataTag.Value as string;
			}
			return result;
		}

		/// <summary>
		/// Sets the comment of a JPEG, PNG or GIF image.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="comment">New comment of the FreeImage bitmap.
		/// Use null to remove the comment.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static bool SetImageComment(FIBITMAP dib, string comment)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			bool result;
			if (comment != null)
			{
				FITAG tag = CreateTag();
				MetadataTag metadataTag = new MetadataTag(tag, FREE_IMAGE_MDMODEL.FIMD_COMMENTS);
				metadataTag.Value = comment;
				result = SetMetadata(FREE_IMAGE_MDMODEL.FIMD_COMMENTS, dib, "Comment", tag);
				DeleteTag(tag);
			}
			else
			{
				result = SetMetadata(FREE_IMAGE_MDMODEL.FIMD_COMMENTS, dib, "Comment", FITAG.Zero);
			}
			return result;
		}

		/// <summary>
		/// Retrieve a metadata attached to a FreeImage bitmap.
		/// </summary>
		/// <param name="model">The metadata model to look for.</param>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="key">The metadata field name.</param>
		/// <param name="tag">A <see cref="MetadataTag"/> structure returned by the function.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static bool GetMetadata(
			FREE_IMAGE_MDMODEL model,
			FIBITMAP dib,
			string key,
			out MetadataTag tag)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}

			bool result;
			if (GetMetadata(model, dib, key, out FITAG _tag))
			{
				tag = new MetadataTag(_tag, model);
				result = true;
			}
			else
			{
				tag = null;
				result = false;
			}
			return result;
		}

		/// <summary>
		/// Attach a new metadata tag to a FreeImage bitmap.
		/// </summary>
		/// <param name="model">The metadata model used to store the tag.</param>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="key">The tag field name.</param>
		/// <param name="tag">The <see cref="MetadataTag"/> to be attached.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static bool SetMetadata(
			FREE_IMAGE_MDMODEL model,
			FIBITMAP dib,
			string key,
			MetadataTag tag)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}
			return SetMetadata(model, dib, key, tag.tag);
		}

		/// <summary>
		/// Provides information about the first instance of a tag that matches the metadata model.
		/// </summary>
		/// <param name="model">The model to match.</param>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="tag">Tag that matches the metadata model.</param>
		/// <returns>Unique search handle that can be used to call FindNextMetadata or FindCloseMetadata.
		/// Null if the metadata model does not exist.</returns>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static FIMETADATA FindFirstMetadata(
			FREE_IMAGE_MDMODEL model,
			FIBITMAP dib,
			out MetadataTag tag)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}

			FIMETADATA result = FindFirstMetadata(model, dib, out FITAG _tag);
			if (result.IsNull)
			{
				tag = null;
				return result;
			}
			tag = new MetadataTag(_tag, model);

			metaDataSearchHandler[result] = model;
			
			return result;
		}

		/// <summary>
		/// Find the next tag, if any, that matches the metadata model argument in a previous call
		/// to FindFirstMetadata, and then alters the tag object contents accordingly.
		/// </summary>
		/// <param name="mdhandle">Unique search handle provided by FindFirstMetadata.</param>
		/// <param name="tag">Tag that matches the metadata model.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		public static bool FindNextMetadata(FIMETADATA mdhandle, out MetadataTag tag)
		{
			bool result;
			if (FindNextMetadata(mdhandle, out FITAG _tag))
			{
				tag = new MetadataTag(_tag, metaDataSearchHandler[mdhandle]);
				result = true;
			}
			else
			{
				tag = null;
				result = false;
			}
			return result;
		}

		/// <summary>
		/// Closes the specified metadata search handle and releases associated resources.
		/// </summary>
		/// <param name="mdhandle">The handle to close.</param>
		public static void FindCloseMetadata(FIMETADATA mdhandle)
		{
			if (metaDataSearchHandler.ContainsKey(mdhandle))
			{
				metaDataSearchHandler.Remove(mdhandle);
			}
			FindCloseMetadata_(mdhandle);
		}

		/// <summary>
		/// This dictionary links FIMETADATA handles and FREE_IMAGE_MDMODEL models.
		/// </summary>
		private static Dictionary<FIMETADATA, FREE_IMAGE_MDMODEL> metaDataSearchHandler = new(1);

		#endregion

		#region Rotation and Flipping

		/// <summary>
		/// This function rotates a 1-, 8-bit greyscale or a 24-, 32-bit color image by means of 3 shears.
		/// 1-bit images rotation is limited to integer multiple of 90°.
		/// <c>null</c> is returned for other values.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="angle">The angle of rotation.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		public static FIBITMAP Rotate(FIBITMAP dib, double angle)
		{
			return Rotate(dib, angle, IntPtr.Zero);
		}

		/// <summary>
		/// This function rotates a 1-, 8-bit greyscale or a 24-, 32-bit color image by means of 3 shears.
		/// 1-bit images rotation is limited to integer multiple of 90°.
		/// <c>null</c> is returned for other values.
		/// </summary>
		/// <typeparam name="T">The type of the color to use as background.</typeparam>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="angle">The angle of rotation.</param>
		/// <param name="backgroundColor">The color used used to fill the bitmap's background.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		public static FIBITMAP Rotate<T>(FIBITMAP dib, double angle, T? backgroundColor) where T : struct
		{
			if (backgroundColor.HasValue)
			{
				GCHandle handle = new GCHandle();
				try
				{
					T[] buffer = new T[] { backgroundColor.Value };
					handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
					return Rotate(dib, angle, handle.AddrOfPinnedObject());
				}
				finally
				{
					if (handle.IsAllocated)
						handle.Free();
				}
			}

			return Rotate(dib, angle, IntPtr.Zero);
		}

		/// <summary>
		/// Rotates a 4-bit color FreeImage bitmap.
		/// Allowed values for <paramref name="angle"/> are 90, 180 and 270.
		/// In case <paramref name="angle"/> is 0 or 360 a clone is returned.
		/// 0 is returned for other values or in case the rotation fails.
		/// </summary>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="angle">The angle of rotation.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <remarks>
		/// This function is kind of temporary due to FreeImage's lack of
		/// rotating 4-bit images. It's particularly used by <see cref="FreeImageBitmap"/>'s
		/// method RotateFlip. This function will be removed as soon as FreeImage
		/// supports rotating 4-bit images.
		/// </remarks>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="dib"/> is null.</exception>
		public static unsafe FIBITMAP Rotate4bit(FIBITMAP dib, double angle)
		{
			if (dib.IsNull)
			{
				throw new ArgumentNullException("dib");
			}

			FIBITMAP result = new FIBITMAP();
			int ang = (int)angle;

			if ((GetImageType(dib) == FREE_IMAGE_TYPE.FIT_BITMAP) &&
				(GetBPP(dib) == 4) &&
				((ang % 90) == 0))
			{
				int xOrg, yOrg;
				Scanline<FI4BIT>[] src, dst;
				var width = (int)GetWidth(dib);
				var height = (int)GetHeight(dib);
				byte index = 0;
				switch (ang)
				{
					case 90:
						result = Allocate(height, width, 4, 0, 0, 0);
						if (result.IsNull)
						{
							break;
						}
						CopyPalette(dib, result);
						src = Get04BitScanlines(dib);
						dst = Get04BitScanlines(result);
						for (int y = 0; y < width; y++)
						{
							yOrg = height - 1;
							for (int x = 0; x < height; x++, yOrg--)
							{
								index = src[yOrg][y];
								dst[y][x] = index;
							}
						}
						break;
					case 180:
						result = Allocate(width, height, 4, 0, 0, 0);
						if (result.IsNull)
						{
							break;
						}
						CopyPalette(dib, result);
						src = Get04BitScanlines(dib);
						dst = Get04BitScanlines(result);

						yOrg = height - 1;
						for (int y = 0; y < height; y++, yOrg--)
						{
							xOrg = width - 1;
							for (int x = 0; x < width; x++, xOrg--)
							{
								index = src[yOrg][xOrg];
								dst[y][x] = index;
							}
						}
						break;
					case 270:
						result = Allocate(height, width, 4, 0, 0, 0);
						if (result.IsNull)
						{
							break;
						}
						CopyPalette(dib, result);
						src = Get04BitScanlines(dib);
						dst = Get04BitScanlines(result);
						xOrg = width - 1;
						for (int y = 0; y < width; y++, xOrg--)
						{
							for (int x = 0; x < height; x++)
							{
								index = src[x][xOrg];
								dst[y][x] = index;
							}
						}
						break;
					case 0:
					case 360:
						result = Clone(dib);
						break;
				}
			}
			return result;
		}

		#endregion

		#region Upsampling / downsampling

		/// <summary>
		/// Enlarges or shrinks the FreeImage bitmap selectively per side and fills newly added areas
		/// with the specified background color. See remarks for further details.
		/// </summary>
		/// <typeparam name="T">The type of the specified color.</typeparam>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="left">The number of pixels, the image should be enlarged on its left side.
		/// Negative values shrink the image on its left side.</param>
		/// <param name="top">The number of pixels, the image should be enlarged on its top side.
		/// Negative values shrink the image on its top side.</param>
		/// <param name="right">The number of pixels, the image should be enlarged on its right side.
		/// Negative values shrink the image on its right side.</param>
		/// <param name="bottom">The number of pixels, the image should be enlarged on its bottom side.
		/// Negative values shrink the image on its bottom side.</param>
		/// <param name="color">The color, the enlarged sides of the image should be filled with.</param>
		/// <param name="options">Options that affect the color search process for palletized images.</param>
		/// <returns>Handle to a FreeImage bitmap.</returns>
		/// <remarks>
		/// This function enlarges or shrinks an image selectively per side.
		/// The main purpose of this function is to add borders to an image.
		/// To add a border to any of the image's sides, a positive integer value must be passed in
		/// any of the parameters <paramref name="left"/>, <paramref name="top"/>, <paramref name="right"/>
		/// or <paramref name="bottom"/>. This value represents the border's
		/// width in pixels. Newly created parts of the image (the border areas) are filled with the
		/// specified <paramref name="color"/>.
		/// Specifying a negative integer value for a certain side, will shrink or crop the image on
		/// this side. Consequently, specifying zero for a certain side will not change the image's
		/// extension on that side.
		/// <para/>
		/// So, calling this function with all parameters <paramref name="left"/>, <paramref name="top"/>,
		/// <paramref name="right"/> and <paramref name="bottom"/> set to zero, is
		/// effectively the same as calling function <see cref="Clone"/>; setting all parameters
		/// <paramref name="left"/>, <paramref name="top"/>, <paramref name="right"/> and
		/// <paramref name="bottom"/> to value equal to or smaller than zero, my easily be substituted
		/// by a call to function <see cref="Copy"/>. Both these cases produce a new image, which is
		/// guaranteed not to be larger than the input image. Thus, since the specified
		/// <paramref name="color"/> is not needed in these cases, <paramref name="color"/>
		/// may be <c>null</c>.
		/// <para/>
		/// Both parameters <paramref name="color"/> and <paramref name="options"/> work according to
		/// function <see cref="FillBackground&lt;T&gt;"/>. So, please refer to the documentation of
		/// <see cref="FillBackground&lt;T&gt;"/> to learn more about parameters <paramref name="color"/>
		/// and <paramref name="options"/>. For palletized images, the palette of the input image is
		/// transparently copied to the newly created enlarged or shrunken image, so any color look-ups
		/// are performed on this palette.
		/// </remarks>
		/// <example>
		/// // create a white color<br/>
		/// RGBQUAD c;<br/>
		/// c.rgbRed = 0xFF;<br/>
		/// c.rgbGreen = 0xFF;<br/>
		/// c.rgbBlue = 0xFF;<br/>
		/// c.rgbReserved = 0x00;<br/>
		/// <br/>
		/// // add a white, symmetric 10 pixel wide border to the image<br/>
		/// dib2 = FreeImage_EnlargeCanvas(dib, 10, 10, 10, 10, c, FREE_IMAGE_COLOR_OPTIONS.FICO_RGB);<br/>
		/// <br/>
		/// // add white, 20 pixel wide stripes to the top and bottom side of the image<br/>
		/// dib3 = FreeImage_EnlargeCanvas(dib, 0, 20, 0, 20, c, FREE_IMAGE_COLOR_OPTIONS.FICO_RGB);<br/>
		/// <br/>
		/// // add white, 30 pixel wide stripes to the right side of the image and<br/>
		/// // cut off the 40 leftmost pixel columns<br/>
		/// dib3 = FreeImage_EnlargeCanvas(dib, -40, 0, 30, 0, c, FREE_IMAGE_COLOR_OPTIONS.FICO_RGB);<br/>
		/// </example>
		public static FIBITMAP EnlargeCanvas<T>(FIBITMAP dib, int left, int top, int right, int bottom,
			T? color, FREE_IMAGE_COLOR_OPTIONS options) where T : struct
		{
			if (dib.IsNull)
				return FIBITMAP.Zero;

			if (color.HasValue)
			{
				if (!CheckColorType(GetImageType(dib), color.Value))
					return FIBITMAP.Zero;

				GCHandle handle = new GCHandle();
				try
				{
					T[] buffer = new T[] { color.Value };
					handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
					return EnlargeCanvas(dib, left, top, right, bottom, handle.AddrOfPinnedObject(), options);
				}
				finally
				{
					if (handle.IsAllocated)
						handle.Free();
				}
			}

			return EnlargeCanvas(dib, left, top, right, bottom, IntPtr.Zero, options);
		}

		#endregion

		#region Color

		/// <summary>
		/// Sets all pixels of the specified image to the color provided through the
		/// <paramref name="color"/> parameter. See remarks for further details.
		/// </summary>
		/// <typeparam name="T">The type of the specified color.</typeparam>
		/// <param name="dib">Handle to a FreeImage bitmap.</param>
		/// <param name="color">The color to fill the bitmap with. See remarks for further details.</param>
		/// <param name="options">Options that affect the color search process for palletized images.</param>
		/// <returns><c>true</c> on success, <c>false</c> on failure.</returns>
		/// <remarks>
		/// This function sets all pixels of an image to the color provided through
		/// the <paramref name="color"/> parameter. <see cref="RGBQUAD"/> is used for standard type images.
		/// For non standard type images the underlaying structure is used.
		/// <para/>
		/// So, <paramref name="color"/> must be of type <see cref="Double"/>, if the image to be filled is of type
		/// <see cref="FREE_IMAGE_TYPE.FIT_DOUBLE"/> and must be a <see cref="FIRGBF"/> structure if the
		/// image is of type <see cref="FREE_IMAGE_TYPE.FIT_RGBF"/> and so on.
		/// <para/>
		/// However, the fill color is always specified through a <see cref="RGBQUAD"/> structure
		/// for all images of type <see cref="FREE_IMAGE_TYPE.FIT_BITMAP"/>.
		/// So, for 32- and 24-bit images, the red, green and blue members of the <see cref="RGBQUAD"/>
		/// structure are directly used for the image's red, green and blue channel respectively.
		/// Although alpha transparent <see cref="RGBQUAD"/> colors are
		/// supported, the alpha channel of a 32-bit image never gets modified by this function.
		/// A fill color with an alpha value smaller than 255 gets blended with the image's actual
		/// background color, which is determined from the image's bottom-left pixel.
		/// So, currently using alpha enabled colors, assumes the image to be unicolor before the
		/// fill operation. However, the <see cref="RGBQUAD.rgbReserved"/> field is only taken into account,
		/// if option <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_RGBA"/> has been specified.
		/// <para/>
		/// For 16-bit images, the red-, green- and blue components of the specified color are
		/// transparently translated into either the 16-bit 555 or 565 representation. This depends
		/// on the image's actual red- green- and blue masks.
		/// <para/>
		/// Special attention must be payed for palletized images. Generally, the RGB color specified
		/// is looked up in the image's palette. The found palette index is then used to fill the image.
		/// There are some option flags, that affect this lookup process:
		/// <list type="table">
		/// <listheader>
		/// <term>Value</term>
		/// <description>Meaning</description>
		/// </listheader>
		/// <item>
		/// <term><see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_DEFAULT"/></term>
		/// <description>
		/// Uses the color, that is nearest to the specified color.
		/// This is the default behavior and should always find a
		/// color in the palette. However, the visual result may
		/// far from what was expected and mainly depends on the
		/// image's palette.
		/// </description>
		/// </item>
		/// <item>
		/// <term><see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_EQUAL_COLOR"/></term>
		/// <description>
		/// Searches the image's palette for the specified color
		/// but only uses the returned palette index, if the specified
		/// color exactly matches the palette entry. Of course,
		/// depending on the image's actual palette entries, this
		/// operation may fail. In this case, the function falls back
		/// to option <see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/>
		/// and uses the RGBQUAD's rgbReserved member (or its low nibble for 4-bit images
		/// or its least significant bit (LSB) for 1-bit images) as
		/// the palette index used for the fill operation.
		/// </description>
		/// </item>
		/// <item>
		/// <term><see cref="FREE_IMAGE_COLOR_OPTIONS.FICO_ALPHA_IS_INDEX"/></term>
		/// <description>
		/// Does not perform any color lookup from the palette, but
		/// uses the RGBQUAD's alpha channel member rgbReserved as
		/// the palette index to be used for the fill operation.
		/// However, for 4-bit images, only the low nibble of the
		/// rgbReserved member are used and for 1-bit images, only
		/// the least significant bit (LSB) is used.
		/// </description>
		/// </item>
		/// </list>
		/// </remarks>
		public static bool FillBackground<T>(FIBITMAP dib, T color, FREE_IMAGE_COLOR_OPTIONS options)
			where T : struct
		{
			if (dib.IsNull)
				return false;

			if (!CheckColorType(GetImageType(dib), color))
				return false;

			GCHandle handle = new GCHandle();
			try
			{
				T[] buffer = new T[] { color };
				handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
				return FillBackground(dib, handle.AddrOfPinnedObject(), options);
			}
			finally
			{
				if (handle.IsAllocated)
					handle.Free();
			}
		}

		#endregion

		#region Wrapper functions

		/// <summary>
		/// Returns the next higher possible color depth.
		/// </summary>
		/// <param name="bpp">Color depth to increase.</param>
		/// <returns>The next higher color depth or 0 if there is no valid color depth.</returns>
		internal static int GetNextColorDepth(int bpp)
		{
			int result = bpp switch
			{
				1 => 4,
				4 => 8,
				8 => 16,
				16 => 24,
				24 => 32,
				_ => 0
			};
			return result;
		}

		/// <summary>
		/// Returns the next lower possible color depth.
		/// </summary>
		/// <param name="bpp">Color depth to decrease.</param>
		/// <returns>The next lower color depth or 0 if there is no valid color depth.</returns>
		internal static int GetPrevousColorDepth(int bpp)
		{
			int result = bpp switch
			{
				32 => 24,
				24 => 16,
				16 => 8,
				8 => 4,
				4 => 1,
				_ => 0
			};
			return result;
		}

		/// <summary>
		/// Reads a null-terminated c-string.
		/// </summary>
		/// <param name="ptr">Pointer to the first char of the string.</param>
		/// <returns>The converted string.</returns>
		internal static unsafe string PtrToStr(byte* ptr)
		{
			string result = null;
			if (ptr != null)
			{
				System.Text.StringBuilder sb = new System.Text.StringBuilder();

				while (*ptr != 0)
				{
					sb.Append((char)(*(ptr++)));
				}
				result = sb.ToString();
			}
			return result;
		}

		internal static unsafe byte[] CreateShrunkenPaletteLUT(FIBITMAP dib, out int uniqueColors)
		{
			byte[] result = null;
			uniqueColors = 0;

			if ((!dib.IsNull) && (GetImageType(dib) == FREE_IMAGE_TYPE.FIT_BITMAP) && (GetBPP(dib) <= 8))
			{
				int size = (int)GetColorsUsed(dib);
				List<RGBQUAD> newPalette = new List<RGBQUAD>(size);
				List<byte> lut = new List<byte>(size);
				RGBQUAD* palette = (RGBQUAD*)GetPalette(dib);
				RGBQUAD color;
				int index;

				for (int i = 0; i < size; i++)
				{
					color = palette[i];
					color.rgbReserved = 255; // ignore alpha

					index = newPalette.IndexOf(color);
					if (index < 0)
					{
						newPalette.Add(color);
						lut.Add((byte)(newPalette.Count - 1));
					}
					else
					{
						lut.Add((byte)index);
					}
				}
				result = lut.ToArray();
				uniqueColors = newPalette.Count;
			}
			return result;
		}

		internal static PropertyItem CreatePropertyItem()
		{
			return (PropertyItem)Activator.CreateInstance(typeof(PropertyItem), true);
		}

		private static unsafe void CopyPalette(FIBITMAP src, FIBITMAP dst)
		{
			uint size = (uint)(sizeof(RGBQUAD) * GetColorsUsed(src));

			ref byte dstPalleteBytes = ref Unsafe.AsRef<byte>((RGBQUAD*) GetPalette(dst));
			ref byte srcPalleteBytes = ref Unsafe.AsRef<byte>((RGBQUAD*) GetPalette(src));
			Unsafe.CopyBlockUnaligned(ref dstPalleteBytes, ref srcPalleteBytes, size);
		}

		private static unsafe Scanline<FI4BIT>[] Get04BitScanlines(FIBITMAP dib)
		{
			int height = (int)GetHeight(dib);
			Scanline<FI4BIT>[] array = new Scanline<FI4BIT>[height];
			for (int i = 0; i < height; i++)
			{
				array[i] = new Scanline<FI4BIT>(dib, i);
			}
			return array;
		}

		/// <summary>
		/// Changes a bitmaps color depth.
		/// Used by SaveEx and SaveToStream.
		/// </summary>
		private static FIBITMAP PrepareBitmapColorDepth(FIBITMAP dibToSave, FREE_IMAGE_FORMAT format, FREE_IMAGE_COLOR_DEPTH colorDepth)
		{
			FREE_IMAGE_TYPE type = GetImageType(dibToSave);
			if (type == FREE_IMAGE_TYPE.FIT_BITMAP)
			{
				int bpp = (int)GetBPP(dibToSave);
				int targetBpp = (int)(colorDepth & FREE_IMAGE_COLOR_DEPTH.FICD_COLOR_MASK);

				if (colorDepth != FREE_IMAGE_COLOR_DEPTH.FICD_AUTO)
				{
					// A fix colordepth was chosen
					if (FIFSupportsExportBPP(format, targetBpp))
					{
						dibToSave = ConvertColorDepth(dibToSave, colorDepth, false);
					}
					else
					{
						throw new ArgumentException("FreeImage\n\nFreeImage Library plugin " +
							GetFormatFromFIF(format) + " is unable to write images with a color depth of " +
							targetBpp + " bpp.");
					}
				}
				else
				{
					// Auto selection was chosen
					if (!FIFSupportsExportBPP(format, bpp))
					{
						// The color depth is not supported
						int bppUpper = bpp;
						int bppLower = bpp;
						// Check from the bitmaps current color depth in both directions
						do
						{
							bppUpper = GetNextColorDepth(bppUpper);
							if (FIFSupportsExportBPP(format, bppUpper))
							{
								dibToSave = ConvertColorDepth(dibToSave, (FREE_IMAGE_COLOR_DEPTH)bppUpper, false);
								break;
							}
							bppLower = GetPrevousColorDepth(bppLower);
							if (FIFSupportsExportBPP(format, bppLower))
							{
								dibToSave = ConvertColorDepth(dibToSave, (FREE_IMAGE_COLOR_DEPTH)bppLower, false);
								break;
							}
						} while (!((bppLower == 0) && (bppUpper == 0)));
					}
				}
			}
			return dibToSave;
		}

		/// <summary>
		/// Compares blocks of memory.
		/// </summary>
		/// <param name="buf1">A pointer to a block of memory to compare.</param>
		/// <param name="buf2">A pointer to a block of memory to compare.</param>
		/// <param name="length">Specifies the number of bytes to be compared.</param>
		/// <returns>true, if all bytes compare as equal, false otherwise.</returns>
		public static unsafe bool CompareMemory(void* buf1, void* buf2, uint length)
		{
			return (length == RtlCompareMemory(buf1, buf2, length));
		}

		/// <summary>
		/// Compares blocks of memory.
		/// </summary>
		/// <param name="buf1">A pointer to a block of memory to compare.</param>
		/// <param name="buf2">A pointer to a block of memory to compare.</param>
		/// <param name="length">Specifies the number of bytes to be compared.</param>
		/// <returns>true, if all bytes compare as equal, false otherwise.</returns>
		public static unsafe bool CompareMemory(void* buf1, void* buf2, long length)
		{
			return (length == RtlCompareMemory(buf1, buf2, checked((uint)length)));
		}

		/// <summary>
		/// Compares blocks of memory.
		/// </summary>
		/// <param name="buf1">A pointer to a block of memory to compare.</param>
		/// <param name="buf2">A pointer to a block of memory to compare.</param>
		/// <param name="length">Specifies the number of bytes to be compared.</param>
		/// <returns>true, if all bytes compare as equal, false otherwise.</returns>
		public static unsafe bool CompareMemory(IntPtr buf1, IntPtr buf2, uint length)
		{
			return (length == RtlCompareMemory(buf1.ToPointer(), buf2.ToPointer(), length));
		}

		/// <summary>
		/// Compares blocks of memory.
		/// </summary>
		/// <param name="buf1">A pointer to a block of memory to compare.</param>
		/// <param name="buf2">A pointer to a block of memory to compare.</param>
		/// <param name="length">Specifies the number of bytes to be compared.</param>
		/// <returns>true, if all bytes compare as equal, false otherwise.</returns>
		public static unsafe bool CompareMemory(IntPtr buf1, IntPtr buf2, long length)
		{
			return (length == RtlCompareMemory(buf1.ToPointer(), buf2.ToPointer(), checked((uint)length)));
		}

		internal static string ColorToString(Color color)
		{
			return string.Format(
				System.Globalization.CultureInfo.CurrentCulture,
				"{{Name={0}, ARGB=({1}, {2}, {3}, {4})}}",
				new object[] { color.Name, color.A, color.R, color.G, color.B });
		}

		internal static void Resize(ref string str, int length)
		{
			if ((str != null) && (length >= 0) && (str.Length != length))
			{
				char[] chars = str.ToCharArray();
				Array.Resize(ref chars, length);
				str = new string(chars);
			}
		}

		internal static void Resize(ref string str, int min, int max)
		{
			if ((str != null) && (min >= 0) && (max >= 0) && (min <= max))
			{
				if (str.Length < min)
				{
					char[] chars = str.ToCharArray();
					Array.Resize(ref chars, min);
					str = new string(chars);
				}
				else if (str.Length > max)
				{
					char[] chars = str.ToCharArray();
					Array.Resize(ref chars, max);
					str = new string(chars);
				}
			}
		}

		internal static void Resize<T>(ref T[] array, int length)
		{
			if ((array != null) && (length >= 0) && (array.Length != length))
			{
				Array.Resize(ref array, length);
			}
		}

		internal static void Resize<T>(ref T[] array, int min, int max)
		{
			if ((array != null) && (min >= 0) && (max >= 0) && (min <= max))
			{
				if (array.Length < min)
				{
					Array.Resize(ref array, min);
				}
				else if (array.Length > max)
				{
					Array.Resize(ref array, max);
				}
			}
		}

		internal static bool CheckColorType<T>(FREE_IMAGE_TYPE imageType, T color)
		{
			Type type = typeof(T);
			bool result = imageType switch
			{
				FREE_IMAGE_TYPE.FIT_BITMAP => (type == typeof(RGBQUAD)),
				FREE_IMAGE_TYPE.FIT_COMPLEX => (type == typeof(FICOMPLEX)),
				FREE_IMAGE_TYPE.FIT_DOUBLE => (type == typeof(double)),
				FREE_IMAGE_TYPE.FIT_FLOAT => (type == typeof(float)),
				FREE_IMAGE_TYPE.FIT_INT16 => (type == typeof(Int16)),
				FREE_IMAGE_TYPE.FIT_INT32 => (type == typeof(Int32)),
				FREE_IMAGE_TYPE.FIT_RGB16 => (type == typeof(FIRGB16)),
				FREE_IMAGE_TYPE.FIT_RGBA16 => (type == typeof(FIRGBA16)),
				FREE_IMAGE_TYPE.FIT_RGBAF => (type == typeof(FIRGBAF)),
				FREE_IMAGE_TYPE.FIT_RGBF => (type == typeof(FIRGBF)),
				FREE_IMAGE_TYPE.FIT_UINT16 => (type == typeof(UInt16)),
				FREE_IMAGE_TYPE.FIT_UINT32 => (type == typeof(UInt32)),
				_ => false
			};
			return result;
		}

		#endregion

		#region Dll-Imports

		/// <summary>
		/// Retrieves a handle to a display device context (DC) for the client area of a specified window
		/// or for the entire screen. You can use the returned handle in subsequent GDI functions to draw in the DC.
		/// </summary>
		/// <param name="hWnd">Handle to the window whose DC is to be retrieved.
		/// If this value is IntPtr.Zero, GetDC retrieves the DC for the entire screen. </param>
		/// <returns>If the function succeeds, the return value is a handle to the DC for the specified window's client area.
		/// If the function fails, the return value is NULL.</returns>
		[DllImport("user32.dll")]
		private static extern IntPtr GetDC(IntPtr hWnd);

		/// <summary>
		/// Releases a device context (DC), freeing it for use by other applications.
		/// The effect of the ReleaseDC function depends on the type of DC. It frees only common and window DCs.
		/// It has no effect on class or private DCs.
		/// </summary>
		/// <param name="hWnd">Handle to the window whose DC is to be released.</param>
		/// <param name="hDC">Handle to the DC to be released.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		[DllImport("user32.dll")]
		private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

		/// <summary>
		/// Creates a DIB that applications can write to directly.
		/// The function gives you a pointer to the location of the bitmap bit values.
		/// You can supply a handle to a file-mapping object that the function will use to create the bitmap,
		/// or you can let the system allocate the memory for the bitmap.
		/// </summary>
		/// <param name="hdc">Handle to a device context.</param>
		/// <param name="pbmi">Pointer to a BITMAPINFO structure that specifies various attributes of the DIB,
		/// including the bitmap dimensions and colors.</param>
		/// <param name="iUsage">Specifies the type of data contained in the bmiColors array member of the BITMAPINFO structure
		/// pointed to by pbmi (either logical palette indexes or literal RGB values).</param>
		/// <param name="ppvBits">Pointer to a variable that receives a pointer to the location of the DIB bit values.</param>
		/// <param name="hSection">Handle to a file-mapping object that the function will use to create the DIB.
		/// This parameter can be NULL.</param>
		/// <param name="dwOffset">Specifies the offset from the beginning of the file-mapping object referenced by hSection
		/// where storage for the bitmap bit values is to begin. This value is ignored if hSection is NULL.</param>
		/// <returns>If the function succeeds, the return value is a handle to the newly created DIB,
		/// and *ppvBits points to the bitmap bit values. If the function fails, the return value is NULL, and *ppvBits is NULL.</returns>
		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateDIBSection(
			IntPtr hdc,
			[In] IntPtr pbmi,
			uint iUsage,
			out IntPtr ppvBits,
			IntPtr hSection,
			uint dwOffset);

		/// <summary>
		/// Deletes a logical pen, brush, font, bitmap, region, or palette, freeing all system resources associated with the object.
		/// After the object is deleted, the specified handle is no longer valid.
		/// </summary>
		/// <param name="hObject">Handle to a logical pen, brush, font, bitmap, region, or palette.</param>
		/// <returns>Returns true on success, false on failure.</returns>
		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr hObject);

		/// <summary>
		/// Creates a compatible bitmap (DDB) from a DIB and, optionally, sets the bitmap bits.
		/// </summary>
		/// <param name="hdc">Handle to a device context.</param>
		/// <param name="lpbmih">Pointer to a bitmap information header structure.</param>
		/// <param name="fdwInit">Specifies how the system initializes the bitmap bits - (use 4).</param>
		/// <param name="lpbInit">Pointer to an array of bytes containing the initial bitmap data.</param>
		/// <param name="lpbmi">Pointer to a BITMAPINFO structure that describes the dimensions
		/// and color format of the array pointed to by the lpbInit parameter.</param>
		/// <param name="fuUsage">Specifies whether the bmiColors member of the BITMAPINFO structure
		/// was initialized - (use 0).</param>
		/// <returns>Handle to a DIB or null on failure.</returns>
		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateDIBitmap(
			IntPtr hdc,
			IntPtr lpbmih,
			uint fdwInit,
			IntPtr lpbInit,
			IntPtr lpbmi,
			uint fuUsage);

		/// <summary>
		/// Retrieves information for the specified graphics object.
		/// </summary>
		/// <param name="hgdiobj">Handle to the graphics object of interest.</param>
		/// <param name="cbBuffer">Specifies the number of bytes of information to
		/// be written to the buffer.</param>
		/// <param name="lpvObject">Pointer to a buffer that receives the information
		/// about the specified graphics object.</param>
		/// <returns>0 on failure.</returns>
		[DllImport("gdi32.dll")]
		private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, IntPtr lpvObject);

		/// <summary>
		/// Retrieves the bits of the specified compatible bitmap and copies them into a buffer
		/// as a DIB using the specified format.
		/// </summary>
		/// <param name="hdc">Handle to the device context.</param>
		/// <param name="hbmp">Handle to the bitmap. This must be a compatible bitmap (DDB).</param>
		/// <param name="uStartScan">Specifies the first scan line to retrieve.</param>
		/// <param name="cScanLines">Specifies the number of scan lines to retrieve.</param>
		/// <param name="lpvBits">Pointer to a buffer to receive the bitmap data.</param>
		/// <param name="lpbmi">Pointer to a BITMAPINFO structure that specifies the desired
		/// format for the DIB data.</param>
		/// <param name="uUsage">Specifies the format of the bmiColors member of the
		/// BITMAPINFO structure - (use 0).</param>
		/// <returns>0 on failure.</returns>
		[DllImport("gdi32.dll")]
		private static extern unsafe int GetDIBits(
			IntPtr hdc,
			IntPtr hbmp,
			uint uStartScan,
			uint cScanLines,
			IntPtr lpvBits,
			IntPtr lpbmi,
			uint uUsage);

		/// <summary>
		/// The RtlCompareMemory routine compares blocks of memory
		/// and returns the number of bytes that are equivalent.
		/// </summary>
		/// <param name="buf1">A pointer to a block of memory to compare.</param>
		/// <param name="buf2">A pointer to a block of memory to compare.</param>
		/// <param name="count">Specifies the number of bytes to be compared.</param>
		/// <returns>RtlCompareMemory returns the number of bytes that compare as equal.
		/// If all bytes compare as equal, the input Length is returned.</returns>
		[DllImport("ntdll.dll", EntryPoint = "RtlCompareMemory", SetLastError = false)]
		internal static extern unsafe uint RtlCompareMemory(void* buf1, void* buf2, uint count);

		#endregion
	}
}
