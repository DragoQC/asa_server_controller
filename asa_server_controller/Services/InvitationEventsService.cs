namespace asa_server_controller.Services;

public sealed class InvitationEventsService(ILogger<InvitationEventsService> logger)
{
    public event Action? Changed;

    public void NotifyChanged()
    {
        if (Changed is null)
        {
            return;
        }

        IEnumerable<Action> handlers = Changed
            .GetInvocationList()
            .Cast<Action>();

        foreach (Action handler in handlers)
        {
            try
            {
                handler();
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Invitation changed subscriber failed.");
            }
        }
    }
}
