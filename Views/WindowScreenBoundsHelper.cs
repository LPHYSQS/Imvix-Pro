using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ImvixPro.Views
{
    internal readonly record struct ConstrainedWindowBounds(
        double WidthDip,
        double HeightDip,
        double MinWidthDip,
        double MinHeightDip,
        PixelPoint Position);

    internal static class WindowScreenBoundsHelper
    {
        private sealed class WindowScreenSafetyState
        {
            public WindowScreenSafetyState(Window window)
            {
                OriginalMinWidth = window.MinWidth;
                OriginalMinHeight = window.MinHeight;
            }

            public double OriginalMinWidth { get; }

            public double OriginalMinHeight { get; }

            public EventHandler? OpenedHandler { get; set; }

            public EventHandler? ClosedHandler { get; set; }

            public EventHandler? ScreensChangedHandler { get; set; }

            public bool IsScreensChangedSubscribed { get; set; }
        }

        private static readonly ConditionalWeakTable<Window, WindowScreenSafetyState> ScreenSafetyStates = new();

        internal static void EnableScreenSafety(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);

            if (ScreenSafetyStates.TryGetValue(window, out var existingState))
            {
                if (window.IsVisible)
                {
                    RegisterScreensChanged(window, existingState);
                }

                return;
            }

            var state = new WindowScreenSafetyState(window);
            state.OpenedHandler = (_, _) =>
            {
                ApplyToCurrentScreen(window);
                RegisterScreensChanged(window, state);
            };
            state.ClosedHandler = (_, _) => Unregister(window, state);

            window.Opened += state.OpenedHandler;
            window.Closed += state.ClosedHandler;
            ScreenSafetyStates.Add(window, state);

            if (window.IsVisible)
            {
                RegisterScreensChanged(window, state);
            }
        }

        internal static void PrepareCenteredWindow(Window window, Window? owner)
        {
            ArgumentNullException.ThrowIfNull(window);

            EnableScreenSafety(window);

            var screen = ResolveAnchorScreen(window, owner);
            if (screen is null)
            {
                return;
            }

            var desiredSizeDip = GetDesiredConfiguredWindowDipSize(window);
            var constrainedSizeDip = ClampSizeToWorkingArea(
                desiredSizeDip.WidthDip,
                desiredSizeDip.HeightDip,
                screen.WorkingArea,
                screen.Scaling);
            var desiredPosition = ResolveCenteredPosition(owner, screen, constrainedSizeDip.WidthDip, constrainedSizeDip.HeightDip);

            ApplyWindowFit(window, screen, desiredPosition, desiredSizeDip.WidthDip, desiredSizeDip.HeightDip);
        }

        internal static bool TryPrepareSavedPlacement(Window window, PixelPoint desiredPosition)
        {
            ArgumentNullException.ThrowIfNull(window);

            EnableScreenSafety(window);

            var screens = window.Screens;
            if (screens is null)
            {
                return false;
            }

            var desiredSizeDip = GetDesiredConfiguredWindowDipSize(window);
            var screen = screens.ScreenFromPoint(desiredPosition);
            if (screen is null)
            {
                var fallbackScaling = screens.Primary?.Scaling ?? 1d;
                var estimatedSize = GetWindowPixelSize(desiredSizeDip.WidthDip, desiredSizeDip.HeightDip, fallbackScaling);
                var rect = new PixelRect(desiredPosition, estimatedSize);
                screen = screens.ScreenFromBounds(rect);
            }

            if (screen is null)
            {
                return false;
            }

            ApplyWindowFit(window, screen, desiredPosition, desiredSizeDip.WidthDip, desiredSizeDip.HeightDip);
            return true;
        }

        internal static void PrepareForCurrentPlacement(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);

            EnableScreenSafety(window);

            var screen = ResolveCurrentScreen(window);
            if (screen is null)
            {
                PrepareForPrimaryScreen(window);
                return;
            }

            var desiredSizeDip = GetCurrentWindowDipSize(window);
            ApplyWindowFit(window, screen, window.Position, desiredSizeDip.WidthDip, desiredSizeDip.HeightDip);
        }

        internal static void PrepareForPrimaryScreen(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);

            EnableScreenSafety(window);

            var screen = ResolvePrimaryScreen(window.Screens);
            if (screen is null)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            var desiredSizeDip = GetDesiredConfiguredWindowDipSize(window);
            var constrainedSizeDip = ClampSizeToWorkingArea(
                desiredSizeDip.WidthDip,
                desiredSizeDip.HeightDip,
                screen.WorkingArea,
                screen.Scaling);
            var desiredPosition = GetCenteredPosition(
                new PixelSize(
                    ToPixels(constrainedSizeDip.WidthDip, screen.Scaling),
                    ToPixels(constrainedSizeDip.HeightDip, screen.Scaling)),
                screen.WorkingArea);

            ApplyWindowFit(window, screen, desiredPosition, desiredSizeDip.WidthDip, desiredSizeDip.HeightDip);
        }

        internal static ConstrainedWindowBounds CalculateConstrainedBounds(
            PixelRect workingArea,
            PixelPoint desiredPosition,
            double desiredWidthDip,
            double desiredHeightDip,
            double originalMinWidthDip,
            double originalMinHeightDip,
            double scaling)
        {
            var safeScaling = scaling > 0 ? scaling : 1d;
            var constrainedSizeDip = ClampSizeToWorkingArea(desiredWidthDip, desiredHeightDip, workingArea, safeScaling);

            var windowWidthPx = ToPixels(constrainedSizeDip.WidthDip, safeScaling);
            var windowHeightPx = ToPixels(constrainedSizeDip.HeightDip, safeScaling);

            var maxX = workingArea.Right - windowWidthPx;
            var maxY = workingArea.Bottom - windowHeightPx;

            var x = maxX < workingArea.X
                ? workingArea.X
                : Math.Clamp(desiredPosition.X, workingArea.X, maxX);

            var y = maxY < workingArea.Y
                ? workingArea.Y
                : Math.Clamp(desiredPosition.Y, workingArea.Y, maxY);

            return new ConstrainedWindowBounds(
                constrainedSizeDip.WidthDip,
                constrainedSizeDip.HeightDip,
                ClampMinimumDimension(originalMinWidthDip, constrainedSizeDip.WidthDip),
                ClampMinimumDimension(originalMinHeightDip, constrainedSizeDip.HeightDip),
                new PixelPoint(x, y));
        }

        private static void ApplyToCurrentScreen(Window window)
        {
            if (!window.IsVisible)
            {
                return;
            }

            var screen = ResolveCurrentScreen(window);
            if (screen is null)
            {
                return;
            }

            var desiredSizeDip = GetCurrentWindowDipSize(window);
            ApplyWindowFit(window, screen, window.Position, desiredSizeDip.WidthDip, desiredSizeDip.HeightDip);
        }

        private static void ApplyWindowFit(
            Window window,
            Screen screen,
            PixelPoint desiredPosition,
            double desiredWidthDip,
            double desiredHeightDip)
        {
            var state = GetOrCreateState(window);
            var bounds = CalculateConstrainedBounds(
                screen.WorkingArea,
                desiredPosition,
                desiredWidthDip,
                desiredHeightDip,
                state.OriginalMinWidth,
                state.OriginalMinHeight,
                screen.Scaling);

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.MinWidth = bounds.MinWidthDip;
            window.MinHeight = bounds.MinHeightDip;
            window.Width = bounds.WidthDip;
            window.Height = bounds.HeightDip;
            window.Position = bounds.Position;
        }

        private static (double WidthDip, double HeightDip) ClampSizeToWorkingArea(
            double desiredWidthDip,
            double desiredHeightDip,
            PixelRect workingArea,
            double scaling)
        {
            var safeScaling = scaling > 0 ? scaling : 1d;
            var availableWidthDip = Math.Max(1d, workingArea.Width / safeScaling);
            var availableHeightDip = Math.Max(1d, workingArea.Height / safeScaling);

            var widthDip = Math.Min(Math.Max(1d, desiredWidthDip), availableWidthDip);
            var heightDip = Math.Min(Math.Max(1d, desiredHeightDip), availableHeightDip);

            return (widthDip, heightDip);
        }

        private static double ClampMinimumDimension(double originalMinimumDip, double constrainedDimensionDip)
        {
            if (double.IsNaN(originalMinimumDip) || originalMinimumDip <= 0)
            {
                return 0d;
            }

            return Math.Min(originalMinimumDip, constrainedDimensionDip);
        }

        private static PixelPoint ResolveCenteredPosition(
            Window? owner,
            Screen screen,
            double windowWidthDip,
            double windowHeightDip)
        {
            var windowSizePx = new PixelSize(
                ToPixels(windowWidthDip, screen.Scaling),
                ToPixels(windowHeightDip, screen.Scaling));

            if (owner is not null && owner.IsVisible)
            {
                var ownerSizePx = GetWindowPixelSize(owner, screen.Scaling);
                var ownerRect = new PixelRect(owner.Position, ownerSizePx);
                return new PixelPoint(
                    ownerRect.X + Math.Max(0, (ownerRect.Width - windowSizePx.Width) / 2),
                    ownerRect.Y + Math.Max(0, (ownerRect.Height - windowSizePx.Height) / 2));
            }

            return GetCenteredPosition(windowSizePx, screen.WorkingArea);
        }

        private static PixelPoint GetCenteredPosition(PixelSize size, PixelRect area)
        {
            return new PixelPoint(
                area.X + Math.Max(0, (area.Width - size.Width) / 2),
                area.Y + Math.Max(0, (area.Height - size.Height) / 2));
        }

        private static (double WidthDip, double HeightDip) GetCurrentWindowDipSize(Window window)
        {
            var widthDip = window.Bounds.Width > 0
                ? window.Bounds.Width
                : (double.IsNaN(window.Width) || window.Width <= 0 ? (window.MinWidth > 0 ? window.MinWidth : 1d) : window.Width);

            var heightDip = window.Bounds.Height > 0
                ? window.Bounds.Height
                : (double.IsNaN(window.Height) || window.Height <= 0 ? (window.MinHeight > 0 ? window.MinHeight : 1d) : window.Height);

            return (Math.Max(1d, widthDip), Math.Max(1d, heightDip));
        }

        private static (double WidthDip, double HeightDip) GetDesiredConfiguredWindowDipSize(Window window)
        {
            var widthDip = !double.IsNaN(window.Width) && window.Width > 0
                ? window.Width
                : (window.Bounds.Width > 0 ? window.Bounds.Width : (window.MinWidth > 0 ? window.MinWidth : 1d));

            var heightDip = !double.IsNaN(window.Height) && window.Height > 0
                ? window.Height
                : (window.Bounds.Height > 0 ? window.Bounds.Height : (window.MinHeight > 0 ? window.MinHeight : 1d));

            return (Math.Max(1d, widthDip), Math.Max(1d, heightDip));
        }

        private static PixelSize GetWindowPixelSize(Window window, double scaling)
        {
            var sizeDip = window.IsVisible
                ? GetCurrentWindowDipSize(window)
                : GetDesiredConfiguredWindowDipSize(window);
            return GetWindowPixelSize(sizeDip.WidthDip, sizeDip.HeightDip, scaling);
        }

        private static PixelSize GetWindowPixelSize(double widthDip, double heightDip, double scaling)
        {
            return new PixelSize(ToPixels(widthDip, scaling), ToPixels(heightDip, scaling));
        }

        private static int ToPixels(double dip, double scaling)
        {
            var safeScaling = scaling > 0 ? scaling : 1d;
            return Math.Max(1, (int)Math.Round(Math.Max(1d, dip) * safeScaling));
        }

        private static Screen? ResolveAnchorScreen(Window window, Window? owner)
        {
            var screens = owner?.Screens ?? window.Screens;
            if (screens is null)
            {
                return null;
            }

            try
            {
                if (owner is not null)
                {
                    return screens.ScreenFromWindow(owner)
                        ?? screens.ScreenFromVisual(owner)
                        ?? screens.ScreenFromPoint(owner.Position)
                        ?? ResolvePrimaryScreen(screens);
                }

                return ResolvePrimaryScreen(screens);
            }
            catch (ObjectDisposedException)
            {
                return ResolvePrimaryScreen(screens);
            }
        }

        private static Screen? ResolveCurrentScreen(Window window)
        {
            var screens = window.Screens;
            if (screens is null)
            {
                return null;
            }

            try
            {
                return screens.ScreenFromWindow(window)
                    ?? screens.ScreenFromVisual(window)
                    ?? screens.ScreenFromPoint(window.Position)
                    ?? ResolvePrimaryScreen(screens);
            }
            catch (ObjectDisposedException)
            {
                return ResolvePrimaryScreen(screens);
            }
        }

        private static Screen? ResolvePrimaryScreen(Screens? screens)
        {
            return screens?.Primary
                ?? screens?.All.FirstOrDefault(screen => screen.IsPrimary)
                ?? screens?.All.FirstOrDefault();
        }

        private static void RegisterScreensChanged(Window window, WindowScreenSafetyState state)
        {
            if (window.Screens is null)
            {
                return;
            }

            if (state.ScreensChangedHandler is null)
            {
                state.ScreensChangedHandler = (_, _) =>
                {
                    if (window.IsVisible)
                    {
                        ApplyToCurrentScreen(window);
                    }
                };
            }

            if (state.IsScreensChangedSubscribed)
            {
                window.Screens.Changed -= state.ScreensChangedHandler;
            }

            window.Screens.Changed += state.ScreensChangedHandler;
            state.IsScreensChangedSubscribed = true;
        }

        private static void Unregister(Window window, WindowScreenSafetyState state)
        {
            if (state.IsScreensChangedSubscribed && state.ScreensChangedHandler is not null && window.Screens is not null)
            {
                window.Screens.Changed -= state.ScreensChangedHandler;
                state.IsScreensChangedSubscribed = false;
            }

            if (state.OpenedHandler is not null)
            {
                window.Opened -= state.OpenedHandler;
            }

            if (state.ClosedHandler is not null)
            {
                window.Closed -= state.ClosedHandler;
            }

            ScreenSafetyStates.Remove(window);
        }

        private static WindowScreenSafetyState GetOrCreateState(Window window)
        {
            if (ScreenSafetyStates.TryGetValue(window, out var existingState))
            {
                return existingState;
            }

            var state = new WindowScreenSafetyState(window);
            ScreenSafetyStates.Add(window, state);
            return state;
        }
    }
}
