using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.EntityFrameworkCore;
using WhatsAppProject.Data;
using WhatsAppProject.Dtos;
using WhatsAppProject.Entities;

namespace WhatsAppProject.Services
{
    public class WhatsAppService
    {
        private readonly WhatsAppContext _context;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public WhatsAppService(WhatsAppContext context, HttpClient httpClient, IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        // Envia mensagem de texto
        public async Task SendMessageAsync(MessageDto messageDto, int sectorId)
        {
            var credentials = await GetWhatsAppCredentialsBySectorIdAsync(sectorId);

            var message = new Message
            {
                Recipient = messageDto.Recipient,
                Content = messageDto.Content,
                SentAt = DateTime.UtcNow
            };

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = messageDto.Recipient,
                type = "text",
                text = new { body = messageDto.Content }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Usa o token do banco de dados
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var response = await _httpClient.PostAsync($"https://graph.facebook.com/v20.0/{credentials.PhoneNumberId}/messages", content);
            response.EnsureSuccessStatusCode();

            await _context.Messages.AddAsync(message);
            await _context.SaveChangesAsync();
        }

        public async Task SendMediaAsync(SendFileDto sendFileDto, int sectorId)
        {
            // Passo 1: Upload do arquivo base64 para o S3
            var fileUrl = await UploadMediaToS3Async(sendFileDto.Base64File, sendFileDto.MediaType, sendFileDto.FileName);

            var mediaType = MapMimeTypeToMediaType(sendFileDto.MediaType);

            // Se for áudio, converta para Opus
            if (mediaType == "audio")
            {
                var opusFileUrl = await ConvertAudioToOpusAndUploadToS3Async(fileUrl);
                await SendMediaMessageAsync(opusFileUrl, sendFileDto.Recipient, "codecs=opus", sendFileDto.FileName, sendFileDto.Caption, sectorId);
            }
            else
            {
                await SendMediaMessageAsync(fileUrl, sendFileDto.Recipient, mediaType, sendFileDto.FileName, sendFileDto.Caption, sectorId);
            }
        }

        // Upload de mídia para o S3
        private async Task<string> UploadMediaToS3Async(string base64File, string mediaType, string fileName)
        {
            var awsAccessKey = _configuration["AWS:AccessKey"];
            var awsSecretKey = _configuration["AWS:SecretKey"];
            var awsBucketName = _configuration["AWS:BucketName"];
            var awsRegion = "sa-east-1";

            var s3Client = new AmazonS3Client(awsAccessKey, awsSecretKey, Amazon.RegionEndpoint.GetBySystemName(awsRegion));

            // Decodificar o base64 para bytes
            var fileBytes = Convert.FromBase64String(base64File);
            var tempFilePath = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tempFilePath, fileBytes);

            try
            {
                var transferUtility = new TransferUtility(s3Client);

                // Faz upload para o S3
                var fileTransferRequest = new TransferUtilityUploadRequest
                {
                    BucketName = awsBucketName,
                    Key = fileName,
                    InputStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read),
                    ContentType = mediaType
                };

                await transferUtility.UploadAsync(fileTransferRequest);

                // Retorna a URL do arquivo
                var fileUrl = $"https://{awsBucketName}.s3.amazonaws.com/{fileName}";

                return fileUrl;
            }
            finally
            {
                File.Delete(tempFilePath); // Limpa o arquivo temporário
            }
        }

        private async Task<string> ConvertAudioToOpusAndUploadToS3Async(string audioFileUrl)
        {
            var tempFilePath = Path.GetTempFileName();
            var opusFilePath = Path.ChangeExtension(tempFilePath, ".opus");

            // Baixar o arquivo do S3
            using (var webClient = new HttpClient())
            {
                var audioData = await webClient.GetByteArrayAsync(audioFileUrl);
                await File.WriteAllBytesAsync(tempFilePath, audioData);
            }

            var ffmpegPath = @"C:\Program Files\ffmpeg\bin\ffmpeg.exe"; // Caminho do executável FFmpeg
            var arguments = $"-i \"{tempFilePath}\" -c:a libopus \"{opusFilePath}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processStartInfo))
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception("FFmpeg conversion to Opus failed.");
                }
            }

            // Passo 2: Fazer upload do arquivo Opus convertido para o S3
            var opusFileName = Path.GetFileNameWithoutExtension(audioFileUrl) + ".opus"; // Use o mesmo nome, mas com extensão .opus
            var opusFileUrl = await UploadMediaToS3Async(Convert.ToBase64String(await File.ReadAllBytesAsync(opusFilePath)), "audio/opus", opusFileName);

            // Limpar o arquivo temporário
            File.Delete(tempFilePath);
            File.Delete(opusFilePath); // Exclui o arquivo Opus temporário

            return opusFileUrl;
        }

        private async Task SendMediaMessageAsync(string fileUrl, string recipient, string mediaType, string fileName, string caption, int sectorId)
        {
            var credentials = await GetWhatsAppCredentialsBySectorIdAsync(sectorId);

            object payload;

            switch (mediaType)
            {
                case "codecs=opus":
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        recipient_type = "individual",
                        to = recipient,
                        type = "audio",
                        audio = new { link = fileUrl }
                    };
                    break;

                case "image":
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        recipient_type = "individual",
                        to = recipient,
                        type = "image",
                        image = new { link = fileUrl, caption = caption }
                    };
                    break;

                case "video":
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        recipient_type = "individual",
                        to = recipient,
                        type = "video",
                        video = new { link = fileUrl }
                    };
                    break;

                case "document":
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        recipient_type = "individual",
                        to = recipient,
                        type = "document",
                        document = new
                        {
                            link = fileUrl,
                            caption = caption,
                            filename = fileName
                        }
                    };
                    break;

                default:
                    throw new ArgumentException("Invalid media type", nameof(mediaType));
            }

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var whatsappBaseUrl = _configuration["WhatsApp:BaseUrl"];

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

            var response = await _httpClient.PostAsync($"{whatsappBaseUrl}/{credentials.PhoneNumberId}/messages", content);
            response.EnsureSuccessStatusCode();
        }

        private async Task<WhatsAppCredentials> GetWhatsAppCredentialsBySectorIdAsync(int sectorId)
        {
            var credentials = await _context.WhatsAppCredentials.FirstOrDefaultAsync(c => c.SectorId == sectorId);
            if (credentials == null)
            {
                throw new Exception($"Credenciais não encontradas para o setor com ID {sectorId}");
            }
            return credentials;
        }

        private string MapMimeTypeToMediaType(string mimeType)
        {
            if (mimeType.StartsWith("image/"))
            {
                return "image";
            }
            else if (mimeType.StartsWith("audio/"))
            {
                return "audio";
            }
            else if (mimeType.StartsWith("video/"))
            {
                return "video";
            }
            else
            {
                return "document";
            }
        }
    }

    // DTOs
    public class WhatsAppMediaResponse
    {
        public List<MediaItem> Media { get; set; }
    }

    public class MediaItem
    {
        public string Id { get; set; }
    }
}
