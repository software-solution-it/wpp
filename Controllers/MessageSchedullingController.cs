using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WhatsAppProject.Services;

namespace WhatsAppProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessageSchedullingController : ControllerBase
    {
        private readonly MessageSchedulingService _messageSchedulingService;

        public MessageSchedullingController(MessageSchedulingService messageSchedulingService)
        {
            _messageSchedulingService = messageSchedulingService;
        }

        // Endpoint para listar todos os agendamentos de mensagens
        [HttpGet]
        public IActionResult GetAll()
        {
            var result = _messageSchedulingService.GetAllMessageSchedulings();
            return Ok(result);
        }

        // Novo endpoint para executar o job de agendamento de mensagens
        [HttpPost("execute-schedule")]
        public async Task<IActionResult> ExecuteSchedule()
        {
            try
            {
                await _messageSchedulingService.ScheduleAllMessagesAsync();
                return Ok("Job de agendamento de mensagens executado com sucesso.");
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, $"Erro ao executar o job: {ex.Message}");
            }
        }
    }
}
