// Data/WhatsAppContext.cs
using Microsoft.EntityFrameworkCore;
using WhatsAppProject.Entities;

namespace WhatsAppProject.Data
{
    public class WhatsAppContext : DbContext
    {
        public WhatsAppContext(DbContextOptions<WhatsAppContext> options) : base(options) { }

        public DbSet<Message> Messages { get; set; }
        public DbSet<MediaFile> MediaFiles { get; set; }

        public DbSet<WhatsAppCredentials> WhatsAppCredentials { get; set; }
    }
}
