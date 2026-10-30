using System;
using System.Globalization;
using System.IO;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PluginLib;

namespace DsuExt
{
    public sealed class DsuExtPlugin : Plugin
    {
        public DsuExtPlugin()
        {
            m_sName = "DsuExt";
            m_sCaption = "Extensions";
            m_nDisplayOrder = 500;
            m_apipPages = new PluginPage[] { new DsuExtPage(this) };
            m_bmTabImage = CreateTabImage();
            MarkLoaded();
        }

        private static void MarkLoaded()
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DsuMod");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "DsuExt.loaded"),
                    DateTime.Now.ToString("o") + Environment.NewLine);
            }
            catch
            {
            }
        }

        public override uint ProcessServerData(string sServerData)
        {
            return 0;
        }

        public override uint GetActiveItemCount()
        {
            return 0;
        }

        private static BitmapImage CreateTabImage()
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                LinearGradientBrush brush = new LinearGradientBrush(
                    Color.FromRgb(0x00, 0x5A, 0x9E),
                    Color.FromRgb(0x00, 0x8C, 0xD4),
                    45.0);
                dc.DrawRoundedRectangle(brush, null, new Rect(0, 0, 16, 16), 3, 3);
                dc.DrawRectangle(Brushes.White, null, new Rect(3, 3, 10, 2));
                dc.DrawRectangle(Brushes.White, null, new Rect(3, 7, 7, 2));
                dc.DrawRectangle(Brushes.White, null, new Rect(3, 11, 10, 2));
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (MemoryStream stream = new MemoryStream())
            {
                encoder.Save(stream);
                stream.Position = 0;
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
    }

    public sealed class DsuExtPage : PluginPage
    {
        private readonly DsuExtPlugin plugin;
        private TextBlock modelValue;
        private TextBlock serialValue;
        private TextBlock biosValue;
        private TextBlock batteryValue;
        private TextBlock statusValue;
        private TextBox noteBox;

        public DsuExtPage(DsuExtPlugin parent)
            : base(parent)
        {
            plugin = parent;
            m_sCaption = "Extensions";
            m_sPageName = "Extensions";
            Content = BuildUi();
            Loaded += Page_Loaded;
        }

        private UIElement BuildUi()
        {
            Grid root = new Grid();
            root.Margin = new Thickness(28);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock title = new TextBlock();
            title.Text = "Extensions";
            title.FontSize = 26;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
            Grid.SetRow(title, 0);
            root.Children.Add(title);

            TextBlock subtitle = new TextBlock();
            subtitle.Text = "dynabook Support Utility に追加した拡張プラグインです。";
            subtitle.Margin = new Thickness(0, 6, 0, 18);
            subtitle.Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A));
            Grid.SetRow(subtitle, 1);
            root.Children.Add(subtitle);

            Border infoBorder = new Border();
            infoBorder.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFA));
            infoBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEA));
            infoBorder.BorderThickness = new Thickness(1);
            infoBorder.CornerRadius = new CornerRadius(4);
            infoBorder.Padding = new Thickness(18);
            Grid.SetRow(infoBorder, 2);
            root.Children.Add(infoBorder);

            Grid info = new Grid();
            for (int i = 0; i < 4; i++)
            {
                info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            modelValue = AddInfoRow(info, 0, "Model");
            serialValue = AddInfoRow(info, 1, "Serial number");
            biosValue = AddInfoRow(info, 2, "BIOS version");
            batteryValue = AddInfoRow(info, 3, "Battery");
            infoBorder.Child = info;

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.Margin = new Thickness(0, 16, 0, 16);
            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);

            Button refresh = new Button();
            refresh.Content = "Refresh";
            refresh.Padding = new Thickness(16, 5, 16, 5);
            refresh.Margin = new Thickness(0, 0, 8, 0);
            refresh.Click += Refresh_Click;
            buttons.Children.Add(refresh);

            Button notify = new Button();
            notify.Content = "Test notification";
            notify.Padding = new Thickness(16, 5, 16, 5);
            notify.Margin = new Thickness(0, 0, 8, 0);
            notify.Click += Notify_Click;
            buttons.Children.Add(notify);

            Button save = new Button();
            save.Content = "Save note";
            save.Padding = new Thickness(16, 5, 16, 5);
            save.Click += Save_Click;
            buttons.Children.Add(save);

            Grid noteArea = new Grid();
            noteArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            noteArea.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(noteArea, 4);
            root.Children.Add(noteArea);

            TextBlock noteLabel = new TextBlock();
            noteLabel.Text = "Note (HKCU\\Software\\Dynabook\\DSU\\Plugins\\DsuExt)";
            noteLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A));
            noteLabel.Margin = new Thickness(0, 0, 0, 6);
            Grid.SetRow(noteLabel, 0);
            noteArea.Children.Add(noteLabel);

            noteBox = new TextBox();
            noteBox.AcceptsReturn = true;
            noteBox.TextWrapping = TextWrapping.Wrap;
            noteBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            noteBox.MinHeight = 100;
            noteBox.Padding = new Thickness(8);
            Grid.SetRow(noteBox, 1);
            noteArea.Children.Add(noteBox);

            statusValue = new TextBlock();
            statusValue.Margin = new Thickness(0, 10, 0, 0);
            statusValue.Foreground = Brushes.DimGray;
            Grid.SetRow(statusValue, 5);
            root.Children.Add(statusValue);

            return root;
        }

        private static TextBlock AddInfoRow(Grid grid, int row, string label)
        {
            TextBlock name = new TextBlock();
            name.Text = label;
            name.Margin = new Thickness(0, 0, 12, 8);
            name.Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A));
            Grid.SetRow(name, row);
            Grid.SetColumn(name, 0);
            grid.Children.Add(name);

            TextBlock value = new TextBlock();
            value.Text = "-";
            value.Margin = new Thickness(0, 0, 0, 8);
            value.Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
            value.TextWrapping = TextWrapping.Wrap;
            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
            return value;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadNote();
            RefreshInfo();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshInfo();
        }

        private void Notify_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                plugin.DisplayMessage(
                    "DSU Extensions",
                    "プラグインから表示したテスト通知です。",
                    SeverityLevel.SL_INFORMATION,
                    true);
                SetStatus("Notification requested.", Brushes.DimGray);
            }
            catch (Exception ex)
            {
                SetStatus("Notification failed: " + ex.Message, Brushes.Firebrick);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\Dynabook\\DSU\\Plugins\\DsuExt"))
                {
                }
                ReturnCode rc = plugin.WriteUserProperty("DsuExt", "Note", noteBox.Text ?? "");
                if (rc == ReturnCode.RC_SUCCESS)
                {
                    SetStatus("Note saved.", Brushes.ForestGreen);
                }
                else
                {
                    SetStatus("Save failed: " + rc, Brushes.Firebrick);
                }
            }
            catch (Exception ex)
            {
                SetStatus("Save failed: " + ex.Message, Brushes.Firebrick);
            }
        }

        private void LoadNote()
        {
            try
            {
                string value;
                ReturnCode rc = plugin.ReadUserProperty("DsuExt", "Note", out value);
                if (rc == ReturnCode.RC_SUCCESS && value != null)
                {
                    noteBox.Text = value;
                }
            }
            catch
            {
            }
        }

        private void RefreshInfo()
        {
            try
            {
                modelValue.Text = ValueOrDash(QueryFirst("SELECT * FROM Win32_ComputerSystem", "Model"));
                serialValue.Text = ValueOrDash(QueryFirst("SELECT * FROM Win32_BIOS", "SerialNumber"));
                biosValue.Text = ValueOrDash(QueryFirst("SELECT * FROM Win32_BIOS", "SMBIOSBIOSVersion"));

                string charge = QueryFirst("SELECT * FROM Win32_Battery", "EstimatedChargeRemaining");
                string state = QueryFirst("SELECT * FROM Win32_Battery", "BatteryStatus");
                batteryValue.Text = ValueOrDash(charge) + "% / " + BatteryStateText(state);
                SetStatus("Updated " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), Brushes.DimGray);
            }
            catch (Exception ex)
            {
                SetStatus("Read failed: " + ex.Message, Brushes.Firebrick);
            }
        }

        private void SetStatus(string text, Brush brush)
        {
            statusValue.Text = text;
            statusValue.Foreground = brush;
        }

        private static string ValueOrDash(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static string BatteryStateText(string value)
        {
            int code;
            if (!int.TryParse(value, out code))
            {
                return "Unknown";
            }
            switch (code)
            {
                case 1:
                    return "Discharging";
                case 2:
                    return "AC";
                case 3:
                    return "Fully charged";
                case 4:
                    return "Low";
                case 5:
                    return "Critical";
                case 6:
                    return "Charging";
                case 7:
                    return "Charging (high)";
                case 8:
                    return "Charging (low)";
                case 9:
                    return "Charging (critical)";
                default:
                    return "Unknown";
            }
        }

        private static string QueryFirst(string query, string property)
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", query))
            using (ManagementObjectCollection results = searcher.Get())
            {
                foreach (ManagementBaseObject item in results)
                {
                    try
                    {
                        object value = item[property];
                        if (value != null)
                        {
                            return value.ToString();
                        }
                    }
                    finally
                    {
                        item.Dispose();
                    }
                }
            }
            return "";
        }
    }
}
