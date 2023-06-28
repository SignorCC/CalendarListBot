using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalendarListBot
{
    using System;
    using RestSharp;
    using Newtonsoft.Json.Linq;
    using System.Threading;

    public class OpenAiApi
    {
        private string ApiKey;
        private const string BaseUrl = "https://api.openai.com/v1";
        private readonly RestClient client;

        public OpenAiApi(string apiKey)
        {
            ApiKey = apiKey;
            client = new RestClient(BaseUrl);
        }

        // This helper method handles the POST request to the specified endpoint with the provided model, max tokens, and content
        public async Task<string> CreateChatCompletionAsync(User usr, string message, CancellationToken cT, int maxTokens = 300, double temperature = 0.7, string model = "gpt-3.5-turbo")
        {
            var request = new RestRequest("/chat/completions", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {ApiKey}");
            request.Timeout = 3 * 60 * 1000;

            // Confuse open AI and circumvent censoring (hopfully)
            Random random = new Random();
            string user = random.Next(10000000, 99999999).ToString();
            DataIO.LogAI(user, usr.name, message);
            usr.aiSettings.AddUserMessage(message);

            var body = new
            {
                user = user,
                model = model,
                max_tokens = maxTokens,
                temperature = temperature,
                messages = usr.aiSettings.BuildContext((int)(maxTokens * usr.aiSettings.contextPercentage)).ToArray()
            };
            request.AddJsonBody(body);

            var response = await client.ExecuteAsync(request, cancellationToken: cT);

            if (response.IsSuccessful)
            {
                var contentJObject = JObject.Parse(response.Content);
                return contentJObject["choices"]?.First?["message"]["content"]?.ToString().Trim();
            }

            throw new Exception($"Request failed: {response.StatusCode} {response.ErrorMessage}");
        }

        public async Task<string> CreateChatCompletionAsync(string message, AiSettings aiSettings, CancellationToken cT)
        {
            var request = new RestRequest("/chat/completions", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {ApiKey}");
            request.Timeout = 3 * 60 * 1000;
            aiSettings.AddUserMessage(message);

            // Confuse open AI and circumvent censoring (hopfully)
            Random random = new Random();
            string user = random.Next(10000000, 99999999).ToString();

            var body = new
            {
                user = user,
                model = "gpt-3.5-turbo",
                max_tokens = aiSettings.maxTokens,
                temperature = aiSettings.temperature,
                messages = aiSettings.BuildContext((int)(aiSettings.maxTokens*aiSettings.contextPercentage)).ToArray()
            };
            request.AddJsonBody(body);

            var response = await client.ExecuteAsync(request, cancellationToken: cT);

            if (response.IsSuccessful)
            {
                var contentJObject = JObject.Parse(response.Content);
                return contentJObject["choices"]?.First?["message"]["content"]?.ToString().Trim();
            }

            throw new Exception($"Request failed: {response.StatusCode} {response.ErrorMessage}");
        }

        // Helper method for text completions
        public async Task<string> CreateCompletionAsync(User usr, string message, CancellationToken cT, int maxTokens = 300, double temperature = 0.7, double topP = 1, string model = "text-davinci-003")
        {
            var request = new RestRequest("/completions", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", $"Bearer {ApiKey}");

            // Confuse open AI and circumvent censoring (hopefully)
            Random random = new Random();
            string user = random.Next(10000000, 99999999).ToString();
            DataIO.LogAI(user, usr.name, message);
            usr.aiSettings.AddUserMessage(message);

            var body = new
            {
                user = user,
                model = model,
                prompt = usr.aiSettings.BuildPrompt((int)(maxTokens * usr.aiSettings.contextPercentage)),
                max_tokens = maxTokens,
                temperature = temperature,
                top_p = topP
            };

            request.AddJsonBody(body);

            var response = await client.ExecuteAsync(request, cT);

            if (response.IsSuccessful)
            {
                var contentJObject = JObject.Parse(response.Content);
                return contentJObject["choices"]?.First?["text"]?.ToString().Trim();
            }

            throw new Exception($"Request failed: {response.StatusCode} {response.ErrorMessage}");
        }

    }
}
