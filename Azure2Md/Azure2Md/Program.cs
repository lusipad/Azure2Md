using System;
using System.IO;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Newtonsoft.Json;

public class Program
{
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

        // 使用个人访问令牌（PAT）进行身份验证
        VssConnection connection = new VssConnection(new Uri(tfsUrl), new VssBasicCredential(string.Empty, personalAccessToken));

        try
        {
            VssConnection connection = new VssConnection(new Uri(collectionUri), new VssClientCredentials());
            WorkItemTrackingHttpClient witClient = connection.GetClient<WorkItemTrackingHttpClient>();
List<QueryHierarchyItem> queryHierarchyItems = witClient.GetQueriesAsync(projectName, depth: 2).Result;
QueryHierarchyItem myQueriesFolder = queryHierarchyItems.FirstOrDefault(qhi => qhi.Name.Equals(





            
            
            // 获取工作项跟踪客户端
            WorkItemTrackingHttpClient workItemTrackingClient = connection.GetClient<WorkItemTrackingHttpClient>();

            // 执行查询
            WorkItemQueryResult workItemQueryResult = workItemTrackingClient.QueryByWiqlAsync(query).Result;

            // 获取工作项ID列表
            var ids = workItemQueryResult.WorkItems.Select(wi => wi.Id).ToArray();

            // 获取工作项详情
            var workItems = workItemTrackingClient.GetWorkItemsAsync(ids, expand: WorkItemExpand.All).Result;

            // 创建Mermaid甘特图内容
            StringBuilder ganttChartContent = new StringBuilder();
            ganttChartContent.AppendLine("gantt");
            ganttChartContent.AppendLine("    title 项目甘特图");
            ganttChartContent.AppendLine("    dateFormat  YYYY-MM-DD");

            foreach (var workItem in workItems)
            {
                string taskId = workItem.Id.ToString();
                string taskTitle = workItem.Fields["System.Title"].ToString();
                string startDate = string.Empty;
                string endDate = string.Empty;

                if (workItem.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate"))
                {
                    startDate = workItem.Fields["Microsoft.VSTS.Scheduling.StartDate"].ToString();
                }
                else if (workItem.Fields.ContainsKey("System.IterationPath"))
                {
                    startDate = workItem.Fields["System.IterationPath"][0].Split('/')[1].Split('(')[0];
                    endDate = workItem.Fields["System.IterationPath"][0].Split('/')[1].Split('(')[1].Split(')')[0];
                }

                if (workItem.Fields.ContainsKey("Microsoft.VSTS.Scheduling.EndDate"))
                {
                    endDate = workItem.Fields["Microsoft.VSTS.Scheduling.EndDate"].ToString();
                }
                else if (workItem.Fields.ContainsKey("System.IterationPath"))
                {
                    endDate = workItem.Fields["System.IterationPath"][0].Split('/')[1].Split('(')[1].Split(')')[0];
                }

                ganttChartContent.AppendLine($"    Task {taskId} :{taskTitle}, {startDate}, {endDate}");
            }

            // 写入文件
            File.WriteAllText(outputFile, ganttChartContent.ToString());
            Console.WriteLine($"Mermaid甘特图已写入到文件：{outputFile}");
        }
        catch (VssServiceResponseException ex)
        {
            Console.WriteLine($"服务响应错误：{ex.Message}");
            Console.WriteLine
