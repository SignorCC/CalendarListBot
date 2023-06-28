using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[Serializable]
public class Event
{
    public long userID { get; set; }
    public DateTime dateTime { get; set; }
    public string? title { get; set; }
    public string? info { get; set; }
    public string? location { get; set; }
    public TimeSpan remindertime { get; set; }
    public bool isDeleted { get; set; }
    public EventType eventType { get; set; }

    public Event() { }

    public Event(long _userID, DateTime _dateTime, string _title, string _info = "", string _location = "", TimeSpan? _remindertime = null, EventType _eventType = EventType.Once)
    {
        userID = _userID;
        dateTime = _dateTime;
        title = _title;
        info = _info;
        location = _location;
        eventType = _eventType;
        isDeleted = false;

        if (_remindertime != null)
            remindertime = (TimeSpan)_remindertime;

        else
            remindertime = new TimeSpan(0, 60, 0); // default 60 minute reminder

    }

    public void setDeleted()
    {
        isDeleted = true;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"Event title: {title}\n");
        sb.Append($"Date and time: {dateTime}\n");

        if (info != null)
            sb.Append($"Info: {info}\n");

        if (location != null)
            sb.Append($"Location: {location}\n");

        sb.Append($"Remindertime: {remindertime}\n");

        return sb.ToString();
    }

}

public enum EventType
{
    Once,
    Daily,
    Weekly,
    Monthly,
    Yearly
}

