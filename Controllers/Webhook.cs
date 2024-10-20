using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace WhatsAppProject.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(ILogger<WebhookController> logger)
        {
            _logger = logger;
        }

        // GET: /webhook
        // Verifica o token para configurar o webhook (validação inicial)
        [HttpGet]
        public IActionResult Get([FromQuery] string hub_mode, [FromQuery] string hub_challenge, [FromQuery] string hub_verify_token)
        {
            const string VerifyToken = "your_verify_token";  // Substitua pelo seu token de verificação

            // Verifica se o modo é 'subscribe' e o token está correto
            if (hub_mode == "subscribe" && hub_verify_token == VerifyToken)
            {
                // Retorna o desafio fornecido pela Meta para validar o webhook
                return Ok(hub_challenge);
            }

            // Retorna status 403 se o token de verificação não for válido
            return Forbid();
        }

        // POST: /webhook
        // Recebe eventos enviados pelo WhatsApp
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] dynamic body)
        {
            try
            {
                // Converte o corpo dinâmico para string para ser registrado no log
                string bodyAsString = Convert.ToString(body);
                _logger.LogInformation("Webhook Received: {0}", bodyAsString);

                // TODO: Processar o webhook conforme necessário

                return Ok(); // Retorna status 200 para confirmar o recebimento
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao processar o webhook: {0}", ex.Message);
                return StatusCode(500, "Erro interno do servidor");
            }
        }

    }
}
