namespace asa_server_controller.Services;

public sealed class ModsEventsService(ILogger<ModsEventsService> logger)
{
    public event Action? Changed;

    public void NotifyChanged()
    {
        if (Changed is null)
        {
            return;
        }

        foreach (Action handler in Changed.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Mods changed subscriber failed.");
            }
        }
    }
}
