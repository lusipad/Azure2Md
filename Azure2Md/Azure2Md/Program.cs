using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

public class Program
{
    // 配置类
    public class AppSettings
    {
        public string TfsUrl { get; set; }
        public string ProjectName { get; set; }
        public string QueryPath { get; set; }
        public string PersonalAccessToken { get; set; }
    }

    public static async Task Main()
    {
        // 读取配置文件
        string configPath = "appsettings.json";
        var config = File.ReadAllText(configPath);
        var settings = JsonConvert.DeserializeObject<AppSettings>(config);

        string tfsUrl = settings.TfsUrl;
        string projectName = settings.ProjectName;
        string queryPath = settings.QueryPath;
        string personalAccessToken = settings.PersonalAccessToken;

        string outputFile = "gantt_chart.mmd";

        try
        {
            var creds = new VssBasicCredential(string.Empty, personalAccessToken);

            // Connect to Azure DevOps Services
            var connection = new VssConnection(new Uri(tfsUrl), creds);

            // 获取工作项跟踪客户端
            WorkItemTrackingHttpClient workItemTrackingClient = connection.GetClient<WorkItemTrackingHttpClient>();

            // 获取查询对象
            var queryHierarchyItem = await workItemTrackingClient.GetQueryAsync(projectName, queryPath);


            // 确保查询项不为null
            if (queryHierarchyItem != null)
            {
                // 如果 Wiql 字段为空，则尝试从查询对象中获取查询结果
                if (string.IsNullOrEmpty(queryHierarchyItem.Wiql))
                {
                    // 尝试从查询对象中获取查询结果
                    var queryResult = await workItemTrackingClient.GetQueryAsync(queryHierarchyItem.Id, projectName);
                    //WorkItemQueryResult workItemQueryResult = queryResult;

                    //// 获取工作项ID列表
                    //var ids = workItemQueryResult.WorkItems.Select(wi => wi.Id).ToArray();

                    //// 获取工作项详情
                    //var workItems = await workItemTrackingClient.GetWorkItemsAsync(ids, expand: WorkItemExpand.Fields);

                    //// 创建Mermaid甘特图内容
                    //StringBuilder ganttChartContent = new StringBuilder();
                    //ganttChartContent.AppendLine("```mermaid");
                    //ganttChartContent.AppendLine("gantt");
                    //ganttChartContent.AppendLine("    title 项目甘特图");
                    //ganttChartContent.AppendLine("    dateFormat  YYYY-MM-DD");

                    //foreach (var workItem in workItems)
                    //{
                    //    string taskId = workItem.Id.ToString();
                    //    string taskTitle = workItem.Fields["System.Title"].ToString();
                    //    string startDate = workItem.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") ? workItem.Fields["Microsoft.VSTS.Scheduling.StartDate"].ToString() : string.Empty;
                    //    string endDate = workItem.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") ? workItem.Fields["Microsoft.VSTS.Scheduling.FinishDate"].ToString() : string.Empty;
                    //    ganttChartContent.AppendLine($"    Task {taskId} :{taskTitle}, {startDate}, {endDate}");
                    //}

                    //ganttChartContent.AppendLine("```");
                    //// 写入文件
                    //File.WriteAllText(outputFile, ganttChartContent.ToString());
                    //Console.WriteLine($"Mermaid甘特图已写入到文件：{outputFile}");
                }
            }
            else
            {
                Console.WriteLine("查询项为空。");
            }
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