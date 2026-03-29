using CommunityToolkit.Mvvm.ComponentModel;
using ImvixPro.Models;
using System;

namespace ImvixPro.ViewModels
{
    public partial class NotificationState : ObservableObject
    {
        [ObservableProperty]
        private string lastFailureLogPath = string.Empty;

        [ObservableProperty]
        private ConversionSummaryDialogRequest? pendingDialogRequest;

        public bool HasFailureLog => !string.IsNullOrWhiteSpace(LastFailureLogPath);

        public bool HasPendingDialogRequest => PendingDialogRequest is not null;

        public void ResetFailureLog()
        {
            LastFailureLogPath = string.Empty;
        }

        public void ApplyCompletionFlow(ConversionSummaryFlowResult flow)
        {
            ArgumentNullException.ThrowIfNull(flow);

            LastFailureLogPath = flow.Summary.FailureLogPath;

            if (flow.DialogRequest is not null)
            {
                PendingDialogRequest = flow.DialogRequest;
            }
        }

        public void ClearPendingDialogRequest(ConversionSummaryDialogRequest dialogRequest)
        {
            ArgumentNullException.ThrowIfNull(dialogRequest);

            if (ReferenceEquals(PendingDialogRequest, dialogRequest))
            {
                PendingDialogRequest = null;
            }
        }

        partial void OnLastFailureLogPathChanged(string value)
        {
            OnPropertyChanged(nameof(HasFailureLog));
        }

        partial void OnPendingDialogRequestChanged(ConversionSummaryDialogRequest? value)
        {
            OnPropertyChanged(nameof(HasPendingDialogRequest));
        }
    }
}
