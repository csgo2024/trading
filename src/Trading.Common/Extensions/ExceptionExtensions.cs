namespace Trading.Common.Extensions;

public static class ExceptionExtensions
{
    public static IEnumerable<Exception> FlattenExceptions(this Exception exception)
    {
        if (exception == null)
        {
            yield break;
        }

        yield return exception;

        if (exception is AggregateException aggEx)
        {
            foreach (var innerEx in aggEx.InnerExceptions)
            {
                foreach (var ex in innerEx.FlattenExceptions())
                {
                    yield return ex;
                }
            }
        }
        else if (exception.InnerException != null)
        {
            foreach (var ex in exception.InnerException.FlattenExceptions())
            {
                yield return ex;
            }
        }
    }
}
