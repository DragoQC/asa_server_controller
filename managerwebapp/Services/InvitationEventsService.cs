namespace managerwebapp.Services;

public sealed class InvitationEventsService(ILogger<InvitationEventsService> logger)
{
    public event Func<Task>? Changed;

    public async Task NotifyChangedAsync()
    {
        if (Changed is null)
        {
            return;
        }

        IEnumerable<Func<Task>> handlers = Changed
            .GetInvocationList()
            .Cast<Func<Task>>();

        foreach (Func<Task> handler in handlers)
        {
            try
            {
                await handler();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Invitation changed subscriber failed.");
            }
        }
    }
}
