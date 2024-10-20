using WhatsAppProject.Data;
using WhatsAppProject.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace WhatsAppProject.Services
{
    public class WhatsAppCredentialsService
    {
        private readonly WhatsAppContext _context;

        public WhatsAppCredentialsService(WhatsAppContext context)
        {
            _context = context;
        }

        public async Task<WhatsAppCredentials> GetCredentialsAsync(int sectorId)
        {
            // Busca as credenciais com base no sectorId
            return await _context.WhatsAppCredentials
                .FirstOrDefaultAsync(c => c.SectorId == sectorId);
        }

        public async Task UpdateCredentialsAsync(string phoneNumber, string accessToken, int sectorId)
        {
            var credentials = await GetCredentialsAsync(sectorId);

            if (credentials == null)
            {
                // Insere novas credenciais se não houver nenhuma para o setor
                credentials = new WhatsAppCredentials
                {
                    PhoneNumberId = phoneNumber,
                    AccessToken = accessToken,
                    SectorId = sectorId
                };

                _context.WhatsAppCredentials.Add(credentials);
            }
            else
            {
                // Atualiza as credenciais existentes
                credentials.PhoneNumberId = phoneNumber;
                credentials.AccessToken = accessToken;
                credentials.SectorId = sectorId;
                _context.WhatsAppCredentials.Update(credentials);
            }

            await _context.SaveChangesAsync();
        }
    }
}
