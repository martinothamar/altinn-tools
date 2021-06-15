using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using RepoCleanup.Application.Commands;
using RepoCleanup.Models;
using RepoCleanup.Services;
using RepoCleanup.Utils;

namespace RepoCleanup.Application.CommandHandlers
{
    public static class DownloadXsdsForServiceCommandHandler
    {
        public static async Task Handle(DownloadXsdsForServiceCommand command)
        {
            command.Logger.AddNothing();
            command.Logger.AddInformation($"Service: {command.Service.ServiceName}");

            Altinn2ReportingService reportingService = await AltinnServiceRepository.GetReportingService(command.Service);

            string serviceName = $"{command.Service.ServiceCode}-{command.Service.ServiceEditionCode}"
                + $"-{command.Service.ServiceName.AsFileName(false).Trim(' ', '.', ',')}";
            string serviceDirectory = $"{command.LocalPath}\\altinn2\\{serviceName}";

            Directory.CreateDirectory(serviceDirectory);

            foreach (Altinn2Form formMetaData in reportingService.FormsMetaData)
            {
                XDocument xsdDocument = await AltinnServiceRepository.GetFormXsd(command.Service, formMetaData);
                if (xsdDocument == null)
                {
                    command.Logger.AddInformation($"DataFormatId: {formMetaData.DataFormatID}-{formMetaData.DataFormatVersion} NOT FOUND");
                    continue;
                }

                string fileName = $"{serviceDirectory}\\{formMetaData.DataFormatID}-{formMetaData.DataFormatVersion}.xsd";
                using (FileStream fileStream = new FileStream(fileName, FileMode.OpenOrCreate))
                {
                    await xsdDocument.SaveAsync(fileStream, SaveOptions.None, CancellationToken.None);
                }
                command.Logger.AddInformation($"DataFormatId: {formMetaData.DataFormatID}-{formMetaData.DataFormatVersion} Downloaded.");
            }
        }
    }
}
