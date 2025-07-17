﻿using DataLayer;
using Dinah.Core;
using LibationFileManager;
using LibationUiBase;
using LibationUiBase.GridView;
using ReactiveUI;
using System;
using System.Linq;

#nullable enable
namespace LibationAvalonia.ViewModels
{
	partial class MainVM
	{
		private bool _queueOpen = false;

		/// <summary> The Process Queue panel is open </summary>
		public bool QueueOpen
		{
			get => _queueOpen;
			set
			{
				this.RaiseAndSetIfChanged(ref _queueOpen, value);
				QueueButtonAngle = value ? 180 : 0;
				this.RaisePropertyChanged(nameof(QueueButtonAngle));
			}
		}

		public double QueueButtonAngle { get; private set; }

		private void Configure_ProcessQueue()
		{
			var collapseState = !Configuration.Instance.GetNonString(defaultValue: true, nameof(QueueOpen));
			setQueueCollapseState(collapseState);
		}

		public async void LiberateClicked(LibraryBook[] libraryBooks)
		{
			try
			{
				if (ProcessQueue.QueueDownloadDecrypt(libraryBooks))
					setQueueCollapseState(false);
				else if (libraryBooks.Length == 1 && libraryBooks[0].Book.Audio_Exists())
				{
					// liberated: open explorer to file
					var filePath = AudibleFileStorage.Audio.GetPath(libraryBooks[0].Book.AudibleProductId);
					if (!Go.To.File(filePath?.ShortPathName))
					{
						var suffix = string.IsNullOrWhiteSpace(filePath) ? "" : $":\r\n{filePath}";
						await MessageBox.Show($"File not found" + suffix);
					}
				}
			}
			catch (Exception ex)
			{
				Serilog.Log.Logger.Error(ex, "An error occurred while handling the stop light button click for {libraryBook}", libraryBooks);
			}
		}

		public void LiberateSeriesClicked(ISeriesEntry series)
		{
			try
			{
				Serilog.Log.Logger.Information("Begin backing up all {series} episodes", series.LibraryBook);

				if (ProcessQueue.QueueDownloadDecrypt(series.Children.Select(c => c.LibraryBook).UnLiberated().ToArray()))
					setQueueCollapseState(false);
			}
			catch (Exception ex)
			{
				Serilog.Log.Logger.Error(ex, "An error occurred while backing up {series} episodes", series.LibraryBook);
			}
		}

		public void ConvertToMp3Clicked(LibraryBook[] libraryBooks)
		{
			try
			{
				if (ProcessQueue.QueueConvertToMp3(libraryBooks))
					setQueueCollapseState(false);
			}
			catch (Exception ex)
			{
				Serilog.Log.Logger.Error(ex, "An error occurred while handling the stop light button click for {libraryBook}", libraryBooks);
			}
		}

		public void ToggleQueueHideBtn() => setQueueCollapseState(QueueOpen);
	}
}
