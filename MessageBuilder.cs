using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types.ReplyMarkups;

namespace CalendarListBot
{
    public static class MessageBuilder
    {
        // This class is used for creating custom messages instead of
        // cluttering other classes with the mess of parsing strings

        public static string IndentLine(string toCut, int maxlength = 30, bool pre = true, string indentation = "     ")
        {
            StringBuilder builder = new StringBuilder();

            // Dirty Hack because I'm lazy
            if (!toCut.EndsWith(" "))
                toCut = toCut + " ";

            // If line exceeds maxlength, cut it and indent it
            int index = 0;
            int length = toCut.Length;

            // Check if the loop is necessary (prevents line breaks)
            if (length <= maxlength)
            {
                if (pre)
                    return $"<pre>{indentation}</pre>{toCut}";
                else
                    return $"{indentation}{toCut}";
            }

            while (index < length)
            {
                int remainingLength = length - index;
                int substringLength = Math.Min(maxlength, remainingLength);
                int lastWhitespaceIndex;

                if (substringLength <= 0)
                    break;

                string substring = toCut.Substring(index, substringLength);

                // Additional statement to cause voluntary line breaks
                // Check voluntary line breaks (formatting)
                if (toCut.Contains("//"))
                {

                    string[] lines = toCut.Split(new[] { "//" }, StringSplitOptions.None);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];

                        // Trim leading and trailing whitespace
                        line = line.Trim();

                        // If it's the last iteration, don't appendLine
                        if(i == lines.Length - 1)
                        {
                            if (pre)
                                builder.Append($"<pre>{indentation}</pre>{line}");

                            else
                                builder.Append($"{indentation}{line}");
                        }

                        else
                        {
                            if (pre)
                                builder.AppendLine($"<pre>{indentation}</pre>{line}");

                            else
                                builder.AppendLine($"{indentation}{line}");
                        }  
                    }
                    
                    Console.WriteLine(builder.ToString());

                    return builder.ToString();
                }

                // Check if the substring ends within a word
                lastWhitespaceIndex = substring.LastIndexOf(' ');

                if (lastWhitespaceIndex >= 0 && lastWhitespaceIndex < substringLength - 1)
                {
                    substring = substring.Substring(0, lastWhitespaceIndex + 1);
                    index += lastWhitespaceIndex + 1;
                }
                else
                    index += substringLength;

                // The pre tag tells telegram to keep whitespacing consistent when using markdown
                if(pre)
                    builder.Append($"<pre>{indentation}</pre>").AppendLine(substring);

                else
                    builder.Append(indentation).AppendLine(substring);
            }

            return builder.ToString();
        }

        public static string CreateDailySchedule(List<Event> events, bool header = false, int lineLength = 42, string title = "")
        {
            StringBuilder builder = new StringBuilder();

            string line = "";

            for (int i = 0; i < lineLength; i++)
                line += "_";
            
            if (events.Count <= 0)
                return builder.AppendLine(line).AppendLine(line).ToString();

            if(header)
            {
                string Head = line.Replace("_", " ").Substring(0, 26);
                string dayOfWeek = title;

                if (title == "")
                    dayOfWeek = events.First().dateTime.ToString("dddd", new CultureInfo("de-DE"));

                int middleIndex = Head.Length / 2;
                Head = Head.Insert(middleIndex, $"</pre><b><u>{dayOfWeek}</u></b><pre>");
                Head = Head.Substring(0, Head.Length - dayOfWeek.Length);
                Head = $"<pre>{Head}</pre>";
                builder.AppendLine(Head);
            }
            

            builder.AppendLine(line);

            // Sort events by dateTime in ascending order
            events = events.OrderBy(e => e.dateTime).ToList();

            foreach (var e in events)
            {
                builder.AppendLine($"<b>{e.dateTime.ToString("HH:mm")}</b> - <b>{e.title}</b>");

                if (e.info != "")
                {
                    builder.AppendLine(IndentLine("<b><u>Info:</u></b>", 30, true, "    "));
                    builder.Append(IndentLine(e.info.ToString(), 38, true, "    "));
                    builder.AppendLine();                    
                }

                if (e.location != "")
                {
                    builder.AppendLine();
                    builder.AppendLine(IndentLine("<b><u>Ort: </u></b>", 30, true, "    "));
                    builder.Append(IndentLine(e.location.ToString(), 38, true, "    "));
                    builder.AppendLine();
                }

                if(e != events.Last())
                    builder.AppendLine();
            }

            builder.AppendLine(line);

            return builder.ToString();
        }

        public static string GenerateEventSchedule(List<Event> events, int maxLength = 42, string indentation = "    ", string title = "Termine:")
        {
            StringBuilder builder = new StringBuilder();

            // Sort events by date and time in ascending order
            events = events.OrderBy(e => e.dateTime).ToList();

            // Create a Heading that's centered
            string line = "";

            for (int i = 0; i < maxLength; i++)
                line += "_";

            string Head = line.Replace("_", " ").Substring(0, 26);
            
            int middleIndex = Head.Length / 2;
            Head = Head.Insert(middleIndex, $"</pre><b><u>{title}</u></b><pre>");
            Head = Head.Substring(0, Head.Length - title.Length);
            Head = $"<pre>{Head}</pre>";
            
            builder.Append(Head);
            builder.AppendLine(line);

            foreach (var e in events)
            {
                builder.AppendLine($"<b>{e.dateTime.ToString("dd.MM.yyyy")} {e.dateTime.ToString("HH:mm")}</b> - <b>{e.title}</b>");

                if (!string.IsNullOrEmpty(e.info))
                {
                    builder.AppendLine(IndentLine("<b><u>Info:</u></b>", maxLength, pre: true, indentation));
                    builder.AppendLine(IndentLine(e.info.ToString(), maxLength, pre: true, indentation));
                    builder.AppendLine();
                }

                if (!string.IsNullOrEmpty(e.location))
                {
                    builder.AppendLine(IndentLine("<b><u>Ort:</u></b>", maxLength, pre: true, indentation));
                    builder.AppendLine(IndentLine(e.location.ToString(), maxLength, pre: true, indentation));

                    if (e != events.Last() && events.Count != 1)
                        builder.AppendLine();
                }

                if (e != events.Last() && events.Count != 1)
                    builder.AppendLine();
            }

            builder.AppendLine(line);

            return builder.ToString();
        }

        public static string CreateEvent(Event e)
        {
            if (e == null)
                return "Error - Event is null";

            StringBuilder builder = new StringBuilder();

            DataIO.GetMessage("");

            return builder.ToString();
        }

        public static InlineKeyboardMarkup GenerateInlineKeyboard(List<string> buttonNames)
        {
            var inlineKeyboardButtons = buttonNames.Select(
                buttonName => new[] { InlineKeyboardButton.WithCallbackData(buttonName) }
                ).ToArray();

            return new InlineKeyboardMarkup(inlineKeyboardButtons);
        }

        public static InlineKeyboardMarkup GenerateInlineKeyboardCallback(List<string> buttonNames, string info)
        {
            var inlineKeyboardButtons = new List<InlineKeyboardButton[]>();
            var row = new List<InlineKeyboardButton>();

            foreach (var buttonName in buttonNames)
            {
                var callbackData = $"Callback::{info}::{buttonName}";
                var button = InlineKeyboardButton.WithCallbackData(buttonName, callbackData);
                row.Add(button);

                // Check if the row is full
                if (row.Count >= 2)
                {
                    inlineKeyboardButtons.Add(row.ToArray());
                    row.Clear();
                }
            }

            // Add the remaining buttons if any
            if (row.Count > 0)
                inlineKeyboardButtons.Add(row.ToArray());

            return new InlineKeyboardMarkup(inlineKeyboardButtons.ToArray());
        }

        public static InlineKeyboardMarkup GenerateInlineKeyboardCallbackVertical(List<string> buttonNames, string info)
        {
            var inlineKeyboardButtons = new List<InlineKeyboardButton[]>();
            var row = new List<InlineKeyboardButton>();

            foreach (var buttonName in buttonNames)
            {
                var callbackData = $"Callback::{info}::{buttonName}";
                var button = InlineKeyboardButton.WithCallbackData(buttonName, callbackData);
                row.Add(button);

                // Check if the row is full
                if (row.Count >= 1)
                {
                    inlineKeyboardButtons.Add(row.ToArray());
                    row.Clear();
                }
            }

            // Add the remaining buttons if any
            if (row.Count > 0)
                inlineKeyboardButtons.Add(row.ToArray());

            return new InlineKeyboardMarkup(inlineKeyboardButtons.ToArray());
        }

        public static List<string> BreakString(string input, int maxLength)
        {
            if (input.Length <= maxLength)
                return new List<string> { input };

            List<string> substrings = new List<string>();
            int index = 0;

            while (index < input.Length)
            {
                int substringLength = Math.Min(maxLength, input.Length - index);
                string substring = input.Substring(index, substringLength);
                substrings.Add(substring);
                index += substringLength;
            }

            return substrings;
        }

        public static string getProject(User user, string projectName)
        {
            var path = DataIO.GetFilePath(projectName+".json",Path.Combine("users",user.id.ToString()),true);
            List<string> ?project = DataIO.LoadFromFile<List<string>>(path);

            if(project == null)
            {
                project = new List<string>();
                project.Add("EmptyProject: "+projectName);
                DataIO.SaveToFile(path, project);
            }

            StringBuilder sb = new();

            foreach (string s in project)
                sb.AppendLine(s);

            return sb.ToString();

        }


    }
}
