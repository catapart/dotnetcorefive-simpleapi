using System.ComponentModel.DataAnnotations;

namespace SimpleAPI_NetCore50.Models
{
    public class WebsocketSessionRequest
    {
        [Required(ErrorMessage ="SessionType is Required")]
        public string SessionType { get; set; }
        public int UnitTotal { get; set; }
    }
}
