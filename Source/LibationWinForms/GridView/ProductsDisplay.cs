﻿using ApplicationServices;
using AudibleUtilities;
using DataLayer;
using FileLiberator;
using LibationFileManager;
using LibationUiBase.GridView;
using LibationWinForms.Dialogs;
using LibationWinForms.SeriesView;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LibationWinForms.GridView
{
	public partial class ProductsDisplay : UserControl
	{
		/// <summary>Number of visible rows has changed</summary>
		public event EventHandler<int> VisibleCountChanged;
		public event EventHandler<int> RemovableCountChanged;
		public event EventHandler<LibraryBook[]> LiberateClicked;
		public event EventHandler<ISeriesEntry> LiberateSeriesClicked;
		public event EventHandler<LibraryBook[]> ConvertToMp3Clicked;
		public event EventHandler InitialLoaded;

		private bool hasBeenDisplayed;

		public ProductsDisplay()
		{
			InitializeComponent();
		}

		#region Button controls		

		private ImageDisplay imageDisplay;
		private void productsGrid_CoverClicked(IGridEntry liveGridEntry)
		{
			var picDef = new PictureDefinition(liveGridEntry.LibraryBook.Book.PictureLarge ?? liveGridEntry.LibraryBook.Book.PictureId, PictureSize.Native);

			void PictureCached(object sender, PictureCachedEventArgs e)
			{
				if (e.Definition.PictureId == picDef.PictureId)
					imageDisplay.SetCoverArt(e.Picture);

				PictureStorage.PictureCached -= PictureCached;
			}

			PictureStorage.PictureCached += PictureCached;
			(bool isDefault, byte[] initialImageBts) = PictureStorage.GetPicture(picDef);

			var windowTitle = $"{liveGridEntry.Title} - Cover";

			if (imageDisplay is null || imageDisplay.IsDisposed || !imageDisplay.Visible)
			{
				imageDisplay = new ImageDisplay();
				imageDisplay.RestoreSizeAndLocation(Configuration.Instance);
				imageDisplay.FormClosed += (_, _) => imageDisplay.SaveSizeAndLocation(Configuration.Instance);
			}

			imageDisplay.BookSaveDirectory = AudibleFileStorage.Audio.GetDestinationDirectory(liveGridEntry.LibraryBook);
			imageDisplay.PictureFileName = System.IO.Path.GetFileName(AudibleFileStorage.Audio.GetBooksDirectoryFilename(liveGridEntry.LibraryBook, ".jpg"));
			imageDisplay.Text = windowTitle;
			imageDisplay.SetCoverArt(initialImageBts);
			if (!isDefault)
				PictureStorage.PictureCached -= PictureCached;

			if (!imageDisplay.Visible)
				imageDisplay.Show(null);
		}

		private void productsGrid_DescriptionClicked(IGridEntry liveGridEntry, Rectangle cellRectangle)
		{
			var displayWindow = new DescriptionDisplay
			{
				SpawnLocation = PointToScreen(cellRectangle.Location + new Size(cellRectangle.Width, 0)),
				DescriptionText = liveGridEntry.Description,
				BorderThickness = 2,
			};

			void CloseWindow(object o, EventArgs e)
			{
				displayWindow.Close();
			}

			productsGrid.Scroll += CloseWindow;
			displayWindow.FormClosed += (_, _) => productsGrid.Scroll -= CloseWindow;
			displayWindow.Show(this);
		}

		private async void productsGrid_DetailsClicked(ILibraryBookEntry liveGridEntry)
		{
			// HACK: workaround for a Winforms bug.
			// This event is fired by the DataGridCell.OnMouseUpInternal
			// method. If any user changes made in the BookDetailsDialog
			// result in the entry's row being removed from the DataGridView,
			// then when this event handler returns, OnMouseUpInternal will
			// throw a NRE trying to access the DataGridCell.DataGridView
			// property.

			//Steps to cause the bug:
			// * book has tag: asdf
			// * filter is `[asdf]`
			// * tag asdf is removed from book
			// * DataGridView throws NRE

			//The workaround is to make this event handler execute
			//asynchronously so that DataGridCell.OnMouseUpInternal completes
			//before the user can change the DataGridView state.

			await Task.Run(() => this.Invoke(runAsync));

			void runAsync()
			{
				var bookDetailsForm = new BookDetailsDialog(liveGridEntry.LibraryBook);
				if (bookDetailsForm.ShowDialog() == DialogResult.OK)
					liveGridEntry.LibraryBook.UpdateUserDefinedItem(bookDetailsForm.NewTags, bookDetailsForm.BookLiberatedStatus, bookDetailsForm.PdfLiberatedStatus);
			}
		}

		#endregion

		#region Cell Context Menu

		private void productsGrid_CellContextMenuStripNeeded(IGridEntry[] entries, ContextMenuStrip ctxMenu)
		{
			var ctx = new GridContextMenu(entries, '&');
			#region Liberate all Episodes (Single series only)

			if (entries.Length == 1 && entries[0] is ISeriesEntry seriesEntry)
			{
				var liberateEpisodesMenuItem = new ToolStripMenuItem()
				{
					Text = ctx.LiberateEpisodesText,
					Enabled = ctx.LiberateEpisodesEnabled
				};

				liberateEpisodesMenuItem.Click += (_, _) => LiberateSeriesClicked?.Invoke(this, seriesEntry);
				ctxMenu.Items.Add(liberateEpisodesMenuItem);
			}

			#endregion
			#region Set Download status to Downloaded

			var setDownloadMenuItem = new ToolStripMenuItem()
			{
				Text = ctx.SetDownloadedText,
				Enabled = ctx.SetDownloadedEnabled
			};
			setDownloadMenuItem.Click += (_, _) => ctx.SetDownloaded();
			ctxMenu.Items.Add(setDownloadMenuItem);

			#endregion
			#region Set Download status to Not Downloaded

			var setNotDownloadMenuItem = new ToolStripMenuItem()
			{
				Text = ctx.SetNotDownloadedText,
				Enabled = ctx.SetNotDownloadedEnabled
			};
			setNotDownloadMenuItem.Click += (_, _) => ctx.SetNotDownloaded();
			ctxMenu.Items.Add(setNotDownloadMenuItem);

			#endregion
			#region Locate file (Single book only)

			if (entries.Length == 1 && entries[0] is ILibraryBookEntry entry)
			{
				var locateFileMenuItem = new ToolStripMenuItem() { Text = ctx.LocateFileText };
				ctxMenu.Items.Add(locateFileMenuItem);
				locateFileMenuItem.Click += (_, _) =>
				{
					try
					{
						var openFileDialog = new OpenFileDialog
						{
							Title = ctx.LocateFileDialogTitle,
							Filter = "All files (*.*)|*.*",
							FilterIndex = 1
						};
						if (openFileDialog.ShowDialog() == DialogResult.OK)
							FilePathCache.Insert(entry.AudibleProductId, openFileDialog.FileName);
					}
					catch (Exception ex)
					{
						MessageBoxLib.ShowAdminAlert(this, ctx.LocateFileErrorMessage, ctx.LocateFileErrorMessage, ex);
					}
				};
			}

			#endregion
			#region Remove from library

			var removeMenuItem = new ToolStripMenuItem() { Text = ctx.RemoveText };
			removeMenuItem.Click += async (_, _) => await ctx.RemoveAsync();
			ctxMenu.Items.Add(removeMenuItem);

			#endregion
			#region Liberate All (multiple books only)
			if (entries.OfType<ILibraryBookEntry>().Count() > 1)
			{
				var downloadSelectedMenuItem = new ToolStripMenuItem()
				{
					Text = ctx.DownloadSelectedText
				};
				ctxMenu.Items.Add(downloadSelectedMenuItem);
				downloadSelectedMenuItem.Click += (s, _) =>
				{
					LiberateClicked?.Invoke(s, ctx.LibraryBookEntries.Select(e => e.LibraryBook).ToArray());
				};
			}

			#endregion
			#region Convert to Mp3

			if (ctx.LibraryBookEntries.Length > 0)
			{
				var convertToMp3MenuItem = new ToolStripMenuItem
				{
					Text = ctx.ConvertToMp3Text,
					Enabled = ctx.ConvertToMp3Enabled
				};
				convertToMp3MenuItem.Click += (_, e) => ConvertToMp3Clicked?.Invoke(this, ctx.LibraryBookEntries.Select(e => e.LibraryBook).ToArray());
				ctxMenu.Items.Add(convertToMp3MenuItem);

			}

			#endregion
			#region Force Re-Download (Single book only)

			if (entries.Length == 1 && entries[0] is ILibraryBookEntry entry4)
			{
				var reDownloadMenuItem = new ToolStripMenuItem()
				{
					Text = ctx.ReDownloadText,
					Enabled = ctx.ReDownloadEnabled
				};
				ctxMenu.Items.Add(reDownloadMenuItem);
				reDownloadMenuItem.Click += (s, _) =>
				{
					//No need to persist these changes. They only needs to last long for the files to start downloading
					entry4.Book.UserDefinedItem.BookStatus = LiberatedStatus.NotLiberated;
					if (entry4.Book.HasPdf())
						entry4.Book.UserDefinedItem.SetPdfStatus(LiberatedStatus.NotLiberated);

					LiberateClicked?.Invoke(s, [entry4.LibraryBook]);
				};
			}

			#endregion

			if (entries.Length > 1)
				return;

			ctxMenu.Items.Add(new ToolStripSeparator());

			#region Edit Templates (Single book only)

			void editTemplate<T>(LibraryBook libraryBook, string existingTemplate, Action<string> setNewTemplate)
				where T : Templates, LibationFileManager.ITemplate, new()
			{
				var template = ctx.CreateTemplateEditor<T>(libraryBook, existingTemplate);
				var form = new EditTemplateDialog(template);
				if (form.ShowDialog(this) == DialogResult.OK)
				{
					setNewTemplate(template.EditingTemplate.TemplateText);
				}
			}

			if (entries.Length == 1 && entries[0] is ILibraryBookEntry entry2)
			{
				var folderTemplateMenuItem = new ToolStripMenuItem { Text = ctx.FolderTemplateText };
				var fileTemplateMenuItem = new ToolStripMenuItem { Text = ctx.FileTemplateText };
				var multiFileTemplateMenuItem = new ToolStripMenuItem { Text = ctx.MultipartTemplateText };
				folderTemplateMenuItem.Click += (s, _) => editTemplate<Templates.FolderTemplate>(entry2.LibraryBook, Configuration.Instance.FolderTemplate, t => Configuration.Instance.FolderTemplate = t);
				fileTemplateMenuItem.Click += (s, _) => editTemplate<Templates.FileTemplate>(entry2.LibraryBook, Configuration.Instance.FileTemplate, t => Configuration.Instance.FileTemplate = t);
				multiFileTemplateMenuItem.Click += (s, _) => editTemplate<Templates.ChapterFileTemplate>(entry2.LibraryBook, Configuration.Instance.ChapterFileTemplate, t => Configuration.Instance.ChapterFileTemplate = t);

				var editTemplatesMenuItem = new ToolStripMenuItem { Text = ctx.EditTemplatesText };
				editTemplatesMenuItem.DropDownItems.AddRange(new[] { folderTemplateMenuItem, fileTemplateMenuItem, multiFileTemplateMenuItem });

				ctxMenu.Items.Add(new ToolStripSeparator());
				ctxMenu.Items.Add(editTemplatesMenuItem);
			}

			#endregion
			#region View Bookmarks/Clips (Single book only)

			if (entries.Length == 1 && entries[0] is ILibraryBookEntry entry3)
			{
				var bookRecordMenuItem = new ToolStripMenuItem { Text = ctx.ViewBookmarksText };
				bookRecordMenuItem.Click += (_, _) => new BookRecordsDialog(entry3.LibraryBook).ShowDialog(this);
				ctxMenu.Items.Add(bookRecordMenuItem);
			}

			#endregion
			#region View All Series (Single book only)

			if (entries.Length == 1 && entries[0].Book.SeriesLink.Any())
			{
				var viewSeriesMenuItem = new ToolStripMenuItem { Text = ctx.ViewSeriesText };
				viewSeriesMenuItem.Click += (_, _) => new SeriesViewDialog(entries[0].LibraryBook).Show();
				ctxMenu.Items.Add(viewSeriesMenuItem);
			}

			#endregion
		}

		#endregion

		#region Scan and Remove Books

		public void CloseRemoveBooksColumn()
			=> productsGrid.RemoveColumnVisible = false;

		public async Task RemoveCheckedBooksAsync()
		{
			var selectedBooks = productsGrid.GetAllBookEntries().Where(lbe => lbe.Remove is true).ToList();

			if (selectedBooks.Count == 0)
				return;

			var booksToRemove = selectedBooks.Select(rge => rge.LibraryBook).ToList();
			var result = MessageBoxLib.ShowConfirmationDialog(
				booksToRemove,
				// do not use `$` string interpolation. See impl.
				"Are you sure you want to remove {0} from Libation's library?",
				"Remove books from Libation?");

			if (result != DialogResult.Yes)
				return;

			productsGrid.RemoveBooks(selectedBooks);
			await booksToRemove.RemoveBooksAsync();
		}

		public async Task ScanAndRemoveBooksAsync(params Account[] accounts)
		{
			RemovableCountChanged?.Invoke(this, 0);
			productsGrid.RemoveColumnVisible = true;

			try
			{
				if (accounts is null || accounts.Length == 0)
					return;

				var allBooks = productsGrid.GetAllBookEntries();
				var lib = allBooks
					.Select(lbe => lbe.LibraryBook)
					.Where(lb => !lb.Book.HasLiberated());

				var removedBooks = await LibraryCommands.FindInactiveBooks(Login.WinformLoginChoiceEager.CreateApiExtendedFunc(this), lib, accounts);

				var removable = allBooks.Where(lbe => removedBooks.Any(rb => rb.Book.AudibleProductId == lbe.AudibleProductId)).ToList();

				foreach (var r in removable)
					r.Remove = true;

				productsGrid_RemovableCountChanged(this, null);
			}
			catch (OperationCanceledException)
			{
				Serilog.Log.Information("Audible login attempt cancelled by user");
			}
			catch (Exception ex)
			{
				MessageBoxLib.ShowAdminAlert(
					this,
					"Error scanning library. You may still manually select books to remove from Libation's library.",
					"Error scanning library",
					ex);
			}
		}

		#endregion

		#region UI display functions		

		public async Task DisplayAsync(List<LibraryBook> libraryBooks = null)
		{
			try
			{
				// don't return early if lib size == 0. this will not update correctly if all books are removed
				libraryBooks ??= DbContexts.GetLibrary_Flat_NoTracking(includeParents: true);

				if (!hasBeenDisplayed)
				{
					// bind
					await productsGrid.BindToGridAsync(libraryBooks);
					hasBeenDisplayed = true;
					InitialLoaded?.Invoke(this, new());
				}
				else
					productsGrid.UpdateGrid(libraryBooks);
			}
			catch (Exception ex)
			{
				Serilog.Log.Error(ex, "Error displaying library in {0}", nameof(ProductsDisplay));
			}
		}

		#endregion

		#region Filter

		public void Filter(string searchString)
			=> productsGrid.Filter(searchString);

		#endregion

		internal List<LibraryBook> GetVisible() => productsGrid.GetVisibleBooks().ToList();

		private void productsGrid_VisibleCountChanged(object sender, int count)
		{
			VisibleCountChanged?.Invoke(this, count);
		}

		private void productsGrid_LiberateClicked(ILibraryBookEntry liveGridEntry)
		{
			if (liveGridEntry.LibraryBook.Book.UserDefinedItem.BookStatus is not LiberatedStatus.Error
				&& !liveGridEntry.Liberate.IsUnavailable)
				LiberateClicked?.Invoke(this, [liveGridEntry.LibraryBook]);
		}

		private void productsGrid_RemovableCountChanged(object sender, EventArgs e)
		{
			RemovableCountChanged?.Invoke(sender, productsGrid.GetAllBookEntries().Count(lbe => lbe.Remove is true));
		}
	}
}
