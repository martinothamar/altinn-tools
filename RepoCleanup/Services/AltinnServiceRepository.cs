using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;

using RepoCleanup.Models;

namespace RepoCleanup.Services
{
    /// <summary>
    ///  Rest client that asks altinn for the xsds in production
    /// </summary>
    public class AltinnServiceRepository
    {
        private static HttpClient client = new HttpClient();
        private static JsonSerializerOptions serializerOptions =
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>
        ///  Gets all resources in altinn metadata
        /// </summary>
        /// <returns>list of the resources</returns>
        public static async Task<List<Altinn2Service>> GetServicesAsync()
        {
            List<Altinn2Service> resources = null;

            string path = "https://www.altinn.no/api/metadata";

            HttpResponseMessage response = await client.GetAsync(path);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                
                resources = JsonSerializer.Deserialize<List<Altinn2Service>>(content, serializerOptions);
            }

            return resources;
        }

        /// <summary>
        ///  Get forms metadata from altinn
        /// </summary>
        /// <param name="altinn2Service">The resource</param>
        /// <returns>The form resource</returns>
        public static async Task<FormTaskService> GetFormTaskService(Altinn2Service altinn2Service)
        {
            string path = FormTaskUrl(altinn2Service);
            FormTaskService result = null;

            HttpResponseMessage response = await client.GetAsync(path);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                result = JsonSerializer.Deserialize<FormTaskService>(content, serializerOptions);
            }

            return result;
        }

        private static string FormTaskUrl(Altinn2Service altinnResource)
        {
            return "https://www.altinn.no/api/metadata/formtask/" + altinnResource.ServiceCode + "/" + altinnResource.ServiceEditionCode;
        }

        private static string XsdUrl(Altinn2Service altinnResource, AltinnFormMetaData formMetaData)
        {
            return FormTaskUrl(altinnResource) + "/forms/" + formMetaData.DataFormatID + "/" + formMetaData.DataFormatVersion + "/xsd";
        }

        /// <summary>
        ///  Reads all altinn services resources and returns a list of these
        /// </summary>
        /// <returns>the list</returns>
        public static async Task<List<Altinn2Service>> ReadAllSchemas()
        {
            List<Altinn2Service> sercvices = await GetServicesAsync();

            List<Altinn2Service> result = new List<Altinn2Service>();
            Dictionary<string, string> orgShortnameToOrgnumberMap = BuildOrganizationNumberMap();
            Dictionary<string, string> serviceCodeToServiceEditionCodeDictionary = new Dictionary<string, string>();

            string[] excludeServiceOwnerCodes = { "ACN", "ASF", "TTD" };

            foreach (Altinn2Service service in sercvices)
            {
                if (excludeServiceOwnerCodes.Contains(service.ServiceOwnerCode))
                {
                    continue;
                }

                List<AltinnFormMetaData> forms = new List<AltinnFormMetaData>();

                FormTaskService r = await GetFormTaskService(service);
                if (r != null && r.FormsMetaData != null && r.FormsMetaData.ToArray() != null)
                {
                    foreach (AltinnFormMetaData form in r.FormsMetaData)
                    {
                        form.XsdSchemaUrl = XsdUrl(service, form);

                        //form.JsonSchema = Zip(DownloadAndConvertXsdToJsonSchema(form.XsdSchemaUrl));
                        forms.Add(form);
                    }
                }

                if (forms.Count > 0)
                {
                    string orgnr = orgShortnameToOrgnumberMap.GetValueOrDefault(service.ServiceOwnerCode);

                    if (string.IsNullOrEmpty(orgnr))
                    {
                        Debug.WriteLine(service.ServiceOwnerCode + "\t" + service.ServiceOwnerName);
                    }

                    service.OrganizationNumber = orgnr;
                    service.Forms = forms;

                    result.Add(service);

                    RememberHighestServiceEditionCode(serviceCodeToServiceEditionCodeDictionary, service);
                }
            }

            List<Altinn2Service> filteredResult = new List<Altinn2Service>();

            foreach (Altinn2Service resource in result)
            {
                string highestEditionCode = serviceCodeToServiceEditionCodeDictionary.GetValueOrDefault(resource.ServiceCode);

                if (resource.ServiceEditionCode.Equals(highestEditionCode))
                {
                    filteredResult.Add(resource);
                }
            }

            return filteredResult;
        }

        private static Dictionary<string, string> BuildOrganizationNumberMap()
        {
            Dictionary<string, string> orgShortnameToOrgnumberMap = new Dictionary<string, string>();

            using (StreamReader r = new StreamReader("Services/orgs.json"))
            {
                string json = r.ReadToEnd();
                List<Organization> orgs = JsonConvert.DeserializeObject<List<Organization>>(json);
                foreach (Organization org in orgs)
                {
                    orgShortnameToOrgnumberMap.Add(org.Shortname, org.Orgnr);
                }
            }

            return orgShortnameToOrgnumberMap;
        }

        private static string DownloadXsd(string xsdSchemaUrl)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;

            XmlReader doc = XmlReader.Create(xsdSchemaUrl, settings);

            return doc.ReadInnerXml();
        }

        private static void RememberHighestServiceEditionCode(Dictionary<string, string> serviceCodeToServiceEditionCodeDictionary, Altinn2Service service)
        {
            string serviceCode = service.ServiceCode;
            string lastHighestServiceEditionCode = serviceCodeToServiceEditionCodeDictionary.GetValueOrDefault(serviceCode);
            string currentServiceEditionCode = service.ServiceEditionCode;

            if (string.IsNullOrEmpty(lastHighestServiceEditionCode))
            {
                serviceCodeToServiceEditionCodeDictionary.Add(serviceCode, service.ServiceEditionCode);
            }
            else
            {
                int.TryParse(lastHighestServiceEditionCode, out int lastEditionCode);
                int.TryParse(currentServiceEditionCode, out int currentEditionCode);

                if (currentEditionCode > lastEditionCode)
                {
                    serviceCodeToServiceEditionCodeDictionary.Remove(serviceCode);
                    serviceCodeToServiceEditionCodeDictionary.Add(serviceCode, service.ServiceEditionCode);
                }
            }
        }
    }
}
