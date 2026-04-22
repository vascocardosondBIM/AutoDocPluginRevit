using System.Text;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

/// <summary>
/// Centraliza a abertura do ficheiro de parâmetros partilhados via API do Revit.
/// O Revit devolve mensagens pouco claras (ex.: «Error in readParamDatabase») quando o ficheiro está inválido.
/// </summary>
public static class SharedParameterFileLoader
{
    public static string FormatOpenFailureMessage(string fullPath, Exception? revitCause = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "O Revit não conseguiu abrir ou ler o ficheiro de parâmetros partilhados. " +
            "Mensagens como «Error in readParamDatabase» indicam normalmente ficheiro corrupto, linhas mal formatadas (tabs) ou incompatibilidade de versão.");
        sb.AppendLine();
        sb.AppendLine($"Ficheiro: {fullPath}");
        if (revitCause is not null)
        {
            sb.AppendLine();
            sb.Append("Mensagem original: ");
            sb.AppendLine(revitCause.Message);
        }

        sb.AppendLine();
        sb.AppendLine("Sugestões:");
        sb.AppendLine("• No Revit: Gerir parâmetros partilhados e tentar carregar este ficheiro — se também falhar, o ficheiro não é válido.");
        sb.AppendLine("• Abrir o .txt num editor (p.ex. Notepad++) e mostrar espaços/tabs: cada campo de PARAM/GROUP deve estar separado por tabulação, sem linhas cortadas a meio.");
        sb.AppendLine("• Fazer cópia de segurança e, se for aceitável perder só as definições deste ficheiro local, renomear ou apagar o ficheiro para o add-in recriar o cabeçalho mínimo na próxima operação.");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Define <see cref="Application.SharedParametersFilename"/>, abre o ficheiro e deixa o caminho activo (fluxo de escrita do add-in).
    /// </summary>
    public static DefinitionFile OpenForSessionOrThrow(Application app, string fullPath)
    {
        app.SharedParametersFilename = fullPath;
        DefinitionFile? defFile;
        try
        {
            defFile = app.OpenSharedParameterFile();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(FormatOpenFailureMessage(fullPath, ex), ex);
        }

        if (defFile is null)
            throw new InvalidOperationException(FormatOpenFailureMessage(fullPath));

        return defFile;
    }

    /// <summary>
    /// Define temporariamente <see cref="Application.SharedParametersFilename"/>, invoca <paramref name="body"/>
    /// com o <see cref="DefinitionFile"/> (ou <c>null</c> se a abertura falhar ou lançar) e repõe o caminho anterior.
    /// O corpo deve consumir o <see cref="DefinitionFile"/> antes de sair — não o guarde para depois do retorno.
    /// </summary>
    public static T? TryExecuteWithTemporarySharedParameterPath<T>(
        Application app,
        string fullPath,
        Func<DefinitionFile?, T?> body)
    {
        var previous = app.SharedParametersFilename;
        try
        {
            app.SharedParametersFilename = fullPath;
            DefinitionFile? defFile;
            try
            {
                defFile = app.OpenSharedParameterFile();
            }
            catch
            {
                defFile = null;
            }

            return body(defFile);
        }
        finally
        {
            app.SharedParametersFilename = previous;
        }
    }

    /// <summary>Variante sem valor de retorno (p.ex. preencher uma colecção).</summary>
    public static void TryExecuteWithTemporarySharedParameterPath(
        Application app,
        string fullPath,
        Action<DefinitionFile?> body)
    {
        TryExecuteWithTemporarySharedParameterPath<object?>(app, fullPath, df =>
        {
            body(df);
            return null;
        });
    }
}
