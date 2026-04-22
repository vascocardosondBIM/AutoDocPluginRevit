using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

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
        TeamParameterKind.YesNo => "Sim / Não",
        TeamParameterKind.Text => "Texto",
        TeamParameterKind.Integer => "Número inteiro",
        TeamParameterKind.DecimalNumber => "Número decimal",
        TeamParameterKind.Length => "Comprimento (m)",
        _ => Kind.ToString()
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
