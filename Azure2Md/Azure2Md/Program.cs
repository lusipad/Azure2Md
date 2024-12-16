using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Newtonsoft.Json;

public class AppSettings
{
    public string TfsUrl { get; set; }
    public List<ProjectConfig> Projects { get; set; }
    public string PersonalAccessToken { get; set; }
}

public class ProjectConfig
{
    public string ProjectName { get; set; }
    public QuerySettings Query { get; set; }
}

public class QuerySettings
{
    public bool UseExistingQuery { get; set; }
    public string QueryPath { get; set; }
    public string CustomWiql { get; set; }
}

public class Program
{
    public static void Main()
    {
        try
        {
            // 读取配置文件
            string configPath = "appsettings.json";
            var config = File.ReadAllText(configPath);
            var settings = JsonConvert.DeserializeObject<AppSettings>(config);

            // 创建连接
            VssConnection connection = new VssConnection(new Uri(settings.TfsUrl), 
                new VssBasicCredential(string.Empty, settings.PersonalAccessToken));
            var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

            foreach (var project in settings.Projects)
            {
                Console.WriteLine($"正在处理项目：{project.ProjectName}");
                ProcessProject(witClient, project, settings.TfsUrl);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误：{ex.Message}");
        }
    }

    private static void ProcessProject(WorkItemTrackingHttpClient witClient, ProjectConfig project, string tfsUrl)
    {
        try
        {
            // 创建或使用现有查询
            Wiql wiql;
            if (project.Query?.UseExistingQuery == true && !string.IsNullOrEmpty(project.Query.QueryPath))
            {
                try
                {
                    var query = witClient.GetQueryAsync(project.ProjectName, project.Query.QueryPath).Result;
                    wiql = query != null ? new Wiql { Query = query.Wiql } : CreateDefaultWiql(project.ProjectName);
                }
                catch
                {
                    wiql = CreateDefaultWiql(project.ProjectName);
                }
            }
            else
            {
                wiql = CreateDefaultWiql(project.ProjectName);
            }

            // 执行查询
            var result = witClient.QueryByWiqlAsync(wiql).Result;
            var workItemIds = result.WorkItems.Select(item => item.Id).ToArray();

            if (workItemIds.Length > 0)
            {
                var workItems = witClient.GetWorkItemsAsync(workItemIds, expand: WorkItemExpand.All).Result;
                
                // 为每个项目生成单独的文件
                string outputFile = $"work_items_{project.ProjectName}.md";
                GenerateReport(workItems, outputFile, project.ProjectName, tfsUrl);
                
                Console.WriteLine($"项目 {project.ProjectName} 的报告已生成：{outputFile}");
            }
            else
            {
                Console.WriteLine($"项目 {project.ProjectName} 未找到任何工作项。");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理项目 {project.ProjectName} 时发生错误：{ex.Message}");
        }
    }

    private static void GenerateReport(IEnumerable<WorkItem> workItems, string outputFile, string projectName, string tfsUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {projectName} 项目工作项报告");
        sb.AppendLine();
        
        // 添加项目链接
        sb.AppendLine($"[在 Azure DevOps 中查看项目]({tfsUrl}/{projectName})");
        sb.AppendLine();

        // 其余报告生成代码保持不变...
        // (原来的报告生成逻辑)

        File.WriteAllText(outputFile, sb.ToString());
    }

    private static Wiql CreateDefaultWiql(string projectName)
    {
        string defaultWiql = 
            "Select [System.Id], [System.Title], [System.State], [System.AssignedTo], " +
            "[Microsoft.VSTS.Scheduling.StartDate], [Microsoft.VSTS.Scheduling.FinishDate], " +
            "[System.WorkItemType], [System.Parent] " +
            $"From WorkItems Where [System.TeamProject] = '{projectName}' " +
            "And ([System.WorkItemType] = 'User Story' Or [System.WorkItemType] = 'Task') " +
            "Order By [System.Id]";

        return new Wiql { Query = defaultWiql };
    }

    private static string FormatDate(object dateValue)
    {
        if (dateValue == null) return DateTime.Now.ToString("yyyy-MM-dd");

        if (DateTime.TryParse(dateValue.ToString(), out DateTime date))
        {
            // 将 UTC 时间转换为本地时间
            if (date.Kind == DateTimeKind.Utc)
            {
                date = date.ToLocalTime();
            }
            return date.ToString("yyyy-MM-dd");
        }
        
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    private static string GetPersonName(object assignedTo)
    {
        if (assignedTo == null) return "未分配";

        try
        {
            // Azure DevOps API 返回的是 IdentityRef 对象
            if (assignedTo is Microsoft.VisualStudio.Services.WebApi.IdentityRef identityRef)
            {
                return identityRef.DisplayName ?? "未分配";
            }
            
            // 如果是字符串，尝试解析 JSON
            if (assignedTo is string assignedToString)
            {
                var identity = JsonConvert.DeserializeObject<Dictionary<string, object>>(assignedToString);
                return identity.ContainsKey("displayName") ? identity["displayName"].ToString() : "未分配";
            }
        }
        catch
        {
            // 如果解析失败，直接返回字符串表示
            return assignedTo.ToString();
        }

        return "未分配";
    }

    private static string GetTaskStatus(string state)
    {
        // 根据 Azure DevOps 的状态映射到 Mermaid 甘特图的状态
        return state.ToLower() switch
        {
            "done" => "done",
            "closed" => "done",
            "completed" => "done",
            "resolved" => "done",
            "removed" => "done",
            "active" => "active",
            "in progress" => "active",
            "doing" => "active",
            _ => ""  // 其他状态不添加特殊标记
        };
    }
}
