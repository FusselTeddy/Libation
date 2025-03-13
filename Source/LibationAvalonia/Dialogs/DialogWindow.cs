﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using LibationFileManager;
using System;
using System.Threading.Tasks;

namespace LibationAvalonia.Dialogs
{
	public abstract class DialogWindow : Window
	{
		protected bool CancelOnEscape { get; set; } = true;
		protected bool SaveOnEnter { get; set; } = true;
		public bool SaveAndRestorePosition { get; set; }
		public Control ControlToFocusOnShow { get; set; }
		protected override Type StyleKeyOverride => typeof(DialogWindow);

		public DialogWindow(bool saveAndRestorePosition = true)
		{
			SaveAndRestorePosition = saveAndRestorePosition;
			KeyDown += DialogWindow_KeyDown;
			Initialized += DialogWindow_Initialized;
			Opened += DialogWindow_Opened;
			Closing += DialogWindow_Closing;

			if (Design.IsDesignMode)
				RequestedThemeVariant = ThemeVariant.Dark;
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			if (MinHeight == MaxHeight && MinWidth == MaxWidth)
			{
				CanResize = false;
				Height = MinHeight;
				Width = MinWidth;
			}
		}

		private void CloseButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			CancelAndClose();
		}

		private void DialogWindow_Initialized(object sender, EventArgs e)
		{
			this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			if (SaveAndRestorePosition)
				this.RestoreSizeAndLocation(Configuration.Instance);
		}

		private void DialogWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (SaveAndRestorePosition)
				this.SaveSizeAndLocation(Configuration.Instance);
		}

		private void DialogWindow_Opened(object sender, EventArgs e)
		{
			ControlToFocusOnShow?.Focus();
		}

		protected virtual void SaveAndClose() => Close(DialogResult.OK);
		protected virtual async Task SaveAndCloseAsync() => await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(SaveAndClose);
		protected virtual void CancelAndClose() => Close(DialogResult.Cancel);
		protected virtual async Task CancelAndCloseAsync() => await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(CancelAndClose);

		private async void DialogWindow_KeyDown(object sender, Avalonia.Input.KeyEventArgs e)
		{
			if (CancelOnEscape && e.Key == Avalonia.Input.Key.Escape)
				await CancelAndCloseAsync();
			else if (SaveOnEnter && e.Key == Avalonia.Input.Key.Return)
				await SaveAndCloseAsync();
		}
	}
}
