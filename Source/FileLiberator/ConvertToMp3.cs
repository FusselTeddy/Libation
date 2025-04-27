﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AAXClean;
using AAXClean.Codecs;
using DataLayer;
using Dinah.Core.ErrorHandling;
using Dinah.Core.Net.Http;
using FileManager;
using LibationFileManager;

namespace FileLiberator
{
	public class ConvertToMp3 : AudioDecodable
	{
		public override string Name => "Convert to Mp3";
		private Mp4Operation Mp4Operation;
		private readonly AaxDecrypter.AverageSpeed averageSpeed = new();
		private static string Mp3FileName(string m4bPath) => Path.ChangeExtension(m4bPath ?? "", ".mp3");

		public override Task CancelAsync() => Mp4Operation?.CancelAsync() ?? Task.CompletedTask;		

		public static bool ValidateMp3(LibraryBook libraryBook)
		{
			var paths = AudibleFileStorage.Audio.GetPaths(libraryBook.Book.AudibleProductId);
			return paths.Any(path => path?.ToString()?.ToLower()?.EndsWith(".m4b") == true && !File.Exists(Mp3FileName(path)));
		}

		public override bool Validate(LibraryBook libraryBook) => ValidateMp3(libraryBook);

		public override async Task<StatusHandler> ProcessAsync(LibraryBook libraryBook)
		{
			OnBegin(libraryBook);

			try
			{
				var m4bPaths = AudibleFileStorage.Audio.GetPaths(libraryBook.Book.AudibleProductId);

				foreach (var m4bPath in m4bPaths)
				{
					var proposedMp3Path = Mp3FileName(m4bPath);
					if (File.Exists(proposedMp3Path) || !File.Exists(m4bPath)) continue;

					var m4bBook = await Task.Run(() => new Mp4File(m4bPath, FileAccess.Read));

					//AAXClean.Codecs only supports decoding AAC and E-AC-3 audio.
					if (m4bBook.AudioSampleEntry.Esds is null && m4bBook.AudioSampleEntry.Dec3 is null)
						continue;

					OnTitleDiscovered(m4bBook.AppleTags.Title);
					OnAuthorsDiscovered(m4bBook.AppleTags.FirstAuthor);
					OnNarratorsDiscovered(m4bBook.AppleTags.Narrator);
					OnCoverImageDiscovered(m4bBook.AppleTags.Cover);

					var config = Configuration.Instance;
					var lameConfig = DownloadOptions.GetLameOptions(config);
					var chapters = m4bBook.GetChaptersFromMetadata();
					//Finishing configuring lame encoder.
					AaxDecrypter.MpegUtil.ConfigureLameOptions(
						m4bBook,
						lameConfig,
						config.LameDownsampleMono,
						config.LameMatchSourceBR,
						chapters);

					if (m4bBook.AppleTags.Tracks is (int trackNum, int trackCount))
					{
						lameConfig.ID3.Track = trackCount > 0 ? $"{trackNum}/{trackCount}" : trackNum.ToString();
					}

					using var mp3File = File.Open(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite);
					try
					{
						Mp4Operation = m4bBook.ConvertToMp3Async(mp3File, lameConfig, chapters);
						Mp4Operation.ConversionProgressUpdate += M4bBook_ConversionProgressUpdate;
						await Mp4Operation;

						if (Mp4Operation.IsCanceled)
						{
							FileUtility.SaferDelete(mp3File.Name);
							return new StatusHandler { "Cancelled" };
						}
						else
						{
							var realMp3Path
								= FileUtility.SaferMoveToValidPath(
									mp3File.Name,
									proposedMp3Path,
									Configuration.Instance.ReplacementCharacters,
									extension: "mp3",
									Configuration.Instance.OverwriteExisting);

							SetFileTime(libraryBook, realMp3Path);
							SetDirectoryTime(libraryBook, Path.GetDirectoryName(realMp3Path));

							OnFileCreated(libraryBook, realMp3Path);
						}
					}
					catch (Exception ex)
					{
						Serilog.Log.Error(ex, "AAXClean error");
						return new StatusHandler { "Conversion failed" };
					}
					finally
					{
						if (Mp4Operation is not null)
							Mp4Operation.ConversionProgressUpdate -= M4bBook_ConversionProgressUpdate;

						m4bBook.InputStream.Close();
						mp3File.Close();
					}
				}
			}
			finally
			{
				OnCompleted(libraryBook);
			}
			return new StatusHandler();
		}

		private void M4bBook_ConversionProgressUpdate(object sender, ConversionProgressEventArgs e)
		{
			averageSpeed.AddPosition(e.ProcessPosition.TotalSeconds);

			var remainingTimeToProcess = (e.EndTime - e.ProcessPosition).TotalSeconds;
			var estTimeRemaining = remainingTimeToProcess / averageSpeed.Average;

			if (double.IsNormal(estTimeRemaining))
				OnStreamingTimeRemaining(TimeSpan.FromSeconds(estTimeRemaining));

			double progressPercent = 100 * e.FractionCompleted;

			OnStreamingProgressChanged(
				new DownloadProgress
				{
					ProgressPercentage = progressPercent,
					BytesReceived = (long)(e.ProcessPosition - e.StartTime).TotalSeconds,
					TotalBytesToReceive = (long)(e.EndTime - e.StartTime).TotalSeconds
				});
		}
	}
}
