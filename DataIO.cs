using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CalendarListBot
{
    public static class DataIO
    {
        // Variables Section
        private static Dictionary<string, string> messagesCache = new Dictionary<string, string>();
        private static Dictionary<string, string> settings = new Dictionary<string, string>();
        private static Dictionary<string, string> ?map = null;

        public static void SaveToFile(string path, object obj)
        {
            string json = JsonConvert.SerializeObject(obj);
            File.WriteAllText(path, json);
        }

        public static T? LoadFromFile<T>(string path)
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json);
            }

            return default(T);
        }

        public static void DeleteFileIfExists(string path)
        {

            if (File.Exists(path))
                File.Delete(path);
            
        }

        public static string GetMessage(string messageKey)
        {
            string ?message = null;
            string filePath = GetFilePath("messages.json");

            if (messagesCache.Count == 0)
            {
                // Load messages from JSON file if cache is empty
                messagesCache = LoadFromFile<Dictionary<string, string>>(filePath) ?? new Dictionary<string, string>();
            }

            if (messagesCache.TryGetValue(messageKey, out message))
                return message;

            // Try searching the .json if nothing was found in the cache
            
            LoadFromFile<Dictionary<string, string>>(filePath).TryGetValue(messageKey, out message);

            // Add to Cache in case of hit
            if (message != null)
                messagesCache.Add(messageKey, message);

            else
                message = "MessageNotFoundinJSON";

            return message;
        }

        public static string ModelTranslation(string model)
        {
            string name = "";

            if (map == null)
                map = DataIO.LoadFromFile<Dictionary<string, string>>(DataIO.GetFilePath("models.json"));

            return name = map.GetValueOrDefault(model) ?? "";

        }

        public static string? GetSetting(string setting)
        {
            string? message = null;
            string filePath = GetFilePath("settings.json");

            if (settings.Count == 0)
            {
                // Load messages from JSON file if cache is empty
                settings = LoadFromFile<Dictionary<string, string>>(filePath) ?? new Dictionary<string, string>();
            }

            if (settings.TryGetValue(setting, out message))
                return message;

            // Try searching the .json if nothing was found in the cache

            LoadFromFile<Dictionary<string, string>>(filePath).TryGetValue(setting, out message);

            // Add to Cache in case of hit
            if (message != null)
                messagesCache.Add(setting, message);

            else
                message = null;

            return message;
        }

        public static string GetFilePath(string filename, string? path = null, bool appendToMainDir = false)
        {
            string fullPath = string.Empty;

            if (path != null)
            {
                string mainDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

                if (appendToMainDir)
                    fullPath = Path.Combine(mainDir, path);

                else
                    fullPath = path;

                Directory.CreateDirectory(fullPath);
                fullPath = Path.Combine(fullPath, filename);
            }

            else
                fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data", filename);

            if (!File.Exists(fullPath))
                File.Create(fullPath).Close();

            return fullPath;
        }

        public static string GetPath(string path)
        {
            
            string mainDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

            return Path.Combine(mainDir, path);

        }

        public static Dictionary<string, string>? LoadSettings(string path)
        {
            Dictionary<string, string>? settings = new();

            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                settings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }

            return settings;
        }

        public static void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException ioExp)
                {
                    Log($"Error while deleting file at {path}. Error: {ioExp.Message}", ioExp.HResult.ToString(), "Error");
                }
                catch (Exception exp)
                {
                    Log($"Unexpected error while deleting file at {path}. Error: {exp.Message}", exp.HResult.ToString(), "Error");
                }
            }

            else
                Log($"File at {path} not found.", null, "Warning");
            
        }

        public static void Log(string message, string? errorCode = null, string severity = "Info")
        {
            string logFileName = DateTime.Now.ToString("dd.MM.yyyy") + ".txt";

            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data", "logs");

            // create directory if it doesn't exist
            Directory.CreateDirectory(logDirectory);

            // create log file if it doesn't exist
            string logFilePath = Path.Combine(logDirectory, logFileName);

            if (!File.Exists(logFilePath))
            {
                using (FileStream fs = File.Create(logFilePath))
                    fs.Close();
            }

            // construct log entry
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            string entry = $"{timestamp} [{severity}]";

            if (!string.IsNullOrEmpty(errorCode))
                entry += $" [{errorCode}]";
            
            entry += $" {message}\n";

            // write log to console (logs in docker)
            Console.WriteLine(entry);

            // write log entry to file
            using (StreamWriter sw = File.AppendText(logFilePath))
            {
                sw.Write(entry);
                sw.Close();
            }
        }

        // Pseudo logging for AI
        public static void LogAI(string user, string random, string message)
        {
            string logFileName = "AILog_" + DateTime.Now.ToString("dd.MM.yyyy") + ".txt";

            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "logs", "AI");

            // create directory if it doesn't exist
            Directory.CreateDirectory(logDirectory);

            // create log file if it doesn't exist
            string logFilePath = Path.Combine(logDirectory, logFileName);

            if (!File.Exists(logFilePath))
            {
                using (FileStream fs = File.Create(logFilePath))
                    fs.Close();
            }

            // construct log entry
            string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            string entry = $"{timestamp}";

            entry += $"[r:{random}][{user}] {message}\n";

            // write log entry to file
            using (StreamWriter sw = File.AppendText(logFilePath))
            {
                sw.Write(entry);
                sw.Close();
            }
        }

    }
}
