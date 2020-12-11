using System.ComponentModel.DataAnnotations;

namespace SimpleAPI_NetCore50.Schemas
{
    public class SocketSessionRequest
    {
        [Required(ErrorMessage ="SessionType is Required")]
        public string SessionType { get; set; }
        public int UnitTotal { get; set; }
    }
}
