using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RepoCleanup.Models;
using RepoCleanup.Services;
using RepoCleanup.Utils;

namespace RepoCleanup.Functions
{
    public static class MigrateXsdSchemas
    {
        public static async Task Run()
        {
            StringBuilder logBuilder = new StringBuilder();
            string logName = @$"MigrateXsdSchemas-Log.txt";

            Console.Clear();
            Console.WriteLine("");
            Console.WriteLine("----------------------------------------------------------------");
            Console.WriteLine("--- Migrating XSD Schemas from active services in Altinn II  ---");
            Console.WriteLine("----------------------------------------------------------------");

            await Task.Delay(1);

            using (StreamWriter file = new StreamWriter(logName, true))
            {
                file.WriteLine(logBuilder.ToString());
            }

            logBuilder.Clear();

            return;
        }
    }
}
