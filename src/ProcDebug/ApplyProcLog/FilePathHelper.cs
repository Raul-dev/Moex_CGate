using System.IO;

namespace ApplyProcLog;

/// <summary>
/// Статический helper для работы с путями к папкам проекта.
/// Единая точка определения всех путей к папкам.
/// </summary>
public static class FilePathHelper
{
    /// <summary>
    /// Возвращает абсолютный путь к папке PROC.
    /// Если передан абсолютный путь — возвращает как есть,
    /// если относительный — добавляет текущую директорию.
    /// </summary>
    /// <param name="targetDirectory">Имя или путь к папке (по умолчанию: PROC)</param>
    /// <returns>Абсолютный путь к папке</returns>
    public static string GetProcDirectory(string targetDirectory = "PROC")
    {
        if (Path.IsPathRooted(targetDirectory))
            return targetDirectory;

        return Path.Combine(Directory.GetCurrentDirectory(), targetDirectory);
    }

    /// <summary>
    /// Возвращает абсолютный путь к подпапке внутри PROC.
    /// </summary>
    /// <param name="subFolder">Имя подпапки (Table, Base, Original)</param>
    /// <param name="procFolder">Путь к папке PROC (по умолчанию: GetProcDirectory())</param>
    /// <returns>Абсолютный путь к подпапке</returns>
    public static string GetProcSubFolder(string subFolder, string? procFolder = null)
    {
        var baseFolder = procFolder ?? GetProcDirectory();
        return Path.Combine(baseFolder, subFolder);
    }

    /// <summary>
    /// Возвращает абсолютный путь к папке для экспорта данных.
    /// </summary>
    /// <param name="outputDirectory">Имя или путь к папке (по умолчанию: DataExport)</param>
    /// <returns>Абсолютный путь к папке экспорта</returns>
    public static string GetDataExportDirectory(string outputDirectory = "DataExport")
    {
        if (Path.IsPathRooted(outputDirectory))
            return outputDirectory;

        return Path.Combine(Directory.GetCurrentDirectory(), outputDirectory);
    }
}
