namespace Trading.Common.Models;

public class PagedRequest
{
    public int PageIndex { get; set; } // 当前页码
    public int PageSize { get; set; }    // 每页大小
    public object? Filter { get; set; }

    public object? Sort { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; }  // 数据项
    public int PageIndex { get; set; } // 当前页码
    public int TotalPages { get; set; }  // 总页数
    public int PageSize { get; set; }    // 每页大小
    public int TotalCount { get; set; }  // 总记录数

    public PagedResult(List<T> items, int pageIndex, int pageSize, int totalCount)
    {
        Items = items;
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize); // 计算总页数
    }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }   // 是否成功
    public T? Data { get; set; }         // 泛型数据部分

    // 构造函数
    public ApiResponse(bool success, T data)
    {
        Success = success;
        Data = data;
    }

    // 静态方法帮助创建成功响应
    public static ApiResponse<T> SuccessResponse(T data)
    {
        return new ApiResponse<T>(true, data);
    }

    // 静态方法帮助创建错误响应
    public static ApiResponse<T?> ErrorResponse()
    {
        return new ApiResponse<T?>(false, default);
    }
}
