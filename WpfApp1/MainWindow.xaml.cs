using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WpfApp1
{
    // Позиция накладной: товар и количество упаковок
    public class InvoiceItem
    {
        public string Name;
        public int Quantity;
    }

    // Накладная из файла
    public class Invoice
    {
        public string Number;
        public DateTime Date = DateTime.Today;
        public string Supplier = "не указан";
        public List<InvoiceItem> Items = new List<InvoiceItem>();

        public int Total
        {
            get { return Items.Sum(i => i.Quantity); }
        }
    }

    public partial class MainWindow : Window
    {
        private readonly string connectionString;
        private List<Invoice> invoices = new List<Invoice>();

        public MainWindow()
        {
            InitializeComponent();
            // строка подключения из App.config
            connectionString = ConfigurationManager.ConnectionStrings["SQLServerConnection"].ConnectionString;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadWarehouses();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadWarehouses();
        }

        // ================= ВЫБОРКА ДАННЫХ =================

        // Загрузка складов: выборка из представления Загрузка_складов (SqlDataReader)
        private void LoadWarehouses()
        {
            try
            {
                using (var cnn = new SqlConnection(connectionString))
                {
                    cnn.Open();
                    var cmd = new SqlCommand(
                        @"SELECT код_склада AS Id, название AS Name, вместимость AS Capacity,
                                 занято AS Used, свободно AS Free
                          FROM Загрузка_складов
                          ORDER BY код_склада", cnn);
                    var table = new DataTable();
                    using (SqlDataReader r = cmd.ExecuteReader())
                        table.Load(r);
                    gridWarehouses.ItemsSource = table.DefaultView;

                    var total = new SqlCommand("SELECT ISNULL(SUM(свободно), 0) FROM Загрузка_складов", cnn);
                    txtFree.Text = "Всего свободно на складах: " + total.ExecuteScalar() + " уп.";
                }
                gridContents.ItemsSource = null;
                boxContents.Header = "Содержимое склада";
            }
            catch (Exception ex)
            {
                ShowError("Не удалось загрузить склады. Проверьте строку подключения в App.config", ex);
            }
        }

        // Содержимое выбранного склада
        private void GridWarehouses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = gridWarehouses.SelectedItem as DataRowView;
            if (row == null) return;
            try
            {
                using (var cnn = new SqlConnection(connectionString))
                {
                    cnn.Open();
                    var cmd = new SqlCommand(
                        @"SELECT т.наименование AS Product, р.количество AS Qty,
                                 р.номер_накладной AS Invoice, н.поставщик AS Supplier
                          FROM Размещение р
                          JOIN Товары т ON т.код_товара = р.код_товара
                          JOIN Накладные н ON н.номер_накладной = р.номер_накладной
                          WHERE р.код_склада = @id
                          ORDER BY т.наименование", cnn);
                    cmd.Parameters.AddWithValue("@id", (int)row["Id"]);
                    var table = new DataTable();
                    using (SqlDataReader r = cmd.ExecuteReader())
                        table.Load(r);
                    gridContents.ItemsSource = table.DefaultView;
                }
                boxContents.Header = "Содержимое: " + row["Name"];
            }
            catch (Exception ex)
            {
                ShowError("Не удалось загрузить содержимое склада", ex);
            }
        }

        // ================= ЧТЕНИЕ ФАЙЛА НАКЛАДНЫХ =================

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите файл накладных",
                Filter = "Накладные (*.txt)|*.txt|Все файлы (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                invoices = ParseInvoices(ReadLines(dlg.FileName));
                listItems.Items.Clear();
                foreach (var inv in invoices)
                {
                    listItems.Items.Add(string.Format("Накладная {0} от {1:dd.MM.yyyy}, {2}",
                        inv.Number, inv.Date, inv.Supplier));
                    foreach (var item in inv.Items)
                        listItems.Items.Add(string.Format("     {0} — {1} уп.", item.Name, item.Quantity));
                }
                txtFile.Text = string.Format("{0}: накладных {1}, всего {2} уп.",
                    Path.GetFileName(dlg.FileName), invoices.Count, invoices.Sum(i => i.Total));
                btnAccept.IsEnabled = invoices.Count > 0;
            }
            catch (Exception ex)
            {
                invoices.Clear();
                listItems.Items.Clear();
                btnAccept.IsEnabled = false;
                txtFile.Text = "Файл не выбран";
                ShowError("Ошибка в файле накладных", ex);
            }
        }

        // Файл может быть в UTF-8 или в Windows-1251 (старый Блокнот)
        private static string[] ReadLines(string path)
        {
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Any(l => l.IndexOf('\uFFFD') >= 0))
                lines = File.ReadAllLines(path, Encoding.GetEncoding(1251));
            return lines;
        }

        // Формат файла (в одном файле может быть несколько накладных):
        //   Номер: Н-101          <- начало новой накладной
        //   Дата: 29.09.2026
        //   Поставщик: ООО «Ромашка»
        //   Молоко 3,2% 1 л; 15    <- товар; количество упаковок
        // Строки, начинающиеся с #, и пустые строки пропускаются.
        private static List<Invoice> ParseInvoices(string[] lines)
        {
            var list = new List<Invoice>();
            Invoice current = null;

            for (int n = 1; n <= lines.Length; n++)
            {
                string line = lines[n - 1].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int semi = line.IndexOf(';');
                if (semi >= 0)
                {
                    if (current == null)
                        throw new FormatException("Строка " + n + ": товар указан раньше, чем «Номер: ...» накладной.");
                    string name = line.Substring(0, semi).Trim();
                    int qty;
                    if (name.Length == 0 || name.Length > 200 ||
                        !int.TryParse(line.Substring(semi + 1).Trim(), out qty) || qty <= 0)
                        throw new FormatException("Строка " + n + ": ожидается «наименование; количество», количество - целое больше нуля.");

                    // одинаковый товар в одной накладной складываем в одну позицию
                    var same = current.Items.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (same != null) same.Quantity += qty;
                    else current.Items.Add(new InvoiceItem { Name = name, Quantity = qty });
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon < 0)
                    throw new FormatException("Строка " + n + ": непонятная строка «" + line + "».");
                string key = line.Substring(0, colon).Trim().ToLower();
                string value = line.Substring(colon + 1).Trim();

                if (key == "номер")
                {
                    if (value.Length == 0 || value.Length > 30)
                        throw new FormatException("Строка " + n + ": номер накладной пустой или длиннее 30 символов.");
                    if (list.Any(i => i.Number == value))
                        throw new FormatException("Строка " + n + ": накладная " + value + " встречается в файле дважды.");
                    current = new Invoice { Number = value };
                    list.Add(current);
                }
                else if (current == null)
                {
                    throw new FormatException("Строка " + n + ": сначала укажите «Номер: ...» накладной.");
                }
                else if (key == "дата")
                {
                    DateTime date;
                    if (!DateTime.TryParse(value, new CultureInfo("ru-RU"), DateTimeStyles.None, out date))
                        throw new FormatException("Строка " + n + ": неверная дата «" + value + "».");
                    current.Date = date;
                }
                else if (key == "поставщик" && value.Length > 0)
                {
                    current.Supplier = value;
                }
                // остальные поля (например, «Товары:») не нужны программе и пропускаются
            }

            if (list.Count == 0)
                throw new FormatException("В файле нет ни одной накладной (строки «Номер: ...»).");
            foreach (var inv in list)
                if (inv.Items.Count == 0)
                    throw new FormatException("В накладной " + inv.Number + " нет товаров.");
            return list;
        }

        // ================= ПРИЁМ ТОВАРА (МОДИФИКАЦИЯ ДАННЫХ) =================

        private void BtnAccept_Click(object sender, RoutedEventArgs e)
        {
            var report = new StringBuilder();
            bool warning = false;

            foreach (var inv in invoices)
            {
                string result;
                try
                {
                    result = AcceptInvoice(inv);
                }
                catch (Exception ex)
                {
                    result = "Накладная " + inv.Number + " НЕ принята из-за ошибки: " + ex.Message;
                }
                if (result.Contains("ВНИМАНИЕ") || result.Contains("НЕ принята") || result.Contains("пропущена"))
                    warning = true;
                report.AppendLine(result);
            }

            txtLog.AppendText(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + Environment.NewLine +
                              report + Environment.NewLine);
            txtLog.ScrollToEnd();
            MessageBox.Show(report.ToString(), "Итоги приёма товара", MessageBoxButton.OK,
                warning ? MessageBoxImage.Warning : MessageBoxImage.Information);

            invoices.Clear();
            listItems.Items.Clear();
            txtFile.Text = "Файл не выбран";
            btnAccept.IsEnabled = false;
            LoadWarehouses();
        }

        // Приём одной накладной. Всё в одной транзакции: либо принята целиком, либо ничего не изменилось.
        private string AcceptInvoice(Invoice inv)
        {
            using (var cnn = new SqlConnection(connectionString))
            {
                cnn.Open();

                var check = new SqlCommand("SELECT COUNT(*) FROM Накладные WHERE номер_накладной = @n", cnn);
                check.Parameters.AddWithValue("@n", inv.Number);
                if ((int)check.ExecuteScalar() > 0)
                    return "Накладная " + inv.Number + " уже была принята раньше - пропущена.";

                var freeCmd = new SqlCommand("SELECT ISNULL(SUM(свободно), 0) FROM Загрузка_складов", cnn);
                int free = (int)freeCmd.ExecuteScalar();
                if (free < inv.Total)
                    return string.Format("Накладная {0} НЕ принята: в ней {1} уп., а на всех складах свободно только {2} уп.",
                        inv.Number, inv.Total, free);

                SqlTransaction tr = cnn.BeginTransaction();
                try
                {
                    var head = new SqlCommand(
                        "INSERT INTO Накладные (номер_накладной, дата, поставщик) VALUES (@n, @d, @s)", cnn, tr);
                    head.Parameters.AddWithValue("@n", inv.Number);
                    head.Parameters.AddWithValue("@d", inv.Date);
                    head.Parameters.AddWithValue("@s", inv.Supplier);
                    head.ExecuteNonQuery();

                    var sb = new StringBuilder();
                    sb.AppendLine("Накладная " + inv.Number + " принята:");

                    foreach (var item in inv.Items)
                    {
                        int productId = GetProductId(cnn, tr, item.Name);
                        int remaining = item.Quantity;
                        var parts = new List<string>();

                        // кладём на первый склад со свободным местом, остаток - на следующий
                        while (remaining > 0)
                        {
                            int whId, whFree;
                            string whName;
                            var first = new SqlCommand(
                                @"SELECT TOP 1 код_склада, название, свободно
                                  FROM Загрузка_складов
                                  WHERE свободно > 0
                                  ORDER BY код_склада", cnn, tr);
                            using (SqlDataReader r = first.ExecuteReader())
                            {
                                if (!r.Read())
                                    throw new InvalidOperationException("на складах закончилось место");
                                whId = r.GetInt32(0);
                                whName = r.GetString(1);
                                whFree = r.GetInt32(2);
                            }

                            int put = Math.Min(remaining, whFree);
                            var place = new SqlCommand(
                                @"INSERT INTO Размещение (код_склада, код_товара, номер_накладной, количество)
                                  VALUES (@w, @p, @n, @q)", cnn, tr);
                            place.Parameters.AddWithValue("@w", whId);
                            place.Parameters.AddWithValue("@p", productId);
                            place.Parameters.AddWithValue("@n", inv.Number);
                            place.Parameters.AddWithValue("@q", put);
                            place.ExecuteNonQuery();

                            remaining -= put;
                            parts.Add(whName + " - " + put + " уп.");
                        }

                        if (parts.Count == 1)
                            sb.AppendLine("   " + item.Name + ": " + parts[0]);
                        else
                            sb.AppendLine(string.Format("   ВНИМАНИЕ! {0} ({1} уп.) не поместился на один склад и разделён: {2}",
                                item.Name, item.Quantity, string.Join("; ", parts)));
                    }

                    tr.Commit();
                    return sb.ToString();
                }
                catch
                {
                    // транзакцию мог уже откатить триггер вместимости
                    try { tr.Rollback(); } catch (InvalidOperationException) { }
                    throw;
                }
            }
        }

        // Код товара по наименованию; нового товара ещё нет в справочнике - добавляем
        private static int GetProductId(SqlConnection cnn, SqlTransaction tr, string name)
        {
            var cmd = new SqlCommand(
                @"IF NOT EXISTS (SELECT 1 FROM Товары WHERE наименование = @name)
                      INSERT INTO Товары (наименование) VALUES (@name);
                  SELECT код_товара FROM Товары WHERE наименование = @name;", cnn, tr);
            cmd.Parameters.AddWithValue("@name", name);
            return (int)cmd.ExecuteScalar();
        }

        private static void ShowError(string text, Exception ex)
        {
            MessageBox.Show(text + ":\n\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
