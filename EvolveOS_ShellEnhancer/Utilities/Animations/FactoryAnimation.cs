// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Windowing;
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

        public static void AnimateAppCardHoverEnter(UIElement element, string style)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            // --- FIX: Explicitly enable the Translation facade so the engine doesn't crash ---
            ElementCompositionPreview.SetIsTranslationEnabled(element, true);

            // Set CenterPoint for proper scaling and rotation
            visual.CenterPoint = new Vector3((float)(element.RenderSize.Width / 2), (float)(element.RenderSize.Height / 2), 0f);

            switch (style)
            {
                case "Rise":
                    var riseAnim = compositor.CreateSpringVector3Animation();
                    riseAnim.Target = "Translation";
                    riseAnim.FinalValue = new Vector3(0, -6f, 0); // Move up 6px
                    riseAnim.DampingRatio = 0.6f;
                    riseAnim.Period = TimeSpan.FromMilliseconds(50);
                    visual.StartAnimation("Translation", riseAnim);
                    break;

                case "Grow":
                    var growAnim = compositor.CreateSpringVector3Animation();
                    growAnim.Target = "Scale";
                    growAnim.FinalValue = new Vector3(1.15f, 1.15f, 1f); // 15% larger
                    growAnim.DampingRatio = 0.6f;
                    growAnim.Period = TimeSpan.FromMilliseconds(50);
                    visual.StartAnimation("Scale", growAnim);
                    break;

                case "Tilt":
                    var tiltAnim = compositor.CreateSpringScalarAnimation();
                    tiltAnim.Target = "RotationAngleInDegrees";
                    tiltAnim.FinalValue = 6f; // 6 degree tilt
                    tiltAnim.DampingRatio = 0.5f;
                    tiltAnim.Period = TimeSpan.FromMilliseconds(50);
                    visual.StartAnimation("RotationAngleInDegrees", tiltAnim);
                    break;

                case "Breathing":
                    var breathingAnim = compositor.CreateVector3KeyFrameAnimation();
                    breathingAnim.Target = "Scale";
                    breathingAnim.InsertKeyFrame(0f, new Vector3(1.0f, 1.0f, 1.0f));
                    breathingAnim.InsertKeyFrame(0.5f, new Vector3(1.10f, 1.10f, 1.0f)); // Gently expand by 10%
                    breathingAnim.InsertKeyFrame(1.0f, new Vector3(1.0f, 1.0f, 1.0f));
                    breathingAnim.Duration = TimeSpan.FromMilliseconds(2000); // 2 seconds per full breath cycle
                    breathingAnim.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever; // Loop continuously
                    visual.StartAnimation("Scale", breathingAnim);
                    break;

                case "Wobble":
                    var wobbleAnim = compositor.CreateScalarKeyFrameAnimation();
                    wobbleAnim.Target = "RotationAngleInDegrees";
                    wobbleAnim.InsertKeyFrame(0.25f, -6f);
                    wobbleAnim.InsertKeyFrame(0.50f, 6f);
                    wobbleAnim.InsertKeyFrame(0.75f, -3f);
                    wobbleAnim.InsertKeyFrame(1.0f, 0f);
                    wobbleAnim.Duration = TimeSpan.FromMilliseconds(400);
                    visual.StartAnimation("RotationAngleInDegrees", wobbleAnim);
                    break;

                case "Standard":
                default:
                    // Standard relies solely on the background color change in CustomTaskbarWindow
                    break;
            }
        }

        public static void AnimateAppCardHoverExit(UIElement element, string style)
        {
            if (style == "Standard" || string.IsNullOrEmpty(style)) return;

            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            // --- FIX: Explicitly enable the Translation facade here too ---
            ElementCompositionPreview.SetIsTranslationEnabled(element, true);

            // Reset Scale
            var scaleAnim = compositor.CreateSpringVector3Animation();
            scaleAnim.Target = "Scale";
            scaleAnim.FinalValue = new Vector3(1f, 1f, 1f);
            scaleAnim.DampingRatio = 0.7f;
            scaleAnim.Period = TimeSpan.FromMilliseconds(50);
            visual.StartAnimation("Scale", scaleAnim);

            // Reset Translation
            var transAnim = compositor.CreateSpringVector3Animation();
            transAnim.Target = "Translation";
            transAnim.FinalValue = Vector3.Zero;
            transAnim.DampingRatio = 0.7f;
            transAnim.Period = TimeSpan.FromMilliseconds(50);
            visual.StartAnimation("Translation", transAnim);

            // Reset Rotation
            var rotAnim = compositor.CreateSpringScalarAnimation();
            rotAnim.Target = "RotationAngleInDegrees";
            rotAnim.FinalValue = 0f;
            rotAnim.DampingRatio = 0.7f;
            rotAnim.Period = TimeSpan.FromMilliseconds(50);
            visual.StartAnimation("RotationAngleInDegrees", rotAnim);
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

        #endregion

        #region Taskbar Position Transition Animations

        public static void AnimatePositionSpring(UIElement element, double fromX, double fromY)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            visual.Offset = new Vector3((float)fromX, (float)fromY, 0f);

            var springX = compositor.CreateSpringVector3Animation();
            springX.Target = "Offset";
            springX.FinalValue = Vector3.Zero;
            springX.DampingRatio = 0.7f;
            springX.Period = TimeSpan.FromMilliseconds(80);

            visual.StartAnimation("Offset", springX);
        }

        public static void AnimatePositionBackEase(UIElement element, double fromX, double fromY)
        {
            if (element.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

            transform.X = fromX;
            transform.Y = fromY;

            var animX = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut } };
            var animY = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut } };

            var sb = new Storyboard();
            Storyboard.SetTarget(animX, transform);
            Storyboard.SetTargetProperty(animX, "X");
            Storyboard.SetTarget(animY, transform);
            Storyboard.SetTargetProperty(animY, "Y");
            sb.Children.Add(animX);
            sb.Children.Add(animY);
            sb.Begin();
        }

        public static void AnimatePositionExponential(UIElement element, double fromX, double fromY)
        {
            if (element.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

            transform.X = fromX;
            transform.Y = fromY;
            element.Opacity = 0.2;

            var animX = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut } };
            var animY = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut } };
            var fade = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(250) };

            var sb = new Storyboard();
            Storyboard.SetTarget(animX, transform);
            Storyboard.SetTargetProperty(animX, "X");
            Storyboard.SetTarget(animY, transform);
            Storyboard.SetTargetProperty(animY, "Y");
            Storyboard.SetTarget(fade, element);
            Storyboard.SetTargetProperty(fade, "Opacity");

            sb.Children.Add(animX);
            sb.Children.Add(animY);
            sb.Children.Add(fade);
            sb.Begin();
        }

        public static void AnimatePositionElastic(UIElement element, double fromX, double fromY)
        {
            if (element.RenderTransform is not TranslateTransform transform)
            {
                transform = new TranslateTransform();
                element.RenderTransform = transform;
            }

            transform.X = fromX;
            transform.Y = fromY;

            var animX = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(500), EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 3, EasingMode = EasingMode.EaseOut } };
            var animY = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(500), EasingFunction = new ElasticEase { Oscillations = 2, Springiness = 3, EasingMode = EasingMode.EaseOut } };

            var sb = new Storyboard();
            Storyboard.SetTarget(animX, transform);
            Storyboard.SetTargetProperty(animX, "X");
            Storyboard.SetTarget(animY, transform);
            Storyboard.SetTargetProperty(animY, "Y");
            sb.Children.Add(animX);
            sb.Children.Add(animY);
            sb.Begin();
        }

        public static void AnimatePositionScaleMorph(UIElement element)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            visual.CenterPoint = new Vector3((float)(element.RenderSize.Width / 2), (float)(element.RenderSize.Height / 2), 0f);

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1.0f));

            var scaleAnim = compositor.CreateScalarKeyFrameAnimation();
            scaleAnim.InsertKeyFrame(0f, 0.92f);
            scaleAnim.InsertKeyFrame(1f, 1.0f, easing);
            scaleAnim.Duration = TimeSpan.FromMilliseconds(350);

            var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
            opacityAnim.InsertKeyFrame(0f, 0.3f);
            opacityAnim.InsertKeyFrame(1f, 1.0f);
            opacityAnim.Duration = TimeSpan.FromMilliseconds(250);

            visual.StartAnimation("Scale.X", scaleAnim);
            visual.StartAnimation("Scale.Y", scaleAnim);
            visual.StartAnimation("Opacity", opacityAnim);
        }

        #endregion

        #region Start Menu Native Animations

        private static bool _isSmAnimating = false;
        private static DateTime _smAnimStartTime;
        private static double _smAnimDuration;
        private static AppWindow? _smAppWindow;
        private static string _smAnimStyle = "Standard";
        private static int _smStartX, _smStartY, _smStartW, _smStartH;
        private static int _smTargetX, _smTargetY, _smTargetW, _smTargetH;
        private static Action? _smOnComplete;

        public static void PlayStartMenuAnimation(
            AppWindow appWindow,
            string animStyle, double animSpeed, bool isEntrance,
            int startX, int startY, int startW, int startH,
            int targetX, int targetY, int targetW, int targetH,
            Action? onComplete)
        {
            StopStartMenuAnimation();

            _smAppWindow = appWindow;
            _smAnimStyle = animStyle;
            _smStartX = startX; _smStartY = startY; _smStartW = startW; _smStartH = startH;
            _smTargetX = targetX; _smTargetY = targetY; _smTargetW = targetW; _smTargetH = targetH;
            _smOnComplete = onComplete;

            int baseDuration = isEntrance ? 300 : 250;
            _smAnimDuration = baseDuration / Math.Max(0.1, animSpeed);
            _smAnimStartTime = DateTime.Now;
            _isSmAnimating = true;

            CompositionTarget.Rendering += SmAnim_Rendering;
        }

        public static void StopStartMenuAnimation()
        {
            if (_isSmAnimating)
            {
                _isSmAnimating = false;
                CompositionTarget.Rendering -= SmAnim_Rendering;
                _smAppWindow = null;
                _smOnComplete = null;
            }
        }

        private static void SmAnim_Rendering(object? sender, object e)
        {
            if (!_isSmAnimating || _smAppWindow == null) return;

            double elapsed = (DateTime.Now - _smAnimStartTime).TotalMilliseconds;
            double t = elapsed / _smAnimDuration;
            if (t >= 1.0) t = 1.0;

            double easeBounds = CalculateSmEasing(t, _smAnimStyle);

            if (t >= 1.0) easeBounds = 1.0;

            int curX = (int)(_smStartX + (_smTargetX - _smStartX) * easeBounds);
            int curY = (int)(_smStartY + (_smTargetY - _smStartY) * easeBounds);
            int curW = (int)(_smStartW + (_smTargetW - _smStartW) * easeBounds);
            int curH = (int)(_smStartH + (_smTargetH - _smStartH) * easeBounds);

            _smAppWindow.MoveAndResize(new Windows.Graphics.RectInt32(curX, curY, curW, curH));

            if (t >= 1.0)
            {
                var callback = _smOnComplete;
                StopStartMenuAnimation();
                callback?.Invoke();
            }
        }

        private static double CalculateSmEasing(double t, string style)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;

            switch (style)
            {
                case "Glide":
                    return 1 - Math.Pow(1 - t, 3);
                case "Spring":
                    return 1 - Math.Exp(-t * 6) * Math.Cos(t * Math.PI * 1.5);
                case "Bounce":
                    double n1 = 7.5625;
                    double d1 = 2.75;
                    if (t < 1 / d1) return n1 * t * t;
                    else if (t < 2 / d1) return n1 * (t -= 1.5 / d1) * t + 0.75;
                    else if (t < 2.5 / d1) return n1 * (t -= 2.25 / d1) * t + 0.9375;
                    else return n1 * (t -= 2.625 / d1) * t + 0.984375;
                case "Elastic":
                    double c4 = (2 * Math.PI) / 0.3;
                    return Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1;
                case "Exponential":
                    return 1 - Math.Pow(2, -10 * t);
                case "Overshoot":
                    double c1 = 1.70158;
                    double c3 = c1 + 1;
                    return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
                case "Circle":
                    return Math.Sqrt(1 - Math.Pow(t - 1, 2));
                case "Sine":
                    return Math.Sin((t * Math.PI) / 2);
                case "Standard":
                default:
                    return 1 - Math.Pow(1 - t, 4);
            }
        }

        #endregion

        #region Scroll & StartMenu Panel Animations

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

        internal static void AnimateCardScale(FrameworkElement element, double targetScale)
        {
            FrameworkElement targetElement = element;

            if (element is Border border && border.Child is FrameworkElement child)
            {
                targetElement = child;
            }
            else if (element is ContentControl contentControl && contentControl.Content is FrameworkElement content)
            {
                targetElement = content;
            }

            targetElement.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);

            if (targetElement.RenderTransform is not CompositeTransform transform)
            {
                transform = new CompositeTransform();
                targetElement.RenderTransform = transform;
            }

            var storyboard = new Storyboard();

            var animX = new DoubleAnimation
            {
                To = targetScale,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            var animY = new DoubleAnimation
            {
                To = targetScale,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };

            Storyboard.SetTarget(animX, transform);
            Storyboard.SetTargetProperty(animX, "ScaleX");

            Storyboard.SetTarget(animY, transform);
            Storyboard.SetTargetProperty(animY, "ScaleY");

            storyboard.Children.Add(animX);
            storyboard.Children.Add(animY);
            storyboard.Begin();
        }

        #endregion
    }
}