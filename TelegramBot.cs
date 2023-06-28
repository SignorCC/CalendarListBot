using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


namespace CalendarListBot
{

    public class TelegramBot
    {
        public TelegramBotClient botClient;
        public List<User> users;
        public DateTime day;
        public List<Event> pendingEvents;
        public bool run = true;
        public bool error = false;

        public TelegramBot(string token)
        {
            botClient = new TelegramBotClient(token);
            users = DataIO.LoadFromFile<List<User>>(DataIO.GetFilePath("users.json")) ?? new List<User>();
            pendingEvents = EventHandler.GetCurrentEvents(this);
            day = DateTime.Now;
        }

        public void StartBot(CancellationTokenSource cts)
        {          
            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
            };

            botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
            );
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {

                if (update.Type is UpdateType.CallbackQuery)
                    await CallbackHandler(update, cancellationToken);

                // Only process Message updates: https://core.telegram.org/bots/api#message
                if (update.Message is not { } message)
                    return;

                // Only process text messages
                if (message.Text is not { } messageText)
                    messageText = "Non Text Type";

                // Only answer to whitelist
                var chatId = message.Chat.Id;

                Console.WriteLine($"Received '{messageText}' from {chatId}.");
                DataIO.Log($"Received '{messageText}' from {chatId}.");

                // Pass received message to Handler
                await MessageHandler(update, cancellationToken);
            }

            catch(Exception e)
            {
                Console.WriteLine(e.Message.ToString() + e.ToString());
            }
            
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            this.error = true;
            return Task.CompletedTask;
        }

        async Task MessageHandler(Update update, CancellationToken cT)
        {

            // New User and no Text message
            if (!users.Any(user => user.id == update.Message.Chat.Id) && update.Message.Text == null)
                return;

            // Check if user is in users or tries to register (LINQ)
            if (!users.Any(user => user.id == update.Message.Chat.Id) && !update.Message.Text.StartsWith("/register"))
                return;

            User user = this.users.FirstOrDefault(u => u.id == update.Message.Chat.Id);

            // Now pass to other Handlers
            // Handle Voice Messages
            if (update.Message.Voice != null)
                await BotCommands.ReceivedVoice(user, update, this);

            if (update.Message.Text == null)
                return;

            string[] msg = update.Message.Text.Split(" ");

            string command = msg[0];
            string text = update.Message.Text.Replace($"{command} ", "");

            switch (command)
            {
                case "/set":
                    await EventHandler.SetEvent(update, cT, this);
                    break;

                case "/del":
                    await EventHandler.DeleteEventOnMessage(update, cT, text,this);
                    break;

                case "/events":
                    await BotCommands.SendEvents(user, this);
                    break;

                case "/schedule":
                    await BotCommands.SendEventsLong(user,this);
                    break;

                case "/setday":
                    await BotCommands.SetDayCommand(user, text, this);
                    break;

                case "/getday":
                    await BotCommands.GetDay(user, text, this);
                    break;

                case "/register":
                    await WhitelistUser(update, text, cT);
                    break;

                case "/restart":
                    if (user.id.ToString() != DataIO.GetSetting("Admin"))
                    {
                        await botClient.SendTextMessageAsync(update.Message.Chat.Id, DataIO.GetMessage("Forbidden"), cancellationToken: cT, parseMode: ParseMode.Html);
                        return;
                    }
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, DataIO.GetMessage("OnExit"), cancellationToken: cT, parseMode: ParseMode.Html);
                    this.run = false;
                    break;

                case "/help":
                    await botClient.SendTextMessageAsync(update.Message.Chat.Id, DataIO.GetMessage("Help"), cancellationToken: cT, parseMode: ParseMode.Html);
                    break;

                case "/todo":
                    await BotCommands.ToDo(update, this, cT);
                    break;

                case "/clear":
                    await BotCommands.Clear(user, this, cT);
                    break;

                case "/waketime":
                    await BotCommands.SetWakeTime(user, this, text, cT);
                    break;

                case "/done":
                    await BotCommands.Done(user, this);
                    break;

                case "/chat":
                    await BotCommands.Chat(user, this, cT);
                    break;

                case "/aisettings":
                    await BotCommands.AiSettings(user, this, text);
                    break;

                case "/setsystem":
                    await BotCommands.SetSystem(user, this, text);
                    break;

                case "/transcribe":
                    await BotCommands.Transcribe(user, this);
                    break;

                default:
                    await TextHandler(update, text, cT);
                    break;
            }

        }

        async Task CallbackHandler(Update update, CancellationToken cT)
        {
            var cb = update.CallbackQuery;
            User user = users.FirstOrDefault(user => user.id == update.CallbackQuery.From.Id);

            // Handle Callback Queries containing Callback::
            if (update.CallbackQuery.Data.StartsWith("Callback::"))
            {
                await MessageCallback(update, user, update.CallbackQuery.Data.Replace("Callback::",""),cT);
                return;
            }
            
            List<string> ?todo = new();
            string ?newMessage = null;
            
            switch(cb.Data)
            {
                case "Gestern":
                    todo = DataIO.LoadFromFile<List<string>>(DataIO.GetFilePath(this.day.AddDays(-1).DayOfWeek.ToString()+"_ToDo.json", Path.Combine("users", user.id.ToString()), true));
                    newMessage = "To Do Liste für Gestern:";
                    break;

                case "Heute":
                    todo = DataIO.LoadFromFile<List<string>>(DataIO.GetFilePath(this.day.DayOfWeek.ToString() + "_ToDo.json", Path.Combine("users", user.id.ToString()), true));
                    newMessage = "To Do Liste für Heute:";
                    break;

                case "Morgen":
                    todo = DataIO.LoadFromFile<List<string>>(DataIO.GetFilePath(this.day.AddDays(1).DayOfWeek.ToString() + "_ToDo.json", Path.Combine("users", user.id.ToString()), true));
                    newMessage = "To Do Liste für Morgen:";
                    break;

                case "Allgemein":
                    todo = DataIO.LoadFromFile<List<string>>(DataIO.GetFilePath("General_ToDo.json", Path.Combine("users", user.id.ToString()), true));
                    newMessage = "Allgemeine To Do Liste:";
                    break;

                default:
                    todo = user.GetLastList();
                    break;
            }

            if (todo == null)
            {
                todo = new();
                todo.Add("Nothing To Do Here!");
            }

            if (todo.Count < 1)
                todo.Add("Nothing To Do Here!");

            
            // No new list was loaded from Memory now let's edit
            else if (newMessage == null)
            {
                int index = todo.IndexOf(cb.Data);

                if (cb.Data.Contains("✅ "))
                    todo[index] = cb.Data.Replace("✅ ", "");

                else
                    todo[index] = "✅ " + cb.Data;
            }

            try
            {

                user.SetLastInlineMessage(
                    await botClient.EditMessageTextAsync(
                    messageId: cb.Message.MessageId,
                    chatId: user.id,
                    text : newMessage ?? cb.Message.Text.ToString(),
                    replyMarkup: MessageBuilder.GenerateInlineKeyboard(todo)
                ));

                await botClient.AnswerCallbackQueryAsync(cb.Id);

                // Save the modified todo list
                user.SetLastList(todo);

                string filePath = string.Empty;

                switch (user.GetLastInlineMessage().Text)
                {
                    case "To Do Liste für Gestern:":
                        filePath = DataIO.GetFilePath(this.day.AddDays(-1).DayOfWeek.ToString() + "_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                        break;

                    case "To Do Liste für Heute:":
                        filePath = DataIO.GetFilePath(this.day.DayOfWeek.ToString() + "_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                        break;

                    case "To Do Liste für Morgen:":
                        filePath = DataIO.GetFilePath(this.day.AddDays(1).DayOfWeek.ToString() + "_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                        break;

                    case "Allgemeine To Do Liste:":
                        filePath = DataIO.GetFilePath("General_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                        break;
                }

                if(filePath != String.Empty)
                    DataIO.SaveToFile(filePath, user.GetLastList());
            }

            catch (Exception e)
            {
                Console.WriteLine(e.ToString()+e.Message.ToString());
            }
            
        }

        async Task MessageCallback(Update update, User user, string CallbackInfo, CancellationToken cT)
        {
            string info = CallbackInfo.Split("::")[0];
            string ButtonInfo = CallbackInfo.Split("::")[1];

            if(info.Contains("/chatModel"))
            {
                user.SetLastCommand("/chat-"+ButtonInfo);

                await botClient.EditMessageTextAsync(
                        messageId: update.CallbackQuery.Message.MessageId,
                        chatId: user.id,
                        text: DataIO.GetMessage("Chat"));

            }

            else if(ButtonInfo.StartsWith("Ja"))
            {
                if(user.GetLastCommand() == "/clear")
                {
                    user.ClearEvents();

                    await botClient.EditMessageTextAsync(
                        messageId: update.CallbackQuery.Message.MessageId,
                        chatId: user.id,
                        text: DataIO.GetMessage("EventsCleared"));

                    return;
                        
                }
            }

            else if(ButtonInfo.StartsWith("Nein"))
            {
                if (user.GetLastCommand() == "/clear")
                {
                    await botClient.EditMessageTextAsync(
                        messageId: update.CallbackQuery.Message.MessageId,
                        chatId: user.id,
                        text: DataIO.GetMessage("EventsNotCleared"));

                    return;
                }
            }

            else if(ButtonInfo.StartsWith("Löschen::"))
            {
                Console.WriteLine($"Löschen");
                return;
            }

            else if (ButtonInfo.StartsWith("Neu::"))
            {
                Console.WriteLine($"Neu");
                return;
            }

            else
            {               
                Console.WriteLine($"Received Callbackinfo {CallbackInfo}");
            }

            await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
        }

        async Task WhitelistUser(Update update, string text, CancellationToken cT)
        {

            Dictionary<string, string>? settings = DataIO.LoadSettings(DataIO.GetFilePath("settings.json"));


            if (text.Equals(settings.GetValueOrDefault("Password")))
            {
                users.Add(new User(update.Message.Chat.Id, update.Message.Chat.FirstName));
                DataIO.SaveToFile(DataIO.GetFilePath("users.json"), users);

                DataIO.Log($"Added User: {update.Message.Chat.FirstName} [{update.Message.Chat.Id}] to Whitelist");
                await botClient.SendTextMessageAsync(update.Message.Chat.Id, DataIO.GetMessage("RegisterSuccess"));
            }

            else
                DataIO.Log($"User: {update.Message.Chat.FirstName} [{update.Message.Chat.Id}] tried to register");
        }

        public async Task MinutePassed()
        {
            // this method checks for events that are to be sent out

            DateTime currentTime = DateTime.Now; // note this check doesn't incorporate different time zones
            bool newDay = false;

            // if a new day has started, cache all pending events for the day

            if (this.day.Date != currentTime.Date)
                newDay = true;

            foreach (User u in users)
            {
                string[] time = u.waketime.Split(":");
                int hour = int.Parse(time[0]);
                int minutes = int.Parse(time[1]);

                if (currentTime.Hour == hour && currentTime.Minute == minutes)
                    await EventHandler.WakeUser(u, this);
            }

            // check for minutes and hours, ignore seconds and milliseconds
            foreach (Event e in pendingEvents)
                if (currentTime.Add(e.remindertime).Hour == e.dateTime.Hour && currentTime.Add(e.remindertime).Minute == e.dateTime.Minute)
                    await EventHandler.NotifyUser(e, this);


            if (newDay)
            {
                pendingEvents = EventHandler.GetCurrentEvents(this);
                day = currentTime;

                // Clean up old ToDo (older than 1 day)

                string oldToDo = day.AddDays(-2).DayOfWeek + "_ToDo.json";

                foreach(User user in users)
                {
                    // simply save an empty list instead of recreating files
                    string path = DataIO.GetFilePath(oldToDo,Path.Combine("users", user.id.ToString()),true);
                    DataIO.SaveToFile(path, new List<string>());

                    // remove old items from general todo
                    var filePath = DataIO.GetFilePath("General_ToDo.json", Path.Combine("users", user.id.ToString()), true);
                    List<string> newTodo = DataIO.LoadFromFile<List<string>>(filePath);
                    List<string> prevTodo = DataIO.LoadFromFile<List<string>>(filePath);

                    foreach (string s in prevTodo)
                        if (s.Contains("✅"))
                            newTodo.Remove(s);

                    DataIO.SaveToFile(filePath, newTodo);
                }
            }         
                

            for (int i = pendingEvents.Count - 1; i >= 0; i--)
            {
                Event e = pendingEvents[i];

                if (e.isDeleted)
                    pendingEvents.RemoveAt(i);
            }
        }

        private async Task TextHandler(Update update, string text, CancellationToken cT)
        {
            User user = this.users.FirstOrDefault(u => u.id == update.Message.Chat.Id);

            if (user == null)
                return;

            switch(user.GetLastCommand())
            {
                case "/todo":
                    await BotCommands.ToDo(update, this, cT);
                    break;

                case "/chat":
                    await botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("ChooseModel"));
                    break;

                default:
                    if (user.GetLastCommand().StartsWith("/setday"))
                        await EventHandler.SetDay(update, user, cT, this);

                    else if(user.GetLastCommand().StartsWith("/chat-"))
                        await BotCommands.Chat(user, this, cT, update.Message.Text);

                    else if(text.StartsWith("/"))
                        await botClient.SendTextMessageAsync(user.id, DataIO.GetMessage("UnrecognisedCommand"));

                    else
                        await botClient.SendTextMessageAsync(user.id,DataIO.GetMessage("UnrecognisedText"));

                    break;
            }
                
        }
        private async Task SendProject(User user, string projectName)
        {
            List<string> buttons = new();
            buttons.Add("Neu");
            buttons.Add("Löschen");

            Message newProjectMessage = await botClient.SendTextMessageAsync(
                replyMarkup: MessageBuilder.GenerateInlineKeyboardCallback(buttons, info: projectName),
                chatId: user.id,
                text: MessageBuilder.getProject(user, projectName)
                ); ;

            user.SetLastInlineMessage(newProjectMessage);
        }

        public async Task SendTypingIndicator(CancellationToken cT, User user)
        {
            while (!cT.IsCancellationRequested)
            {
                await botClient.SendChatActionAsync(user.id, ChatAction.Typing);
                await Task.Delay(TimeSpan.FromSeconds(3), cT);
            }
        }

        public void SaveUsers()
        {
            DataIO.SaveToFile(DataIO.GetFilePath("users.json"), this.users);
        }


    }
}
