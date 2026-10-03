using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One text input of a form: its label, the text typed and the error shown under it.
    /// </summary>
    public sealed class FormField : ObservableObject
    {
        private readonly Action _onTextChanged;
        private string _text = string.Empty;
        private string _error;

        public FormField(string label, Action onTextChanged)
        {
            Label = label;
            _onTextChanged = onTextChanged;
        }

        public string Label { get; }

        public string Text
        {
            get => _text;
            set
            {
                if (SetProperty(ref _text, value))
                {
                    _onTextChanged?.Invoke();
                }
            }
        }

        public string Error
        {
            get => _error;
            set => SetProperty(ref _error, value);
        }
    }
}
