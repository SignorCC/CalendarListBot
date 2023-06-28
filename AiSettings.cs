using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalendarListBot
{
    public class AiSettings
    {
        public int maxTokens;
        public double temperature;
        public double topP;
        public double contextPercentage;
        public double cost;
        public int tokenCount;
        private List<(string, string)> messages;  // List of (role, content) tuples

        public AiSettings()
        {
            maxTokens = 120;
            temperature = 0.7;
            topP = 1;
            contextPercentage = 0.3;
            cost = 0;
            tokenCount = 0;

            messages = new List<(string, string)>();
        }

        public List<object> BuildContext(int maxTokens)
        {
            var contextMessages = new List<object>();
            int totalTokens = 0;

            // Filter system messages from messages list and process them first as they have highest priority
            foreach (var (role, message) in messages.Where(m => m.Item1 == "system").Reverse())
            {
                int messageTokens = message.Length / 4;

                // If adding this message would exceed maxTokens, skip it
                if (totalTokens + messageTokens > maxTokens)
                    continue;

                contextMessages.Add(new { role = role, content = message });
                totalTokens += messageTokens;
            }

            // Check if we've already exceeded the max tokens
            if (totalTokens > maxTokens)
                throw new Exception("Max tokens exceeded by system messages.");

            // Process user and assistant messages
            foreach (var (role, message) in messages.Where(m => m.Item1 != "system").Reverse())
            {
                int messageTokens = message.Length / 4;

                // If we're at an assistant message and we can't fit this whole message, try to fit part of it
                if (role == "assistant" && totalTokens + messageTokens > maxTokens)
                {
                    int remainingTokens = maxTokens - totalTokens;
                    string truncatedMessage = message.Substring(message.Length - remainingTokens * 4);

                    contextMessages.Add(new { role = role, content = truncatedMessage });
                    totalTokens += remainingTokens;

                    break;
                }

                // If we're at a user message and adding this message would exceed maxTokens, skip it
                if (role == "user" && totalTokens + messageTokens > maxTokens)
                    continue;

                // If adding this message would exceed maxTokens, stop here
                if (totalTokens + messageTokens > maxTokens)
                    break;

                contextMessages.Add(new { role = role, content = message });
                totalTokens += messageTokens;
            }

            // Reverse the contextMessages list so oldest messages are first
            contextMessages.Reverse();

            tokenCount += totalTokens;

            return contextMessages;
        }

        public string BuildPrompt(int maxTokens)
        {
            var prompt = new StringBuilder();
            int totalTokens = 0;

            // Filter system messages from messages list and calculate their tokens
            var systemMessages = messages.Where(m => m.Item1 == "system");
            int systemMessageTokens = systemMessages.Sum(m => m.Item2.Length / 4);

            // If system messages tokens exceed maxTokens, throw an exception
            if (systemMessageTokens > maxTokens)
                throw new Exception("System messages exceed max tokens.");

            // Calculate the tokens left after system messages
            int tokensLeft = maxTokens - systemMessageTokens;

            // Filter and reverse user and assistant messages from messages list
            var userAndAssistantMessages = messages.Where(m => m.Item1 != "system").ToList();
            userAndAssistantMessages.Reverse();

            // Process user and assistant messages from newest to oldest, respecting the tokens left
            foreach (var (role, message) in userAndAssistantMessages)
            {
                int messageTokens = message.Length / 4;

                // If adding this message would exceed tokensLeft, try to truncate if assistant or skip if user
                if (totalTokens + messageTokens > tokensLeft)
                {
                    if (role == "assistant")
                    {
                        int remainingTokens = tokensLeft - totalTokens;
                        string truncatedMessage = message.Substring(message.Length - remainingTokens * 4);

                        prompt.Insert(0, $"{role}: {truncatedMessage}\n");
                        totalTokens += remainingTokens;

                        break;
                    }

                    if (role == "user")
                        continue;
                }

                prompt.Insert(0, $"{role}: {message}\n");
                totalTokens += messageTokens;
            }

            // Add system messages at the top
            foreach (var (role, message) in systemMessages)
                prompt.Insert(0, $"{role}: {message}\n");
            

            // Ensure the last role is assistant
            if (!prompt.ToString().EndsWith("assistant:\n"))
                prompt.AppendLine("assistant:");
            

            tokenCount += totalTokens;

            return prompt.ToString();
        }

        public void AddSystemMessage(string message)
        {
            AddMessage("system", message);
        }

        public void AddUserMessage(string message)
        {
            AddMessage("user", message);
        }

        public void AddAiMessage(string message)
        {
            AddMessage("assistant", message);
        }

        public void ResetMessages()
        {
            this.messages = new();
            this.cost = 0;
            this.tokenCount = 0;
        }

        private void AddMessage(string role, string message)
        {
            // If role is not "system" and there are more than 30 non-system messages, remove the oldest non-system message
            if (role != "system" && messages.Count(m => m.Item1 != "system") >= 30)
            {
                var oldestNonSystemMessageIndex = messages.FindIndex(m => m.Item1 != "system");
                if (oldestNonSystemMessageIndex != -1)
                {
                    messages.RemoveAt(oldestNonSystemMessageIndex);
                }
            }

            // Add the new message
            messages.Add((role, message));
        }


    }
}
