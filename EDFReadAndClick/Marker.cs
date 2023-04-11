namespace EDFReadAndClick;

public class Marker
{
    public string SignalName { get; set; }
    public int TimeMillis { get; set; }
    public int Value { get; set; }

    public Marker(string signalName, int timeMillis, int value)
    {
        SignalName = signalName;
        TimeMillis = timeMillis;
        Value = value;
    }
}