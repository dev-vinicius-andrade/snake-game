namespace Library.Commons.Api.Entities.Response.Abstractions;

public class BaseErrorResponseModel:BaseResponseModel
{
    protected BaseErrorResponseModel()
        : base(true)
    {
    }

    protected BaseErrorResponseModel(string message = null)
        : base(true, message)
    {
    }
}