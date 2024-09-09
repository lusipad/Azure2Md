using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Newtonsoft.Json;

public class Program
{
    // 配置类
    public class AppSettings
    {
        public string TfsUrl { get; set; }
        public string ProjectName { get; set; }
        public string Query { get; set; }
        public string PersonalAccessToken { get; set; }
    }

    public static void Main()
    {
        // 读取配置文件
        string configPath = "appsettings.json";
        var config = File.ReadAllText(configPath);
        var settings = JsonConvert.DeserializeObject<AppSettings>(config);

        string tfsUrl = settings.TfsUrl;
        string projectName = settings.ProjectName;
        string query = settings.Query;
        string personalAccessToken = settings.PersonalAccessToken;

        string outputFile = "gantt_chart.mmd";

        try
        {
            var creds = new VssBasicCredential(string.Empty, personalAccessToken);

            // Connect to Azure DevOps Services
            var connection = new VssConnection(new Uri(tfsUrl), creds);

            // 获取工作项跟踪客户端
            WorkItemTrackingHttpClient workItemTrackingClient = connection.GetClient<WorkItemTrackingHttpClient>();

            // 定义查询
            Wiql wiql = new Wiql()
            {
                Query = "SELECT [ID], [Title], [StartDate], [EndDate] FROM WorkItems"
            };

            // 执行查询
            WorkItemQueryResult workItemQueryResult = workItemTrackingClient.QueryByWiqlAsync(wiql).Result;

            // 获取工作项ID列表
            var ids = workItemQueryResult.WorkItems.Select(wi => wi.Id).ToArray();

            // 获取工作项详情
            var workItems = workItemTrackingClient.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields).Result;

            // 创建Mermaid甘特图内容
            StringBuilder ganttChartContent = new StringBuilder();
            ganttChartContent.AppendLine("```mermaid");
            ganttChartContent.AppendLine("gantt");
            ganttChartContent.AppendLine("    title 项目甘特图");
            ganttChartContent.AppendLine("    dateFormat  YYYY-MM-DD");

            foreach (var workItem in workItems)
            {
                string taskId = workItem.Id.ToString();
                string taskTitle = workItem.Fields["System.Title"].ToString();
                string startDate = workItem.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") ? workItem.Fields["Microsoft.VSTS.Scheduling.StartDate"].ToString() : string.Empty;
                string endDate = workItem.Fields.ContainsKey("Microsoft.VSTS.Scheduling.EndDate") ? workItem.Fields["Microsoft.VSTS.Scheduling.EndDate"].ToString() : string.Empty;
                ganttChartContent.AppendLine($"    Task {taskId} :{taskTitle}, {startDate}, {endDate}");
            }

            ganttChartContent.AppendLine("```");
            // 写入文件
            File.WriteAllText(outputFile, ganttChartContent.ToString());
            Console.WriteLine($"Mermaid甘特图已写入到文件：{outputFile}");
        }
        catch (VssServiceResponseException ex)
        {
            Console.WriteLine($"服务响应错误：{ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误：{ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"内部错误：{ex.InnerException.Message}");
            }
        }
    }
}