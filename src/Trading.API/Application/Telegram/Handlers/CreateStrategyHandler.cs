using MediatR;
using Newtonsoft.Json;
using Trading.API.Application.Commands;

namespace Trading.API.Application.Telegram.Handlers
{
    public class CreateStrategyHandler : ICommandHandler
    {
        private readonly IMediator _mediator;
        private readonly ILogger<CreateStrategyHandler> _logger;

        public static string Command => "/create";

        public CreateStrategyHandler(IMediator mediator, ILogger<CreateStrategyHandler> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task HandleAsync(string parameters)
        {
            try
            {
                if (string.IsNullOrEmpty(parameters))
                {
                    throw new ArgumentNullException(nameof(parameters));
                }
                var command = JsonConvert.DeserializeObject<CreateStrategyCommand>(parameters);
                if (command == null)
                {
                    throw new InvalidOperationException("<pre>Failed to Deserialize parameters.</pre>");
                }
                await _mediator.Send(command);
                _logger.LogInformation("<pre>策略创建成功 ✅</pre>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "<pre>Create strategy failed</pre>");
            }
        }
    }
}