using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

public static class TeamParameterSharedFileKindMapper
{
    public static bool TryParseDataTypeToken(string token, out TeamParameterKind kind)
    {
        kind = TeamParameterKind.Text;
        switch (token.Trim().ToUpperInvariant())
        {
            case "TEXT":
                kind = TeamParameterKind.Text;
                return true;
            case "INTEGER":
                kind = TeamParameterKind.Integer;
                return true;
            case "NUMBER":
                kind = TeamParameterKind.DecimalNumber;
                return true;
            case "LENGTH":
                kind = TeamParameterKind.Length;
                return true;
            case "YESNO":
            case "BOOLEAN":
                kind = TeamParameterKind.YesNo;
                return true;
            default:
                return false;
        }
    }

    public static bool TokenMatchesKind(string token, TeamParameterKind expected) =>
        TryParseDataTypeToken(token, out var k) && k == expected;
}
