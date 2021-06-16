using System;
using System.IO;
using System.Text;

namespace RepoCleanup.Utils
{
    public class NotALogger
    {
        private StringBuilder _logBuilder = new StringBuilder();

        private string _logFile;

        public NotALogger(string logFile)
        {
            _logFile = logFile;
        }

        public void AddNothing()
        {
            Console.WriteLine();
            _logBuilder.AppendLine();
        }

        public void AddInformation(string message)
        {
            message = $"{DateTime.Now} - INFO - {message}";
            
            Console.WriteLine(message);
            _logBuilder.AppendLine(message);
        }

        public void AddError(Exception exception)
        {
            string message = $"{DateTime.Now} - ERRR - {exception.Message}";
            message += $"{DateTime.Now} - ERRR - {exception.StackTrace}";

            Console.WriteLine(message);
            _logBuilder.AppendLine(message);
        }

        public void WriteLog()
        {
            using (StreamWriter file = new StreamWriter(_logFile, true))
            {
                file.WriteLine(_logBuilder.ToString());
            }

            _logBuilder.Clear();
        }
    }
}
