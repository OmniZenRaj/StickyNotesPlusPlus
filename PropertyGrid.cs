using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Xceed.Wpf.Toolkit.PropertyGrid;

namespace OmniZenNotes
{
    public partial class NoteViewer : Window
    {
        // Reminder and Settings PropertyGrid UX Management:
        void ShowReminderPanel(ToggleButton toggleButton) {
            uxReminderPanel.Visibility = toggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        void ShowSettingsPanel(ToggleButton toggleButton) {
            uxSettingsPanel.Visibility = toggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        void uxReminderPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible) {
                uxViewNoteReminderMenuItem.IsChecked = visible;
                // Toggle Settings panel & button to be mutualy exclusive of the Reminder panel
                uxSettingsPanel.Visibility = visible ? Visibility.Collapsed : uxSettingsPanel.Visibility;
                uxSettingsButton.IsChecked = !visible && uxSettingsButton.IsChecked == true;
                uxViewNoteSettingsMenuItem.IsChecked = uxSettingsButton.IsChecked == true;
            }
        }

        void uxSettingsPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible) {
                uxViewNoteSettingsMenuItem.IsChecked = visible;
                // Toggle Reminder panel & button to be mutualy exclusive of the Settings panel
                uxReminderPanel.Visibility = visible ? Visibility.Collapsed : uxReminderPanel.Visibility;
                uxReminderButton.IsChecked = !visible && uxReminderButton.IsChecked == true;
                uxViewNoteReminderMenuItem.IsChecked = uxReminderButton.IsChecked == true;
            }
        }
                
        void uxColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            SetBackgroundColor((Color)e.NewValue);
            uxSettingsPropertyGrid.Update();
        }

        void uxReminderPanel_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                uxReminderPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        void uxSettingsPanel_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                uxSettingsPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        void uxReminderPropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e) {
            if (e.OriginalSource is PropertyItem item) {
                switch (item.PropertyName) {
                    case "LongNotification":
                        break;
                    default: break;
                }
            }
        }

        void uxSettingsPropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e) {
            if (e.OriginalSource is PropertyItem item) {
                Color foregroundColor = Colors.Black;
                if (uxRichTextBox.Foreground is SolidColorBrush scba) {
                    foregroundColor = scba.Color;
                }

                switch (item.PropertyName) {
                    case "BackgroundColor":
                        SetBackgroundColor((Color)e.NewValue);
                        break;
                    case "FontFamily":
                        SetFont((FontFamily)e.NewValue, uxRichTextBox.FontSize, foregroundColor, uxRichTextBox.FontStyle);
                        break;
                    case "FontSize":
                        SetFont(uxRichTextBox.FontFamily, (double)e.NewValue, foregroundColor, uxRichTextBox.FontStyle);
                        break;
                    case "FontColor":
                        SetFont(uxRichTextBox.FontFamily, uxRichTextBox.FontSize, (Color)e.NewValue, uxRichTextBox.FontStyle);
                        break;
                    case "FontStyle":
                        SetFont(uxRichTextBox.FontFamily, uxRichTextBox.FontSize, foregroundColor, (FontStyle)e.NewValue);
                        break;
                    case "Topmost":
                        Topmost = (bool)e.NewValue;
                        UpdatePinTabUX();
                        break;
                    case "Title":
                        Title = (string)e.NewValue;
                        uxNoteTitleLabel.Content = Title;
                        break;

                    default: break;
                }
            }
        }
    }
}