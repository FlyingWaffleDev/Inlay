using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Inlay.ViewModels;

internal sealed partial class TemplateFlyoutViewModel : ReactiveObject
{
    private readonly Action<string> _selectOption;
    private readonly Action _removeTemplate;
    private int _selectedIndex;

    [Reactive(nameof(CanAddChoice))]
    private string _newChoice = string.Empty;

    public TemplateFlyoutViewModel(
        ObservableCollection<string> options,
        int selectedIndex,
        Action<string> selectOption,
        Action removeTemplate)
    {
        Options = options;
        _selectedIndex = selectedIndex;
        _selectOption = selectOption;
        _removeTemplate = removeTemplate;
        Options.CollectionChanged += OnOptionsChanged;
    }

    public ObservableCollection<string> Options { get; }
    public bool HasOptions => Options.Count > 0;
    public bool CanAddChoice => NewChoice.Trim().Length > 0 && !Options.Contains(NewChoice.Trim());

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedIndex, value);
            var selectedText = value >= 0 && value < Options.Count
                ? Options[value]
                : TemplateTextElement.PlaceholderText;
            _selectOption(selectedText);
        }
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
        if (removedIndex == SelectedIndex)
        {
            SelectedIndex = -1;
        }
        else if (removedIndex < SelectedIndex)
        {
            _selectedIndex--;
            this.RaisePropertyChanged(nameof(SelectedIndex));
            _selectOption(Options[_selectedIndex]);
        }
    }

    [ReactiveCommand]
    private void RemoveTemplate() => _removeTemplate();

    private void OnOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.RaisePropertyChanged(nameof(HasOptions));
        this.RaisePropertyChanged(nameof(CanAddChoice));
    }
}
