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
    public string ProjectName { get; set; }
    public string PersonalAccessToken { get; set; }
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
        // 读取配置文件
        string configPath = "appsettings.json";
        var config = File.ReadAllText(configPath);
        var settings = JsonConvert.DeserializeObject<AppSettings>(config);

        string tfsUrl = settings.TfsUrl;
        string projectName = settings.ProjectName;
        string personalAccessToken = settings.PersonalAccessToken;

        string outputFile = "work_items.md";

        try
        {
            // 使用个人访问令牌（PAT）进行身份验证
            VssConnection connection = new VssConnection(new Uri(tfsUrl), 
                new VssBasicCredential(string.Empty, personalAccessToken));

            // 获取工作项跟踪客户端
            var witClient = connection.GetClient<WorkItemTrackingHttpClient>();

            // 创建或使用现有查询
            Wiql wiql;
            if (settings.Query?.UseExistingQuery == true && !string.IsNullOrEmpty(settings.Query.QueryPath))
            {
                try
                {
                    // 使用现有查询
                    var query = witClient.GetQueryAsync(projectName, settings.Query.QueryPath).Result;
                    if (query == null)
                    {
                        Console.WriteLine($"警告：未找到查询 '{settings.Query.QueryPath}'，将使用默认查询。");
                        wiql = CreateDefaultWiql(projectName);
                    }
                    else
                    {
                        wiql = new Wiql { Query = query.Wiql };
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告：获取查询 '{settings.Query.QueryPath}' 失败：{ex.Message}");
                    Console.WriteLine("将使用默认查询。");
                    wiql = CreateDefaultWiql(projectName);
                }
            }
            else
            {
                wiql = CreateDefaultWiql(projectName);
            }

            // 执行查询
            var result = witClient.QueryByWiqlAsync(wiql).Result;
            var workItemIds = result.WorkItems.Select(item => item.Id).ToArray();

            if (workItemIds.Length > 0)
            {
                // 获取工作项详细信息
                var workItems = witClient.GetWorkItemsAsync(workItemIds, 
                    expand: WorkItemExpand.All).Result;

                // 生成 Markdown 内容
                var sb = new StringBuilder();
                sb.AppendLine("# 项目工作项报告");
                sb.AppendLine();

                // 1. 总体甘特图
                sb.AppendLine("## 总体甘特图");
                GenerateGanttChart(sb, "项目整体进度甘特图", workItems);

                // 2. 总体工作项列表
                sb.AppendLine("## 总体工作项列表");
                sb.AppendLine();
                sb.AppendLine("| ID | 标题 | 状态 | 负责人 |");
                sb.AppendLine("|---|---|---|---|");

                foreach (var item in workItems)
                {
                    var id = item.Id;
                    var title = item.Fields["System.Title"].ToString();
                    var state = item.Fields["System.State"].ToString();
                    var assignedTo = item.Fields.ContainsKey("System.AssignedTo") 
                        ? item.Fields["System.AssignedTo"].ToString()
                        : "未分配";

                    sb.AppendLine($"| {id} | {title} | {state} | {assignedTo} |");
                }

                sb.AppendLine();

                // 3. 按人分组的视图
                sb.AppendLine("## 按人员分组的任务");
                var workItemsByPerson = workItems
                    .GroupBy(item => item.Fields.ContainsKey("System.AssignedTo") 
                        ? item.Fields["System.AssignedTo"].ToString()
                        : "未分配");

                foreach (var personGroup in workItemsByPerson)
                {
                    sb.AppendLine($"## {personGroup.Key}的任务");
                    sb.AppendLine();
                    
                    // 该人员的甘特图
                    sb.AppendLine("### 甘特图");
                    GenerateGanttChart(sb, $"{personGroup.Key}的进度甘特图", personGroup);
                    
                    // 该人员的工作项列表
                    sb.AppendLine("### 工作项列表");
                    sb.AppendLine();
                    sb.AppendLine("| ID | 标题 | 状态 |");
                    sb.AppendLine("|---|---|---|");

                    foreach (var item in personGroup)
                    {
                        var id = item.Id;
                        var title = item.Fields["System.Title"].ToString();
                        var state = item.Fields["System.State"].ToString();

                        sb.AppendLine($"| {id} | {title} | {state} |");
                    }
                    
                    sb.AppendLine();
                }

                // 写入文件
                File.WriteAllText(outputFile, sb.ToString());
                Console.WriteLine($"工作项报告已生成：{outputFile}");
            }
            else
            {
                Console.WriteLine("未找到任何工作项。");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误：{ex.Message}");
        }
    }

    private static void GenerateGanttChart(StringBuilder sb, string title, IEnumerable<WorkItem> items)
    {
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {title}");
        sb.AppendLine("    dateFormat YYYY-MM-DD");

        // 获取所有 UserStory
        var userStories = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();
        
        // 获取所有 Task
        var tasks = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Task").ToList();

        foreach (var story in userStories)
        {
            var storyTitle = story.Fields["System.Title"].ToString();
            sb.AppendLine($"    section {storyTitle}");

            // 显示 UserStory 本身
            var storyStartDate = story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate")
                ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"].ToString().Split('T')[0]
                : DateTime.Now.ToString("yyyy-MM-dd");
            var storyEndDate = story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate")
                ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"].ToString().Split('T')[0]
                : DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");
            
            sb.AppendLine($"    {storyTitle} :{storyStartDate}, {storyEndDate}");

            // 显示属于该 UserStory 的 Tasks
            var storyTasks = tasks.Where(t => 
                t.Fields.ContainsKey("System.Parent") && 
                t.Fields["System.Parent"].ToString() == story.Id.ToString());

            foreach (var task in storyTasks)
            {
                var taskTitle = task.Fields["System.Title"].ToString();
                var startDate = task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate")
                    ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"].ToString().Split('T')[0]
                    : DateTime.Now.ToString("yyyy-MM-dd");
                var endDate = task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate")
                    ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"].ToString().Split('T')[0]
                    : DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");

                sb.AppendLine($"    {taskTitle} :{startDate}, {endDate}");
            }
        }

        // 显示没有父级的 Tasks
        var orphanTasks = tasks.Where(t => 
            !t.Fields.ContainsKey("System.Parent")).ToList();

        if (orphanTasks.Any())
        {
            sb.AppendLine("    section 其他任务");
            foreach (var task in orphanTasks)
            {
                var taskTitle = task.Fields["System.Title"].ToString();
                var startDate = task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate")
                    ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"].ToString().Split('T')[0]
                    : DateTime.Now.ToString("yyyy-MM-dd");
                var endDate = task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate")
                    ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"].ToString().Split('T')[0]
                    : DateTime.Now.AddDays(7).ToString("yyyy-MM-dd");

                sb.AppendLine($"    {taskTitle} :{startDate}, {endDate}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
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
}
