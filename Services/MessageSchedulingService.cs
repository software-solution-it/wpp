using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using WhatsAppProject.Data;
using WhatsAppProject.Entities;
using WhatsAppProject.Dtos;
using MongoDB.Driver;
using WhatsAppProject.Entities.WhatsAppProject.Entities;
using MongoDB.Bson;
using tests_.src.Domain.Entities;
using System.Diagnostics.Eventing.Reader;

namespace WhatsAppProject.Services
{
    public class MessageSchedulingService
    {
        private readonly SaasDbContext _saasContext;
        private readonly MongoDbContext _mongoDbContext;
        private readonly ContactService _contactService;
        private readonly WhatsAppService _whatsappService;
        private readonly WebhookService _webhookService;
        private readonly IMongoCollection<FlowWhatsapp> _flowsWhatsapp;

        public MessageSchedulingService(SaasDbContext context,
                                        MongoDbContext mongoDbContext,
                                        ContactService contactService,
                                        WhatsAppService whatsappService,
                                        WebhookService webhookService
                                        )
        {
            _saasContext = context;
            _mongoDbContext = mongoDbContext;
            _contactService = contactService;
            _whatsappService = whatsappService;
            _flowsWhatsapp = mongoDbContext.Flows;
            _webhookService = webhookService;
        }


        public async Task ContinuousExecutionAsync()
        {
            Console.WriteLine("Worker iniciado. Executando continuamente...");

            while (true)
            {
                try
                {
                    await ScheduleAllMessagesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro na execução do worker: {ex.Message}");
                }

                // Aguarda alguns segundos antes de reiniciar (configurável)
                await Task.Delay(TimeSpan.FromSeconds(3)); // Configura o intervalo entre execuções
            }
        }

        public async Task ScheduleAllMessagesAsync()
        {
            var messageSchedulings = GetAllMessageSchedulings();

            foreach (var message in messageSchedulings)
            {
                var contacts = await _contactService.GetContactsByTagIdAsync(message.Tags);

                foreach (var contact in contacts)
                {
                    if (message.FlowId == null)
                    {
                        if (!IsMessageSentToContact(contact.Id, message.Id))
                        {
                            EnqueueMessageForScheduledTime(message, contact);
                        }
                    }
                    else
                    {
                        await EnqueueMessageForFluxAsync(message, contact);
                    }
                }
            }
            BackgroundJob.Schedule(() => ScheduleAllMessagesAsync(), TimeSpan.FromSeconds(5));
        }

        public List<MessageScheduling> GetAllMessageSchedulings()
        {
            return _saasContext.MessageScheduling.ToList();
        }

        public void EnqueueMessageForScheduledTime(MessageScheduling message, Contacts contact)
        {
            if (DateTime.TryParseExact(message.SendDate, "dd-MM-yyyy HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out DateTime scheduledDateTime))
            {
                var delay = scheduledDateTime - DateTime.Now;

                if (delay > TimeSpan.Zero)
                {
                    BackgroundJob.Schedule(() => SendMessageAsync(message, contact), delay);
                    Console.WriteLine($"Mensagem agendada para {contact.Name} ({contact.Id}) às {scheduledDateTime}");
                }
                else
                {
                    Console.WriteLine($"Horário de envio para {contact.Name} já passou. Job não enfileirado.");
                }
            }
            else
            {
                Console.WriteLine($"Formato de data inválido para {message.SendDate}. Job não enfileirado.");
            }
        }

        public async Task EnqueueMessageForFluxAsync(MessageScheduling message, Contacts contact)
        {
            // Verifica o status do fluxo para o contato
            var contactFlowStatus = _saasContext.ContactFlowStatus
                .FirstOrDefault(status => status.ContactId == contact.Id && status.FlowId == message.FlowId);

            // Se não houver status, cria um novo
            if (contactFlowStatus == null)
            {
                contactFlowStatus = new ContactFlowStatus
                {
                    ContactId = contact.Id,
                    FlowId = message.FlowId,
                    CurrentNodeId = "",
                    IsFlowComplete = false
                };

                _saasContext.ContactFlowStatus.Add(contactFlowStatus);
                _saasContext.SaveChanges();
            }

            // Verifica se o fluxo não está completo
            if (!contactFlowStatus.IsFlowComplete)
            {
                try
                {
                    var flowIdString = message.FlowId;

                    // Valida o formato do FlowId
                    if (!ObjectId.TryParse(flowIdString, out var objectId))
                    {
                        Console.WriteLine($"ID inválido: {flowIdString}");
                        return;
                    }

                    // Busca o fluxo no MongoDB
                    var flow = await _flowsWhatsapp
                        .Find(flow => flow.Id == objectId.ToString())
                        .FirstOrDefaultAsync();

                    if (flow == null)
                    {
                        Console.WriteLine($"Nenhum fluxo encontrado com o ID: {flowIdString}");
                        return;
                    }

                    // Se o fluxo foi encontrado
                    Console.WriteLine($"Fluxo encontrado: {flow.Name}");

                    // Define o primeiro nó, se necessário
                    if (string.IsNullOrEmpty(contactFlowStatus.CurrentNodeId))
                    {
                        var firstNode = flow.Nodes.FirstOrDefault();
                        if (firstNode != null)
                        {
                            contactFlowStatus.CurrentNodeId = firstNode.Id;
                            _saasContext.ContactFlowStatus.Update(contactFlowStatus);
                            _saasContext.SaveChanges();
                        }
                    }

                    // Obtém o nó atual do fluxo
                    var currentNode = flow.Nodes.FirstOrDefault(node => node.Id == contactFlowStatus.CurrentNodeId);
                    if (currentNode != null)
                    {
                        await ProcessNodeAsync(flow, currentNode, contact, contactFlowStatus);
                    }
                    else
                    {
                        Console.WriteLine($"Nó atual não encontrado para ID: {contactFlowStatus.CurrentNodeId}");
                    }
                }
                catch (FormatException ex)
                {
                    Console.WriteLine($"Erro de formato ao buscar o fluxo: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro inesperado ao processar o fluxo: {ex.Message}");
                }
            }
        }


        private async Task ProcessNodeAsync(FlowWhatsapp flow, NodeDTO currentNode, Contacts contact, ContactFlowStatus contactFlowStatus)
        {
            if (contactFlowStatus.IsAwaitingUserResponse)
            {
                Console.WriteLine($"Aguardando resposta do contato {contact.Id} no nó {currentNode.Id}. Nenhuma ação será realizada até o contato responder.");
                return;
            }

            if (currentNode.Blocks != null && currentNode.Blocks.Count > 0)
            {
                foreach (var block in currentNode.Blocks)
                {
                    switch (block.Type)
                    {
                        case "text":
                            var messageDto = new MessageDto
                            {
                                Content = block.Content,
                                Recipient = contact.PhoneNumber,
                                ContactId = contact.Id,
                                SectorId = contact.SectorId
                            };
                            await _whatsappService.SendMessageAsync(messageDto);
                            break;

                        case "image":
                        case "attachment":
                            var base64Content = block.Media.Base64.Contains(",")
                                ? block.Media.Base64.Split(',')[1]
                                : block.Media.Base64;

                            var sendFileDto = new SendFileDto
                            {
                                Base64File = base64Content,
                                MediaType = block.Media.MimeType,
                                FileName = block.Media.Name,
                                Caption = block.Content,
                                Recipient = contact.PhoneNumber,
                                SectorId = contact.SectorId,
                                ContactId = contact.Id
                            };
                            await _whatsappService.SendMediaAsync(sendFileDto);
                            break;

                        case "timer":
                            var delayInMilliseconds = block.Duration * 1000;
                            await Task.Delay(delayInMilliseconds);
                            break;
                    }
                }
            }

            if (currentNode.MenuOptions != null && currentNode.MenuOptions.Content.Count > 0)
            {
                var menuDto = new InteractiveMenuDto
                {
                    Recipient = contact.PhoneNumber,
                    ContactId = contact.Id,
                    SectorId = contact.SectorId,
                    Header = currentNode.MenuOptions.Title,
                    Options = currentNode.MenuOptions.Content.Select(option => new MenuOptionDto
                    {
                        Title = option,
                        Description = string.Empty,
                        Value = option
                    }).ToList()
                };

                await _whatsappService.SendInteractiveMenuAsync(menuDto);
            }

            if (currentNode.Condition != null && !string.IsNullOrEmpty(currentNode.Condition.Condition))
            {
                bool conditionMet = EvaluateCondition(currentNode.Condition, contact);
                var nextEdge = flow.Edges.FirstOrDefault(edge =>
                    edge.Source == currentNode.Id &&
                    ((conditionMet && edge.SourceHandle == "true") || (!conditionMet && edge.SourceHandle == "false")));

                if (nextEdge != null)
                {
                    MoveToNextNode(flow, nextEdge.Target, contactFlowStatus);
                }
            }
            else
            {

                var nextEdge = flow.Edges.FirstOrDefault(edge => edge.Source == currentNode.Id);
                if (nextEdge != null)
                {
                    MoveToNextNode(flow, nextEdge.Target, contactFlowStatus);
                }
                else
                {

                    contactFlowStatus.IsFlowComplete = true;
                    contactFlowStatus.UpdatedAt = DateTime.UtcNow;
                    _saasContext.ContactFlowStatus.Update(contactFlowStatus);
                    _saasContext.SaveChanges();

                    Console.WriteLine($"Fluxo concluído para contato {contact.Id}");
                }
            }

            // Marca o estado como aguardando resposta do usuário
            contactFlowStatus.IsAwaitingUserResponse = true;
            contactFlowStatus.UpdatedAt = DateTime.UtcNow;
            _saasContext.ContactFlowStatus.Update(contactFlowStatus);
            await _saasContext.SaveChangesAsync();

            Console.WriteLine($"Fluxo pausado no nó {currentNode.Id} para contato {contact.Id}. Aguardando resposta do usuário.");
        }


        private bool EvaluateCondition(ConditionDTO condition, Contacts contact)
        {
            // Avaliação da condição simples baseada na lógica fornecida
            switch (condition.Condition.ToLower())
            {
                case "contem":
                    return contact.Name.Contains(condition.Value, StringComparison.OrdinalIgnoreCase);
                case "igual":
                    return contact.Name.Equals(condition.Value, StringComparison.OrdinalIgnoreCase);
                case "diferente":
                    return !contact.Name.Equals(condition.Value, StringComparison.OrdinalIgnoreCase);
                default:
                    Console.WriteLine($"Condição desconhecida: {condition.Condition}");
                    return false;
            }
        }

        private void MoveToNextNode(FlowWhatsapp flow, string nextNodeId, ContactFlowStatus contactFlowStatus)
        {
            if (string.IsNullOrEmpty(nextNodeId))
            {
                Console.WriteLine($"ID inválido ou vazio: {nextNodeId}");
                return;
            }

            // Procura o próximo nó no fluxo
            var nextNode = flow.Nodes.FirstOrDefault(node => node.Id == nextNodeId);

            if (nextNode != null)
            {
                // Atualiza o estado atual do contato para o próximo nó
                contactFlowStatus.CurrentNodeId = nextNode.Id;
                contactFlowStatus.UpdatedAt = DateTime.UtcNow;

                _saasContext.ContactFlowStatus.Update(contactFlowStatus);
                _saasContext.SaveChanges();

                Console.WriteLine($"Movendo para o próximo nó: {nextNode.Id}");
            }
            else
            {
                Console.WriteLine($"Erro: Nó com ID {nextNodeId} não encontrado no fluxo.");
            }
        }


        public async Task SendMessageAsync(MessageScheduling message, Contacts contact)
        {
            var messageDto = MapToMessageDto(message, contact);
            await _whatsappService.SendMessageAsync(messageDto);

            await _webhookService.TriggerWebhookEventAsync(message.SectorId ?? 0, messageDto);

            if (message.FileAttachment != null || message.FileMimeType != null || message.FileName != null)
            {
                var fileDto = MapToFileDto(message, contact);
                await _whatsappService.SendMediaAsync(fileDto);
            }

            if (message.ImageAttachment != null && message.ImageMimeType != null && message.ImageName != null)
            {
                var fileDto = MapToFileImageDto(message, contact);
                await _whatsappService.SendMediaAsync(fileDto);
            }

            await MarkMessageAsSent(contact.Id, message.Id);
            Console.WriteLine($"Mensagem enviada para {contact.Name} ({contact.Id}) com sucesso.");
        }

        private bool IsMessageSentToContact(int contactId, int messageId)
        {
            return _saasContext.ContactMessageStatus
                .Any(status => status.ContactId == contactId && status.MessageSchedulingId == messageId && status.IsSent);
        }

        private async Task MarkMessageAsSent(int contactId, int messageId)
        {
            var status = new ContactMessageStatus
            {
                ContactId = contactId,
                MessageSchedulingId = messageId,
                IsSent = true,
                SentAt = DateTime.UtcNow
            };

            _saasContext.ContactMessageStatus.Add(status);
            await _saasContext.SaveChangesAsync();
        }

        public SendFileDto MapToFileImageDto(MessageScheduling message, Contacts contact)
        {
            return new SendFileDto
            {
                Base64File = message.ImageAttachment ?? message.FileAttachment,
                MediaType = message.ImageMimeType ?? message.FileMimeType,
                FileName = message.ImageName ?? message.FileName,
                Caption = "",
                Recipient = contact.PhoneNumber,
                ContactId = contact.Id,
                SectorId = message.SectorId ?? 0
            };
        }

        public SendFileDto MapToFileDto(MessageScheduling message, Contacts contact)
        {
            return new SendFileDto
            {
                Base64File = message.FileAttachment,
                MediaType = message.FileMimeType,
                FileName = message.FileName,
                Caption = "",
                Recipient = contact.PhoneNumber,
                ContactId = contact.Id,
                SectorId = message.SectorId ?? 0
            };
        }

        public MessageDto MapToMessageDto(MessageScheduling message, Contacts contact)
        {
            return new MessageDto
            {
                Content = message.MessageText ?? string.Empty,
                MediaType = message.ImageMimeType ?? message.FileMimeType,
                MediaUrl = message.ImageAttachment ?? message.FileAttachment,
                SectorId = message.SectorId ?? 0,
                Recipient = contact.PhoneNumber,
                ContactId = contact.Id
            };
        }
    }
}
