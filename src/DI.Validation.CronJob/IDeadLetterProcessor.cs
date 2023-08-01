namespace DI.Validation.CronJob;

public interface IDeadLetterProcessor
{
    Task Execute(CancellationToken cancellationToken);
}