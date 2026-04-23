using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;
using AutoDocumentation.Services;

namespace AutoDocumentation.Models;

/// <summary>
/// Uma linha na grelha de edição: parâmetro comum à selecção.
/// </summary>
public sealed class TeamParameterRowModel : INotifyPropertyChanged
{
    private string _valueText;

    public TeamParameterRowModel(string parameterName, TeamParameterKind kind, StorageType storageType, string? commonValueHint)
    {
        ParameterName = parameterName;
        Kind = kind;
        StorageType = storageType;
        _valueText = commonValueHint ?? string.Empty;
    }

    public string ParameterName { get; }

    public TeamParameterKind Kind { get; }

    public StorageType StorageType { get; }

    public string ValueText
    {
        get => _valueText;
        set
        {
            if (_valueText == value)
                return;
            _valueText = value;
            OnPropertyChanged();
        }
    }

    public string KindDisplay => Kind switch
    {
        TeamParameterKind.YesNo => PluginStrings.T("Kind.YesNo"),
        TeamParameterKind.Text => PluginStrings.T("Kind.Text"),
        TeamParameterKind.Integer => PluginStrings.T("Kind.Integer"),
        TeamParameterKind.DecimalNumber => PluginStrings.T("Kind.Decimal"),
        TeamParameterKind.Length => PluginStrings.T("Kind.Length"),
        _ => Kind.ToString()
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
