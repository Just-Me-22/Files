// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Shared.Helpers;
using System.Collections.Concurrent;
using System.IO;
using Windows.Storage.FileProperties;

namespace Files.App.Utils.Storage
{
	public static class FileThumbnailHelper
	{
		// The shell serves these from hand-tuned icon frames and cache buckets; in-between sizes can yield padded or regenerated results
		private static readonly uint[] _standardSizes = [16, 24, 32, 48, 64, 96, 128, 256];

		/// <summary>
		/// Returns icon or thumbnail for given file or folder
		/// </summary>
		public static async Task<byte[]?> GetIconAsync(string? path, uint requestedSize, bool isFolder, IconOptions iconOptions)
		{
			var scaledSize = requestedSize * App.AppModel.AppWindowDPI;

			// Snap up to the next standard size; the result is never below the displayed size, so it only ever downscales.
			// Callers can opt out (sidebar) because snapping a small icon up makes the shell return a folder-style variant.
			var size = iconOptions.HasFlag(IconOptions.SkipSizeSnapping)
				? Math.Max(1u, (uint)Math.Round(scaledSize))
				: _standardSizes.FirstOrDefault(s => s >= scaledSize, _standardSizes[^1]);

			if (!isFolder && !iconOptions.HasFlag(IconOptions.ReturnIconOnly) && !iconOptions.HasFlag(IconOptions.ReturnOnlyIfCached))
			{
				var extension = Path.GetExtension(path);

				//Restrict to only %windir%\fonts
				if (FileExtensionHelpers.IsFontFile(extension) && path is not null && PathHelpers.IsInSystemFontsFolder(path))
				{
					var winrtThumbnail = await FontFileHelper.GetWinRTThumbnailAsync(path, (uint)size);
					if (winrtThumbnail is not null)
						return winrtThumbnail;

					if (!string.Equals(extension, ".fon", StringComparison.OrdinalIgnoreCase))
					{
						var fontThumbnail = await STATask.Run(() => FontFileHelper.GenerateFontThumbnail(path, (int)size), App.Logger);
						if (fontThumbnail is not null)
							return fontThumbnail;
					}
				}
			}

			var resolvedPath = path is not null && path.StartsWith(@"\\?\", StringComparison.Ordinal)
				? MtpHelpers.ResolveMtpShellPath(path) ?? path
				: path;

			var isRefusedLocalFile = false;
			var result = await STATask.RunPooled(() => Win32Helper.GetIcon(resolvedPath, (int)size, isFolder, iconOptions, out isRefusedLocalFile), App.Logger);

			if (result is null && path is not null && (isRefusedLocalFile || iconOptions.HasFlag(IconOptions.ReturnOnlyIfCached)))
				result = await GetDecodedThumbnailAsync(path, size, decodeIfMissing: isRefusedLocalFile);

			return result;
		}

		// The shell never caches thumbnails it refused, and decoding one takes ~100 ms, so keep them for revisits and scrolling back
		private static readonly ConcurrentDictionary<(string Path, uint Size), (DateTime Modified, byte[] Data)> _decodedThumbnails = new();
		private static long _decodedThumbnailBytes;
		private const long DecodedThumbnailBytesLimit = 64 * 1024 * 1024;

		private static async Task<byte[]?> GetDecodedThumbnailAsync(string path, uint size, bool decodeIfMissing)
		{
			if (_decodedThumbnails.TryGetValue((path, size), out var cached) && cached.Modified == GetLastWriteTimeUtc(path))
				return cached.Data;

			if (!decodeIfMissing)
				return null;

			var modified = GetLastWriteTimeUtc(path);
			var thumbnail = await BitmapHelper.CreateThumbnailAsync(path, size);
			if (thumbnail is null)
				return null;

			if (Interlocked.Add(ref _decodedThumbnailBytes, thumbnail.Length) > DecodedThumbnailBytesLimit)
			{
				_decodedThumbnails.Clear();
				Interlocked.Exchange(ref _decodedThumbnailBytes, thumbnail.Length);
			}

			_decodedThumbnails[(path, size)] = (modified, thumbnail);
			return thumbnail;
		}

		// Unreadable files report DateTime.MinValue rather than throwing out of a thumbnail request
		private static DateTime GetLastWriteTimeUtc(string path)
			=> SafetyExtensions.IgnoreExceptions(() => File.GetLastWriteTimeUtc(path));

		/// <summary>
		/// Returns overlay for given file or folder
		/// /// </summary>
		/// <param name="path"></param>
		/// <param name="isFolder"></param>
		/// <returns></returns>
		public static async Task<byte[]?> GetIconOverlayAsync(string? path, uint requestedSize, bool isFolder)
		{
			// Overlays render at 32px in thumbnail layouts and 16px in details/columns; scale by DPI so the badge isn't upscaled at fractional scaling
			var overlaySize = (requestedSize >= 48 ? 32u : 16u) * App.AppModel.AppWindowDPI;
			return await STATask.RunPooled(() => Win32Helper.GetIconOverlay(path, (int)overlaySize, isFolder), App.Logger);
		}

		[Obsolete]
		public static async Task<byte[]?> LoadIconFromPathAsync(string filePath, uint thumbnailSize, ThumbnailMode thumbnailMode, ThumbnailOptions thumbnailOptions, bool isFolder = false)
		{
			var result = await GetIconAsync(filePath, thumbnailSize, isFolder, IconOptions.None);
			return result;
		}
	}
}
