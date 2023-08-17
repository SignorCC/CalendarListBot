using Telegram.Bot;


namespace CalendarListBot
{
    class Program
    {
        // define Bot
        public static TelegramBot Bot;
        //static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1); // Limit concurrent access to the Update method
        private static int? lastCheckedMinute = null;

        static async Task Main(string[] args)
        {
            // define variables

            string ?token = null;

            Dictionary<string, string>? settings = DataIO.LoadSettings(DataIO.GetFilePath("settings.json"));
            
            DataIO.Log("Booting Up");

            // error handling
            if(settings != null)
                settings.TryGetValue("Token", out token);

            if (settings == null || token == null)
            {
                DataIO.Log("settings.json not found or incomplete! Exiting...", severity: "Critical");
                Environment.Exit(1);
            }

            // instaniate Bot and start it
            Bot = new TelegramBot(token);
            CancellationTokenSource cts = new();

            Bot.StartBot(cts);

            var me = await Bot.botClient.GetMeAsync();

            Console.WriteLine($"Start listening for @{me.Username}");


            // timer logic
            /*
            DateTime now = DateTime.Now;
            DateTime nextMinute = now.AddMinutes(1).AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond);
            TimeSpan initialDelay = nextMinute - now;

            // timer triggers at every full minute
            Timer timer = new Timer(state => Task.Run(UpdateAsync), null, initialDelay, TimeSpan.FromMinutes(1));
            */
            System.Timers.Timer timer = new System.Timers.Timer(30000); // 30 seconds interval
            timer.Elapsed += TimerElapsed;
            timer.AutoReset = true; // Ensures timer restarts after each elapsed event
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
        /*
        private static async Task UpdateAsync()
        {
            // Ensure only one instance of the Update method runs at a time
            await semaphore.WaitAsync();

            try
            {
                // Your existing Update method logic goes here
                await Bot.MinutePassed();
            }

            finally
            {
                semaphore.Release();
            }
        }
        */
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
