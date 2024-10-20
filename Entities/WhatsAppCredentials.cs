using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppProject.Entities
{
    [Table("whatsapp_credentials")] // Define o nome da tabela em snake_case
    public class WhatsAppCredentials
    {
        [Key]
        [Column("id")] // Define o nome da coluna em snake_case
        public int Id { get; set; }

        [Column("phone_number_id")] // Define o nome da coluna em snake_case
        public string PhoneNumberId { get; set; }

        [Column("access_token")] // Define o nome da coluna em snake_case
        public string AccessToken { get; set; }

        [Column("sector_id")] // Define o nome da coluna em snake_case
        public int SectorId { get; set; } // Coluna para associar ao setor

        [Column("created_at")] // Define o nome da coluna em snake_case
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
