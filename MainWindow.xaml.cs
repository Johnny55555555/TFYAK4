using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace ТФЯК__1
{
    public class SearchResult
    {
        public string Fragment { get; set; }
        public string Position { get; set; }
        public int Length { get; set; }
    }

    public partial class MainWindow : Window
    {
        private string currentFilePath = "";
        private bool isTextChanged = false;

        public MainWindow()
        {
            InitializeComponent();

            Editor.TextChanged += Editor_TextChanged;
            ResultGrid.MouseDoubleClick += ResultGrid_MouseDoubleClick;
            this.Closing += MainWindow_Closing;
        }

        #region Редактирование текста

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            isTextChanged = true;
        }

        private bool CheckSaveChanges()
        {
            if (!isTextChanged && string.IsNullOrWhiteSpace(Editor.Text))
                return true;

            var result = MessageBox.Show(
                "Файл был изменён.\nСохранить изменения?",
                "Подтверждение",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Save_Click(null, null);
                return true;
            }

            if (result == MessageBoxResult.No)
                return true;

            return false;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!CheckSaveChanges())
                e.Cancel = true;
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckSaveChanges()) return;

            Editor.Clear();
            ResultGrid.ItemsSource = null;
            currentFilePath = "";
            isTextChanged = false;
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckSaveChanges()) return;

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                currentFilePath = dialog.FileName;
                Editor.Text = File.ReadAllText(currentFilePath);
                ResultGrid.ItemsSource = null;
                isTextChanged = false;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveAs_Click(sender, e);
                return;
            }

            File.WriteAllText(currentFilePath, Editor.Text);
            isTextChanged = false;
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                currentFilePath = dialog.FileName;
                File.WriteAllText(currentFilePath, Editor.Text);
                isTextChanged = false;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckSaveChanges()) return;

            Close();
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => Editor.Undo();
        private void Redo_Click(object sender, RoutedEventArgs e) => Editor.Redo();
        private void Cut_Click(object sender, RoutedEventArgs e) => Editor.Cut();
        private void Copy_Click(object sender, RoutedEventArgs e) => Editor.Copy();
        private void Paste_Click(object sender, RoutedEventArgs e) => Editor.Paste();
        private void Delete_Click(object sender, RoutedEventArgs e) => Editor.SelectedText = "";
        private void SelectAll_Click(object sender, RoutedEventArgs e) => Editor.SelectAll();

        #endregion

        #region Поиск подстрок

        private List<SearchResult> FindMatches(string text, string pattern)
        {
            var results = new List<SearchResult>();
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var lines = text.Split('\n');

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (Match match in regex.Matches(lines[lineIndex]))
                {
                    results.Add(new SearchResult
                    {
                        Fragment = match.Value,
                        Position = $"Строка {lineIndex + 1}, Символ {match.Index + 1}",
                        Length = match.Length
                    });
                }
            }

            return results;
        }

        private List<SearchResult> FindTime12h(string text)
        {
            var results = new List<SearchResult>();
            int lineNumber = 1;
            int columnNumber = 1;

            for (int i = 0; i < text.Length; i++)
            {
                int state = 0;
                int startIndex = i;
                int startLine = lineNumber;
                int startColumn = columnNumber;

                int j = i;
                while (j < text.Length)
                {
                    char c = text[j];

                    switch (state)
                    {
                        case 0: // первый символ часа
                            if (c == '0' || c == '1') state = 1;
                            else if (c == '1' && j + 1 < text.Length && text[j + 1] == '2') state = 1;
                            else goto NextChar;
                            break;

                        case 1: // второй символ часа
                            if (char.IsDigit(c))
                            {
                                string hourStr = text.Substring(i, 2);
                                int hour = int.Parse(hourStr);
                                if ((hour >= 1 && hour <= 11) || hour == 12) state = 2;
                                else goto NextChar;
                            }
                            else goto NextChar;
                            break;

                        case 2: // двоеточие
                            if (c == ':') state = 3;
                            else goto NextChar;
                            break;

                        case 3: // первая цифра минут
                            if (c >= '0' && c <= '5') state = 4;
                            else goto NextChar;
                            break;

                        case 4: // вторая цифра минут
                            if (c >= '0' && c <= '9')
                            {
                                string minuteStr = text.Substring(j - 1, 2);
                                int minute = int.Parse(minuteStr);
                                if (minute >= 0 && minute <= 59) state = 5;
                                else goto NextChar;
                            }
                            else goto NextChar;
                            break;

                        case 5: // пробел перед AM/PM
                            if (c == ' ') state = 6;
                            else goto NextChar;
                            break;

                        case 6: // A/P
                            if (c == 'A' || c == 'a' || c == 'P' || c == 'p') state = 7;
                            else goto NextChar;
                            break;

                        case 7: // M
                            if (c == 'M' || c == 'm')
                            {
                                // Проверка особого случая 12:00
                                string timeStr = text.Substring(startIndex, j - startIndex + 1);
                                string[] parts = timeStr.Split(new char[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                int hour = int.Parse(parts[0]);
                                int minute = int.Parse(parts[1]);

                                if (!(hour == 12 && minute > 0) && !(hour == 0))
                                {
                                    results.Add(new SearchResult
                                    {
                                        Fragment = timeStr,
                                        Position = $"Строка {startLine}, Символ {startColumn}",
                                        Length = j - startIndex + 1
                                    });
                                }
                                goto NextChar;
                            }
                            else goto NextChar;
                    }

                    // Обновление счетчиков строки и столбца
                    if (text[j] == '\n')
                    {
                        lineNumber++;
                        columnNumber = 1;
                    }
                    else columnNumber++;

                    j++;
                }

            NextChar:
                if (text[i] == '\n')
                {
                    lineNumber++;
                    columnNumber = 1;
                }
                else columnNumber++;
            }

            return results;
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Editor.Text))
            {
                MessageBox.Show("Нет данных для поиска", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string selected = (RegexSelector.SelectedItem as ComboBoxItem)?.Content.ToString();
            List<SearchResult> results;

            switch (selected)
            {
                case "Идентификатор":
                    results = FindMatches(Editor.Text, @"(?<!\S)[a-zA-Z$_][a-zA-Z]*(?=\s|$|[.,;:!?])");
                    break;

                case "Восьмеричное число":
                    results = FindMatches(Editor.Text, @"(?<!\S)(0o[0-7]+|&O[0-7]+|0(?![0-7])|0[0-7]+)(?=\s|$|[.,;:!?])");
                    break;

                case "Время (12h)":
                    results = FindTime12h(Editor.Text);
                    break;

                default:
                    MessageBox.Show("Выберите тип поиска", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
            }

            ResultGrid.ItemsSource = results;
            ErrorCount.Text = $"Совпадений: {results.Count}";

            if (results.Count == 0)
                MessageBox.Show("Совпадений не найдено", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            Search_Click(sender, e);
        }

        #endregion

        #region Подсветка совпадений

        private void ResultGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ResultGrid.SelectedItem is SearchResult result)
            {
                var parts = result.Position.Replace("Строка ", "").Replace("Символ ", "").Split(',');
                int line = int.Parse(parts[0]);
                int column = int.Parse(parts[1]);

                int index = GetIndexFromLineColumn(line, column);
                if (index >= 0)
                {
                    Editor.Focus();
                    Editor.Select(index, result.Length);
                }
            }
        }

        private int GetIndexFromLineColumn(int line, int column)
        {
            int currentLine = 1;
            int currentColumn = 1;

            for (int i = 0; i < Editor.Text.Length; i++)
            {
                if (currentLine == line && currentColumn == column)
                    return i;

                if (Editor.Text[i] == '\n')
                {
                    currentLine++;
                    currentColumn = 1;
                }
                else
                {
                    currentColumn++;
                }
            }

            return -1;
        }

        #endregion

        #region Справка и информация о программе

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Программа выполняет поиск идентификаторов, восьмеричных чисел и времени.",
                "Справка",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Компилятор (ЛР4)\n\n" +
                "Синтаксический анализатор и поиск с использованием регулярных выражений\n\n" +
                "Студент: Пузырный Д.А.",
                "О программе",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #endregion
    }
}