using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

public static class TeamParameterSharedFileReloader
{
    public static void ReloadDefinitionFile(Autodesk.Revit.ApplicationServices.Application app, Document doc)
    {
        SharedParameterRevitTempTxt.MaterializeFromJson(doc);
        _ = SharedParameterFileLoader.OpenForSessionOrThrow(app, SharedParameterRevitTempTxt.GetCacheTxtPath(doc));
    }
}
