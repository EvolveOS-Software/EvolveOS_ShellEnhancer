// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;

namespace EvolveOS_ShellEnhancer.Utilities.Animations
{
    public static class FactoryAnimation
    {
        #region Taskbar Animations

        public static void StartButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element) AnimateScaleBounce(element, 0.85f);
        }

        public static void StartButton_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement element) AnimateScaleBounce(element, 1.0f);
        }

        public static void AnimateAppCardClickDown(UIElement card)
        {
            AnimateScaleBounce(card, 0.85f);
        }

        public static void AnimateAppCardClickUp(UIElement card)
        {
            AnimateScaleBounce(card, 1.0f);
        }

        public static void AnimateScaleBounce(UIElement element, float targetScale)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            visual.CenterPoint = new Vector3((float)(element.RenderSize.Width / 2), (float)(element.RenderSize.Height / 2), 0f);

            var springAnim = compositor.CreateSpringVector3Animation();
            springAnim.Target = "Scale";
            springAnim.FinalValue = new Vector3(targetScale, targetScale, 1f);
            springAnim.DampingRatio = 0.5f;
            springAnim.Period = TimeSpan.FromMilliseconds(50);

            visual.StartAnimation("Scale", springAnim);
        }

        public static void AnimateHorizontalSlide(UIElement element, double offsetX)
        {
            if (element.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

            transform.X = offsetX;

            var anim = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut }
            };

            var sb = new Storyboard();
            Storyboard.SetTarget(anim, transform);
            Storyboard.SetTargetProperty(anim, "X");
            sb.Children.Add(anim);
            sb.Begin();
        }

        public static async Task PlayScrollTransitionAsync(
            UIElement rootElement,
            Panel centerPanel,
            Panel leftPanel,
            Panel pinnedAppsPanel,
            Func<Task> updateTask,
            bool scrollUp)
        {
            Point centerBefore = default;
            Point leftBefore = default;

            if (rootElement != null && centerPanel != null && leftPanel != null)
            {
                centerBefore = centerPanel.TransformToVisual(rootElement).TransformPoint(new Point(0, 0));
                leftBefore = leftPanel.TransformToVisual(rootElement).TransformPoint(new Point(0, 0));
            }

            if (pinnedAppsPanel.RenderTransform is not TranslateTransform tTransform)
            {
                tTransform = new TranslateTransform();
                pinnedAppsPanel.RenderTransform = tTransform;
            }

            double exitY = scrollUp ? 48 : -48;
            double enterY = scrollUp ? -48 : 48;

            double originalWidth = pinnedAppsPanel.ActualWidth;
            pinnedAppsPanel.Width = originalWidth;

            var slideOut = new DoubleAnimation
            {
                To = exitY,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn }
            };

            var fadeOut = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(150) };

            Storyboard.SetTarget(slideOut, tTransform);
            Storyboard.SetTargetProperty(slideOut, "Y");
            Storyboard.SetTarget(fadeOut, pinnedAppsPanel);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            var sbOut = new Storyboard();
            sbOut.Children.Add(slideOut);
            sbOut.Children.Add(fadeOut);

            var tcs = new TaskCompletionSource<bool>();
            sbOut.Completed += (s, e) => tcs.SetResult(true);
            sbOut.Begin();

            await tcs.Task;

            await updateTask();

            pinnedAppsPanel.Width = double.NaN;

            if (rootElement != null && centerPanel != null && leftPanel != null)
            {
                rootElement.UpdateLayout();

                var centerAfter = centerPanel.TransformToVisual(rootElement).TransformPoint(new Point(0, 0));
                var leftAfter = leftPanel.TransformToVisual(rootElement).TransformPoint(new Point(0, 0));

                double centerOffsetX = centerBefore.X - centerAfter.X;
                double leftOffsetX = leftBefore.X - leftAfter.X;

                if (Math.Abs(centerOffsetX) > 0.5) AnimateHorizontalSlide(centerPanel, centerOffsetX);
                if (Math.Abs(leftOffsetX) > 0.5) AnimateHorizontalSlide(leftPanel, leftOffsetX);
            }

            tTransform.Y = enterY;

            var slideIn = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
            };

            var fadeIn = new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(200) };

            Storyboard.SetTarget(slideIn, tTransform);
            Storyboard.SetTargetProperty(slideIn, "Y");
            Storyboard.SetTarget(fadeIn, pinnedAppsPanel);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");

            var sbIn = new Storyboard();
            sbIn.Children.Add(slideIn);
            sbIn.Children.Add(fadeIn);
            sbIn.Begin();
        }

        #endregion

        #region StartMenu Animations



        #endregion
    }
}