using tests_.src.Domain.Entities;

namespace WhatsAppProject.Dtos
{
    public class InteractiveMenuDto
    {
        public string Recipient { get; set; }
        public int ContactId { get; set; }
        public int SectorId { get; set; }
        public string Header { get; set; }
        public List<MenuOptionDto> Options { get; set; }
    }

    public class MenuOptionDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Value { get; set; }
    }


}
