using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TL;
using Update = Telegram.Bot.Types.Update;


namespace CalendarListBot
{
    public static class EventHandler
    {
        public static List<Event> GetCurrentEvents(TelegramBot bot)
        {
            // remove all events that have been tagged as deleted
            foreach (User user in bot.users)
                user.Clean();

            // instead of using nested loops, the whole list is iterated by LINQ
            return bot.users
                .SelectMany(user => user.GetEvents())
                .Where(e => e.dateTime.Date == DateTime.Now.Date)
                .ToList();
        }
        async public static Task SetEvent(Update update, CancellationToken cT, TelegramBot bot)
        {
            string[] info = update.Message.Text.Split("-");
            TelegramBotClient botClient = bot.botClient;
            List<User> users = bot.users;
            List<Event> pendingEvents = bot.pendingEvents;

            // error handling first:
            if (info.Length < 2)
            {
                await botClient.SendTextMessageAsync(update.Message.Chat.Id, DataIO.GetMessage("ArgumentError1"), cancellationToken: cT);
                return;
            }

            try
            {
                // generate date
                string[] date = info[0].Split(".");
                string[] dayAr = date[0].Split(" ");


                int.TryParse(dayAr[1], out int day);
                int.TryParse(date[1], out int month);
                int.TryParse(date[2], out int year);

                string[] time = info[1].Split(":");

                int.TryParse(time[0], out int hours);
                int.TryParse(time[1], out int minutes);

                string title = info[2];
                string information = "";
                string location = "";

                if (info.Length >= 4)
                    information = info[3];

                if (info.Length >= 5)
                    location = info[4];



                DateTime dt = new DateTime(year, month, day, hours, minutes, 0);

                // add new event
                Event newEvent = new Event(update.Message.Chat.Id, dt, title, information, location);

                // Find User and AddEvent using LINQ
                User user = users.Find(user => user.id == update.Message.Chat.Id);

                if (user != null)
                {
                    user.AddEvent(newEvent);

                    // add to pending events if event is today
                    if (newEvent.dateTime.Date == bot.day.Date)
                        pendingEvents.Add(newEvent);

                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, DataIO.GetMessage("EventAdded"), parseMode: ParseMode.Html);
                }


            }

            catch (Exception exc)
            {
                await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"Error:{exc.Message}");
                DataIO.Log($"Error adding event in Chat {update.Message.Chat.FirstName} [{update.Message.Chat.Id}]:{exc.Message}", severity: "Warning");
            }
        }

        public static bool SetEvent(User user, string text, CancellationToken cT, TelegramBot bot)
        {
            Console.WriteLine(text);          

            string[] info = text.Split('-');
            TelegramBotClient botClient = bot.botClient;
            List<User> users = bot.users;
            List<Event> pendingEvents = bot.pendingEvents;

            // error handling first:
            if (info.Length < 2)
                return false;
            
            try
            {
                // generate date
                string[] date = info[0].Split(".");
                string[] dayAr = date[0].Split(" ");


                int.TryParse(dayAr[1], out int day);
                int.TryParse(date[1], out int month);
                int.TryParse(date[2], out int year);

                string[] time = info[1].Split(":");

                int.TryParse(time[0], out int hours);
                int.TryParse(time[1], out int minutes);

                string title = info[2];
                string information = "";
                string location = "";

                if (info.Length >= 4)
                    information = info[3];

                if (info.Length >= 5)
                    location = info[4];



                DateTime dt = new DateTime(year, month, day, hours, minutes, 0);

                // add new event
                Event newEvent = new Event(user.id, dt, title, information, location);

                if (user != null)
                {
                    user.AddEvent(newEvent);

                    // add to pending events if event is today
                    if (newEvent.dateTime.Date == bot.day.Date)
                        pendingEvents.Add(newEvent);

                    return true;
                }


            }

            catch (Exception e)
            {
                throw new Exception("Error adding Event: "+e.Message +text);
            }

            return false;
        }

        async public static Task SetDay(Update update, User user, CancellationToken cT, TelegramBot bot)
        {
            // Extract day, capitalise and concatenate
            string day = user.GetLastCommand().Split(" ")[1];

            char firstChar = char.ToUpper(day[0]);
            string remainingChars = day.Length > 1 ? day.Substring(1) : string.Empty;
            string text = update.Message.Text;


            day = firstChar + remainingChars;

            List<Event> newEvents = new();
            string[] split = {""};

            if (text.Contains("/n"))
                text = text.Replace("/n","");

            if (text.Contains(";"))
                split = text.Split(";");
            else
                split[0] = text;

            foreach (string s in split)
            {
                Event ?nE = ExtractWeeklyEventFromString(user.id, s, day);

                if (nE == null)
                    continue;
                else
                    newEvents.Add(nE);
            }

            if(newEvents.Count == 0)
            {
                await bot.botClient.SendTextMessageAsync(
                chatId: user.id,
                text: DataIO.GetMessage("ArgumentError2"),
                parseMode: ParseMode.Html
                );

                return;
            }

            string filepath = DataIO.GetFilePath(day + ".json", Path.Combine("users", user.id.ToString()), true);

            List<Event> oldEvents = DataIO.LoadFromFile<List<Event>>(filepath) ?? new();


            foreach(Event evt in newEvents)
            {
                // Find the event to delete using LINQ
                IEnumerable<Event> eventsToDelete = oldEvents.Where(e =>
                    e.dateTime == evt.dateTime &&
                    e.eventType == EventType.Weekly &&
                    (e.title == "" || e.title == evt.title)
                    );
                    

                if (eventsToDelete.Any())
                {
                    // Remove matching events from oldEvents
                    oldEvents.RemoveAll(eventsToDelete.Contains);
                    break;
                }

                else
                    oldEvents.Add(evt);

            }
           

            DataIO.SaveToFile(filepath, oldEvents);

            await bot.botClient.SendTextMessageAsync(
                chatId : user.id,
                text : DataIO.GetMessage("EventAdded"),
                parseMode : ParseMode.Html
                );
            
        }

        public static Event? ExtractWeeklyEventFromString(long id, string text, string weekDay, EventType type = EventType.Weekly)
        {
            string[] info = text.Split("-");
            
            // error handling first:
            if (info.Length < 2)
            {
                return null;
            }

            try
            {

                string[] daysOfWeek = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

                // Start with a Monday, then increment
                List<Event> dummyEvents = new();
                DateTime dt = new DateTime(2000, 1, 3, 0,0,0);

                foreach (string d in daysOfWeek)
                {
                    if (d == weekDay)
                        break;
                    dt = dt.AddDays(1);
                }

                string[] time = info[0].Split(":");

                int.TryParse(time[0], out int hours);
                int.TryParse(time[1], out int minutes);

                string title = info[1];
                string information = "";
                string location = "";

                if (info.Length >= 3)
                    information = info[2];

                if (info.Length >= 4)
                    location = info[3];


                dt = dt.AddHours(hours).AddMinutes(minutes);

                return new Event(id, dt, title, information, location, _eventType: EventType.Weekly);
            }

            catch
            {
                Console.WriteLine("Error Parsing Event: "+text);
                return null;
            }


        }

        async public static Task DeleteEventOnMessage(Update update, CancellationToken cT, string text, TelegramBot bot)
        {
            string[] info = update.Message.Text.Split("-");
            TelegramBotClient botClient = bot.botClient;


            // error handling first:
            if (info.Length < 2)
            {
                await botClient.SendTextMessageAsync(update.Message.Chat.Id, DataIO.GetMessage("ArgumentError1"), cancellationToken: cT);
                return;
            }

            try
            {
                // generate date
                string[] date = info[0].Split(".");
                string[] dayAr = date[0].Split(" ");


                int.TryParse(dayAr[1], out int day);
                int.TryParse(date[1], out int month);
                int.TryParse(date[2], out int year);

                string[] time = info[1].Split(":");

                int.TryParse(time[0], out int hours);
                int.TryParse(time[1], out int minutes);


                string? information = null;
                string? location = null;
                string? title = null;

                if (info.Length >= 3)
                    title = info[2];

                if (info.Length >= 4)
                    information = info[3];

                if (info.Length >= 5)
                    location = info[4];


                DateTime dt = new DateTime(year, month, day, hours, minutes, 0);

                // Call Delete Function
                await DeleteEvent(dt, title, information, location, update.Message.Chat.Id, cT, bot);

            }

            catch (Exception exc)
            {
                await botClient.SendTextMessageAsync(update.Message.Chat.Id, $"Error: {exc.Message}");
                DataIO.Log($"Error adding event in Chat {update.Message.Chat.FirstName} [{update.Message.Chat.Id}]:{exc.Message}", severity: "Warning");
            }
        }

        public static Event ExtractEventFromString(string text, User user)
        {
            string[] info = text.Split("-");

            try
            {
                // generate date
                string[] date = info[0].Split(".");
                string[] dayAr = date[0].Split(" ");


                int.TryParse(dayAr[1], out int day);
                int.TryParse(date[1], out int month);
                int.TryParse(date[2], out int year);

                string[] time = info[1].Split(":");

                int.TryParse(time[0], out int hours);
                int.TryParse(time[1], out int minutes);


                string? information = null;
                string? location = null;
                string? title = null;

                if (info.Length >= 3)
                    title = info[2];

                if (info.Length >= 4)
                    information = info[3];

                if (info.Length >= 5)
                    location = info[4];


                DateTime dt = new DateTime(year, month, day, hours, minutes, 0);

                return new Event(user.id, dt, title, information, location);

            }

            catch(Exception e)
            {
                throw new Exception("Error parsing Event Info: " + e.Message);
            }

      
        }

        async public static Task<bool> DeleteEvent(DateTime dateTime, string? title, string? information, string? location, long chatId, CancellationToken cT, TelegramBot bot, bool sendMsg = true)
        {
            TelegramBotClient botClient = bot.botClient;
            List<User> users = bot.users;
            List<Event> pendingEvents = bot.pendingEvents;
            DateTime day = bot.day;

            // Find User using LINQ
            User user = users.Find(user => user.id == chatId);

            if (user != null)
            {
                // Find the event to delete using LINQ
                IEnumerable<Event> eventsToDelete = user.GetEvents().Where(e =>
                    e.dateTime == dateTime &&
                    e.eventType == EventType.Once &&
                    (title == null || e.title == title) &&
                    (information == null || e.info == information) &&
                    (location == null || e.location == location))
                    ;

                bool isDeleted = false;

                foreach (Event eventToDelete in eventsToDelete.ToList())
                {
                    isDeleted = user.DeleteEvent(eventToDelete);

                    if (isDeleted)
                    {
                        // Remove from pending events if necessary
                        if (eventToDelete.dateTime.Date == day.Date)
                            pendingEvents.Remove(eventToDelete);
                    }
                }

                // only send Messages if sendMsg is true
                if(sendMsg)
                {
                    if (isDeleted)
                        await botClient.SendTextMessageAsync(chatId, DataIO.GetMessage("EventDeleted"), parseMode: ParseMode.Html);

                    else
                        await botClient.SendTextMessageAsync(chatId, DataIO.GetMessage("EventNotFound"), parseMode: ParseMode.Html);
                }

                return isDeleted;

            }

            return false;
        }

        public static bool DeleteEvent(string title, User user, TelegramBot bot)
        {
            TelegramBotClient botClient = bot.botClient;
            List<User> users = bot.users;
            List<Event> pendingEvents = bot.pendingEvents;
            DateTime day = bot.day;

            if (String.IsNullOrEmpty(title))
                return false;

            // Find the event to delete using LINQ
            IEnumerable<Event> eventsToDelete = user.GetEvents().Where(e =>
                e.eventType == EventType.Once &&
                (e.title.ToLower().Replace(" ", "") == title.ToLower().Replace(" ", "")))
                ;

            bool isDeleted = false;

            foreach (Event eventToDelete in eventsToDelete.ToList())
            {
                isDeleted = user.DeleteEvent(eventToDelete);

                if (isDeleted)
                {
                    // Remove from pending events if necessary
                    if (eventToDelete.dateTime.Date == day.Date)
                        pendingEvents.Remove(eventToDelete);
                }
            }

            return isDeleted;
        }


        async public static Task WakeUser(User u, TelegramBot bot)
        {
            TelegramBotClient botClient = bot.botClient;

            List<Event> schedule = DataIO.LoadFromFile<List<Event>>(DataIO.GetFilePath(DateTime.Now.DayOfWeek.ToString() + ".json", Path.Combine("users", u.id.ToString()), true)) ?? new List<Event>();

            foreach (Event e in u.GetEvents())
                if (e.dateTime.Date == DateTime.Now.Date)
                    schedule.Add(e);

            StringBuilder stringBuilder = new();

            stringBuilder.Append($"Guten Morgen, {u.name}! ☀️\n");

            stringBuilder.Append($"Heute ist {DateTime.Now.ToString("dddd", new CultureInfo("de-DE"))} der {DateTime.Now.ToString("dd.MM.yyyy")}\n");
            stringBuilder.Append("Dein heutiger Tagesplan sieht wie folgt aus: \n");
            stringBuilder.Append(MessageBuilder.CreateDailySchedule(schedule, true));

            await botClient.SendTextMessageAsync(u.id, stringBuilder.ToString(), parseMode: ParseMode.Html);

        }

        async public static Task NotifyUser(Event e, TelegramBot bot)
        {
            TelegramBotClient botClient = bot.botClient;
            List<User> users = bot.users;

            // build message
            string message = DataIO.GetMessage("EventReminder");

            message = message.Replace("{e.remindertime.Hours}", e.remindertime.Hours.ToString())
                             .Replace("{e.remindertime.Minutes}", e.remindertime.Minutes.ToString())
                             .Replace("{e.title}", e.title)
                             .Replace("{e.info}", e.info)
                             .Replace("{e.location}", e.location)
                             ;

            await botClient.SendTextMessageAsync(e.userID, message, parseMode: ParseMode.Html);

            if (e.eventType == EventType.Once)
            {
                e.setDeleted();

                users
                    .SelectMany(user => user.GetEvents())
                    .Where(filter => filter == e)
                    .ToList()
                .ForEach(e => e.setDeleted());

                users.Find(user => user.id == e.userID).Clean();
            }
        }

        public async static Task HandleEventRequest(User user, string message, TelegramBot bot, CancellationToken cT)
        {
            // Call the OpenAI API with the user's message and get the response
            var api = new OpenAiApi(DataIO.GetSetting("OpenAiAPIKey"));
            AiSettings settings = new AiSettings();
            settings.maxTokens = 2000;
            settings.temperature = 0.5;
            settings.topP = 1;
            settings.contextPercentage = 0.5;
            settings.AddSystemMessage(DataIO.GetSetting("VoiceSysMsg1").Replace("{date}", DateTime.Now.ToString("dddd dd.MM.yyyy HH:mm", new CultureInfo("de-AT"))));
            settings.AddSystemMessage(DataIO.GetSetting("VoiceSysMsg2"));
            for (int i = 1; i <= 7; i++)
            {
                settings.AddUserMessage(DataIO.GetSetting($"UserMsg{i}"));
                settings.AddAiMessage(DataIO.GetSetting($"AiMsg{i}"));
            }


            string aiResponse = await api.CreateChatCompletionAsync(message, settings, cT);

            // Check if the request is to delete an event
            if (aiResponse.Contains("/del"))
            {

                // just parse the events title
                bool del = DeleteEvent(aiResponse.Substring(aiResponse.IndexOf("/del ")).Replace("/del ",""), user, bot);

                if (del)
                    await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("VoiceDelEvent"));

                else
                    await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("VoiceEventError") +" Ai Output:\n\n" + aiResponse);

            }

            // Check if the request is to add an event
            else if (aiResponse.Contains("/set"))
            {
                bool set = SetEvent(user, aiResponse.Substring(aiResponse.IndexOf("/set ")), cT, bot);

                if (set)
                    await bot.botClient.SendTextMessageAsync(user.id,DataIO.GetMessage("VoiceSetEvent"));

                else
                    await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("VoiceEventError")+ " Ai Output:\n\n" + aiResponse);
            }

            // Assume regular assistant job or error
            else
            {
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("VoiceSuccess") +aiResponse, parseMode: ParseMode.Html);
            }
        }

    }
}
