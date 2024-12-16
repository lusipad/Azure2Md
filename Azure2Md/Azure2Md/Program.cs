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
    public ReportSettings ReportSettings { get; set; }
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

public class ReportSettings
{
    public bool MergeProjects { get; set; }
    public string MergedTitle { get; set; }
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

            if (settings.ReportSettings?.MergeProjects == true)
            {
                // 创建总报告的 StringBuilder
                var totalReport = new StringBuilder();
                totalReport.AppendLine("# " + (settings.ReportSettings.MergedTitle ?? "Azure DevOps 工作项报告"));
                totalReport.AppendLine();
                totalReport.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                totalReport.AppendLine();

                // 收集所有项目的工作项
                var allWorkItems = new List<(WorkItem Item, string ProjectName)>();
                
                foreach (var project in settings.Projects)
                {
                    Console.WriteLine($"正在处理项目：{project.ProjectName}");
                    var projectItems = GetProjectWorkItems(witClient, project);
                    if (projectItems != null)
                    {
                        allWorkItems.AddRange(projectItems.Select(item => (item, project.ProjectName)));
                    }
                }

                if (allWorkItems.Any())
                {
                    GenerateMergedReport(allWorkItems, settings.TfsUrl, totalReport);
                }

                // 保存总报告
                string outputFile = "work_items_report.md";
                File.WriteAllText(outputFile, totalReport.ToString());
                Console.WriteLine($"总报告已生成：{outputFile}");
            }
            else
            {
                // 原有的分项目生成逻辑
                // ... 保持不变 ...
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误：{ex.Message}");
        }
    }

    private static void ProcessProject(WorkItemTrackingHttpClient witClient, ProjectConfig project, string tfsUrl, StringBuilder totalReport)
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
                
                // 将项目报告添加到总报告中
                GenerateReport(workItems, project.ProjectName, tfsUrl, totalReport);
                
                Console.WriteLine($"项目 {project.ProjectName} 的报告已生成");
            }
            else
            {
                Console.WriteLine($"项目 {project.ProjectName} 未找到任何工作项。");
                totalReport.AppendLine($"## {project.ProjectName}");
                totalReport.AppendLine();
                totalReport.AppendLine("未找到任何工作项。");
                totalReport.AppendLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理项目 {project.ProjectName} 时发生错误：{ex.Message}");
            totalReport.AppendLine($"## {project.ProjectName}");
            totalReport.AppendLine();
            totalReport.AppendLine($"处理出错：{ex.Message}");
            totalReport.AppendLine();
        }
    }

    private static void GenerateReport(IEnumerable<WorkItem> workItems, string projectName, string tfsUrl, StringBuilder sb)
    {
        // 项目标题使用二级标题
        sb.AppendLine($"## {projectName}");
        sb.AppendLine();
        
        // 添加��目链接
        sb.AppendLine($"[在 Azure DevOps 中查看项目]({tfsUrl}/{projectName})");
        sb.AppendLine();

        // 1. 项目概览（使用三级标题）
        sb.AppendLine("### 项目概览");
        sb.AppendLine();
        sb.AppendLine($"- 工作项总数：{workItems.Count()}");
        sb.AppendLine($"- 完成项数量：{workItems.Count(w => GetTaskStatus(w.Fields["System.State"].ToString()) == "done")}");
        sb.AppendLine($"- 进行中数量：{workItems.Count(w => GetTaskStatus(w.Fields["System.State"].ToString()) == "active")}");
        sb.AppendLine();

        // 2. 甘特图部分（使用三级标题）
        sb.AppendLine("### 项目甘特图");

        // 2.1 Feature + User Story 视图
        sb.AppendLine("#### Feature 层级视图");
        GenerateFeatureGanttChart(sb, $"{projectName} Feature 进度", workItems);

        // 2.2 User Story + Task 视图
        sb.AppendLine("#### User Story 层级视图");
        GenerateUserStoryGanttChart(sb, $"{projectName} User Story 进度", workItems);

        // 3. 按工作项类型分组（使用三级标题）
        sb.AppendLine("### 工作项分类");
        
        // 3.1 User Stories（使用四级标题）
        var userStories = workItems.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();
        if (userStories.Any())
        {
            sb.AppendLine("#### User Stories");
            sb.AppendLine();
            sb.AppendLine("| ID | 标题 | 状态 | 负责人 | 开始日期 | 结束日期 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var story in userStories)
            {
                var id = story.Id;
                var title = story.Fields["System.Title"].ToString();
                var state = story.Fields["System.State"].ToString();
                var assignedTo = GetPersonName(
                    story.Fields.ContainsKey("System.AssignedTo") 
                        ? story.Fields["System.AssignedTo"] 
                        : null);
                var startDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));

                sb.AppendLine($"| {id} | {title} | {state} | {assignedTo} | {startDate} | {endDate} |");
            }
            sb.AppendLine();
        }

        // 3.2 Tasks（使用四级标题）
        var tasks = workItems.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Task").ToList();
        if (tasks.Any())
        {
            sb.AppendLine("#### Tasks");
            sb.AppendLine();
            sb.AppendLine("| ID | 标题 | 状态 | 负责人 | 开始日期 | 结束日期 | 所属 Story |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var task in tasks)
            {
                var id = task.Id;
                var title = task.Fields["System.Title"].ToString();
                var state = task.Fields["System.State"].ToString();
                var assignedTo = GetPersonName(
                    task.Fields.ContainsKey("System.AssignedTo") 
                        ? task.Fields["System.AssignedTo"] 
                        : null);
                var startDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));
                var parentId = task.Fields.ContainsKey("System.Parent") 
                    ? task.Fields["System.Parent"].ToString() 
                    : "-";

                sb.AppendLine($"| {id} | {title} | {state} | {assignedTo} | {startDate} | {endDate} | {parentId} |");
            }
            sb.AppendLine();
        }

        // 4. 按人员分组的视图（使用三级标题）
        sb.AppendLine("### 团队成员任务分配");
        var workItemsByPerson = workItems
            .GroupBy(item => GetPersonName(
                item.Fields.ContainsKey("System.AssignedTo") 
                    ? item.Fields["System.AssignedTo"] 
                    : null));

        foreach (var personGroup in workItemsByPerson)
        {
            // 每个人的任务（使用四级标题）
            sb.AppendLine($"#### {personGroup.Key}");
            sb.AppendLine();
            
            // 个人甘特图（使用五级标题）
            sb.AppendLine("##### 个人甘特图");
            // 根据工作项类型选择合适的甘特图生成方法
            var hasFeatures = personGroup.Any(i => i.Fields["System.WorkItemType"].ToString() == "Feature");
            if (hasFeatures)
            {
                // 如果有 Feature，使用 Feature 层级视图
                GenerateFeatureGanttChart(sb, $"{personGroup.Key}的进度甘特图", personGroup);
            }
            else
            {
                // 否则使用 User Story 层级视图
                GenerateUserStoryGanttChart(sb, $"{personGroup.Key}的进度甘特图", personGroup);
            }
            
            // 个人工作项列表（使用五级标题）
            sb.AppendLine("##### 工作项列表");
            sb.AppendLine();
            sb.AppendLine("| ID | 类型 | 标题 | 状态 |");
            sb.AppendLine("|---|---|---|---|");

            foreach (var item in personGroup)
            {
                var id = item.Id;
                var type = item.Fields["System.WorkItemType"].ToString();
                var title = item.Fields["System.Title"].ToString();
                var state = item.Fields["System.State"].ToString();

                sb.AppendLine($"| {id} | {type} | {title} | {state} |");
            }
            
            sb.AppendLine();
        }

        // 添加分隔线
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static Wiql CreateDefaultWiql(string projectName)
    {
        string defaultWiql = 
            "Select [System.Id], [System.Title], [System.State], [System.AssignedTo], " +
            "[Microsoft.VSTS.Scheduling.StartDate], [Microsoft.VSTS.Scheduling.FinishDate], " +
            "[System.WorkItemType], [System.Parent] " +
            $"From WorkItems Where [System.TeamProject] = '{projectName}' " +
            "And ([System.WorkItemType] = 'Feature' Or [System.WorkItemType] = 'User Story' Or [System.WorkItemType] = 'Task') " +
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

    private static void GenerateGanttChart(StringBuilder sb, string title, IEnumerable<WorkItem> items)
    {
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {title}");
        sb.AppendLine("    dateFormat YYYY-MM-DD");

        // 获取所有工作项类型
        var features = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Feature").ToList();
        var userStories = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();
        var tasks = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Task").ToList();

        foreach (var feature in features)
        {
            var featureTitle = feature.Fields["System.Title"].ToString();
            sb.AppendLine($"    section {featureTitle}");

            // 显示 Feature 本身
            var featureStartDate = FormatDate(
                feature.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                    ? feature.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                    : null);
            var featureEndDate = FormatDate(
                feature.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                    ? feature.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                    : DateTime.Now.AddDays(30));
            var featureStatus = GetTaskStatus(feature.Fields["System.State"].ToString());
            
            sb.AppendLine($"    {featureTitle} :{featureStatus}, {featureStartDate}, {featureEndDate}");

            // 显示属于该 Feature 的 User Stories
            var featureStories = userStories.Where(s => 
                s.Fields.ContainsKey("System.Parent") && 
                s.Fields["System.Parent"].ToString() == feature.Id.ToString());

            foreach (var story in featureStories)
            {
                var storyTitle = story.Fields["System.Title"].ToString();
                var storyStartDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var storyEndDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {storyTitle} :{storyStatus}, {storyStartDate}, {storyEndDate}");

                // 显示属于该 User Story 的 Tasks
                var storyTasks = tasks.Where(t => 
                    t.Fields.ContainsKey("System.Parent") && 
                    t.Fields["System.Parent"].ToString() == story.Id.ToString());

                foreach (var task in storyTasks)
                {
                    var taskTitle = task.Fields["System.Title"].ToString();
                    var startDate = FormatDate(
                        task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                            ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                            : null);
                    var endDate = FormatDate(
                        task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                            ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                            : DateTime.Now.AddDays(7));
                    var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                    sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
                }
            }
        }

        // 显示没有父级 Feature 的 User Stories
        var orphanStories = userStories.Where(s => 
            !s.Fields.ContainsKey("System.Parent")).ToList();

        if (orphanStories.Any())
        {
            sb.AppendLine("    section 其他 User Stories");
            foreach (var story in orphanStories)
            {
                // 显示 User Story 和其关联的 Tasks...
                // (与之前的 User Story 处理逻辑相同)
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
                var startDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));
                var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void GenerateFeatureGanttChart(StringBuilder sb, string title, IEnumerable<WorkItem> items)
    {
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {title}");
        sb.AppendLine("    dateFormat YYYY-MM-DD");

        var features = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Feature").ToList();
        var userStories = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();

        foreach (var feature in features)
        {
            var featureTitle = feature.Fields["System.Title"].ToString();
            sb.AppendLine($"    section {featureTitle}");

            // 显示 Feature 本身
            var featureStartDate = FormatDate(
                feature.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                    ? feature.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                    : null);
            var featureEndDate = FormatDate(
                feature.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                    ? feature.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                    : DateTime.Now.AddDays(30));
            var featureStatus = GetTaskStatus(feature.Fields["System.State"].ToString());
            
            sb.AppendLine($"    {featureTitle} :{featureStatus}, {featureStartDate}, {featureEndDate}");

            // 显示属于该 Feature 的 User Stories
            var featureStories = userStories.Where(s => 
                s.Fields.ContainsKey("System.Parent") && 
                s.Fields["System.Parent"].ToString() == feature.Id.ToString());

            foreach (var story in featureStories)
            {
                var storyTitle = story.Fields["System.Title"].ToString();
                var storyStartDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var storyEndDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {storyTitle} :{storyStatus}, {storyStartDate}, {storyEndDate}");
            }
        }

        // 显示没有父级 Feature 的 User Stories
        var orphanStories = userStories.Where(s => 
            !s.Fields.ContainsKey("System.Parent")).ToList();

        if (orphanStories.Any())
        {
            sb.AppendLine("    section 其他 User Stories");
            foreach (var story in orphanStories)
            {
                var storyTitle = story.Fields["System.Title"].ToString();
                var storyStartDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var storyEndDate = FormatDate(
                    story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(14));
                var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
                
                sb.AppendLine($"    {storyTitle} :{storyStatus}, {storyStartDate}, {storyEndDate}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void GenerateUserStoryGanttChart(StringBuilder sb, string title, IEnumerable<WorkItem> items)
    {
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {title}");
        sb.AppendLine("    dateFormat YYYY-MM-DD");

        var userStories = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "User Story").ToList();
        var tasks = items.Where(i => 
            i.Fields["System.WorkItemType"].ToString() == "Task").ToList();

        foreach (var story in userStories)
        {
            var storyTitle = story.Fields["System.Title"].ToString();
            sb.AppendLine($"    section {storyTitle}");

            // 显示 User Story 本身
            var storyStartDate = FormatDate(
                story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                    ? story.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                    : null);
            var storyEndDate = FormatDate(
                story.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                    ? story.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                    : DateTime.Now.AddDays(14));
            var storyStatus = GetTaskStatus(story.Fields["System.State"].ToString());
            
            sb.AppendLine($"    {storyTitle} :{storyStatus}, {storyStartDate}, {storyEndDate}");

            // 显示属于该 User Story 的 Tasks
            var storyTasks = tasks.Where(t => 
                t.Fields.ContainsKey("System.Parent") && 
                t.Fields["System.Parent"].ToString() == story.Id.ToString());

            foreach (var task in storyTasks)
            {
                var taskTitle = task.Fields["System.Title"].ToString();
                var startDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));
                var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
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
                var startDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StartDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.StartDate"] 
                        : null);
                var endDate = FormatDate(
                    task.Fields.ContainsKey("Microsoft.VSTS.Scheduling.FinishDate") 
                        ? task.Fields["Microsoft.VSTS.Scheduling.FinishDate"] 
                        : DateTime.Now.AddDays(7));
                var taskStatus = GetTaskStatus(task.Fields["System.State"].ToString());

                sb.AppendLine($"    {taskTitle} :{taskStatus}, {startDate}, {endDate}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static IEnumerable<WorkItem> GetProjectWorkItems(WorkItemTrackingHttpClient witClient, ProjectConfig project)
    {
        try
        {
            var wiql = project.Query?.UseExistingQuery == true && !string.IsNullOrEmpty(project.Query.QueryPath)
                ? witClient.GetQueryAsync(project.ProjectName, project.Query.QueryPath).Result?.Wiql
                : CreateDefaultWiql(project.ProjectName).Query;

            var result = witClient.QueryByWiqlAsync(new Wiql { Query = wiql }).Result;
            var workItemIds = result.WorkItems.Select(item => item.Id).ToArray();

            if (workItemIds.Length > 0)
            {
                return witClient.GetWorkItemsAsync(workItemIds, expand: WorkItemExpand.All).Result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取项目 {project.ProjectName} 的工作项时发生错误：{ex.Message}");
        }

        return null;
    }

    private static void GenerateMergedReport(List<(WorkItem Item, string ProjectName)> allWorkItems, string tfsUrl, StringBuilder sb)
    {
        // 1. 总体概览
        sb.AppendLine("## 总体概览");
        sb.AppendLine();
        sb.AppendLine($"- 工作项总数：{allWorkItems.Count}");
        sb.AppendLine($"- 完成项数量：{allWorkItems.Count(w => GetTaskStatus(w.Item.Fields["System.State"].ToString()) == "done")}");
        sb.AppendLine($"- 进行中数量：{allWorkItems.Count(w => GetTaskStatus(w.Item.Fields["System.State"].ToString()) == "active")}");
        sb.AppendLine();

        // 2. 按项目统计
        sb.AppendLine("## 项目统计");
        foreach (var projectGroup in allWorkItems.GroupBy(w => w.ProjectName))
        {
            sb.AppendLine($"### {projectGroup.Key}");
            sb.AppendLine($"- 工作项数量：{projectGroup.Count()}");
            sb.AppendLine($"- 完成项数量：{projectGroup.Count(w => GetTaskStatus(w.Item.Fields["System.State"].ToString()) == "done")}");
            sb.AppendLine($"- 进行中数量：{projectGroup.Count(w => GetTaskStatus(w.Item.Fields["System.State"].ToString()) == "active")}");
            sb.AppendLine();
        }

        // 3. 总体甘特图
        sb.AppendLine("## 总体甘特图");
        
        // 3.1 Feature 视图
        sb.AppendLine("### Feature 层级视图");
        GenerateFeatureGanttChart(sb, "所有项目 Feature 进度", allWorkItems.Select(w => w.Item));

        // 3.2 User Story 视图
        sb.AppendLine("### User Story 层级视图");
        GenerateUserStoryGanttChart(sb, "所有项目 User Story 进度", allWorkItems.Select(w => w.Item));

        // 4. 工作项分类
        sb.AppendLine("## 工作项分类");
        
        // 4.1 Features
        var features = allWorkItems.Where(w => 
            w.Item.Fields["System.WorkItemType"].ToString() == "Feature");
        if (features.Any())
        {
            sb.AppendLine("### Features");
            sb.AppendLine("| 项目 | ID | 标题 | 状态 | 负责人 | 开始日期 | 结束日期 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var (item, projectName) in features)
            {
                // ... 生成 Feature 表格行 ...
            }
            sb.AppendLine();
        }

        // 4.2 User Stories（类似处理）
        // 4.3 Tasks（类似处理）

        // 5. 按人员分组的视图
        sb.AppendLine("## 团队成员任务分配");
        var workItemsByPerson = allWorkItems
            .GroupBy(w => GetPersonName(
                w.Item.Fields.ContainsKey("System.AssignedTo") 
                    ? w.Item.Fields["System.AssignedTo"] 
                    : null));

        foreach (var personGroup in workItemsByPerson)
        {
            // ... 生成个人任务视图 ...
        }
    }
}
