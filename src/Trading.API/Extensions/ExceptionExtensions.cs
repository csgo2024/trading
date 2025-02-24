namespace Trading.API.Extensions;

public static class ExceptionExtensions
{
    /// <summary>
    /// 获取异常及其所有内部异常的集合
    /// </summary>
    /// <param name="exception">要处理的异常</param>
    /// <returns>异常及其所有内部异常的集合</returns>
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
