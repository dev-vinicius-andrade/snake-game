using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Library.Commons.Api.Exceptions;

public class ModelStateException : Exception
{
    public const string DefaultMessage = "Error while validating request model";

    public ModelStateException(ModelStateDictionary modelStateDictionary, string aditionalMessage = null, Exception innerException = null) :
        base($"{DefaultMessage}{(string.IsNullOrWhiteSpace(aditionalMessage) ? string.Empty : $" AditionalMessage: {aditionalMessage}")}",
            innerException)
    {
        Errors = modelStateDictionary.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .ToList();
    }

    public IReadOnlyList<string> Errors { get; set; }
}