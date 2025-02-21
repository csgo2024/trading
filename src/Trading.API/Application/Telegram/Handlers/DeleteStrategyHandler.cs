using MediatR;
using Trading.API.Application.Commands;

namespace Trading.API.Application.Telegram.Handlers
{
    public class DeleteStrategyHandler : ICommandHandler
    {
        private readonly IMediator _mediator;
        private readonly ILogger<DeleteStrategyHandler> _logger;

        public static string Command => "/delete";

        public DeleteStrategyHandler(IMediator mediator, ILogger<DeleteStrategyHandler> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task HandleAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id), "<pre>Strategy ID cannot be null or empty</pre>");
            }
            var strategyId = id.Trim();
            var command = new DeleteStrategyCommand { Id = strategyId };
            var deleteResult = await _mediator.Send(command);
            if (!deleteResult)
            {
                throw new InvalidOperationException($"<pre>Failed to delete strategy {strategyId}</pre>");
            }
            _logger.LogInformation("<pre>策略[{StrategyId}]已删除 ✅</pre>", strategyId);
        }
    }
}