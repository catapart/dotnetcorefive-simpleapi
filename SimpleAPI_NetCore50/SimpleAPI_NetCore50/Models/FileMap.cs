namespace SimpleAPI_NetCore50.Models
{
    public class FileMap
    {
        public int Id { get; set; }
        public string FilenameForDisplay { get; set; }
        public string FilenameOnDisk { get; set; }
        public string ContentType { get; set; }
        public string UnadjustedDisplayFilename { get; set; }
    }
}
