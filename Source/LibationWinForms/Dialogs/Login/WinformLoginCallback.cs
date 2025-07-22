﻿using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using AudibleApi;
using AudibleUtilities;
using LibationWinForms.Dialogs.Login;

namespace LibationWinForms.Login
{
	public class WinformLoginCallback : WinformLoginBase, ILoginCallback
	{
		private Account _account { get; }

		public string DeviceName { get; } = "Libation";

		public WinformLoginCallback(Account account, Control owner) : base(owner)
		{
			_account = Dinah.Core.ArgumentValidator.EnsureNotNull(account, nameof(account));
		}

		public Task<string> Get2faCodeAsync(string prompt)
			=> Owner.Invoke(() =>
			{
				using var dialog = new _2faCodeDialog(prompt);
				if (ShowDialog(dialog))
					return Task.FromResult(dialog.Code);
				return Task.FromResult<string>(null);
			});

		public Task<(string password, string guess)> GetCaptchaAnswerAsync(string password, byte[] captchaImage)
			=> Owner.Invoke(() =>
			{
				using var dialog = new CaptchaDialog(password, captchaImage);
				if (ShowDialog(dialog))
					return Task.FromResult((dialog.Password, dialog.Answer));
				return Task.FromResult<(string, string)>((null, null));
			});

		public Task<(string name, string value)> GetMfaChoiceAsync(MfaConfig mfaConfig)
			=> Owner.Invoke(() =>
			{
				using var dialog = new MfaDialog(mfaConfig);
				if (ShowDialog(dialog))
					return Task.FromResult((dialog.SelectedName, dialog.SelectedValue));
				return Task.FromResult<(string, string)>((null, null));
			});

		public Task<(string email, string password)> GetLoginAsync()
			=> Owner.Invoke(() =>
			{
				using var dialog = new LoginCallbackDialog(_account);
				if (ShowDialog(dialog))
					return Task.FromResult((dialog.Email, dialog.Password));
				return Task.FromResult<(string, string)>((null, null));
			});

		public Task ShowApprovalNeededAsync()
			=> Owner.Invoke(() =>
			{
				using var dialog = new ApprovalNeededDialog();
				ShowDialog(dialog);
				return Task.CompletedTask;
			});
	}
}