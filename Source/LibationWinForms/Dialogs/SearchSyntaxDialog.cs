﻿using LibationSearchEngine;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace LibationWinForms.Dialogs
{
	public partial class SearchSyntaxDialog : Form
	{
		public SearchSyntaxDialog()
		{
			InitializeComponent();

			lboxNumberFields.Items.AddRange(SearchEngine.FieldIndexRules.NumberFieldNames.ToArray());
			lboxStringFields.Items.AddRange(SearchEngine.FieldIndexRules.StringFieldNames.ToArray());
			lboxBoolFields.Items.AddRange(SearchEngine.FieldIndexRules.BoolFieldNames.ToArray());
			lboxIdFields.Items.AddRange(SearchEngine.FieldIndexRules.IdFieldNames.ToArray());
			this.SetLibationIcon();
			this.RestoreSizeAndLocation(LibationFileManager.Configuration.Instance);
		}
		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			this.SaveSizeAndLocation(LibationFileManager.Configuration.Instance);
		}
	}
}
