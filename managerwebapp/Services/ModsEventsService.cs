namespace managerwebapp.Services;

public sealed class ModsEventsService(ILogger<ModsEventsService> logger)
{
    public event Func<Task>? Changed;

    public async Task NotifyChangedAsync()
    {
        if (Changed is null)
        {
            return;
        }

        foreach (Func<Task> handler in Changed.GetInvocationList().Cast<Func<Task>>())
        {
            try
            {
                await handler();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Mods changed subscriber failed.");
            }
        }
    }
}
