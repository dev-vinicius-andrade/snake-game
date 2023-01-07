using Library.Commons.Api.Entities.Response.Abstractions;

namespace Library.Commons.Api.Entities.Response;

public class ResponseModel : BaseResponseModel
{
    public ResponseModel() : base(false)
    {
    }

    public ResponseModel(string message = null) : base(false, message)
    {
    }
}