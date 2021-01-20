namespace SimpleAPI_NetCore50.Models
{
    public class WebsocketSessionFileProgress
    {
        public string SessionKey { get; set; }
        public int UnitsCompleted { get; set; }
        public int UnitTotal { get; set; }
        public string StepID { get; set; }
    }
}
