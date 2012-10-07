using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LinqToVisualTree;
using Microsoft.Phone.Controls;

namespace FMUtils.Phone.Animation
{
    public class ListInertia
    {
        public static readonly DependencyProperty IndentLevelProperty = DependencyProperty.RegisterAttached("IndentLevel", typeof(int), typeof(ListInertia), new PropertyMetadata(-1));

        public static int GetIndentLevel(DependencyObject obj)
        {
            return (int)obj.GetValue(IndentLevelProperty);
        }

        public static void SetIndentLevel(DependencyObject obj, int value)
        {
            obj.SetValue(IndentLevelProperty, value);
        }



        public static readonly DependencyProperty IsPivotAnimatedProperty = DependencyProperty.RegisterAttached("IsPivotAnimated", typeof(bool), typeof(ListInertia), new PropertyMetadata(false, OnIsPivotAnimatedChanged));

        public static bool GetIsPivotAnimated(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsPivotAnimatedProperty);
        }

        public static void SetIsPivotAnimated(DependencyObject obj, bool value)
        {
            obj.SetValue(IsPivotAnimatedProperty, value);
        }



        private static void OnIsPivotAnimatedChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            (d as ItemsControl).Loaded += ItemsControl_Loaded;
        }

        private static void ItemsControl_Loaded(object sender, RoutedEventArgs re)
        {
            var list = sender as ItemsControl;

            // locate the pivot control that this list is within
            Pivot pivot = list.Ancestors<Pivot>().Single() as Pivot;

            // and its index within the pivot
            int pivotIndex = pivot.Items.IndexOf(list.Ancestors<PivotItem>().Single());

            bool selectionChanged = false;
            pivot.SelectionChanged += (s, e) => selectionChanged = true;

            // handle manipulation events which occur when the user moves between pivot items
            pivot.ManipulationCompleted += (s, e) =>
            {
                if (!selectionChanged)
                    return;

                selectionChanged = false;

                if (pivotIndex != pivot.SelectedIndex)
                    return;

                // determine which direction this tab will be scrolling in from
                bool fromRight = e.TotalManipulation.Translation.X <= 0;

                // locate the stack panel that hosts the items
                VirtualizingStackPanel vsp = list.Descendants<VirtualizingStackPanel>().First() as VirtualizingStackPanel;

                // iterate over each of the items in view
                int firstVisibleItem = (int)vsp.VerticalOffset;
                int visibleItemCount = (int)vsp.ViewportHeight;
                for (int index = firstVisibleItem; index <= firstVisibleItem + visibleItemCount; index++)
                {
                    // find all the item that have the IndentLevel attached property set
                    var lbi = list.ItemContainerGenerator.ContainerFromIndex(index);
                    if (lbi == null)
                        continue;

                    vsp.Dispatcher.BeginInvoke(() =>
                    {
                        foreach (var a in lbi.Descendants().Where(p => ListInertia.GetIndentLevel(p) > -1).SelectMany(target => GetAnimations(target as FrameworkElement, fromRight)))
                            a.Begin();
                    });
                };
            };
        }

        /// <summary>
        /// Creates a necessary storyboards for animation. All elements receive a TranslateTransform animation. Level 0 elements also have an opacity animation.
        /// </summary>
        private static Storyboard[] GetAnimations(FrameworkElement element, bool fromRight)
        {
            int level = ListInertia.GetIndentLevel(element);

            double delay = 0.4;
            double from = (fromRight ? 35 : -35) * (level + 1);

            element.RenderTransform = new TranslateTransform() { X = from };

            double duration = 0.6;

            var SlideSB = new Storyboard()
            {
                BeginTime = TimeSpan.FromSeconds(delay),
                Duration = TimeSpan.FromSeconds(duration),
            };

            var SlideDA = new DoubleAnimation()
            {
                //EasingFunction = new PowerEase() { Power = 3, EasingMode = EasingMode.EaseOut },// - level },
                EasingFunction = new CircleEase() { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut },
                From = from,
                To = 0,
                Duration = TimeSpan.FromSeconds(duration),
            };

            if (level == 0)
                SlideDA.EasingFunction = new PowerEase()
                {
                    Power = 4.0,
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                };

            SlideSB.Children.Add(SlideDA);
            Storyboard.SetTarget(SlideDA, element.RenderTransform);
            Storyboard.SetTargetProperty(SlideDA, new PropertyPath("X"));


            var FadeSB = new Storyboard()
            {
                Duration = TimeSpan.FromSeconds(0.35),
            };

            var FadeDA = new DoubleAnimation()
            {
                EasingFunction = new QuarticEase() { EasingMode = EasingMode.EaseIn },
                From = level == 0 ? 0 : 0.4,
                To = 1,
                Duration = FadeSB.Duration,
            };

            FadeSB.Children.Add(FadeDA);
            Storyboard.SetTarget(FadeDA, element);
            Storyboard.SetTargetProperty(FadeDA, new PropertyPath(UIElement.OpacityProperty));

            return new Storyboard[] { SlideSB, FadeSB };
        }
    }
}
