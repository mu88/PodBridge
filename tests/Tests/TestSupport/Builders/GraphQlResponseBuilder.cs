using PodBridge.Logic.EpisodeSourcing;

namespace Tests.TestSupport.Builders;

internal sealed class GraphQlResponseBuilder
{
    private ProgramSet? _programSet;
    private List<GraphQlError>? _errors;
    private bool _hasDataWrapper = true;

    public GraphQlResponseBuilder WithDefaults()
    {
        _programSet = new ProgramSetBuilder().WithDefaults().Build();
        _errors = null;
        _hasDataWrapper = true;
        return this;
    }

    public GraphQlResponseBuilder WithProgramSet(ProgramSet programSet)
    {
        _programSet = programSet;
        return this;
    }

    public GraphQlResponseBuilder WithErrors(params string[] errorMessages)
    {
        _errors = errorMessages.Select(msg => new GraphQlError { Message = msg }).ToList();
        return this;
    }

    public GraphQlResponseBuilder WithNoProgramSet()
    {
        _programSet = null;
        _hasDataWrapper = false;
        return this;
    }

    public GraphQlResponseBuilder WithDataWrapperButNoProgramSet()
    {
        _programSet = null;
        _hasDataWrapper = true;
        return this;
    }

    public string BuildJson()
    {
        var graphQlResponse = new GraphQlResponse
        {
            Data = _hasDataWrapper ? new GraphQlData { ProgramSet = _programSet } : null,
            Errors = _errors,
        };

        return System.Text.Json.JsonSerializer.Serialize(graphQlResponse, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
    }
}
