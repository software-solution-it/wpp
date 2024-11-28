using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WhatsAppProject.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TokenController : ControllerBase
    {
        private readonly ILogger<TokenController> _logger;
        private readonly IHttpClientFactory _clientFactory;

        public TokenController(ILogger<TokenController> logger, IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _clientFactory = clientFactory;
        }

        [HttpGet("validate")]
        public async Task<IActionResult> ValidateTokenAndPhoneId([FromQuery] string token, [FromQuery] string phoneId)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(phoneId))
            {
                return BadRequest(new { message = "Token ou phone_id estão ausentes." });
            }

            var requestUri = $"https://graph.facebook.com/v17.0/{phoneId}?fields=id&access_token={token}";
            var client = _clientFactory.CreateClient();

            try
            {
                var response = await client.GetAsync(requestUri);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var jsonResponse = JsonSerializer.Deserialize<MetaValidationResponse>(responseBody);

                    if (jsonResponse != null && jsonResponse.Id == phoneId)
                    {
                        return Ok(new { message = "Token e phone_id são válidos." });
                    }

                    return BadRequest(new { message = "Token ou phone_id são inválidos." });
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Erro na validação do token: {Error}", errorResponse);
                    return BadRequest(new { message = "Erro ao validar token e phone_id.", details = errorResponse });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar o token ou phone_id.");
                return StatusCode(500, new { message = "Erro interno ao validar o token." });
            }
        }

        private class MetaValidationResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }
    }
}
