using System.Globalization;
using Autodesk.Revit.DB;

namespace AutoDocumentation.Services;

/// <summary>
/// Escreve valores de parâmetro de instância a partir de texto introduzido pelo utilizador.
/// </summary>
public static class TeamParameterValueWriter
{
    public static bool TrySetFromDisplayText(Element element, string parameterName, string rawText, out string error)
    {
        error = string.Empty;
        var p = element.LookupParameter(parameterName);
        if (p is null || p.IsReadOnly)
        {
            error = "Parâmetro não encontrado ou só de leitura.";
            return false;
        }

        if (p.Element is ElementType)
        {
            error = "Parâmetro de tipo não suportado neste editor.";
            return false;
        }

        if (p.Element.Id != element.Id)
        {
            error = "Parâmetro não pertence a esta instância.";
            return false;
        }

        var text = rawText?.Trim() ?? string.Empty;

        try
        {
            switch (p.StorageType)
            {
                case StorageType.String:
                    p.Set(text);
                    return true;
                case StorageType.Integer:
                {
                    if (p.Definition?.GetDataType() == SpecTypeId.Boolean.YesNo)
                    {
                        // Célula vazia na grelha = manter sem valor (não confundir com «Não»).
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            try
                            {
                                p.ClearValue();
                            }
                            catch
                            {
                                // Revit pode recusar ClearValue; não forçar Set(0) aqui.
                            }

                            return true;
                        }

                        if (IsTruthy(text))
                        {
                            p.Set(1);
                            return true;
                        }

                        if (IsFalsy(text))
                        {
                            p.Set(0);
                            return true;
                        }

                        error = "Para Sim/Não use Sim, Não, S, N, 1 ou 0.";
                        return false;
                    }

                    if (text.Length == 0)
                    {
                        TryClearOrZeroNumeric(p);
                        return true;
                    }

                    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var iv)
                        && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv))
                    {
                        error = "Valor inteiro inválido.";
                        return false;
                    }

                    p.Set(iv);
                    return true;
                }
                case StorageType.Double:
                {
                    if (text.Length == 0)
                    {
                        TryClearOrZeroNumeric(p);
                        return true;
                    }

                    if (!TryParseDouble(text, out var dv))
                    {
                        error = "Número inválido.";
                        return false;
                    }

                    if (p.Definition?.GetDataType() == SpecTypeId.Length)
                        p.Set(UnitUtils.ConvertToInternalUnits(dv, UnitTypeId.Meters));
                    else
                        p.Set(dv);

                    return true;
                }
                default:
                    error = $"Tipo de armazenamento não suportado: {p.StorageType}.";
                    return false;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool IsTruthy(string t)
    {
        t = t.Trim();
        return t.Equals("1", StringComparison.Ordinal)
               || t.Equals("sim", StringComparison.OrdinalIgnoreCase)
               || t.Equals("s", StringComparison.OrdinalIgnoreCase)
               || t.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || t.Equals("true", StringComparison.OrdinalIgnoreCase)
               || t.Equals("verdadeiro", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFalsy(string t)
    {
        t = t.Trim();
        return t.Equals("0", StringComparison.Ordinal)
               || t.Equals("não", StringComparison.OrdinalIgnoreCase)
               || t.Equals("nao", StringComparison.OrdinalIgnoreCase)
               || t.Equals("n", StringComparison.OrdinalIgnoreCase)
               || t.Equals("no", StringComparison.OrdinalIgnoreCase)
               || t.Equals("false", StringComparison.OrdinalIgnoreCase)
               || t.Equals("falso", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseDouble(string text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return true;

        return double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// O Revit só permite <see cref="Parameter.ClearValue"/> em certas definições (p.ex. com HideWhenNoValue no ficheiro partilhado);
    /// noutros casos lança — aí gravamos zero como valor “vazio” aceite pela API.
    /// </summary>
    private static void TryClearOrZeroNumeric(Parameter p)
    {
        try
        {
            p.ClearValue();
            return;
        }
        catch (Exception)
        {
            // Revit (ex.: HideWhenNoValue == false) recusa ClearValue; outras falhas aqui são improváveis.
        }

        if (p.StorageType == StorageType.Integer)
            p.Set(0);
        else if (p.StorageType == StorageType.Double)
            p.Set(0d);
    }
}
