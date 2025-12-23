using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using forAxxon.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace forAxxon.services
{
    public class AvaloniaDialogService : IDialogService
    {
        public Window? Owner { get; set; }

        public AvaloniaDialogService(Window? owner = null)
        {
            Owner = owner;
        }
        public async Task<DialogResult> ShowConfirmationAsync(string title, string message, DialogButtons buttons)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 320,
                Height = 160,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            var result = DialogResult.Cancel;

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(15, 15, 15, 0)
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 15, 15),
                Spacing = 10
            };

            void AddButton(string content, DialogResult res, bool isDefault = false)
            {
                var btn = new Button { Content = content, Width = 80, IsDefault = isDefault };
                btn.Click += (_, _) => { result = res; dialog.Close(); };
                buttonPanel.Children.Add(btn);
            }

            switch (buttons)
            {
                case DialogButtons.OK:
                    AddButton("OK", DialogResult.OK, true);
                    break;
                case DialogButtons.YesNo:
                    AddButton("Да", DialogResult.Yes, true);
                    AddButton("Нет", DialogResult.No);
                    break;
                case DialogButtons.YesNoCancel:
                    AddButton("Да", DialogResult.Yes);
                    AddButton("Нет", DialogResult.No);
                    AddButton("Отмена", DialogResult.Cancel, true);
                    break;
            }

            var layout = new DockPanel();
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            layout.Children.Add(buttonPanel);
            layout.Children.Add(textBlock);
            dialog.Content = layout;

            await dialog.ShowDialog(Owner);
            return result;
        }
    }
}

