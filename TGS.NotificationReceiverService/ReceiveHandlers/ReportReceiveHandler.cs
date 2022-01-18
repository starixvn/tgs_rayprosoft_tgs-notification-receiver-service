using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tgs.MessageQueue;

namespace TGS.NotificationReceiverService.ReceiveHandlers
{
    internal class ReportReceiveHandler : IMessageHandler
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ReportReceiveHandler> _logger;
        private readonly TelegramSettings _settings;

        public ReportReceiveHandler(
            ILogger<ReportReceiveHandler> logger,
            HttpClient httpClient,
            TelegramSettings settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async ValueTask HandleAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                var request = JsonSerializer.Deserialize<ReportErrorMessage>(json);

                var model = new
                {
                    chat_id = _settings.Channel,
                    text = $"<b><i>{request.Message}</i></b>",
                    parse_mode = "HTML"
                };

                var todoItemJson = new StringContent(
                    JsonSerializer.Serialize(model),
                    Encoding.UTF8,
                    "application/json");

                await _httpClient.PostAsync($"{_settings.Api}/bot{_settings.BotToken}/sendMessage", todoItemJson,
                    cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.InnerException?.Message);
                throw;
            }
        }
    }
}