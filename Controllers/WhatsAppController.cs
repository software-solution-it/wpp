// Controllers/WhatsAppController.cs
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhatsAppProject.Dtos;
using WhatsAppProject.Services;

namespace WhatsAppProject.Controllers
{
    [ApiController]
    [Route("whatsapp")]
    public class WhatsAppController : ControllerBase
    {
        private readonly WhatsAppService _whatsappService;
        private readonly WebSocketManager _webSocketManager;

        public WhatsAppController(WhatsAppService whatsappService, WebSocketManager webSocketManager)
        {
            _whatsappService = whatsappService;
            _webSocketManager = webSocketManager;
        }

        [HttpPost("send-message")]
        public async Task<IActionResult> SendMessage([FromBody] MessageDto messageDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _whatsappService.SendMessageAsync(messageDto);
            return Ok(new { message = "Mensagem enviada com sucesso!" });
        }

        [HttpPost("send-file")]
        public async Task<IActionResult> SendFile([FromBody] Dtos.SendFileDto sendFileDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var mediaResponse = await _whatsappService.SendMediaAsync(sendFileDto);

            return Ok(new
            {
                message = "Arquivo enviado com sucesso!",
                media = mediaResponse
            });
        }

        [HttpPost("message-read")]
        public async Task<IActionResult> MarkMessageAsReadViaWebSocket([FromBody] MarkMessageReadDto markMessageReadDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Marcar como lida no banco de dados
            var isUpdated = await _whatsappService.MarkMessageAsReadAsync(markMessageReadDto.MessageId);

            if (isUpdated)
            {
                // Enviar notificação via WebSocket
                await _webSocketManager.MarkMessageAsReadAsync(markMessageReadDto.SectorId, markMessageReadDto.MessageId);

                return Ok(new { message = "Mensagem marcada como lida e notificada via WebSocket!" });
            }
            else
            {
                return NotFound(new { message = "Mensagem não encontrada." });
            }
        }


        public class WhatsAppMediaResponse
        {
            public string Id { get; set; }
        }


    }


}
