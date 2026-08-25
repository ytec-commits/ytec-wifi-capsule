using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ytec.WifiCapsule.App;

public sealed class SelectableProfileRow : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _detail;

    public SelectableProfileRow(
        string name,
        string detail,
        bool isSelected = false)
    {
        Name = name;
        _detail = detail;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string Detail
    {
        get => _detail;
        set
        {
            if (string.Equals(
                    _detail,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _detail = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
