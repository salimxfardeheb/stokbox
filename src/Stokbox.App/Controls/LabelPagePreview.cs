using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Stokbox.App.Printing;
using Stokbox.Core.Entities;

namespace Stokbox.App.Controls
{
    /// <summary>
    /// Shows one page of labels exactly as it will be printed, at its real size (1 mm = 96/25.4 units),
    /// shrunk only when the page is larger than the room available.
    /// </summary>
    public sealed class LabelPagePreview : Decorator
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            nameof(Settings), typeof(LabelSettings), typeof(LabelPagePreview), new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty LabelsProperty = DependencyProperty.Register(
            nameof(Labels), typeof(IReadOnlyList<LabelContent>), typeof(LabelPagePreview), new PropertyMetadata(null, OnChanged));

        public LabelSettings Settings
        {
            get => (LabelSettings)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }

        /// <summary>
        /// Labels of the page shown; the positions beyond them stay empty.
        /// </summary>
        public IReadOnlyList<LabelContent> Labels
        {
            get => (IReadOnlyList<LabelContent>)GetValue(LabelsProperty);
            set => SetValue(LabelsProperty, value);
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((LabelPagePreview)d).Rebuild();
        }

        private void Rebuild()
        {
            if (Settings == null)
            {
                Child = null;
                return;
            }

            var page = LabelDocumentBuilder.CreatePage(
                Labels ?? new LabelContent[0],
                Settings,
                LabelElement.DefaultDeviceDpi,
                true);

            Child = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA5, 0xB1)),
                    BorderThickness = new Thickness(1),
                    Child = page
                }
            };
        }
    }
}
