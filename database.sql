-- Выполнить в SQL Server Management Studio: New Query -> вставить -> F5
IF DB_ID('PracticeDB') IS NULL
    CREATE DATABASE PracticeDB;
GO
USE PracticeDB;
GO
IF OBJECT_ID('dbo.Students') IS NULL
BEGIN
    CREATE TABLE Students (
        Id        INT IDENTITY(1,1) PRIMARY KEY,
        LastName  NVARCHAR(50) NOT NULL,
        FirstName NVARCHAR(50) NOT NULL,
        GroupName NVARCHAR(20) NULL,
        BirthDate DATE NULL
    );
    INSERT INTO Students (LastName, FirstName, GroupName, BirthDate) VALUES
    (N'Иванов',  N'Иван',  N'ИС-21', '2005-03-12'),
    (N'Петрова', N'Анна',  N'ИС-21', '2004-11-02'),
    (N'Сидоров', N'Павел', N'ИС-22', '2005-07-25');
END
GO
SELECT * FROM Students;
