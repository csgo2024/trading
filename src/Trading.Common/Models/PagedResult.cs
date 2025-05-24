namespace Trading.Common.Models;

public class PagedRequest
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public object? Filter { get; set; }

    public object? Sort { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int PageIndex { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }

    public PagedResult(List<T> items, int pageIndex, int pageSize, int totalCount)
    {
        Items = items;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
    }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }

    public ApiResponse(bool success, T data)
    {
        Success = success;
        Data = data;
    }

    public static ApiResponse<T> SuccessResponse(T data)
    {
        return new ApiResponse<T>(true, data);
    }

    public static ApiResponse<T?> ErrorResponse()
    {
        return new ApiResponse<T?>(false, default);
    }
}
