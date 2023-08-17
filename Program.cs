using Telegram.Bot;


namespace CalendarListBot
{
    class Program
    {
        // Constants
        public static TelegramBot Bot;
        private static int? lastCheckedMinute = null;

        static async Task Main(string[] args)
        {
            // Define variables
            string ?token = null;

            Dictionary<string, string>? settings = DataIO.LoadSettings(DataIO.GetFilePath("settings.json"));
            
            DataIO.Log("Booting Up");

            // Error handling
            if(settings != null)
                settings.TryGetValue("Token", out token);

            if (settings == null || token == null)
            {
                DataIO.Log("settings.json not found or incomplete! Exiting...", severity: "Critical");
                Environment.Exit(1);
            }

            // Instaniate Bot and start it
            Bot = new TelegramBot(token);
            CancellationTokenSource cts = new();

            Bot.StartBot(cts);

            var me = await Bot.botClient.GetMeAsync();

            Console.WriteLine($"Start listening for @{me.Username}");


            // Timer logic
            DateTime now = DateTime.Now;
            int secondsUntilNextTenSeconds = 10 - (now.Second % 10);
            int millisecondsUntilNextTenSeconds = secondsUntilNextTenSeconds * 1000 - now.Millisecond;

            System.Timers.Timer timer = new System.Timers.Timer(millisecondsUntilNextTenSeconds);
            timer.Elapsed += (sender, e) =>
            {
                TimerElapsed(sender, e);
                timer.Interval = 10000; // Reset to 10 seconds after the first trigger
            };
            timer.AutoReset = true;
            timer.Start();

            // As long as Bot doesn't kill itself, keep task alive
            while (Bot.run)
            {
                await Task.Delay(100);

                if(Bot.error)
                {
                    Bot.error = false;
                    cts.Cancel();
                    cts = new();
                    Bot.StartBot(cts);
                }
            }

            // Send cancellation request to stop bot and stop timer
            timer.Dispose();
            cts.Cancel();

        }

        private static async void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            int currentMinute = DateTime.Now.Minute;

            if (lastCheckedMinute == null)
            {
                lastCheckedMinute = currentMinute;
                return; // Skip the first run, only initializing the lastCheckedMinute
            }

            if (currentMinute != lastCheckedMinute.Value)
            {
                await Bot.MinutePassed();
                lastCheckedMinute = currentMinute;
            }
        }
    }
}
