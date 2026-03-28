using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImvixPro.Models;
using System;
using System.Threading.Tasks;

namespace ImvixPro.Views
{
    public partial class PdfUnlockWindow : Window
    {
        private readonly Func<string, Task<PdfUnlockAttemptResult>> _unlockAsync;
        private bool _isSubmitting;

        public PdfUnlockWindow()
        {
            InitializeComponent();
            _unlockAsync = static _ => Task.FromResult(PdfUnlockAttemptResult.Failure(string.Empty));
        }

        public PdfUnlockWindow(
            string title,
            string message,
            string passwordPlaceholder,
            string confirmText,
            string cancelText,
            Func<string, Task<PdfUnlockAttemptResult>> unlockAsync)
            : this()
        {
            Title = title;
            MessageText.Text = message;
            PasswordTextBox.Watermark = passwordPlaceholder;
            ConfirmButtonText.Text = confirmText;
            CancelButtonText.Text = cancelText;
            _unlockAsync = unlockAsync ?? throw new ArgumentNullException(nameof(unlockAsync));
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            PasswordTextBox.Focus();
        }

        private async void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            await SubmitAsync();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private async void OnPasswordKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await SubmitAsync();
                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close(false);
            }
        }

        private async Task SubmitAsync()
        {
            if (_isSubmitting)
            {
                return;
            }

            _isSubmitting = true;
            SetError(string.Empty);
            SetBusyState(true);

            try
            {
                var result = await _unlockAsync(PasswordTextBox.Text ?? string.Empty);
                if (result.Succeeded)
                {
                    Close(true);
                    return;
                }

                PasswordTextBox.Text = string.Empty;
                SetError(result.ErrorMessage);
                PasswordTextBox.Focus();
            }
            finally
            {
                SetBusyState(false);
                _isSubmitting = false;
            }
        }

        private void SetBusyState(bool isBusy)
        {
            PasswordTextBox.IsEnabled = !isBusy;
            ConfirmButton.IsEnabled = !isBusy;
            CancelButton.IsEnabled = !isBusy;
        }

        private void SetError(string? message)
        {
            ErrorText.Text = message ?? string.Empty;
            ErrorText.IsVisible = !string.IsNullOrWhiteSpace(ErrorText.Text);
        }
    }
}
