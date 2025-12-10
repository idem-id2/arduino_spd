using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace HexEditor
{
    public partial class InputDialog : Window
    {
        public static readonly DependencyProperty PromptProperty =
            DependencyProperty.Register(nameof(Prompt), typeof(string), typeof(InputDialog), new PropertyMetadata(""));

        public static readonly DependencyProperty DefaultValueProperty =
            DependencyProperty.Register(nameof(DefaultValue), typeof(string), typeof(InputDialog), new PropertyMetadata(""));

        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.Register(nameof(MaxLength), typeof(int), typeof(InputDialog), new PropertyMetadata(255));

        public static readonly DependencyProperty AllowOnlyAsciiProperty =
            DependencyProperty.Register(nameof(AllowOnlyAscii), typeof(bool), typeof(InputDialog), new PropertyMetadata(false));

        public string Prompt
        {
            get => (string)GetValue(PromptProperty);
            set => SetValue(PromptProperty, value);
        }

        public string DefaultValue
        {
            get => (string)GetValue(DefaultValueProperty);
            set => SetValue(DefaultValueProperty, value);
        }

        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public bool AllowOnlyAscii
        {
            get => (bool)GetValue(AllowOnlyAsciiProperty);
            set => SetValue(AllowOnlyAsciiProperty, value);
        }

        public string ResponseText { get; private set; } = string.Empty;

        public InputDialog()
        {
            InitializeComponent();
            Loaded += InputDialog_Loaded;
            InputTextBox.PreviewTextInput += InputTextBox_PreviewTextInput;
            InputTextBox.TextChanged += InputTextBox_TextChanged;
        }

        public new string Title
        {
            get => base.Title;
            set => base.Title = value;
        }

        private void InputDialog_Loaded(object sender, RoutedEventArgs e)
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void InputTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (AllowOnlyAscii)
            {
                // Проверяем, что все символы в тексте являются ASCII (0-127)
                foreach (char c in e.Text)
                {
                    if (c > 127)
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        private void InputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (AllowOnlyAscii && InputTextBox != null)
            {
                // Удаляем все не-ASCII символы, если они были вставлены (например, через Ctrl+V)
                string text = InputTextBox.Text;
                string asciiOnly = new string(text.Where(c => c <= 127).ToArray());
                
                if (text != asciiOnly)
                {
                    int selectionStart = InputTextBox.SelectionStart;
                    int selectionLength = InputTextBox.SelectionLength;
                    InputTextBox.Text = asciiOnly;
                    InputTextBox.SelectionStart = Math.Min(selectionStart, asciiOnly.Length);
                    InputTextBox.SelectionLength = selectionLength;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }
    }
}

