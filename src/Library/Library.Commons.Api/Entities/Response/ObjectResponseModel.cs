using Library.Commons.Api.Entities.Response.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Library.Commons.Api.Entities.Response;

public class ObjectResponseModel<TObject> : BaseResponseModel
    where TObject : class
{
    public ObjectResponseModel() : base(false)
    {
    }

    public ObjectResponseModel(string message = "", bool hasError = false) : base(hasError, message)
    {
    }

    [MaybeNull]
    [JsonPropertyName("data")]
    public TObject Data { get; init; }

    [JsonPropertyName("status")]
    public int Status { get; set; }
}