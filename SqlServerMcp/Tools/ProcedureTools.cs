using System.ComponentModel;
using ModelContextProtocol.Server;
using SqlServerMcp.Services;

namespace SqlServerMcp.Tools;

[McpServerToolType]
public class ProcedureTools
{
    private readonly ISqlService _sqlService;
    private readonly ResultFormatter _formatter;

    public ProcedureTools(ISqlService sqlService, ResultFormatter formatter)
    {
        _sqlService = sqlService;
        _formatter = formatter;
    }

    [McpServerTool]
    [Description("Get the full T-SQL definition of a stored procedure.")]
    public async Task<string> GetProcedureDefinition(
        [Description("Procedure name (can include schema)")] string procedureName)
    {
        try
        {
            var definition = await _sqlService.GetProcedureDefinitionAsync(procedureName);
            return definition;
        }
        catch (Exception ex)
        {
            return $"Error getting procedure definition: {ex.Message}";
        }
    }

}
