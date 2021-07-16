﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Dinah.Core;
using FileManager;

namespace LibationWinForms.Dialogs
{
	public partial class DirectoryOrCustomSelectControl : UserControl
	{
		public string SelectedDirectory
			=> customDirectoryRb.Checked ? customTb.Text.Trim()
			: knownDirectoryRb.Checked ? directorySelectControl.SelectedDirectory
			: null;

		public DirectoryOrCustomSelectControl()
		{
			InitializeComponent();

			// doing this after InitializeComponent will fire event
			this.knownDirectoryRb.Checked = true;
		}

		/// <summary>Set items for combobox</summary>
		/// <param name="knownDirectories">List rather than IEnumerable so that client can determine display order</param>
		/// <param name="defaultDirectory"></param>
		public void SetDirectoryItems(List<Configuration.KnownDirectories> knownDirectories, Configuration.KnownDirectories? defaultDirectory = Configuration.KnownDirectories.UserProfile)
			=> this.directorySelectControl.SetDirectoryItems(knownDirectories, defaultDirectory);

		/// <summary>select, set default, or rehydrate</summary>
		/// <param name="directory"></param>
		public void SelectDirectory(Configuration.KnownDirectories directory)
		{
			// if None: take no action
			if (directory != Configuration.KnownDirectories.None)
				selectDir(directory, null);
		}

		/// <summary>select, set default, or rehydrate</summary>
		public void SelectDirectory(string directory)
		{
			directory = directory?.Trim() ?? "";
			selectDir(Configuration.GetKnownDirectory(directory), directory);
		}

		private void selectDir(Configuration.KnownDirectories knownDir, string customDir)
		{
			var unknown = knownDir == Configuration.KnownDirectories.None;
			customDirectoryRb.Checked = unknown;
			knownDirectoryRb.Checked = !unknown;
			this.directorySelectControl.SelectDirectory(knownDir);
			this.customTb.Text = unknown ? customDir : "";
		}

		private string dirSearchTitle;
		public void SetSearchTitle(string dirSearchTitle) => this.dirSearchTitle = dirSearchTitle?.Trim();

		private void customBtn_Click(object sender, EventArgs e)
		{
			using var dialog = new FolderBrowserDialog
			{
				Description = string.IsNullOrWhiteSpace(dirSearchTitle) ? "Search" : $"Search for {dirSearchTitle}",
				SelectedPath = this.customTb.Text
			};
			dialog.ShowDialog();
			if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
				this.customTb.Text = dialog.SelectedPath;
		}

		private void radioButton_CheckedChanged(object sender, EventArgs e)
		{
			var isCustom = this.customDirectoryRb.Checked;

			customTb.Enabled = isCustom;
			customBtn.Enabled = isCustom;

			directorySelectControl.Enabled = !isCustom;
		}

		private void DirectoryOrCustomSelectControl_Load(object sender, EventArgs e)
		{
			if (this.DesignMode)
				return;

		}
	}
}
