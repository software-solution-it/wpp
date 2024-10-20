using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppProject.Entities
{
    [Table("messages")] // Define o nome da tabela em snake_case
    public class Message
    {
        [Key]
        [Column("id")] // Define o nome da coluna em snake_case
        public int Id { get; set; }

        [Column("recipient")] // Define o nome da coluna em snake_case
        public string Recipient { get; set; }

        [Column("content")] // Define o nome da coluna em snake_case
        public string Content { get; set; }

        [Column("sent_at")] // Define o nome da coluna em snake_case
        public DateTime SentAt { get; set; }
    }
}
