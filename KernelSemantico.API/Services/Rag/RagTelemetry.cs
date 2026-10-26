using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KernelSemantico.API.Services.Rag;

public sealed class RagTelemetry : IDisposable
{
    private readonly Meter _meter = new("KernelSemantico.API.Rag");

    public ActivitySource ActivitySource { get; } = new("KernelSemantico.API.Rag");

    public Counter<long> IngestRequests { get; }
    public Counter<long> IngestErrors { get; }
    public Histogram<double> IngestDurationMs { get; }

    public Counter<long> AskRequests { get; }
    public Counter<long> AskErrors { get; }
    public Counter<long> GuardrailBlocked { get; }
    public Histogram<double> AskDurationMs { get; }
    public Histogram<int> RetrievalCandidateCount { get; }

    public Counter<long> EvaluateRequests { get; }
    public Counter<long> EvaluateErrors { get; }
    public Histogram<double> EvaluateDurationMs { get; }

    public RagTelemetry()
    {
        IngestRequests = _meter.CreateCounter<long>("rag_ingest_requests_total");
        IngestErrors = _meter.CreateCounter<long>("rag_ingest_errors_total");
        IngestDurationMs = _meter.CreateHistogram<double>("rag_ingest_duration_ms");

        AskRequests = _meter.CreateCounter<long>("rag_ask_requests_total");
        AskErrors = _meter.CreateCounter<long>("rag_ask_errors_total");
        GuardrailBlocked = _meter.CreateCounter<long>("rag_guardrail_blocked_total");
        AskDurationMs = _meter.CreateHistogram<double>("rag_ask_duration_ms");
        RetrievalCandidateCount = _meter.CreateHistogram<int>("rag_retrieval_candidate_count");

        EvaluateRequests = _meter.CreateCounter<long>("rag_evaluate_requests_total");
        EvaluateErrors = _meter.CreateCounter<long>("rag_evaluate_errors_total");
        EvaluateDurationMs = _meter.CreateHistogram<double>("rag_evaluate_duration_ms");
    }

    public void Dispose()
    {
        ActivitySource.Dispose();
        _meter.Dispose();
    }
}
