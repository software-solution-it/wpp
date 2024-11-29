using MongoDB.Bson;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WhatsAppProject.Entities
{
    [Table("contact_flow_status")]
    public class ContactFlowStatus
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("contact_id")]
        public int ContactId { get; set; }

        [Column("flow_id")]
        [Required]
        public string FlowId { get; set; } // ID do fluxo específico

        [Column("current_node_id")]
        public string CurrentNodeId { get; set; } // ID do nó atual do fluxo

        [Column("is_flow_complete")]
        public bool IsFlowComplete { get; set; } = false; // Indica se o fluxo foi concluído

        [Column("is_awaiting_user_response")]
        public bool IsAwaitingUserResponse { get; set; } = false; // Indica se está aguardando resposta do usuário

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Relacionamentos
        [ForeignKey("ContactId")]
        public virtual Contacts Contact { get; set; }
    }
}
