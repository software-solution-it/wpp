// Dtos/MessageDto.cs
namespace WhatsAppProject.Dtos
{
    public class MessageDto
    {
        public string Recipient { get; set; }
        public string Content { get; set; }
    }

    public class UpdateCredentialsDto
    {
        public string PhoneNumber { get; set; }
        public string AccessToken { get; set; }
        public int SectorId { get; set; }
    }
}
