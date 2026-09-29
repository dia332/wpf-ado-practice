using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        // строка подключения из App.config
        private readonly string connStr =
            ConfigurationManager.ConnectionStrings["PracticeDB"].ConnectionString;

        private int? selectedId; // Id выбранной строки

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        // ---------- ВЫБОРКА (SqlDataAdapter + DataTable) ----------
        private void LoadData(string search = null)
        {
            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    string sql = "SELECT Id, LastName, FirstName, GroupName, BirthDate FROM Students";
                    var cmd = new SqlCommand(sql, conn);
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        cmd.CommandText += " WHERE LastName LIKE @s";
                        cmd.Parameters.AddWithValue("@s", "%" + search + "%");
                    }
                    cmd.CommandText += " ORDER BY LastName";

                    var adapter = new SqlDataAdapter(cmd);
                    var table = new DataTable();
                    adapter.Fill(table); // соединение откроется и закроется само
                    dgStudents.ItemsSource = table.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message);
            }
        }

        // ---------- МОДИФИКАЦИЯ (SqlCommand + ExecuteNonQuery) ----------
        private int Execute(string sql, bool withId)
        {
            using (var conn = new SqlConnection(connStr))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@LastName", txtLastName.Text.Trim());
                cmd.Parameters.AddWithValue("@FirstName", txtFirstName.Text.Trim());
                cmd.Parameters.AddWithValue("@GroupName",
                    string.IsNullOrWhiteSpace(txtGroup.Text) ? (object)DBNull.Value : txtGroup.Text.Trim());
                cmd.Parameters.AddWithValue("@BirthDate",
                    dpBirth.SelectedDate.HasValue ? (object)dpBirth.SelectedDate.Value : DBNull.Value);
                if (withId) cmd.Parameters.AddWithValue("@Id", selectedId.Value);

                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        private bool ValidateFields()
        {
            if (string.IsNullOrWhiteSpace(txtLastName.Text) || string.IsNullOrWhiteSpace(txtFirstName.Text))
            {
                MessageBox.Show("Заполните фамилию и имя.");
                return false;
            }
            return true;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateFields()) return;
            try
            {
                Execute("INSERT INTO Students (LastName, FirstName, GroupName, BirthDate) " +
                        "VALUES (@LastName, @FirstName, @GroupName, @BirthDate)", false);
                MessageBox.Show("Запись добавлена.");
                ClearFields();
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (selectedId == null) { MessageBox.Show("Выберите запись в таблице."); return; }
            if (!ValidateFields()) return;
            try
            {
                Execute("UPDATE Students SET LastName=@LastName, FirstName=@FirstName, " +
                        "GroupName=@GroupName, BirthDate=@BirthDate WHERE Id=@Id", true);
                MessageBox.Show("Запись изменена.");
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (selectedId == null) { MessageBox.Show("Выберите запись в таблице."); return; }
            if (MessageBox.Show("Удалить выбранную запись?", "Подтверждение",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            try
            {
                using (var conn = new SqlConnection(connStr))
                using (var cmd = new SqlCommand("DELETE FROM Students WHERE Id=@Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", selectedId.Value);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                ClearFields();
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        // выбор строки -> заполняем поля
        private void DgStudents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = dgStudents.SelectedItem as DataRowView;
            if (row == null) return;
            selectedId = (int)row["Id"];
            txtLastName.Text = row["LastName"].ToString();
            txtFirstName.Text = row["FirstName"].ToString();
            txtGroup.Text = row["GroupName"].ToString();
            dpBirth.SelectedDate = row["BirthDate"] == DBNull.Value ? (DateTime?)null : (DateTime)row["BirthDate"];
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            LoadData(txtSearch.Text.Trim());
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
            LoadData();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearFields();
        }

        private void ClearFields()
        {
            selectedId = null;
            txtLastName.Clear();
            txtFirstName.Clear();
            txtGroup.Clear();
            dpBirth.SelectedDate = null;
            dgStudents.SelectedItem = null;
        }
    }
}
