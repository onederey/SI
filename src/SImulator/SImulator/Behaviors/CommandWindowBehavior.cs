﻿using SImulator.ViewModel;
using SImulator.ViewModel.Core;
using System.Windows;

namespace SImulator.Behaviors;

public static class CommandWindowBehavior
{
    public static bool GetIsAttached(DependencyObject obj) => (bool)obj.GetValue(IsAttachedProperty);

    public static void SetIsAttached(DependencyObject obj, bool value) => obj.SetValue(IsAttachedProperty, value);

    // Using a DependencyProperty as the backing store for IsAttached.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsAttachedProperty =
        DependencyProperty.RegisterAttached("IsAttached", typeof(bool), typeof(CommandWindowBehavior), new UIPropertyMetadata(false, IsAttachedChanged));

    private static void IsAttachedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CommandWindow window)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            window.PreviewKeyDown += Window_PreviewKeyDown;
        }
        else
        {
            window.PreviewKeyDown -= Window_PreviewKeyDown;
        }
    }

    private static void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var window = (CommandWindow)sender;

        if (window.DataContext is MainViewModel main)
        {
            e.Handled = main.OnKeyboardPressed((GameKey)e.Key);
        }
    }
}
