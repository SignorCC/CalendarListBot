using FFMpegCore;
using System.Globalization;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using OpenAI;
using OpenAI.Audio;

namespace CalendarListBot
{
    public static class BotCommands
    {
        private static OpenAiApi ?apiClient;
        public static async Task ToDo(Update update, TelegramBot bot, CancellationToken cT)
        {
            TelegramBotClient botClient = bot.botClient;

            // First obtain the last command of the user
            User user = bot.users.FirstOrDefault(u => u.id == update.Message.Chat.Id);

            string lastCommand = user.GetLastCommand();
            string text = update.Message.Text;

            if (!lastCommand.Contains("/todo"))
                // User entered the first time, respond with a what do you want
                bot.users.FirstOrDefault(u => u.id == update.Message.Chat.Id).SetLastCommand("/todo");

            // User is editing an inline keyboard (hopefully)
            else if (lastCommand == "/todo" && text != "/todo")
            {
                // Return in case inline keyboard should not be edited
                if (user.GetLastInlineMessage().Text.Contains("Welche ToDo Liste?"))
                    return;

                List<string> newTodo = user.GetLastList();

                // Remove placeholder item
                if (newTodo.Contains("Nothing To Do Here!"))
                    newTodo.Remove("Nothing To Do Here!");

                if (newTodo.Contains(text))
                    newTodo.Remove(text);

                else if (newTodo.Contains("✅ " + text))
                    newTodo.Remove("✅ " + text);

                else
                    newTodo.Add(text);

                if (user.GetLastInlineMessage == null)
                    return;

                try
                {
                    // Edit the last Inline Message
                    user.SetLastInlineMessage(
                        await botClient.EditMessageReplyMarkupAsync(
                        chatId: user.id,
                        messageId: user.GetLastInlineMessage().MessageId,
                        replyMarkup: MessageBuilder.GenerateInlineKeyboard(newTodo)
                        ));

                    // Save the modified todo list
                    user.SetLastList(newTodo);

                    string filePath = string.Empty;

                    switch (user.GetLastInlineMessage().Text)
                    {
                        case "To Do Liste für Gestern:":
                            filePath = DataIO.GetFilePath(bot.day.AddDays(-1).DayOfWeek.ToString() + "_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                            break;

                        case "To Do Liste für Heute:":
                            filePath = DataIO.GetFilePath(bot.day.DayOfWeek.ToString() + "_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                            break;

                        case "To Do Liste für Morgen:":
                            filePath = DataIO.GetFilePath(bot.day.AddDays(1).DayOfWeek.ToString() + "_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                            break;

                        case "Allgemeine To Do Liste:":
                            filePath = DataIO.GetFilePath("General_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                            break;
                    }

                    if (filePath != String.Empty)
                        DataIO.SaveToFile(filePath, user.GetLastList());

                    return;
                }

                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

            }


            List<string> s = new();
            s.Add("Gestern");
            s.Add("Heute");
            s.Add("Morgen");
            s.Add("Allgemein");

            user.SetLastList(s);

            user.SetLastInlineMessage(
                await botClient.SendTextMessageAsync(
                chatId: update.Message.Chat.Id,
                text: "Welche ToDo Liste?",
                replyMarkup: MessageBuilder.GenerateInlineKeyboard(s)
                ));


        }

        async public static Task SendEvents(User user, TelegramBot bot)
        {
            List<Event> eventsToSend = user.GetEvents().OrderBy(e => e.dateTime).ToList();

            if (eventsToSend == null || eventsToSend.Count == 0)
            {
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("ScheduleEmpty"));
                return;
            }

            StringBuilder sb = new();

            sb.AppendLine("Alle Events:");

            foreach (Event e in eventsToSend)
            {
                sb.Append(e.dateTime.ToString("dd.MM.yyyy") + "-")
                    .Append(e.dateTime.ToString("HH:mm"))
                    .Append("-" + e.title)
                    .AppendLine();
            }

            await bot.botClient.SendTextMessageAsync(user.id, sb.ToString());

        }

        async public static Task SendEventsLong(User user, TelegramBot bot, int maxLength = 42)
        {

            List<Event> eventsToSend = user.GetEvents();

            if (eventsToSend == null || eventsToSend.Count == 0)
            {
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("ScheduleEmpty"));
                return;
            }

            string schedule = MessageBuilder.GenerateEventSchedule(user.GetEvents(),maxLength);

            await bot.botClient.SendTextMessageAsync(user.id, schedule, parseMode: ParseMode.Html);

        }

        async public static Task Clear(User user, TelegramBot bot, CancellationToken cT)
        {
            user.SetLastCommand("/clear");

            List<string> options = new();
            options.Add("Ja");
            options.Add("Nein");

            await bot.botClient.SendTextMessageAsync(
                chatId : user.id,
                text : DataIO.GetMessage("ClearEvents"),
                replyMarkup : MessageBuilder.GenerateInlineKeyboardCallback(options, "clearRequest")
                );

        }

        async public static Task SetWakeTime(User user, TelegramBot bot, string text, CancellationToken cT)
        {
            bool success = false;


            if(text.Contains(':'))
            {
                string[] split = text.Split(":");

                if(int.TryParse(split[0], out int hours) && int.TryParse(split[1], out int minutes))
                    if(hours >= 0 && hours <25 && minutes >= 0 && minutes < 60)
                    {
                        string h, m;
                        h = m = "";

                        if (hours <= 9)
                            h = "0";
                        if (minutes <= 9)
                            m = "0";

                        text = user.waketime = $"{h}{hours}:{m}{minutes}";
                        bot.SaveUsers();
                        success = true;
                    }
            }

            if(success)
               await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("SetWakeTimeSuccess").Replace("{time}",text), parseMode: ParseMode.Html);

            else
               await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("SetWakeTimeError"), parseMode: ParseMode.Html);
        }

        async public static Task SetDayCommand(User user, string text, TelegramBot bot)
        {
            if (Enum.GetNames(typeof(DayOfWeek)).Any(d => d.Equals(text, StringComparison.OrdinalIgnoreCase)))
            {
                user.SetLastCommand($"/setday {text.ToLower()}");

                await bot.botClient.SendTextMessageAsync(
                    chatId: user.id,
                    text: DataIO.GetMessage("SetDayPrompt"),
                    parseMode: ParseMode.Html
                    );
            }

            else if(string.IsNullOrEmpty(text.Replace("\n","").Replace("/setday","").Trim()))
            {
                await bot.botClient.SendTextMessageAsync(
                chatId: user.id,
                text: DataIO.GetMessage("ArgumentError1"),
                parseMode: ParseMode.Html
                );
                return;
            }

            else
            {
                await bot.botClient.SendTextMessageAsync(
                chatId: user.id,
                text: DataIO.GetMessage("ArgumentError2"),
                parseMode: ParseMode.Html
                );
                return;
            }
        }

        async public static Task GetDay(User user, string text, TelegramBot bot, int maxLength = 42)
        {
            if (Enum.GetNames(typeof(DayOfWeek)).Any(d => d.Equals(text, StringComparison.OrdinalIgnoreCase)))
                user.SetLastCommand($"/getday");


            else if (string.IsNullOrEmpty(text))
            {
                await bot.botClient.SendTextMessageAsync(
                chatId: user.id,
                text: DataIO.GetMessage("ArgumentError1"),
                parseMode: ParseMode.Html
                );
                return;
            }

            else
            {
                await bot.botClient.SendTextMessageAsync(
                chatId: user.id,
                text: DataIO.GetMessage("ArgumentError2"),
                parseMode: ParseMode.Html
                );
                return;
            }

            // Finally create the schedule
            string day = text;

            char firstChar = char.ToUpper(day[0]);
            string remainingChars = day.Length > 1 ? day.Substring(1) : string.Empty;

            day = firstChar + remainingChars;

            string filepath = DataIO.GetFilePath(day+".json",Path.Combine("users", user.id.ToString()),true);

            List<Event> ?eventsToSend = DataIO.LoadFromFile<List<Event>>(filepath);

            if (eventsToSend == null || eventsToSend.Count == 0)
            {
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("DayEmpty"));
                return;
            }

            string schedule = MessageBuilder.CreateDailySchedule(eventsToSend, true, maxLength, title : eventsToSend.First().dateTime.ToString("dddd", new CultureInfo("de-DE")));

            await bot.botClient.SendTextMessageAsync(user.id, schedule, parseMode: ParseMode.Html);
        }

        async public static Task Done(User user, TelegramBot bot)
        {
            if (!user.GetLastCommand().Contains("/done"))
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("Done"), parseMode: ParseMode.Html);

            else
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("AlreadyDone"), parseMode: ParseMode.Html);

            user.aiSettings.ResetMessages();
            user.SetLastCommand("/done");
        }

        async public static Task Transcribe(User user, TelegramBot bot)
        {
            await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("Transcribe"), parseMode: ParseMode.Html);

            user.SetLastCommand("/transcribe");
        }

        async public static Task Chat(User user, TelegramBot bot, CancellationToken cT, string msg = "")
        {
            // User enters the first time
            if(user.GetLastCommand() != "/chat" && !user.GetLastCommand().Contains("/chat-"))
            {
                user.SetLastCommand("/chat");
                
                // Access to the GPT4 models is currently unavailable
                List<string> s = new();
                s.Add("Davinci");
                s.Add("Curie");
                s.Add("ChatGPT");
                //s.Add("GPT4");
                //s.Add("GPT4-32K");

                user.SetLastInlineMessage(await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("ModelToUse"), replyMarkup: MessageBuilder.GenerateInlineKeyboardCallbackVertical(s,"/chatModel")));
            }


            else
            {
                string modelToUse = DataIO.ModelTranslation(user.GetLastCommand().Replace("/chat-",""));

                if (apiClient == null)
                {
                    string apiKey = DataIO.GetSetting("OpenAiAPIKey") ?? "void";

                    if (apiKey == "void")
                    {
                        DataIO.Log("OpenAiAPIKey not found in settings.json!", severity: "Error");
                        return;
                    }

                    apiClient = new(apiKey);
                }

                var c = new CancellationTokenSource();

                // Request the OpenAI Api
                try
                {
                    // Let the bot type while the request is being processed
                    
                    Task typing = bot.SendTypingIndicator(c.Token, user);
                    string response;

                    if (modelToUse == "text-davinci-003" || modelToUse == "text-curie-001")
                         response = await apiClient.CreateCompletionAsync(user, msg, cT, maxTokens: user.aiSettings.maxTokens, temperature: user.aiSettings.temperature, topP : user.aiSettings.topP, model: modelToUse);

                    else
                        response = await apiClient.CreateChatCompletionAsync(user, msg, cT, maxTokens: user.aiSettings.maxTokens, temperature: user.aiSettings.temperature, model: modelToUse);

                    c.Cancel();

                    // Save response to array
                    user.aiSettings.AddAiMessage(response);

                    // split up the response if necessary and send them individually
                    List<string> substrings = MessageBuilder.BreakString(response, 3900);
                    user.aiSettings.tokenCount += response.Length / 4;

                    // calculate cost and increase
                    double total = user.aiSettings.cost;
                    if (modelToUse == "text-davinci-003")
                        total += user.aiSettings.tokenCount * 0.02 / 1000;

                    else if (modelToUse == "text-curie-001")
                        total += user.aiSettings.tokenCount * 0.002 / 1000;

                    else if (modelToUse == "gpt-3.5-turbo")
                        total += user.aiSettings.tokenCount * 0.002 / 1000;

                    // finally send the strings
                    foreach (string substring in substrings)
                        await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("AIResponse").Replace("{tokens}",user.aiSettings.tokenCount.ToString()) +$"(total: {total.ToString("F6")}$)\n\n" + substring, parseMode: ParseMode.Html);

                    user.aiSettings.tokenCount = 0;

                }

                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("AIResponseFailed") + e.Message, parseMode: ParseMode.Html);
                    return;
                }

                finally
                {
                    c.Cancel();
                }

            }
            

        }

        async public static Task AiSettings(User user, TelegramBot bot, string msg)
        {
            try
            {
                msg = msg.Replace(',','.');

                string[] s = msg.Split('-');
                int MaxTokens;
                double Temperature;
                double TopP;
                double contextPercentage;
                CultureInfo cultureInfo = new CultureInfo("en-US");

                // cultureInfo is necessary because otherwise servers perform differently -> 1,0 = 1.0 in AT, 1,0 = 10 in US
                if (int.TryParse(s[0], NumberStyles.Integer, cultureInfo, out MaxTokens) &&
                    double.TryParse(s[1], NumberStyles.Float, cultureInfo, out Temperature) &&
                    double.TryParse(s[2], NumberStyles.Float, cultureInfo, out TopP) &&
                    double.TryParse(s[3], NumberStyles.Float, cultureInfo, out contextPercentage))
                {
                    user.aiSettings.maxTokens = MaxTokens;
                    user.aiSettings.temperature = Temperature;
                    user.aiSettings.topP = TopP;
                    user.aiSettings.contextPercentage = contextPercentage;
                    await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("AiSettingsParsed"), parseMode: ParseMode.Html);
                    bot.SaveUsers();
                }

                else
                {
                    await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("ArgumentError2"), parseMode: ParseMode.Html);
                }
                    
            }

            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("ArgumentError2"), parseMode: ParseMode.Html);
            }

        }

        async public static Task SetSystem(User user, TelegramBot bot, string msg)
        {
            if(String.IsNullOrEmpty(msg))
            {
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("ArgumentError1"), parseMode: ParseMode.Html);
                return;
            }

            user.aiSettings.AddSystemMessage(msg);
            await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("SystemMsgParsed"), parseMode: ParseMode.Html);
        }
         
        async public static Task ReceivedVoice(User user, Update update, TelegramBot bot)
        {
            // User is not whitelisted
            if (!bot.users.Any(user => user.id == update.Message.Chat.Id))
                return;

            Console.WriteLine("Voice Message received!");
            CancellationTokenSource cts = new();
            string path = DataIO.GetFilePath($"{user.id}_{DateTime.Now.ToString("dd.MM.yyyy_hh-mm-ss")}_voice.ogg", "voice", true);

            try
            {
                
                Task typingIndicator = bot.SendTypingIndicator(cts.Token, user);

                // Handle Voice Messages
                string fileId;
                if (update.Message.Voice != null)
                    fileId = update.Message.Voice.FileId;

                // Assume Audio File
                else
                    fileId = update.Message.Audio.FileId;

                using (FileStream filestream = System.IO.File.Create(path))
                {
                    var file = await bot.botClient.GetFileAsync(fileId);
                    await bot.botClient.DownloadFileAsync(file.FilePath, filestream);
                    await filestream.FlushAsync();
                }

                await FFMpegArguments
                    .FromFileInput(path)
                    .OutputToFile(path+".mp3",true, options => options
                    .WithSpeedPreset(FFMpegCore.Enums.Speed.Fast))
                    .ProcessAsynchronously();

                // Call to OpenAiAPI
                OpenAIAuthentication auth = new(DataIO.GetSetting("OpenAiAPIKey"));


                // Now parse the result to LLM unless user wants a transcription
                if(user.GetLastCommand().Contains("/transcribe"))
                {
                    // Whisper handled by external lib
                    var api = new OpenAIClient(auth);
                    var request = new AudioTranscriptionRequest(Path.GetFullPath(path + ".mp3"));
                    string result = await api.AudioEndpoint.CreateTranscriptionAsync(request);
                    string summary = "";

                    foreach(string s in MessageBuilder.BreakString(result, 3900))
                        await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("TranscriptionSuccess") + s, parseMode: ParseMode.Html);
                    
                    // Summarise Voice Message
                    if(apiClient == null)
                        apiClient = new OpenAiApi(DataIO.GetSetting("OpenAiAPIKey"));

                    var settings = new AiSettings();
                    settings.temperature = 0.3;
                    settings.contextPercentage = 0.7;
                    settings.maxTokens = 1800;
                    settings.AddSystemMessage(DataIO.GetSetting("SummarisePrompt"));

                    summary = await apiClient.CreateChatCompletionAsync(result, settings, cts.Token);

                    foreach (string s in MessageBuilder.BreakString(summary, 3900))
                        await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("TranscriptionSummary") + summary, parseMode: ParseMode.Html);

                    return;
                }

                // Whisper handled by external lib
                var api2 = new OpenAIClient(auth);
                var request2 = new AudioTranscriptionRequest(Path.GetFullPath(path + ".mp3"), language: "de");
                string result2 = await api2.AudioEndpoint.CreateTranscriptionAsync(request2);

                // replace - as they would cause errors when parsing
                result2 = result2.Replace('-', '_');

                await EventHandler.HandleEventRequest(user, result2, bot, cts.Token);

                // clean up files used
                DataIO.DeleteFileIfExists(path);
                DataIO.DeleteFileIfExists(path + ".mp3");

            }

            catch(Exception e)
            {
                await bot.botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("VoiceError") + e.Message, parseMode: ParseMode.Html);
            }

            finally
            {
                DataIO.DeleteFileIfExists(path);
                DataIO.DeleteFileIfExists(path + ".mp3");
                cts.Cancel();
            }

            





        }
    }

}
