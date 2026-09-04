using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Inlay.ViewModels;

internal sealed partial class TemplateFlyoutViewModel : ReactiveObject
{
    private readonly Action<string> _selectOption;
    private readonly Action _removeTemplate;
    private string? _selectedChoice;
    private bool _isDisconnected;

    [Reactive(nameof(CanAddChoice))]
    private string _newChoice = string.Empty;

    public TemplateFlyoutViewModel(
        ObservableCollection<string> options,
        int selectedIndex,
        Action<string> selectOption,
        Action removeTemplate)
    {
        Options = options;
        _selectedChoice = selectedIndex >= 0 && selectedIndex < options.Count
            ? options[selectedIndex]
            : null;
        _selectOption = selectOption;
        _removeTemplate = removeTemplate;
        Options.CollectionChanged += OnOptionsChanged;
    }

    public ObservableCollection<string> Options { get; }
    public bool HasOptions => Options.Count > 0;
    public bool CanAddChoice => NewChoice.Trim().Length > 0 && !Options.Contains(NewChoice.Trim());

    public string? SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            if (_selectedChoice == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedChoice, value);
            this.RaisePropertyChanged(nameof(SelectedIndex));
            _selectOption(value ?? TemplateTextElement.PlaceholderText);
        }
    }

    public int SelectedIndex
    {
        get => _selectedChoice is null ? -1 : Options.IndexOf(_selectedChoice);
        set => SelectedChoice = value >= 0 && value < Options.Count ? Options[value] : null;
    }

    internal void SynchronizeSelectedIndex(int selectedIndex)
    {
        var selectedChoice = selectedIndex >= 0 && selectedIndex < Options.Count
            ? Options[selectedIndex]
            : null;
        if (_selectedChoice == selectedChoice)
        {
            this.RaisePropertyChanged(nameof(SelectedIndex));
            return;
        }

        this.RaiseAndSetIfChanged(ref _selectedChoice, selectedChoice, nameof(SelectedChoice));
        this.RaisePropertyChanged(nameof(SelectedIndex));
    }

    internal void SynchronizeOptions()
    {
        this.RaisePropertyChanged(nameof(HasOptions));
        this.RaisePropertyChanged(nameof(CanAddChoice));
    }

    internal void Disconnect()
    {
        if (_isDisconnected)
        {
            return;
        }

        Options.CollectionChanged -= OnOptionsChanged;
        _isDisconnected = true;
    }

    [ReactiveCommand]
    public void AddChoice()
    {
        var choice = NewChoice.Trim();
        if (choice.Length == 0 || Options.Contains(choice))
        {
            return;
        }

        Options.Add(choice);
        NewChoice = string.Empty;
    }

    [ReactiveCommand]
    public void RemoveChoice(string choice)
    {
        var removedIndex = Options.IndexOf(choice);
        if (removedIndex < 0)
        {
            return;
        }

        Options.RemoveAt(removedIndex);
    }

    [ReactiveCommand]
    private void RemoveTemplate() => _removeTemplate();

    private void OnOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_selectedChoice is not null && !Options.Contains(_selectedChoice))
        {
            SelectedChoice = null;
        }
        else if (_selectedChoice is not null)
        {
            this.RaisePropertyChanged(nameof(SelectedIndex));
            _selectOption(_selectedChoice);
        }

        SynchronizeOptions();
    }
}
