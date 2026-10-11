#nullable enable
namespace FishingFun
{
    using log4net.Appender;
    using log4net.Core;
    using log4net.Repository.Hierarchy;
    using System;
    using System.Collections.ObjectModel;
    using System.Timers;
    using System.Windows;
    using System.Windows.Controls;
    using System.Globalization;
    using System.Windows.Media;

    public partial class MainWindow : Window, IAppender
    {
        private System.Drawing.Point lastPoint = System.Drawing.Point.Empty;
        public ObservableCollection<LogEntry> LogEntries { get; set; }

        private IBobberFinder bobberFinder;
        private IPixelClassifier pixelClassifier;
        private IBiteWatcher? biteWatcher; // created when starting the bot
        private ReticleDrawer reticleDrawer = new ReticleDrawer();

        private FishingBot? bot;

        // strike depth (pixels) required for a bite; lower = more sensitive
        private int strikeValue = 6;

        // sensitivity multiplier applied to strikeValue (values < 1.0 are more sensitive)
        private double sensitivity = 1.0;

        // wait timeout in seconds used by the bot when waiting for a bite
        private int waitTimeoutSecs = 30;

        private Timer WindowSizeChangedTimer;
        private System.Threading.Thread? botThread;

        public MainWindow()
        {
            InitializeComponent();

            ((Logger)FishingBot.logger.Logger).AddAppender(this);

            this.DataContext = LogEntries = new ObservableCollection<LogEntry>();
            this.pixelClassifier = new PixelClassifier();
             
            this.bobberFinder = new SearchBobberFinder(pixelClassifier);

            var imageProvider = bobberFinder as IImageProvider;
            if (imageProvider != null)
            {
                imageProvider.BitmapEvent += ImageProvider_BitmapEvent;
            }

            // biteWatcher will be created when the bot is started so we can use the UI settings

            this.WindowSizeChangedTimer = new Timer { AutoReset = false, Interval = 100 };
            this.WindowSizeChangedTimer.Elapsed += SizeChangedTimer_Elapsed;
            this.CardGrid.SizeChanged += MainWindow_SizeChanged;
            this.Closing += (s, e) => botThread?.Abort();

            this.KeyChooser.CastKeyChanged += (s, e) =>
            {
                this.Settings.Focus();
                this.bot?.SetCastKey(this.KeyChooser.CastKey);
            };

            // initialize the LootMethod chooser to the current setting
            this.LootMethodChooser.SelectedIndex = WowProcess.LootMethodSetting == WowProcess.LootMethod.LiamCooper ? 0 : 1;

            // initialize UI boxes with defaults
            this.StrikeValueBox.Text = this.strikeValue.ToString();
            this.SensitivityBox.Text = this.sensitivity.ToString("F1", CultureInfo.InvariantCulture);
            this.WaitTimeoutBox.Text = this.waitTimeoutSecs.ToString();
        }

        // Helper to marshal UI updates to the UI thread.
        private void Dispatch(Action action)
        {
            if (this.Dispatcher.CheckAccess()) action();
            else this.Dispatcher.Invoke(action);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            WindowSizeChangedTimer.Stop();
            WindowSizeChangedTimer.Start();
        }

        private void SizeChangedTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatch(() =>
            {
                this.flyingFishAnimation.AnimationWidth = (int)this.ActualWidth;
                this.flyingFishAnimation.AnimationHeight = (int)this.ActualHeight;
                this.LogGrid.Height = this.LogFlipper.ActualHeight;
                this.GraphGrid.Height = this.GraphFlipper.ActualHeight;
                this.GraphGrid.Visibility = Visibility.Visible;
                this.GraphFlipper.IsFlipped = true;
                this.LogFlipper.IsFlipped = true;
                this.GraphFlipper.IsFlipped = false;
                this.LogFlipper.IsFlipped = false;
            });
        }

        private void Stop_Click(object sender, RoutedEventArgs e) => bot?.Stop();

        private void Settings_Click(object sender, RoutedEventArgs e) => new ColourConfiguration(this.pixelClassifier).Show();

        private void CastKey_Click(object sender, RoutedEventArgs e) => this.KeyChooser.Focus();

        private void FishingEventHandler(object sender, FishingEvent e)
        {
            Dispatch(() =>
            {
                switch (e.Action)
                {
                    case FishingAction.BobberMove:
                        if (!this.GraphFlipper.IsFlipped)
                        {
                            this.Chart.Add(e.Amplitude);
                        }
                        break;

                    case FishingAction.Loot:
                        this.flyingFishAnimation.Start();
                        this.LootingGrid.Visibility = Visibility.Visible;
                        break;

                    case FishingAction.Cast:
                        this.Chart.ClearChart();
                        this.LootingGrid.Visibility = Visibility.Collapsed;
                        this.flyingFishAnimation.Stop();
                        // previously we toggled a flag to set background once; simplified to always keep black background
                        break;
                };
            });
        }

        public void DoAppend(LoggingEvent loggingEvent)
        {
            Dispatch(() =>
                LogEntries.Insert(0, new LogEntry()
                {
                    DateTime = DateTime.Now,
                    Message = loggingEvent.RenderedMessage
                })
            );
        }

        private void SetImageVisibility(Image imageForVisible, Image imageForCollapsed, bool state)
        {
            imageForVisible.Visibility = state ? Visibility.Visible : Visibility.Collapsed;
            imageForCollapsed.Visibility = !state ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetButtonStates(bool isBotRunning)
        {
            Dispatch(() =>
            {
                this.Play.IsEnabled = isBotRunning;
                this.Stop.IsEnabled = !this.Play.IsEnabled;
                SetImageVisibility(this.PlayImage, this.PlayImage_Disabled, this.Play.IsEnabled);
                SetImageVisibility(this.StopImage, this.StopImage_Disabled, this.Stop.IsEnabled);
            });
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (bot == null)
            {
                // parse tuning controls (UI -> fields) before starting the bot thread
                if (!int.TryParse(this.StrikeValueBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sv)) sv = this.strikeValue;
                if (!double.TryParse(this.SensitivityBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var sens)) sens = this.sensitivity;
                if (!int.TryParse(this.WaitTimeoutBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wts)) wts = this.waitTimeoutSecs;

                this.strikeValue = Math.Max(1, sv);
                this.sensitivity = Math.Max(0.1, sens);
                this.waitTimeoutSecs = Math.Max(5, wts);

                // create a bite watcher configured from the UI values
                this.biteWatcher = new PositionBiteWatcher(this.strikeValue, this.sensitivity);

                WowProcess.PressKey(ConsoleKey.Spacebar);
                System.Threading.Thread.Sleep(1500);

                SetButtonStates(false);
                botThread = new System.Threading.Thread(new System.Threading.ThreadStart(this.BotThread));
                botThread.Start();

                // Hide cards after 10 minutes
                var timer = new Timer { Interval = 1000 * 60 * 10, AutoReset = false };
                timer.Elapsed += (s, ev) => this.Dispatch(() => this.LogFlipper.IsFlipped = this.GraphFlipper.IsFlipped = true);
                timer.Start();
            }
        }

        public void BotThread()
        {
            if (this.biteWatcher == null) return;

            bot = new FishingBot(bobberFinder, this.biteWatcher, KeyChooser.CastKey)
            {
                WaitForBiteTimeoutMs = this.waitTimeoutSecs * 1000
            };

            bot.FishingEventHandler += FishingEventHandler;
            bot.Start();

            bot = null;
            SetButtonStates(true);
        }

        private void LootMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LootMethodChooser == null) return;
            WowProcess.LootMethodSetting = LootMethodChooser.SelectedIndex == 0 ? WowProcess.LootMethod.LiamCooper : WowProcess.LootMethod.NoShift;
        }

        private void ImageProvider_BitmapEvent(object sender, BobberBitmapEvent e)
        {
            Dispatch(() =>
            {
                SetBackgroundImageColour(e);
                reticleDrawer.Draw(e.Bitmap, e.Point);
                var bitmapImage = e.Bitmap.ToBitmapImage();
                e.Bitmap.Dispose();

                // update the UI image
                this.Screenshot.Source = bitmapImage;

                // update the lastPoint used for info display
                lastPoint = e.Point;
            });
        }

        // Keep the preview background black.
        private void SetBackgroundImageColour(BobberBitmapEvent e)
        {
            try
            {
                this.ImageBackground.Background = Brushes.Black;
            }
            catch
            {
                // ignore UI errors
            }
        }
    }
}