using Autodesk.Revit.DB;
using AutoDocumentation.Models;

namespace AutoDocumentation.Services;

public static class TeamParameterKindMapping
{
    public static ForgeTypeId GetSpecTypeId(TeamParameterKind kind) =>
        kind switch
        {
            TeamParameterKind.YesNo => SpecTypeId.Boolean.YesNo,
            TeamParameterKind.Text => SpecTypeId.String.Text,
            TeamParameterKind.Integer => SpecTypeId.Int.Integer,
            TeamParameterKind.DecimalNumber => SpecTypeId.Number,
            TeamParameterKind.Length => SpecTypeId.Length,
            _ => SpecTypeId.String.Text
        };

    public static TeamParameterKind MapFromSpecTypeId(ForgeTypeId spec)
    {
        if (spec == SpecTypeId.Boolean.YesNo)
            return TeamParameterKind.YesNo;
        if (spec == SpecTypeId.String.Text)
            return TeamParameterKind.Text;
        if (spec == SpecTypeId.Int.Integer)
            return TeamParameterKind.Integer;
        if (spec == SpecTypeId.Number)
            return TeamParameterKind.DecimalNumber;
        if (spec == SpecTypeId.Length)
            return TeamParameterKind.Length;

        return TeamParameterKind.Text;
    }
}
