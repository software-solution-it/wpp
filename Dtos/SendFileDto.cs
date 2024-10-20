namespace WhatsAppProject.Dtos
{
    public class SendFileDto
    {
        public string Base64File { get; set; }
        public string MediaType { get; set; }
        public string FileName { get; set; } // Adicionado
        public string Caption { get; set; }  // Adicionado
        public string Recipient { get; set; }
    }

}
