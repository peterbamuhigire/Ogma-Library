using OgmaLibrary.Domain.Ai;

namespace OgmaLibrary.Application.Ai;

/// <summary>Produces answers grounded exclusively in local catalogue evidence.</summary>
public interface IAnswerPipeline
{
    /// <summary>Answers a question using local indexed passages and citations.</summary>
    Task<AnswerResponse> GetAnswerAsync(
        AnswerRequest request,
        CancellationToken cancellationToken);
}
