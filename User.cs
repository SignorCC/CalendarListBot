using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace CalendarListBot
{
    public class User
    {
        public long id { get; set; }

        public string waketime { get; set; }
        public string? name { get; set; }

        public AiSettings aiSettings;

        private string lastCommand;

        private List<string> lastList;

        private List<Event> events;

        private Message ?lastInlineMessage;

        public User(long id, string? name, string waketime = "9:00")
        {
            this.id = id;
            this.name = name;
            this.waketime = waketime;
            this.lastCommand = "";
            this.lastList = new();
            this.aiSettings = new();

            this.LoadEvents();

            if(events == null)
            {
                events = new List<Event>();

                // Create Lists and weekly plan
                CreateWeeklyFiles();
            }
            
        }

        public bool AddEvent(Event newEvent)
        {
            if (this.events == null)
            {
                this.events = new List<Event>();
                this.SaveEvents();
            }

            if (newEvent == null || this.events.Contains(newEvent))
                return false;


            this.events.Add(newEvent);
            this.SaveEvents();

            return true;
            
        }

        public bool DeleteEvent(Event oldEvent)
        {
            if (this.events.Contains(oldEvent))
            {
                while(this.events.Contains(oldEvent))
                    this.events.Remove(oldEvent);

                this.SaveEvents();

                return true;
            }

            return false;

        }

        public void ClearEvents()
        {
            this.events = new();
            this.SaveEvents();
        }

        public List<Event> GetEvents()
        {
            return this.events;
        }

        public void Clean()
        {
            for (int i = events.Count - 1; i >= 0; i--)
            {
                Event e = events[i];

                // catch all for undeleted, expired events after a day
                if (e.dateTime.AddDays(1) < DateTime.Now && e.eventType == EventType.Once)
                    e.setDeleted();

                if (e.isDeleted)
                    events.RemoveAt(i);
            }

            SaveEvents();
        }

        public void LoadEvents()
        {
            string filePath = DataIO.GetFilePath("events.json", Path.Combine("users", $"{this.id}"), true);

            events = DataIO.LoadFromFile<List<Event>>(filePath);
        }

        public void SaveEvents()
        {
            string filePath = DataIO.GetFilePath("events.json", Path.Combine("users",$"{this.id}"), true);

            DataIO.SaveToFile(filePath, events);
        }

        private void CreateWeeklyFiles()
        {

            string[] daysOfWeek = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

            // Start with a Monday, then increment
            List<Event> dummyEvents = new();
            DateTime dt = new DateTime(2000, 1, 3, 9,0,0);

            

            foreach (var day in daysOfWeek)
            {
                dummyEvents.Add(new Event(this.id, dt, "Morgen Routine", "", "", _eventType: EventType.Weekly));

                string filePath = DataIO.GetFilePath($"{day}.json", Path.Combine("users", $"{this.id}"), true);

                DataIO.SaveToFile(filePath, dummyEvents);

                dummyEvents.Clear();
                dt = dt.AddDays(1);
            }

            CreateTodoFiles();

        }

        private void CreateTodoFiles()
        {

            var daysOfWeek = Enum.GetNames(typeof(DayOfWeek));

            // Iterate through the days of the week
            foreach (var day in daysOfWeek)
            {

                string filePath = DataIO.GetFilePath($"{day}_ToDo.json", Path.Combine("users",this.id.ToString()), true);

                // Check if the file exists
                if (!File.Exists(filePath))
                    DataIO.SaveToFile(filePath, new List<string>());

            }

            string generalFilePath = DataIO.GetFilePath("General_ToDo.json", Path.Combine("users",this.id.ToString()),true);

            if (!File.Exists(generalFilePath))
                DataIO.SaveToFile(generalFilePath, new List<string>());

        }

        public string GetLastCommand()
        {
            return this.lastCommand;
        }

        public void SetLastCommand(string lastCommand)
        {
            this.lastCommand = lastCommand;
        }

        public List<string> GetLastList()
        {
            return this.lastList;
        }

        public void SetLastList(List<string> lastList)
        {
            this.lastList = lastList;
        }

        public void SetLastInlineMessage(Message message)
        {
            this.lastInlineMessage = message;
        }

        public Message ?GetLastInlineMessage()
        {
            return this.lastInlineMessage;
        }


    }
}
