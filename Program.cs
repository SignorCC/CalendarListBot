using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CalendarListBot
{
    class Program
    {
        // define Bot
        public static TelegramBot Bot;
        static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1); // Limit concurrent access to the Update method

        static async Task Main(string[] args)
        {
            // define variables

            string ?token = null;

            Console.WriteLine(DataIO.GetFilePath("settings.json"));

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
            DateTime now = DateTime.Now;
            DateTime nextMinute = now.AddMinutes(1).AddSeconds(-now.Second).AddMilliseconds(-now.Millisecond);
            TimeSpan initialDelay = nextMinute - now;

            // timer triggers at every full minute
            Timer timer = new Timer(state => Task.Run(UpdateAsync), null, initialDelay, TimeSpan.FromMinutes(1));

            // As long as Bot doesn't kill itself, keep task alive
            while(Bot.run)
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
        
    }
}
