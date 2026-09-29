# Практическое занятие № 9 — WPF + ADO.NET

Приложение WPF (.NET Framework 4.8) для работы с базой SQL Server через ADO.NET:
выборка, поиск, добавление, изменение, удаление записей таблицы `Students`.

## Запуск

1. **База.** Открыть SQL Server Management Studio → «Создать запрос» → вставить `database.sql` → F5.
   Запомнить имя сервера из окна входа SSMS.
2. **Строка подключения.** В Visual Studio открыть `WpfApp1.sln`, затем `WpfApp1/App.config`,
   заменить `.\SQLEXPRESS` в `Data Source=` на своё имя сервера. Ctrl+S.
3. **Запуск.** F5.

## Что где

| Требование | Где в коде |
|---|---|
| Способ подключения | `App.config` → `connectionStrings`, чтение через `ConfigurationManager` |
| Объекты ADO.NET | `SqlConnection`, `SqlCommand`, `SqlDataAdapter`, `DataTable`, `ExecuteNonQuery` |
| Запросы на выборку | `LoadData()` — `SELECT`, поиск через `LIKE` |
| Запросы модификации | `BtnAdd_Click` (`INSERT`), `BtnUpdate_Click` (`UPDATE`), `BtnDelete_Click` (`DELETE`) |
| XAML-разметка | `MainWindow.xaml` — `Grid`, `DataGrid` с `Binding`, `TextBox`, `DatePicker`, `Button` |

Все запросы с параметрами (`@...`) — защита от SQL-инъекций.

## Частые ошибки

- «сервер не найден / network-related» — неверное имя сервера в `App.config`.
- «Cannot open database PracticeDB» — не выполнен `database.sql`.
- Нет .NET Framework 4.8 — Visual Studio Installer → «Изменить» → «Отдельные компоненты» → «Пакет нацеливания .NET Framework 4.8».
